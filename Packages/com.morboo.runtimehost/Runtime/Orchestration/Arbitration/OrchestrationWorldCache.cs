using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-tick world snapshot built by <see cref="OrchestrationArbiter"/> before polling domains.
/// Contains pre-filtered lists of active actors and friendly receivers so that domains
/// and dispatch methods can iterate without re-querying <see cref="OrchestrationRegistry"/>.
/// <para>
/// IMPORTANT — Single instance, reused each tick. Lists are <see cref="Clear"/>ed and
/// refilled; no per-tick allocations.
/// </para>
/// <para>
/// IMPORTANT — Actors list contains only active <see cref="IOrchestrationActor"/>
/// entries that also implement <see cref="IFactionAssetProvider"/> with a non-null faction.
/// Unity-null entries are pruned during build.
/// </para>
/// <para>
/// IMPORTANT — Implements <see cref="IWorldQuery"/> for domain/policy consumption.
/// After <see cref="Freeze"/> is called, domains see a consistent snapshot.
/// RuntimeHost-internal fields (receiver lists, RoleByTransform) are NOT exposed via IWorldQuery.
/// </para>
/// </summary>
public sealed class OrchestrationWorldCache : IWorldQuery, IWorldQuery3D, IActorReadProjectionQuery
{
    // ──────────────────────────────────────────────────────────────────
    //  RuntimeHost-internal — Receiver and actor lists (NOT in IWorldQuery)
    //  IMPORTANT: Only arbiter and execution router access these directly.
    // ──────────────────────────────────────────────────────────────────

    // PERF: Pre-sized, reused each tick. Clear() does not shrink capacity.
    public readonly List<IOrchestrationActor> Actors = new List<IOrchestrationActor>(256);
    public readonly List<ICombatCommandReceiver> FriendlyCombatReceivers = new List<ICombatCommandReceiver>(128);
    public readonly List<IIdleCommandReceiver> FriendlyIdleReceivers = new List<IIdleCommandReceiver>(128);

    /// <summary>
    /// Pre-filtered friendly transforms for crowd scoring (RuntimeHost-internal).
    /// IMPORTANT: Kept for compatibility. Prefer IWorldQuery crowd API for new code.
    /// Built from friendly combat + idle receiver transforms (deduped via HashSet).
    /// </summary>
    public readonly List<Transform> FriendlyCrowdTransforms = new List<Transform>(128);

    /// <summary>
    /// Reusable dedup set for building FriendlyCrowdTransforms.
    /// PERF: Avoids O(n²) Contains on List. Cleared each tick, no allocations after warmup.
    /// </summary>
    internal readonly HashSet<Transform> CrowdDedup = new HashSet<Transform>(256);

    /// <summary>
    /// Per-transform RoleId lookup, resolved once per tick from friendly receivers (RuntimeHost-internal).
    /// IMPORTANT: Stores RoleId (Framework value type), not RoleAsset (ScriptableObject).
    /// RoleAsset → RoleId conversion happens during BuildWorldCache at the Integration boundary.
    /// </summary>
    public readonly Dictionary<Transform, RoleId> RoleByTransform = new Dictionary<Transform, RoleId>(128);

    /// <summary>
    /// Per-role idle bounds, resolved from IdleBoundsRegistry once per tick.
    /// IMPORTANT: Keyed by RoleId, not RoleAsset. Stores AABB2D (Framework type).
    /// </summary>
    public readonly Dictionary<RoleId, AABB2D> ResolvedIdleBounds = new Dictionary<RoleId, AABB2D>(4);

    /// <summary>
    /// Combat target set resolved from OrchestrationRegistry once per tick.
    /// RuntimeHost-internal only.
    /// </summary>
    public CombatTargetSet ResolvedCombatTargetSet;

    // ──────────────────────────────────────────────────────────────────
    //  Receiver identity snapshots — parallel to receiver lists
    //  IMPORTANT: Built by SnapshotReceivers() after RoleByTransform is populated.
    //  The ExecutionRouter iterates these instead of touching MonoBehaviour receivers.
    // ──────────────────────────────────────────────────────────────────

    readonly List<EntityId> _combatReceiverEntityIds = new List<EntityId>(128);
    readonly List<RoleId> _combatReceiverRoleIds = new List<RoleId>(128);
    readonly List<EntityId> _idleReceiverEntityIds = new List<EntityId>(128);
    readonly List<RoleId> _idleReceiverRoleIds = new List<RoleId>(128);

    // ──────────────────────────────────────────────────────────────────
    //  IWorldQuery snapshot data — snapshotted during build, frozen for domains
    // ──────────────────────────────────────────────────────────────────

    Float2 _anchor;
    Float3 _worldAnchor;
    float _now;
    SpatialProjectionPlane _projectionPlane;

