using System.Collections;
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
/// First vertical-slice orchestrator. Periodically scans for the closest hostile
/// entity and issues <see cref="CombatCommandType.AttackTarget"/> (or
/// <see cref="CombatCommandType.Hold"/>) to all faction-matched combat receivers.
/// <para>
/// IMPORTANT — This orchestrator is intentionally naive. It exists to validate
/// the full pipeline: Registry → Orchestrator → Executor → AIBrain.
/// It does not implement priority queues, threat scoring, or per-unit tactics.
/// </para>
/// <para>
/// IMPORTANT — Requires both <see cref="orchestratorFaction"/> and
/// <see cref="typedRelations"/> to be assigned. If either is null, the orchestrator
/// issues Hold to all receivers and logs a one-time warning (fail-closed).
/// </para>
/// <para>
/// IMPORTANT — Faction identity is fully typed via <see cref="FactionAsset"/>.
/// Actors and receivers must implement <see cref="IFactionAssetProvider"/> with a
/// non-null faction asset to participate in combat orchestration.
/// </para>
/// </summary>
public sealed class CombatOrchestratorLite : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────
    //  Serialized
    // ──────────────────────────────────────────────────────────────────

    [SerializeField] FactionRelationTableAsset typedRelations;

    [Header("Orchestration")]
    [SerializeField] FactionAsset orchestratorFaction;
    [SerializeField] float tickInterval = 0.75f;
    [SerializeField] float aggroRadius = 12f;
    [SerializeField] Transform anchorOverride;

    [Header("Target Search")]
    [Tooltip("Radius: search within aggroRadius of anchor. ScreenViewport: search visible hostiles on camera.")]
    [SerializeField] TargetSearchMode searchMode = TargetSearchMode.Radius;
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

    [Header("Debug")]
    [SerializeField] bool debugLog;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime
    // ──────────────────────────────────────────────────────────────────

    Coroutine _tickCoroutine;
    WaitForSeconds _wait;
    bool _warnedNoCamera;
    bool _warnedMissingSetup;

    // Top-K working arrays — allocated once in EnsureTopKArrays(), reused each tick.
    Transform[] _topKTransforms;
    float[] _topKScores;
    int _topKCount;

    // One-shot guard for auto-resolve: try once in OnEnable, once in first Tick, then stop.
    bool _triedResolveTargetSet;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _wait = new WaitForSeconds(tickInterval);
        _warnedNoCamera = false;
        _warnedMissingSetup = false;
        _triedResolveTargetSet = false;

        TryResolveTargetSet();

        _tickCoroutine = StartCoroutine(TickLoop());
    }

    void OnDisable()
    {
        if (_tickCoroutine != null)
        {
            StopCoroutine(_tickCoroutine);
            _tickCoroutine = null;
        }
    }

    IEnumerator TickLoop()
    {
        while (true)
        {
            yield return _wait;
            Tick();
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Tick — domain-level, no Unit-specific types
    // ──────────────────────────────────────────────────────────────────

    void Tick()
    {
        // Fail-closed: if required typed assets are missing, issue Hold and warn once.
        if (orchestratorFaction == null || typedRelations == null)
        {
            if (!_warnedMissingSetup)
            {
                _warnedMissingSetup = true;
                Debug.LogWarning("[CombatOrchestratorLite] Missing orchestratorFaction or typedRelations. " +
                    "Issuing Hold to all receivers. Assign both to enable combat orchestration.", this);
            }

            CombatCommand holdCmd = CombatCommand.Create(CombatCommandType.Hold,
                debugLabel: "Orchestrator=CombatOrchestratorLite");
            IssueToReceivers(holdCmd);
            return;
        }

        Vector2 anchor = anchorOverride != null
            ? (Vector2)anchorOverride.position
            : (Vector2)transform.position;

        // ── Find closest hostile ─────────────────────────────────────
        Transform closestHostile = FindClosestHostile(anchor);

        // Safety guard: if the hostile was destroyed between scan and issue
        if (closestHostile == null)
            closestHostile = null; // explicit null coalesce for Unity destroyed objects

        // ── Build command ────────────────────────────────────────────
        CombatCommand cmd = closestHostile != null
            ? CombatCommand.Create(CombatCommandType.AttackTarget, targetTransform: closestHostile,
                debugLabel: "Orchestrator=CombatOrchestratorLite")
            : CombatCommand.Create(CombatCommandType.Hold,
                debugLabel: "Orchestrator=CombatOrchestratorLite");

        // ── Fill Top-K target set (optional) ──────────────────────
        // Late resolve: if CombatTargetSet registered after our OnEnable, try once.
        if (targetSet == null && autoResolveTargetSet && !_triedResolveTargetSet)
            TryResolveTargetSet();

        if (targetSet != null)
            FillTargetSet(anchor);

        // ── Issue to faction-filtered receivers ──────────────────────
        int commandedCount = IssueToReceivers(cmd);

        if (debugLog)
        {
            string targetName = closestHostile != null ? closestHostile.name : "none";
            int topK = targetSet != null ? _topKCount : 0;
            Debug.Log($"[CombatOrchestratorLite] Target={targetName}, Commanded={commandedCount}, TopK={topK}, Mode={searchMode}", this);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Auto-resolve (Variant B)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// One-shot attempt to resolve <see cref="targetSet"/> from
    /// <see cref="OrchestrationRegistry"/> by typed faction. Sets the guard flag
    /// so subsequent calls are no-ops. Called in OnEnable and once in Tick.
    /// </summary>
    void TryResolveTargetSet()
    {
        _triedResolveTargetSet = true;

        if (targetSet != null) return;
        if (!autoResolveTargetSet) return;
        if (orchestratorFaction == null) return;

        OrchestrationRegistry.TryGetCombatTargetSet(orchestratorFaction, out targetSet);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Faction relation helper
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the relation between this orchestrator and an actor using typed
    /// <see cref="FactionAsset"/> references and <see cref="FactionRelationTableAsset"/>.
    /// Returns <see cref="FactionRelation.Neutral"/> (skip) if the actor does not
    /// implement <see cref="IFactionAssetProvider"/> or returns a null faction.
    /// </summary>
    FactionRelation GetRelationTo(IOrchestrationActor actor)
    {
        if (!(actor is IFactionAssetProvider provider))
            return FactionRelation.Neutral;

        FactionAsset actorFaction = provider.GetFactionAsset();
        if (actorFaction == null)
            return FactionRelation.Neutral;

        return typedRelations.GetRelation(orchestratorFaction, actorFaction);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Hostile search
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans <see cref="OrchestrationRegistry.StateReporters"/> for the closest
    /// alive hostile entity. Candidate filtering depends on <see cref="searchMode"/>:
    /// <list type="bullet">
    /// <item><see cref="TargetSearchMode.Radius"/>: within <see cref="aggroRadius"/> of anchor.</item>
    /// <item><see cref="TargetSearchMode.ScreenViewport"/>: visible on camera viewport.</item>
    /// </list>
    /// Scoring is always by sqrMagnitude to anchor (closest wins).
    /// PERF: Index-based iteration, no LINQ, no per-tick allocations.
    /// </summary>
    Transform FindClosestHostile(Vector2 anchor)
    {
        // Resolve camera once per tick for ScreenViewport mode
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

        float aggroSqr = aggroRadius * aggroRadius;
        float bestDistSqr = float.MaxValue;
        Transform bestTransform = null;

        IReadOnlyList<IStateReporter> reporters = OrchestrationRegistry.StateReporters;
        for (int i = 0; i < reporters.Count; i++)
        {
            IStateReporter reporter = reporters[i];
            if (reporter == null) continue;

            if (!(reporter is IOrchestrationActor actor)) continue;
            if (!actor.IsAlive()) continue;

            if (GetRelationTo(actor) != FactionRelation.Hostile) continue;

            Transform actorTransform = actor.GetTransform();
            if (actorTransform == null) continue;

            Vector2 actorPos = (Vector2)actorTransform.position;

            // ── Candidate filter ─────────────────────────────────────
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

            // ── Scoring: closest to anchor wins ──────────────────────
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
    //  Top-K target set
    //  IMPORTANT: Applies the exact same hostile filters as FindClosestHostile
    //  (faction Hostile, alive, Radius/ScreenViewport). No LINQ, no allocations.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scans hostiles and fills <see cref="targetSet"/> with the Top-K closest to anchor.
    /// Uses insertion into fixed-size arrays to avoid sorting or heap allocations.
    /// </summary>
    void FillTargetSet(Vector2 anchor)
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

        float aggroSqr = aggroRadius * aggroRadius;

        IReadOnlyList<IStateReporter> reporters = OrchestrationRegistry.StateReporters;
        for (int i = 0; i < reporters.Count; i++)
        {
            IStateReporter reporter = reporters[i];
            if (reporter == null) continue;

            if (!(reporter is IOrchestrationActor actor)) continue;
            if (!actor.IsAlive()) continue;

            if (GetRelationTo(actor) != FactionRelation.Hostile) continue;

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

    /// <summary>
    /// Ensures Top-K working arrays are allocated with at least <paramref name="k"/> slots.
    /// </summary>
    void EnsureTopKArrays(int k)
    {
        if (_topKTransforms != null && _topKTransforms.Length >= k)
            return;
        _topKTransforms = new Transform[k];
        _topKScores = new float[k];
    }

    /// <summary>
    /// Insertion-based Top-K maintenance. Keeps entries sorted ascending by score (closest first).
    /// If the array is not full, append. Otherwise replace the worst (last) entry if better,
    /// then shift into sorted position.
    /// </summary>
    void InsertTopK(Transform t, float score, int k)
    {
        if (_topKCount < k)
        {
            // Array not full — insert in sorted position
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
            // Better than worst — replace last, shift into position
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

    /// <summary>
    /// Returns true if the world position is within the camera's viewport
    /// (plus optional margin). Designed for orthographic 2D cameras.
    /// </summary>
    bool IsOnScreen(Vector2 worldPos, Camera cam)
    {
        Vector3 vp = cam.WorldToViewportPoint(new Vector3(worldPos.x, worldPos.y, 0f));
        if (vp.z <= 0f) return false;
        float m = Mathf.Max(0f, viewportMargin);
        return vp.x >= -m && vp.x <= 1f + m && vp.y >= -m && vp.y <= 1f + m;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Command dispatch
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Issues the command to all registered combat receivers whose faction
    /// is Friendly to <see cref="orchestratorFaction"/> via <see cref="typedRelations"/>.
    /// Receivers must implement <see cref="IFactionAssetProvider"/> with a non-null
    /// faction asset. Receivers without typed faction info are skipped.
    /// Returns the number of receivers that were commanded.
    /// </summary>
    int IssueToReceivers(CombatCommand cmd)
    {
        int count = 0;
        IReadOnlyList<ICombatCommandReceiver> receivers = OrchestrationRegistry.CombatReceivers;

        for (int i = 0; i < receivers.Count; i++)
        {
            ICombatCommandReceiver receiver = receivers[i];
            if (receiver == null) continue;

            // Receiver must provide typed faction via IFactionAssetProvider.
            if (!(receiver is IFactionAssetProvider fp))
                continue;

            FactionAsset receiverFaction = fp.GetFactionAsset();
            if (receiverFaction == null)
                continue;

            if (typedRelations.GetRelation(orchestratorFaction, receiverFaction) != FactionRelation.Friendly)
                continue;

            receiver.ApplyCombatCommand(cmd);
            count++;
        }

        return count;
    }
}
