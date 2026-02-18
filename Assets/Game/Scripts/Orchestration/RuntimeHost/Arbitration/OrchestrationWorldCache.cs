using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-tick world snapshot built by <see cref="OrchestrationArbiter"/> before polling domains.
/// Contains pre-filtered lists of alive actors and friendly receivers so that domains
/// and dispatch methods can iterate without re-querying <see cref="OrchestrationRegistry"/>.
/// <para>
/// IMPORTANT — Single instance, reused each tick. Lists are <see cref="Clear"/>ed and
/// refilled; no per-tick allocations.
/// </para>
/// <para>
/// IMPORTANT — Actors list contains only alive <see cref="IOrchestrationActor"/>
/// entries that also implement <see cref="IFactionAssetProvider"/> with a non-null faction.
/// Unity-null entries are pruned during build.
/// </para>
/// <para>
/// IMPORTANT — Implements <see cref="IWorldQuery"/> for domain/policy consumption.
/// After <see cref="Freeze"/> is called, domains see a consistent snapshot.
/// RuntimeHost-internal fields (receiver lists, RoleByTransform) are NOT exposed via IWorldQuery.
/// </para>
/// </summary>
public sealed class OrchestrationWorldCache : IWorldQuery
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
    /// Per-transform role lookup, resolved once per tick from friendly receivers (RuntimeHost-internal).
    /// IMPORTANT: Kept for compatibility. Prefer IWorldQuery.TryGetRole(EntityId) for new code.
    /// </summary>
    public readonly Dictionary<Transform, RoleAsset> RoleByTransform = new Dictionary<Transform, RoleAsset>(128);

    /// <summary>
    /// Per-role idle bounds, resolved from IdleBoundsRegistry once per tick.
    /// IMPORTANT: Domains/policies read bounds ONLY through IWorldQuery.TryGetIdleBounds,
    /// never via registry.
    /// </summary>
    public readonly Dictionary<RoleAsset, Bounds> ResolvedIdleBounds = new Dictionary<RoleAsset, Bounds>(4);

    /// <summary>
    /// Combat target set resolved from OrchestrationRegistry once per tick.
    /// IMPORTANT: Domains read this ONLY through IWorldQuery.GetCombatTargetSet, never via registry.
    /// </summary>
    public CombatTargetSet ResolvedCombatTargetSet;

    // ──────────────────────────────────────────────────────────────────
    //  IWorldQuery snapshot data — snapshotted during build, frozen for domains
    // ──────────────────────────────────────────────────────────────────

    Vector2 _anchor;
    float _now;

    // Actor snapshots (parallel lists, same indices as Actors)
    readonly List<Vector2> _actorPositions = new List<Vector2>(256);
    readonly List<EntityId> _actorEntityIds = new List<EntityId>(256);
    readonly List<FactionAsset> _actorFactions = new List<FactionAsset>(256);
    readonly List<bool> _actorAlive = new List<bool>(256);

    // Crowd snapshots (IWorldQuery-visible, parallel to FriendlyCrowdTransforms)
    readonly List<Vector2> _crowdPositions = new List<Vector2>(128);
    readonly List<EntityId> _crowdEntityIds = new List<EntityId>(128);

    // Per-entity role lookup (EntityId → RoleAsset)
    readonly Dictionary<EntityId, RoleAsset> _roleByEntityId = new Dictionary<EntityId, RoleAsset>(128);

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

    public Vector2 Anchor
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
    public void SnapshotActors()
    {
#if DEBUG
        Debug.Assert(!_frozen, "[OrchestrationWorldCache] SnapshotActors called after Freeze.");
#endif
        _actorPositions.Clear();
        _actorEntityIds.Clear();
        _actorFactions.Clear();
        _actorAlive.Clear();

        for (int i = 0; i < Actors.Count; i++)
        {
            IOrchestrationActor actor = Actors[i];
            Transform t = actor.GetTransform();

            _actorPositions.Add((Vector2)t.position);
            _actorAlive.Add(actor.IsAlive());

            IFactionAssetProvider fap = actor as IFactionAssetProvider;
            _actorFactions.Add(fap != null ? fap.GetFactionAsset() : null);

            IEntityIdProvider idp = actor as IEntityIdProvider;
            _actorEntityIds.Add(idp != null ? idp.GetEntityId() : EntityId.None);
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
        _crowdEntityIds.Clear();

        for (int i = 0; i < FriendlyCrowdTransforms.Count; i++)
        {
            Transform t = FriendlyCrowdTransforms[i];
            _crowdPositions.Add((Vector2)t.position);

            IEntityIdProvider idp = t.GetComponent<IEntityIdProvider>();
            _crowdEntityIds.Add(idp != null ? idp.GetEntityId() : EntityId.None);
        }
    }

    /// <summary>
    /// Builds the per-entity role lookup from RoleByTransform.
    /// Only entities with <see cref="IEntityIdProvider"/> are included.
    /// </summary>
    public void BuildRoleByEntityId()
    {
#if DEBUG
        Debug.Assert(!_frozen, "[OrchestrationWorldCache] BuildRoleByEntityId called after Freeze.");
#endif
        _roleByEntityId.Clear();

        foreach (var kvp in RoleByTransform)
        {
            Transform t = kvp.Key;
            if (t == null) continue;
            IEntityIdProvider idp = t.GetComponent<IEntityIdProvider>();
            if (idp == null) continue;
            EntityId eid = idp.GetEntityId();
            if (eid.IsNone) continue;
            _roleByEntityId[eid] = kvp.Value;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  RuntimeHost-internal — Transform access (NOT in IWorldQuery)
    //  IMPORTANT: Phase 2B removes this when CombatCommand uses EntityId.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the actor's Transform at the given index. RuntimeHost-internal only.
    /// NOT exposed via IWorldQuery (no scene objects in contracts).
    /// </summary>
    public Transform GetActorTransformInternal(int index)
    {
        return Actors[index].GetTransform();
    }

    // ──────────────────────────────────────────────────────────────────
    //  IWorldQueryBase
    // ──────────────────────────────────────────────────────────────────

    Vector2 IWorldQueryBase.Anchor => _anchor;
    float IWorldQueryBase.Now => _now;

    public int ActorCount => _actorPositions.Count;
    public EntityId GetActorEntityId(int index) => _actorEntityIds[index];
    public Vector2 GetActorPosition(int index) => _actorPositions[index];
    public FactionAsset GetActorFaction(int index) => _actorFactions[index];
    public bool GetActorIsAlive(int index) => _actorAlive[index];

    // ──────────────────────────────────────────────────────────────────
    //  ICrowdQuery
    // ──────────────────────────────────────────────────────────────────

    public int CrowdCount => _crowdPositions.Count;
    public Vector2 GetCrowdPosition(int index) => _crowdPositions[index];
    public EntityId GetCrowdEntityId(int index) => _crowdEntityIds[index];

    // ──────────────────────────────────────────────────────────────────
    //  IRoleQuery
    // ──────────────────────────────────────────────────────────────────

    public bool TryGetRole(EntityId entityId, out RoleAsset role)
    {
        return _roleByEntityId.TryGetValue(entityId, out role);
    }

    // ──────────────────────────────────────────────────────────────────
    //  IIdleBoundsQuery
    // ──────────────────────────────────────────────────────────────────

    public bool TryGetIdleBounds(RoleAsset role, out Bounds bounds)
    {
        return ResolvedIdleBounds.TryGetValue(role, out bounds);
    }

    // ──────────────────────────────────────────────────────────────────
    //  ICombatTargetSetQuery
    // ──────────────────────────────────────────────────────────────────

    public CombatTargetSet GetCombatTargetSet()
    {
        return ResolvedCombatTargetSet;
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
        _actorEntityIds.Clear();
        _actorFactions.Clear();
        _actorAlive.Clear();
        _crowdPositions.Clear();
        _crowdEntityIds.Clear();
        _roleByEntityId.Clear();
    }
}