    // Actor snapshots (parallel lists, same indices as Actors)
    readonly List<Float2> _actorPositions = new List<Float2>(256);
    readonly List<Float3> _actorPositionsWorld = new List<Float3>(256);
    readonly List<EntityId> _actorEntityIds = new List<EntityId>(256);
    readonly List<bool> _actorHostile = new List<bool>(256);
    readonly List<EntityLifecycleState> _actorLifecycleStates = new List<EntityLifecycleState>(256);

    // Crowd snapshots (IWorldQuery-visible, parallel to FriendlyCrowdTransforms)
    readonly List<Float2> _crowdPositions = new List<Float2>(128);
    readonly List<Float3> _crowdPositionsWorld = new List<Float3>(128);
    readonly List<EntityId> _crowdEntityIds = new List<EntityId>(128);

    // Per-entity actor index lookup (EntityId → index for O(1) position resolve)
    readonly Dictionary<EntityId, int> _actorIndexByEntityId = new Dictionary<EntityId, int>(256);

    // Per-entity role lookup (EntityId → RoleId)
    readonly Dictionary<EntityId, RoleId> _roleIdByEntityId = new Dictionary<EntityId, RoleId>(128);
    readonly Dictionary<RoleId, AABB2D> _idleBoundsByRoleId = new Dictionary<RoleId, AABB2D>(16);

    // ──────────────────────────────────────────────────────────────────
    //  Freeze lifecycle — #if DEBUG mutation assertions
    //  IMPORTANT: RuntimeHost-only. Not part of IWorldQuery contract.
    // ──────────────────────────────────────────────────────────────────

#if DEBUG
    bool _frozen;
    public bool IsFrozen => _frozen;
#endif

    /// <summary>
    /// Marks the cache as frozen. After this call, domains may read IWorldQuery safely.
    /// In DEBUG builds, mutations after freeze will assert.
    /// </summary>
    public void Freeze()
    {
#if DEBUG
        _frozen = true;
#endif
    }

    // ──────────────────────────────────────────────────────────────────
    //  Anchor / Now — set during build, before freeze
    // ──────────────────────────────────────────────────────────────────

    public Float2 Anchor
    {
        get => _anchor;
        set
        {
#if DEBUG
            Debug.Assert(!_frozen, "[OrchestrationWorldCache] Mutating Anchor after Freeze.");
#endif
            _anchor = value;
        }
    }

    public Float3 WorldAnchor
    {
        get => _worldAnchor;
        set
        {
#if DEBUG
            Debug.Assert(!_frozen, "[OrchestrationWorldCache] Mutating WorldAnchor after Freeze.");
#endif
            _worldAnchor = value;
        }
    }

    public SpatialProjectionPlane ProjectionPlane
    {
        get => _projectionPlane;
        set
        {
#if DEBUG
            Debug.Assert(!_frozen, "[OrchestrationWorldCache] Mutating ProjectionPlane after Freeze.");
#endif
            _projectionPlane = value;
        }
    }

