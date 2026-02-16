using System.Collections.Generic;
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
/// Combat domain evaluator. Scans <see cref="OrchestrationWorldCache.Actors"/> for
/// hostile entities and writes a <see cref="CombatCommand"/> proposal.
/// Owns the <see cref="CombatRolePolicyMapAsset"/> reference and exposes it via
/// <see cref="ICombatRolePolicyMapSource"/> so the arbiter can pull it each tick.
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
/// Uses the per-tick <see cref="OrchestrationWorldCache"/> built by the arbiter.
/// </para>
/// </summary>
public sealed class CombatOrchestratorLite : DomainOrchestrator, ICombatRolePolicyMapSource, ICombatRoleConstraintsMapSource
{
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

    Transform[] _topKTransforms;
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
    //  ICombatRolePolicyMapSource
    // ──────────────────────────────────────────────────────────────────

    public CombatRolePolicyMapAsset GetCombatRolePolicyMap() => rolePolicyMap;

    // ──────────────────────────────────────────────────────────────────
    //  ICombatRoleConstraintsMapSource
    // ──────────────────────────────────────────────────────────────────

    public CombatRoleConstraintsMapAsset GetCombatRoleConstraintsMap() => roleConstraintsMap;

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
        Vector2 anchor = ctx.Anchor;

        // ── Find closest hostile from cached actors ──────────────
        Transform closestHostile = FindClosestHostile(ctx);

        // Unity-null coalesce for destroyed objects
        if (closestHostile == null)
            closestHostile = null;

        // ── Build command ────────────────────────────────────────
        CombatCommand cmd = closestHostile != null
            ? CombatCommand.Create(CombatCommandType.AttackTarget, targetTransform: closestHostile,
                debugLabel: "Orchestrator=CombatOrchestratorLite")
            : CombatCommand.Create(CombatCommandType.Hold,
                debugLabel: "Orchestrator=CombatOrchestratorLite");

        // ── Fill Top-K target set (optional) ─────────────────────
        if (targetSet == null && autoResolveTargetSet && !_triedResolveTargetSet)
            TryResolveTargetSet(ctx.OrchestratorFaction);

        if (targetSet != null)
            FillTargetSet(ctx);

        // ── Write proposal ───────────────────────────────────────
        bool threatPresent = closestHostile != null;
        proposals.SetCombat(cmd, threatPresent);

        if (ctx.DebugLog || debugLog)
        {
            string targetName = closestHostile != null ? closestHostile.name : "none";
            int topK = targetSet != null ? _topKCount : 0;
            Debug.Log(string.Concat(
                "[CombatOrchestratorLite] Target=", targetName,
                ", TopK=", topK.ToString(),
                ", Mode=", searchMode.ToString()), this);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Auto-resolve target set
    // ──────────────────────────────────────────────────────────────────

    void TryResolveTargetSet(FactionAsset faction)
    {
        _triedResolveTargetSet = true;

        if (targetSet != null) return;
        if (!autoResolveTargetSet) return;
        if (faction == null) return;

        OrchestrationRegistry.TryGetCombatTargetSet(faction, out targetSet);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Hostile search — iterates ctx.World.Actors
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans <see cref="OrchestrationWorldCache.Actors"/> for the closest alive hostile
    /// entity. Candidate filtering depends on <see cref="searchMode"/>.
    /// Scoring is always by sqrMagnitude to anchor (closest wins).
    /// PERF: Index-based iteration, no LINQ, no per-tick allocations.
    /// </summary>
    Transform FindClosestHostile(OrchestrationArbiterContext ctx)
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
                return null;
            }
        }

        Vector2 anchor = ctx.Anchor;
        float aggroSqr = aggroRadius * aggroRadius;
        float bestDistSqr = float.MaxValue;
        Transform bestTransform = null;

