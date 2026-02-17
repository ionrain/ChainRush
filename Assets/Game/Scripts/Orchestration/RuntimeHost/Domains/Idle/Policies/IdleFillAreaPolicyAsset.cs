using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Idle policy that chooses a point inside provided bounds while avoiding crowding.
/// Uses deterministic sampling with "personal space" and "crowd penalty" scoring
/// via the shared <see cref="CrowdScoringUtility"/>.
/// <para>
/// IMPORTANT — Allocation-free and stateless. Per-unit state belongs in selectors/executors.
/// </para>
/// <para>
/// IMPORTANT — Crowd scoring uses <see cref="OrchestrationWorldCache.FriendlyCrowdTransforms"/>
/// (pre-filtered friendly spatial units). Without this filter, idle would treat enemies
/// as crowd and behave oddly.
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

        // IMPORTANT: Policy reads world only via ctx.World (Charter v3 §2.1). No GetComponent.
        if (self != null)
        {
            RoleAsset role;
            if (ctx.World.RoleByTransform.TryGetValue(self, out role) && role != null)
                hasBounds = ctx.World.ResolvedIdleBounds.TryGetValue(role, out bounds);
        }

        if (!hasBounds)
            bounds = new Bounds(new Vector3(anchor.x, anchor.y, 0f), FallbackBoundsSize);

        // ── Pre-compute squared thresholds ──────────────────────────────
        float personalSpaceSqr = personalSpace * personalSpace;
        float crowdRadiusSqr = crowdPenaltyRadius * crowdPenaltyRadius;

        // Use pre-filtered friendly crowd transforms from world cache
        IReadOnlyList<Transform> friendlies = ctx.World.FriendlyCrowdTransforms;

        // ── Sample and score candidates ───────────────────────────────
        Vector2 bestPoint = anchor;
        float bestScore = float.MaxValue;
        int sampleCount = samples > 0 ? samples : 10;

        for (int i = 0; i < sampleCount; i++)
        {
            int seed = roleSeed ^ entitySeed ^ (i * SAMPLE_SEED_PRIME);
            Vector2 candidate = CrowdScoringUtility.RandomPointInBounds(bounds, seed);

            // Crowd penalty via shared utility (soft penalty, never rejects)
            float score = CrowdScoringUtility.ScoreCrowdPenalty(
                friendlies, self, candidate,
                personalSpaceSqr, crowdRadiusSqr, PERSONAL_SPACE_PENALTY);

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
}