    public float Now
    {
        get => _now;
        set
        {
#if DEBUG
            Debug.Assert(!_frozen, "[OrchestrationWorldCache] Mutating Now after Freeze.");
#endif
            _now = value;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Build helpers — called by arbiter before Freeze
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Snapshots actor data into parallel lists for IWorldQuery consumption.
    /// Called by arbiter after populating the Actors list.
    /// </summary>
    public void SnapshotActors(FactionAsset orchestratorFaction, FactionRelationTableAsset relations)
    {
#if DEBUG
        Debug.Assert(!_frozen, "[OrchestrationWorldCache] SnapshotActors called after Freeze.");
#endif
        _actorPositions.Clear();
        _actorPositionsWorld.Clear();
        _actorEntityIds.Clear();
        _actorHostile.Clear();
        _actorLifecycleStates.Clear();
        _actorIndexByEntityId.Clear();

        for (int i = 0; i < Actors.Count; i++)
        {
            IOrchestrationActor actor = Actors[i];
            Transform t = actor.GetTransform();

            Vector3 worldPos = t.position;
            _actorPositionsWorld.Add(worldPos.ToFloat3());
            _actorPositions.Add(worldPos.ProjectToFloat2(_projectionPlane));
            _actorLifecycleStates.Add(actor.GetLifecycleState());

            bool isHostile = false;
            IFactionAssetProvider fap = actor as IFactionAssetProvider;
            if (fap != null && orchestratorFaction != null && relations != null)
            {
                FactionAsset actorFaction = fap.GetFactionAsset();
                if (actorFaction != null)
                {
                    isHostile = relations.GetRelation(orchestratorFaction, actorFaction) == FactionRelation.Hostile;
                }
            }
            _actorHostile.Add(isHostile);

            EntityId eid = actor.GetEntityId();
            _actorEntityIds.Add(eid);

            // Build O(1) EntityId → index lookup for TryGetActorPosition
            if (!eid.IsNone)
                _actorIndexByEntityId[eid] = i;
        }
    }

    /// <summary>
    /// Snapshots crowd positions and EntityIds from FriendlyCrowdTransforms.
    /// Called by arbiter after building the crowd list. Only entities with
    /// <see cref="IEntityIdProvider"/> get non-None EntityIds in the snapshot.
    /// </summary>
    public void SnapshotCrowd()
    {
#if DEBUG
        Debug.Assert(!_frozen, "[OrchestrationWorldCache] SnapshotCrowd called after Freeze.");
#endif
        _crowdPositions.Clear();
        _crowdPositionsWorld.Clear();
        _crowdEntityIds.Clear();

        for (int i = 0; i < FriendlyCrowdTransforms.Count; i++)
        {
            Transform t = FriendlyCrowdTransforms[i];
            Vector3 worldPos = t.position;
            _crowdPositionsWorld.Add(worldPos.ToFloat3());
            _crowdPositions.Add(worldPos.ProjectToFloat2(_projectionPlane));

            IEntityIdProvider idp = t.GetComponent<IEntityIdProvider>();
            _crowdEntityIds.Add(idp != null ? idp.GetEntityId() : EntityId.None);
        }
    }

    /// <summary>
    /// Builds the per-entity role lookup from RoleByTransform.
    /// Only entities with <see cref="IEntityIdProvider"/> are included.
    /// IMPORTANT: Uses RoleId (stable value type), not GetInstanceID().
    /// </summary>
    public void BuildRoleByEntityId()
    {
#if DEBUG
        Debug.Assert(!_frozen, "[OrchestrationWorldCache] BuildRoleByEntityId called after Freeze.");
#endif
        _roleIdByEntityId.Clear();
        _idleBoundsByRoleId.Clear();

        foreach (var kvp in RoleByTransform)
        {
            Transform t = kvp.Key;
            if (t == null) continue;
            IEntityIdProvider idp = t.GetComponent<IEntityIdProvider>();
            if (idp == null) continue;
            EntityId eid = idp.GetEntityId();
            if (eid.IsNone) continue;
            RoleId roleId = kvp.Value;
            if (roleId.IsNone) continue;

            _roleIdByEntityId[eid] = roleId;
        }

        foreach (var kvp in ResolvedIdleBounds)
        {
            RoleId roleId = kvp.Key;
            if (roleId.IsNone) continue;
            _idleBoundsByRoleId[roleId] = kvp.Value;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  IWorldQueryBase
    // ──────────────────────────────────────────────────────────────────

    Float2 IWorldQueryBase.Anchor => _anchor;
    Float3 IWorldQueryBase3D.Anchor3D => _worldAnchor;
    float IWorldQueryBase.Now => _now;

    public int ActorCount => _actorPositions.Count;
    public EntityId GetActorEntityId(int index) => _actorEntityIds[index];
    public Float2 GetActorPosition(int index) => _actorPositions[index];
    public Float3 GetActorPosition3D(int index) => _actorPositionsWorld[index];
    public bool GetActorIsAlive(int index) => _actorLifecycleStates[index] == EntityLifecycleState.Active;
    public bool GetActorIsHostile(int index) => _actorHostile[index];

    public bool TryGetActorPosition(EntityId entityId, out Float2 position)
    {
        if (!entityId.IsNone && _actorIndexByEntityId.TryGetValue(entityId, out int idx))
        {
            position = _actorPositions[idx];
            return true;
        }
        position = Float2.Zero;
        return false;
    }

    public bool TryGetActorPosition3D(EntityId entityId, out Float3 position)
    {
        if (!entityId.IsNone && _actorIndexByEntityId.TryGetValue(entityId, out int idx))
        {
            position = _actorPositionsWorld[idx];
            return true;
        }

        position = Float3.Zero;
        return false;
    }

    public bool TryGetActorReadProjection(EntityId entityId, out ActorReadProjection projection)
    {
        if (!entityId.IsNone && _actorIndexByEntityId.TryGetValue(entityId, out int idx))
        {
            RoleId roleId;
            if (!_roleIdByEntityId.TryGetValue(entityId, out roleId))
                roleId = RoleId.None;

            projection = new ActorReadProjection(
                entityId,
                _actorPositionsWorld[idx],
                _actorLifecycleStates[idx],
                roleId);
            return true;
        }

        projection = default;
        return false;
    }

    // ──────────────────────────────────────────────────────────────────
    //  ICrowdQuery
    // ──────────────────────────────────────────────────────────────────

    public int CrowdCount => _crowdPositions.Count;
    public Float2 GetCrowdPosition(int index) => _crowdPositions[index];
    public Float3 GetCrowdPosition3D(int index) => _crowdPositionsWorld[index];
    public EntityId GetCrowdEntityId(int index) => _crowdEntityIds[index];

    // ──────────────────────────────────────────────────────────────────
    //  IRoleQuery
    // ──────────────────────────────────────────────────────────────────

    public bool TryGetRoleId(EntityId entityId, out RoleId roleId)
    {
        return _roleIdByEntityId.TryGetValue(entityId, out roleId);
    }

    // ──────────────────────────────────────────────────────────────────
    //  IIdleBoundsQuery
    // ──────────────────────────────────────────────────────────────────

    public bool TryGetIdleBounds(RoleId roleId, out AABB2D bounds)
    {
        return _idleBoundsByRoleId.TryGetValue(roleId, out bounds);
    }

    public CombatTargetSet GetCombatTargetSetInternal() => ResolvedCombatTargetSet;

    // ──────────────────────────────────────────────────────────────────
    //  Receiver identity snapshot — for ExecutionRouter command emission
    //  IMPORTANT: Router iterates these instead of MonoBehaviour receiver lists.
    // ──────────────────────────────────────────────────────────────────

    public int CombatReceiverCount => _combatReceiverEntityIds.Count;
    public EntityId GetCombatReceiverEntityId(int index) => _combatReceiverEntityIds[index];
    public RoleId GetCombatReceiverRoleId(int index) => _combatReceiverRoleIds[index];

    public int IdleReceiverCount => _idleReceiverEntityIds.Count;
    public EntityId GetIdleReceiverEntityId(int index) => _idleReceiverEntityIds[index];
    public RoleId GetIdleReceiverRoleId(int index) => _idleReceiverRoleIds[index];

    /// <summary>
    /// Snapshots receiver EntityIds and RoleIds into parallel lists.
    /// Called after RoleByTransform is populated, before Freeze.
    /// IMPORTANT: Must be called after BuildRoleByEntityId.
    /// </summary>
    public void SnapshotReceivers()
    {
#if DEBUG
        Debug.Assert(!_frozen, "[OrchestrationWorldCache] SnapshotReceivers called after Freeze.");
#endif
        _combatReceiverEntityIds.Clear();
        _combatReceiverRoleIds.Clear();
        _idleReceiverEntityIds.Clear();
        _idleReceiverRoleIds.Clear();

        for (int i = 0; i < FriendlyCombatReceivers.Count; i++)
        {
            Component c = FriendlyCombatReceivers[i] as Component;
            if (c == null)
            {
                _combatReceiverEntityIds.Add(EntityId.None);
                _combatReceiverRoleIds.Add(RoleId.None);
                continue;
            }

            IEntityIdProvider idp = c.GetComponent<IEntityIdProvider>();
            EntityId eid = idp != null ? idp.GetEntityId() : EntityId.None;
            _combatReceiverEntityIds.Add(eid);

            RoleId rid;
            if (!RoleByTransform.TryGetValue(c.transform, out rid)) rid = RoleId.None;
            _combatReceiverRoleIds.Add(rid);
        }

        for (int i = 0; i < FriendlyIdleReceivers.Count; i++)
        {
            Component c = FriendlyIdleReceivers[i] as Component;
            if (c == null)
            {
                _idleReceiverEntityIds.Add(EntityId.None);
                _idleReceiverRoleIds.Add(RoleId.None);
                continue;
            }

            IEntityIdProvider idp = c.GetComponent<IEntityIdProvider>();
            EntityId eid = idp != null ? idp.GetEntityId() : EntityId.None;
            _idleReceiverEntityIds.Add(eid);

            RoleId rid;
            if (!RoleByTransform.TryGetValue(c.transform, out rid)) rid = RoleId.None;
            _idleReceiverRoleIds.Add(rid);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Clear — resets all state for next tick
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears all cached lists. Called at the start of each arbiter tick.
    /// </summary>
    public void Clear()
    {
#if DEBUG
        _frozen = false;
#endif
        Actors.Clear();
        FriendlyCombatReceivers.Clear();
        FriendlyIdleReceivers.Clear();
        FriendlyCrowdTransforms.Clear();
        CrowdDedup.Clear();
        RoleByTransform.Clear();
        ResolvedIdleBounds.Clear();
        ResolvedCombatTargetSet = null;

        _actorPositions.Clear();
        _actorPositionsWorld.Clear();
        _actorEntityIds.Clear();
        _actorHostile.Clear();
        _actorLifecycleStates.Clear();
        _actorIndexByEntityId.Clear();
        _crowdPositions.Clear();
        _crowdPositionsWorld.Clear();
        _crowdEntityIds.Clear();
        _roleIdByEntityId.Clear();
        _idleBoundsByRoleId.Clear();

        _combatReceiverEntityIds.Clear();
        _combatReceiverRoleIds.Clear();
        _idleReceiverEntityIds.Clear();
        _idleReceiverRoleIds.Clear();
    }
}