        List<IOrchestrationActor> actors = ctx.World.Actors;
        for (int i = 0; i < actors.Count; i++)
        {
            IOrchestrationActor actor = actors[i];
            if (actor == null) continue;
            if (actor is Object uo && uo == null) continue;

            // Hostile check — typed-only
            if (!(actor is IFactionAssetProvider fap)) continue;
            FactionAsset actorFaction = fap.GetFactionAsset();
            if (actorFaction == null) continue;
            if (ctx.Relations.GetRelation(ctx.OrchestratorFaction, actorFaction) != FactionRelation.Hostile)
                continue;

            Transform actorTransform = actor.GetTransform();
            if (actorTransform == null) continue;

            Vector2 actorPos = (Vector2)actorTransform.position;

            bool qualifies;
            switch (searchMode)
            {
                case TargetSearchMode.ScreenViewport:
                    qualifies = IsOnScreen(actorPos, cam);
                    break;

                default: // Radius
                    float distToAnchor = (actorPos - anchor).sqrMagnitude;
                    qualifies = distToAnchor <= aggroSqr;
                    break;
            }

            if (!qualifies) continue;

            float distSqr = (actorPos - anchor).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                bestTransform = actorTransform;
            }
        }

        return bestTransform;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Top-K target set — iterates ctx.World.Actors
    //  IMPORTANT: Applies the exact same hostile filters as FindClosestHostile
    //  (faction Hostile, alive, Radius/ScreenViewport). No LINQ, no allocations.
    // ──────────────────────────────────────────────────────────────────

    void FillTargetSet(OrchestrationArbiterContext ctx)
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

        Vector2 anchor = ctx.Anchor;
        float aggroSqr = aggroRadius * aggroRadius;

        List<IOrchestrationActor> actors = ctx.World.Actors;
        for (int i = 0; i < actors.Count; i++)
        {
            IOrchestrationActor actor = actors[i];
            if (actor == null) continue;
            if (actor is Object uo && uo == null) continue;

            // Hostile check — typed-only
            if (!(actor is IFactionAssetProvider fap)) continue;
            FactionAsset actorFaction = fap.GetFactionAsset();
            if (actorFaction == null) continue;
            if (ctx.Relations.GetRelation(ctx.OrchestratorFaction, actorFaction) != FactionRelation.Hostile)
                continue;

            Transform actorTransform = actor.GetTransform();
            if (actorTransform == null) continue;
            if (!actorTransform.gameObject.activeInHierarchy) continue;

            Vector2 actorPos = (Vector2)actorTransform.position;

            bool qualifies;
            switch (searchMode)
            {
                case TargetSearchMode.ScreenViewport:
                    qualifies = IsOnScreen(actorPos, cam);
                    break;
                default:
                    qualifies = (actorPos - anchor).sqrMagnitude <= aggroSqr;
                    break;
            }

            if (!qualifies) continue;

            float distSqr = (actorPos - anchor).sqrMagnitude;
            InsertTopK(actorTransform, distSqr, k);
        }

        targetSet.SetTargets(_topKTransforms, _topKCount);
    }

    void EnsureTopKArrays(int k)
    {
        if (_topKTransforms != null && _topKTransforms.Length >= k)
            return;
        _topKTransforms = new Transform[k];
        _topKScores = new float[k];
    }

    void InsertTopK(Transform t, float score, int k)
    {
        if (_topKCount < k)
        {
            int pos = _topKCount;
            while (pos > 0 && _topKScores[pos - 1] > score)
            {
                _topKTransforms[pos] = _topKTransforms[pos - 1];
                _topKScores[pos] = _topKScores[pos - 1];
                pos--;
            }
            _topKTransforms[pos] = t;
            _topKScores[pos] = score;
            _topKCount++;
        }
        else if (score < _topKScores[_topKCount - 1])
        {
            int pos = _topKCount - 1;
            while (pos > 0 && _topKScores[pos - 1] > score)
            {
                _topKTransforms[pos] = _topKTransforms[pos - 1];
                _topKScores[pos] = _topKScores[pos - 1];
                pos--;
            }
            _topKTransforms[pos] = t;
            _topKScores[pos] = score;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Screen visibility helper
    // ──────────────────────────────────────────────────────────────────

    bool IsOnScreen(Vector2 worldPos, Camera cam)
    {
        Vector3 vp = cam.WorldToViewportPoint(new Vector3(worldPos.x, worldPos.y, 0f));
        if (vp.z <= 0f) return false;
        float m = Mathf.Max(0f, viewportMargin);
        return vp.x >= -m && vp.x <= 1f + m && vp.y >= -m && vp.y <= 1f + m;
    }
}
