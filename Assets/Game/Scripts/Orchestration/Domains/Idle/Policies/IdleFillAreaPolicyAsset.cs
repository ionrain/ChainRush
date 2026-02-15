using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Idle policy that chooses a point inside provided bounds while avoiding crowding.
/// Uses deterministic sampling with "personal space" and "crowd penalty" scoring.
/// <para>
/// IMPORTANT — Allocation-free and stateless. Per-unit state belongs in selectors/executors.
/// </para>
/// <para>
/// IMPORTANT — Crowd scoring counts FRIENDLY actors only. Without this filter,
/// idle would treat enemies as crowd and behave oddly.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "IdleFillAreaPolicy", menuName = "Game/Orchestration/Idle/Fill Area Policy")]
public sealed class IdleFillAreaPolicyAsset : IdlePolicyAsset
{
    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Sampling
    // ──────────────────────────────────────────────────────────────────

    [Header("Sampling")]
    [Tooltip("Number of candidate positions to evaluate per tick.")]
    [SerializeField] int samples = 10;

    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Personal Space
    // ──────────────────────────────────────────────────────────────────

    [Header("Personal Space")]
    [Tooltip("Minimum desired distance to other friendlies. Hard penalty if violated.")]
    [SerializeField] float personalSpace = 0.8f;

    [Tooltip("Radius within which friendlies contribute to crowding score.")]
    [SerializeField] float crowdPenaltyRadius = 2.0f;

    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Movement
    // ──────────────────────────────────────────────────────────────────

    [Header("Movement")]
    [SerializeField] float stopDistance = 0.1f;

    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Debug
    // ──────────────────────────────────────────────────────────────────

    [Header("Debug")]
    [SerializeField] bool includeDebugInfo;

    // ──────────────────────────────────────────────────────────────────
    //  Constants
    // ──────────────────────────────────────────────────────────────────

    const int FNV_OFFSET = unchecked((int)2166136261);
    const int FNV_PRIME = 16777619;
    const float PERSONAL_SPACE_PENALTY = 1000f;
    const float ANCHOR_DISTANCE_WEIGHT = 0.1f;
    const int SAMPLE_SEED_PRIME = 0x45D9F3B;

    /// <summary>
    /// Fallback bounds size when no bounds provider is available.
    /// </summary>
    static readonly Vector3 FallbackBoundsSize = new Vector3(6f, 3f, 1f);

    // ──────────────────────────────────────────────────────────────────
    //  Base overload — delegates to ctx-aware
    // ──────────────────────────────────────────────────────────────────

    public override IdleCommand ChooseCommand(Transform self, Vector2 anchor, float now, out string debugInfo)
    {
        debugInfo = null;
        return IdleCommand.Hold();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Context-aware overload — main logic
    // ──────────────────────────────────────────────────────────────────

    public override IdleCommand ChooseCommand(
        Transform self, Vector2 anchor, float now,
        int roleSeed, int entitySeed,
        OrchestrationArbiterContext ctx,
        out string debugInfo)
    {
        debugInfo = null;

        // ── Determine bounds ──────────────────────────────────────────
        Bounds bounds = default;
        bool hasBounds = false;

        UnitIdleCommandExecutor executor = self != null
            ? self.GetComponentInParent<UnitIdleCommandExecutor>()
            : null;

        if (executor != null)
            hasBounds = executor.TryGetIdleBounds(out bounds);

        if (!hasBounds)
            bounds = new Bounds(new Vector3(anchor.x, anchor.y, 0f), FallbackBoundsSize);

        // ── Pre-compute friendly positions ────────────────────────────
        // PERF: Iterate actors once, collect friendly positions for scoring
        List<IOrchestrationActor> actors = ctx.World.Actors;
        int actorCount = actors.Count;
        float personalSpaceSqr = personalSpace * personalSpace;
        float crowdRadiusSqr = crowdPenaltyRadius * crowdPenaltyRadius;

        // ── Sample and score candidates ───────────────────────────────
        Vector2 bestPoint = anchor;
        float bestScore = float.MaxValue;
        int sampleCount = samples > 0 ? samples : 10;

        for (int i = 0; i < sampleCount; i++)
        {
            int seed = roleSeed ^ entitySeed ^ (i * SAMPLE_SEED_PRIME);
            Vector2 candidate = RandomPointInBounds(bounds, seed);

            float score = 0f;

            // Score: crowding (friendly actors only)
            for (int a = 0; a < actorCount; a++)
            {
                IOrchestrationActor actor = actors[a];
                if (actor is Object uo && uo == null) continue;

                // IMPORTANT: Only count friendly actors
                IFactionAssetProvider fap = actor as IFactionAssetProvider;
                if (fap == null) continue;
                FactionAsset actorFaction = fap.GetFactionAsset();
                if (actorFaction == null) continue;
                if (ctx.Relations.GetRelation(ctx.OrchestratorFaction, actorFaction) != FactionRelation.Friendly)
                    continue;

                Transform actorTransform = actor.GetTransform();
                if (actorTransform == null) continue;

                // Skip self
                if (actorTransform == self) continue;

                Vector2 actorPos = (Vector2)actorTransform.position;
                float sqrDist = (candidate - actorPos).sqrMagnitude;

                // Hard penalty: personal space violation
                if (sqrDist < personalSpaceSqr)
                    score += PERSONAL_SPACE_PENALTY;

                // Soft penalty: crowding
                if (sqrDist < crowdRadiusSqr)
                    score += 1f;
            }

            // Penalty for distance from anchor (keeps units generally around anchor)
            float anchorSqrDist = (candidate - anchor).sqrMagnitude;
            score += anchorSqrDist * ANCHOR_DISTANCE_WEIGHT;

            if (score < bestScore)
            {
                bestScore = score;
                bestPoint = candidate;
            }
        }

        // ── Build debug info ──────────────────────────────────────────
        if (includeDebugInfo)
        {
            debugInfo = string.Concat(
                "Idle=FillArea PS=", personalSpace.ToString("F1"),
                " BestScore=", bestScore.ToString("F1"));
        }

        return IdleCommand.MoveToPoint(bestPoint, stopDistance,
            includeDebugInfo ? debugInfo : null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Deterministic helpers (private static, no allocations)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stable FNV-1a hash normalized to [0, 1].
    /// IMPORTANT: Does NOT use GetHashCode(). Fully deterministic.
    /// </summary>
    static float Hash01(int x)
    {
        unchecked
        {
            int h = FNV_OFFSET;
            h = (h ^ (x & 0xFF)) * FNV_PRIME;
            h = (h ^ ((x >> 8) & 0xFF)) * FNV_PRIME;
            h = (h ^ ((x >> 16) & 0xFF)) * FNV_PRIME;
            h = (h ^ ((x >> 24) & 0xFF)) * FNV_PRIME;
            return (float)(h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }

    /// <summary>
    /// Returns a deterministic random point within the XY extents of the bounds.
    /// </summary>
    static Vector2 RandomPointInBounds(Bounds b, int seed)
    {
        float tx = Hash01(seed);
        float ty = Hash01(seed ^ 0x6C62272E);
        float x = b.min.x + tx * b.size.x;
        float y = b.min.y + ty * b.size.y;
        return new Vector2(x, y);
    }
}
