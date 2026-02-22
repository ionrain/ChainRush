using UnityEngine;

/// <summary>
/// Controls how <see cref="CombatOrchestratorLite"/> searches for hostile candidates.
/// </summary>
public enum TargetSearchMode
{
    /// <summary>Search within <see cref="CombatOrchestratorLite.aggroRadius"/> of anchor.</summary>
    Radius = 0,

    /// <summary>
    /// Search across the camera viewport. Any hostile visible on screen is a candidate.
    /// IMPORTANT: Uses camera visibility (WorldToViewportPoint), not physics layers.
    /// RATIONALE: Avoids dependency on colliders/layerMask and matches the existing
    /// registry-based architecture.
    /// </summary>
    ScreenViewport = 1
}

/// <summary>
/// Combat domain evaluator. Scans actors via <see cref="IWorldQuery"/> for
/// hostile entities and writes a <see cref="CombatCommand"/> proposal.
/// Owns <see cref="CombatRolePolicyMapAsset"/> / <see cref="CombatRoleConstraintsMapAsset"/>
/// references and contributes them to the arbiter via cached domain registration bindings.
/// <para>
/// IMPORTANT — This class does NOT tick itself. It implements
/// <see cref="IOrchestrationDomain"/> and is polled by
/// <see cref="OrchestrationArbiter"/> each tick.
/// </para>
/// <para>
/// IMPORTANT — Does NOT dispatch commands to receivers. Only writes proposals.
/// The arbiter owns all dispatch logic.
/// </para>
/// <para>
/// IMPORTANT — Does NOT scan <see cref="OrchestrationRegistry"/> directly.
/// Uses the per-tick <see cref="IWorldQuery"/> built by the arbiter.
/// </para>
/// <para>
/// IMPORTANT — CombatCommand uses EntityId for targets (no Transform).
/// CombatTargetSet stores EntityId[]. No OrchestrationWorldCache dependency
/// in FillTargetSet (uses IWorldQuery only).
/// </para>
/// </summary>
public sealed class CombatOrchestratorLite : DomainOrchestrator, IDomainArbitrationProfileSource
{
    public override OrchestrationDomainId DomainId => OrchestrationDomainId.Combat;

    // ──────────────────────────────────────────────────────────────────
    //  Serialized
    // ──────────────────────────────────────────────────────────────────

    [Header("Target Search")]
    [Tooltip("Radius: search within aggroRadius of anchor. ScreenViewport: search visible hostiles on camera.")]
    [SerializeField] TargetSearchMode searchMode = TargetSearchMode.Radius;
    [SerializeField] float aggroRadius = 12f;
    [Tooltip("Camera for ScreenViewport mode. If null, falls back to Camera.main.")]
    [SerializeField] Camera searchCamera;
    [Tooltip("Viewport margin (0..0.2 typical). Allows slightly off-screen targets to qualify.")]
    [SerializeField] float viewportMargin = 0f;

    [Header("Target Set")]
    [Tooltip("Optional shared candidate carrier. If null and autoResolveTargetSet is true, " +
             "resolved from OrchestrationRegistry by faction.")]
    [SerializeField] CombatTargetSet targetSet;
    [Tooltip("When true, resolve targetSet from OrchestrationRegistry if not assigned in inspector.")]
    [SerializeField] bool autoResolveTargetSet = true;
    [Tooltip("Number of Top-K hostile candidates to store (clamped to targetSet.Capacity).")]
    [SerializeField] int targetSetSize = 4;

    [Header("Role Policies")]
    [Tooltip("Optional per-role targeting policy map. If null, units use their own default policies.")]
    [SerializeField] CombatRolePolicyMapAsset rolePolicyMap;

    [Header("Role Constraints")]
    [Tooltip("Optional per-role movement constraints map. If null, units use unconstrained movement.")]
    [SerializeField] CombatRoleConstraintsMapAsset roleConstraintsMap;

    [Header("Debug")]
    [SerializeField] bool debugLog;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Top-K working arrays (allocated once, reused)
    // ──────────────────────────────────────────────────────────────────

    EntityId[] _topKEntityIds;
    float[] _topKScores;
    int _topKCount;

    bool _warnedNoCamera;

