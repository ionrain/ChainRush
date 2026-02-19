using UnityEngine;

/// <summary>
/// Idle policy that chooses a point inside provided bounds while avoiding crowding.
/// Uses deterministic sampling with "personal space" and "crowd penalty" scoring
/// via the shared <see cref="CrowdScoringUtility"/>.
/// <para>
/// IMPORTANT — Allocation-free and stateless. Per-unit state belongs in selectors/executors.
/// </para>
/// <para>
/// IMPORTANT — Crowd scoring uses <see cref="ICrowdQuery"/> (pre-filtered friendly
/// spatial units via IWorldQuery). Without this filter, idle would treat enemies
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
    static readonly Float2 FallbackBoundsSize = new Float2(6f, 3f);

    // ──────────────────────────────────────────────────────────────────
    //  Base overload — Hold (requires world context to function)
    // ──────────────────────────────────────────────────────────────────

    public override IdleCommand ChooseCommand(Transform self, Vector2 anchor, float now, out string debugInfo)
    {
        debugInfo = null;
        return IdleCommand.Hold();
    }

    // ──────────────────────────────────────────────────────────────────
    //  IWorldQuery-based overload — main logic
    //  IMPORTANT: This is the primary entry point called by the arbiter.
    //  Uses ICrowdQuery + IRoleQuery + IIdleBoundsQuery for scoring.
    // ──────────────────────────────────────────────────────────────────

    public override IdleCommand ChooseCommand(
        Float2 selfPosition, EntityId selfId,
        Float2 anchor, float now,
        int roleSeed, int entitySeed,
        IWorldQuery world,
        out string debugInfo)
    {
        debugInfo = null;

        // ── Determine bounds ──────────────────────────────────────────
        AABB2D bounds;
        bool hasBounds = false;

        // IMPORTANT: Policy reads world only via IWorldQuery. No GetComponent.
        RoleId roleId;
        if (world.TryGetRoleId(selfId, out roleId))
            hasBounds = world.TryGetIdleBounds(roleId, out bounds);
        else
            bounds = default;

        if (!hasBounds)
            bounds = AABB2D.FromCenterSize(anchor, FallbackBoundsSize);

        // ── Pre-compute squared thresholds ──────────────────────────────
        float personalSpaceSqr = personalSpace * personalSpace;
        float crowdRadiusSqr = crowdPenaltyRadius * crowdPenaltyRadius;

        // ── Sample and score candidates ───────────────────────────────
        Float2 bestPoint = anchor;
        float bestScore = float.MaxValue;
        int sampleCount = samples > 0 ? samples : 10;

        for (int i = 0; i < sampleCount; i++)
        {
            int seed = roleSeed ^ entitySeed ^ (i * SAMPLE_SEED_PRIME);
            Float2 candidate = CrowdScoringUtility.RandomPointInBounds(bounds, seed);

            // Crowd penalty via shared utility (soft penalty, never rejects)
            float score = CrowdScoringUtility.ScoreCrowdPenalty(
                world, selfId, candidate,
                personalSpaceSqr, crowdRadiusSqr, PERSONAL_SPACE_PENALTY);

            // Penalty for distance from anchor (keeps units generally around anchor)
            float anchorSqrDist = Float2.DistanceSqr(candidate, anchor);
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