    // One-shot guard for auto-resolve target set.
    bool _triedResolveTargetSet;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _triedResolveTargetSet = false;
    }

    // ──────────────────────────────────────────────────────────────────
    //  IDomainArbitrationProfileSource
    // ──────────────────────────────────────────────────────────────────

    public DomainArbitrationProfile GetArbitrationProfile()
    {
        return new DomainArbitrationProfile(stickyPrimary: true);
    }

    protected override IDomainArbiterBindingContributor CreateArbiterBindingContributor()
    {
        return DomainArbiterBindingContributors.CreatePolicyMapContributor(
            idleRolePolicyMapKey: default,
            idleRolePolicyMapApply: null,
            idleRolePolicyMap: null,
            combatRolePolicyMapKey: StrategyCombatArbiterBindingKeys.CombatRolePolicyMap,
            combatRolePolicyMapApply: StrategyCombatArbiterBindingAppliers.CombatRolePolicyMap,
            combatRolePolicyMap: rolePolicyMap,
            combatRoleConstraintsMapKey: StrategyCombatArbiterBindingKeys.CombatRoleConstraintsMap,
            combatRoleConstraintsMapApply: StrategyCombatArbiterBindingAppliers.CombatRoleConstraintsMap,
            combatRoleConstraintsMap: roleConstraintsMap);
    }

    // ──────────────────────────────────────────────────────────────────
    //  IOrchestrationDomain
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates combat state: finds closest hostile from cached actors,
    /// fills Top-K target set, and writes a combat proposal.
    /// Does NOT dispatch commands. Does NOT scan OrchestrationRegistry.
    /// </summary>
    public override void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals)
    {
        IWorldQuery world = ctx.World;

        // ── Find closest hostile from IWorldQuery ────────────────
        int bestIndex = FindClosestHostileIndex(world, ctx);
        EntityId bestEntityId = bestIndex >= 0 ? world.GetActorEntityId(bestIndex) : EntityId.None;

        // ── Build command (engine-agnostic: EntityId, no Transform) ──
        CombatCommand cmd = !bestEntityId.IsNone
            ? CombatCommand.Create(CombatCommandType.AttackTarget, targetEntityId: bestEntityId,
                debugLabel: "Orchestrator=CombatOrchestratorLite")
            : CombatCommand.Create(CombatCommandType.Hold,
                debugLabel: "Orchestrator=CombatOrchestratorLite");

        // ── Fill Top-K target set (optional) ─────────────────────
        if (targetSet == null && autoResolveTargetSet && !_triedResolveTargetSet)
        {
            _triedResolveTargetSet = true;
            OrchestrationWorldCache worldCache = world as OrchestrationWorldCache;
            if (worldCache != null)
                targetSet = worldCache.GetCombatTargetSetInternal(); // may be null — that's ok
        }

        if (targetSet != null)
            FillTargetSet(world, ctx);

        // ── Write proposal ───────────────────────────────────────
        bool threatPresent = !bestEntityId.IsNone;
        proposals.SetCombat(cmd, threatPresent);

        if (ctx.DebugLog || debugLog)
        {
            string targetName = !bestEntityId.IsNone ? bestEntityId.ToStableInt().ToString() : "none";
            int topK = targetSet != null ? _topKCount : 0;
            Debug.Log(string.Concat(
                "[CombatOrchestratorLite] TargetEid=", targetName,
                ", TopK=", topK.ToString(),
                ", Mode=", searchMode.ToString()), this);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Hostile search — uses IWorldQuery index-based API
    //  Returns actor index or -1 if no hostile found.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans actors via <see cref="IWorldQuery"/> for the closest alive hostile entity.
    /// Returns the actor index, or -1 if none found.
    /// PERF: Index-based iteration, no LINQ, no per-tick allocations.
    /// </summary>
    int FindClosestHostileIndex(IWorldQuery world, OrchestrationArbiterContext ctx)
    {
        Camera cam = null;
        if (searchMode == TargetSearchMode.ScreenViewport)
        {
            cam = searchCamera != null ? searchCamera : Camera.main;
            if (cam == null)
            {
                if (debugLog && !_warnedNoCamera)
                {
                    Debug.LogWarning("[CombatOrchestratorLite] ScreenViewport mode but no camera available. " +
                        "Assign searchCamera or ensure Camera.main exists.", this);
                    _warnedNoCamera = true;
                }
                return -1;
            }
        }

        Float2 anchor = ctx.Anchor;
        float aggroSqr = aggroRadius * aggroRadius;
        float bestDistSqr = float.MaxValue;
        int bestIndex = -1;

        int actorCount = world.ActorCount;
        for (int i = 0; i < actorCount; i++)
        {
            // Hostile check — typed-only via IWorldQuery
            if (!world.GetActorIsHostile(i))
                continue;

            Float2 actorPos = world.GetActorPosition(i);

            bool qualifies;
            switch (searchMode)
            {
                case TargetSearchMode.ScreenViewport:
                    qualifies = IsOnScreen(actorPos, cam);
                    break;

                default: // Radius
                    float distToAnchor = Float2.DistanceSqr(actorPos, anchor);
                    qualifies = distToAnchor <= aggroSqr;
                    break;
            }

            if (!qualifies) continue;

            float distSqr = Float2.DistanceSqr(actorPos, anchor);
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Top-K target set — uses IWorldQuery only (no worldCache/Transform)
    //  IMPORTANT: Applies the exact same hostile filters as FindClosestHostileIndex
    //  (faction Hostile, Radius/ScreenViewport). No LINQ, no allocations.
    // ──────────────────────────────────────────────────────────────────

    void FillTargetSet(IWorldQuery world, OrchestrationArbiterContext ctx)
    {
        int k = targetSetSize > 0 ? targetSetSize : 1;
        if (k > targetSet.Capacity) k = targetSet.Capacity;
        EnsureTopKArrays(k);
        _topKCount = 0;

        Camera cam = null;
        if (searchMode == TargetSearchMode.ScreenViewport)
        {
            cam = searchCamera != null ? searchCamera : Camera.main;
            if (cam == null) return;
        }

        Float2 anchor = ctx.Anchor;
        float aggroSqr = aggroRadius * aggroRadius;

        int actorCount = world.ActorCount;
        for (int i = 0; i < actorCount; i++)
        {
            if (!world.GetActorIsHostile(i))
                continue;

            // IMPORTANT: alive check via IWorldQuery — no Transform/gameObject needed
            if (!world.GetActorIsAlive(i))
                continue;

            Float2 actorPos = world.GetActorPosition(i);

            bool qualifies;
            switch (searchMode)
            {
                case TargetSearchMode.ScreenViewport:
                    qualifies = IsOnScreen(actorPos, cam);
                    break;
                default:
                    qualifies = Float2.DistanceSqr(actorPos, anchor) <= aggroSqr;
                    break;
            }

            if (!qualifies) continue;

            EntityId eid = world.GetActorEntityId(i);
            if (eid.IsNone) continue;

            float distSqr = Float2.DistanceSqr(actorPos, anchor);
            InsertTopK(eid, distSqr, k);
        }

        targetSet.SetTargets(_topKEntityIds, _topKCount, ctx.Now);
    }

    void EnsureTopKArrays(int k)
    {
        if (_topKEntityIds != null && _topKEntityIds.Length >= k)
            return;
        _topKEntityIds = new EntityId[k];
        _topKScores = new float[k];
    }

    void InsertTopK(EntityId eid, float score, int k)
    {
        if (_topKCount < k)
        {
            int pos = _topKCount;
            while (pos > 0 && _topKScores[pos - 1] > score)
            {
                _topKEntityIds[pos] = _topKEntityIds[pos - 1];
                _topKScores[pos] = _topKScores[pos - 1];
                pos--;
            }
            _topKEntityIds[pos] = eid;
            _topKScores[pos] = score;
            _topKCount++;
        }
        else if (score < _topKScores[_topKCount - 1])
        {
            int pos = _topKCount - 1;
            while (pos > 0 && _topKScores[pos - 1] > score)
            {
                _topKEntityIds[pos] = _topKEntityIds[pos - 1];
                _topKScores[pos] = _topKScores[pos - 1];
                pos--;
            }
            _topKEntityIds[pos] = eid;
            _topKScores[pos] = score;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Screen visibility helper
    // ──────────────────────────────────────────────────────────────────

    bool IsOnScreen(Float2 worldPos, Camera cam)
    {
        Vector3 vp = cam.WorldToViewportPoint(new Vector3(worldPos.X, worldPos.Y, 0f));
        if (vp.z <= 0f) return false;
        float m = Mathf.Max(0f, viewportMargin);
        return vp.x >= -m && vp.x <= 1f + m && vp.y >= -m && vp.y <= 1f + m;
    }
}
