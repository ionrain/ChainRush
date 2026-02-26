/// <summary>
/// Shared static utility for crowd-aware scoring and deterministic hashing.
/// Consumed by idle policies (<see cref="IdleFillAreaPolicy2DAsset"/>) and combat
/// constraint spread logic (<see cref="UnitCombatCommandExecutor2D"/>).
/// <para>
/// PERF: All methods use index loops only — no LINQ, no allocations.
/// </para>
/// <para>
/// IMPORTANT: Does not use <c>UnityEngine.Random</c>. Does not touch global random state.
/// All randomness is deterministic via FNV-1a hashing.
/// </para>
/// <para>
/// IMPORTANT: All scoring operates on <see cref="ICrowdQuery"/> + <see cref="EntityId"/>.
/// No Transform references. All positions are <see cref="Float2"/>.
/// </para>
/// </summary>
public static class CrowdScoringUtility
{
    // ──────────────────────────────────────────────────────────────────
    //  Constants
    // ──────────────────────────────────────────────────────────────────

    const int FNV_OFFSET = unchecked((int)2166136261);
    const int FNV_PRIME = 16777619;

    // ──────────────────────────────────────────────────────────────────
    //  Deterministic hashing (pure math, no global state)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Stable FNV-1a hash normalized to [0, 1].
    /// IMPORTANT: Does NOT use <c>GetHashCode()</c>. Fully deterministic across platforms.
    /// </summary>
    public static float Hash01(int x)
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
    /// Raw FNV-1a hash returning an int. Useful for seed composition.
    /// </summary>
    public static int Hash01ToInt(int x)
    {
        unchecked
        {
            int h = FNV_OFFSET;
            h = (h ^ (x & 0xFF)) * FNV_PRIME;
            h = (h ^ ((x >> 8) & 0xFF)) * FNV_PRIME;
            h = (h ^ ((x >> 16) & 0xFF)) * FNV_PRIME;
            h = (h ^ ((x >> 24) & 0xFF)) * FNV_PRIME;
            return h;
        }
    }

    /// <summary>
    /// Returns a deterministic random point within the XY extents of the bounds.
    /// Handles zero-size axes safely (clamps to center on that axis).
    /// </summary>
    public static Float3 RandomPointInBounds(AABB3D b, int seed)
    {
        float tx = Hash01(seed);
        float ty = Hash01(seed ^ 0x6C62272E);
        float tz = Hash01(seed ^ 0x6C62272F);
        Float3 size = b.Size;
        Float3 min = b.Min;
        Float3 center = b.Center;
        float x = size.X > 0f ? min.X + tx * size.X : center.X;
        float y = size.Y > 0f ? min.Y + ty * size.Y : center.Y;
        float z = size.Z > 0f ? min.Z + tz * size.Z : center.Z;
        return new Float3(x, y, z);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Crowd scoring — Primary (IWorldQuery-based, EntityId self-skip)
    //  IMPORTANT: Single source of truth. All scoring logic lives here.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a crowd penalty score for a candidate position using <see cref="ICrowdQuery"/>.
    /// Self-skip via <paramref name="selfId"/> (skips EntityId.None entries and self).
    /// <para>
    /// IMPORTANT: This is the primary scoring function.
    /// </para>
    /// </summary>
    public static float ScoreCrowdPenalty(
        ICrowdQuery crowd,
        EntityId selfId,
        Float3 candidatePosition,
        float personalSpaceSqr,
        float crowdRadiusSqr,
        float personalSpacePenalty = 1000f)
    {
        float score = 0f;
        int len = crowd.CrowdCount;
        for (int i = 0; i < len; i++)
        {
            EntityId eid = crowd.GetCrowdEntityId(i);
            if (!selfId.IsNone && eid == selfId) continue;

            Float3 crowdPos = crowd.GetCrowdPosition(i);
            float sqrDist = Float3.DistanceSqr(crowdPos, candidatePosition);

            if (sqrDist < personalSpaceSqr)
                score += personalSpacePenalty;

            if (sqrDist < crowdRadiusSqr)
                score += 1f;
        }
        return score;
    }

    /// <summary>
    /// Counts crowd members within <paramref name="radiusSqr"/> of <paramref name="position"/>,
    /// skipping self via <paramref name="selfId"/>.
    /// </summary>
    public static int CountNear(
        ICrowdQuery crowd,
        EntityId selfId,
        Float3 position,
        float radiusSqr)
    {
        int count = 0;
        int len = crowd.CrowdCount;
        for (int i = 0; i < len; i++)
        {
            EntityId eid = crowd.GetCrowdEntityId(i);
            if (!selfId.IsNone && eid == selfId) continue;

            float sqr = Float3.DistanceSqr(crowd.GetCrowdPosition(i), position);
            if (sqr <= radiusSqr)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Returns true if <paramref name="minNeighbors"/>+ crowd members are within
    /// <paramref name="personalSpaceSqr"/> of <paramref name="position"/>.
    /// </summary>
    public static bool IsCrowded(
        ICrowdQuery crowd,
        EntityId selfId,
        Float3 position,
        float personalSpaceSqr,
        int minNeighbors = 2)
    {
        int count = 0;
        int len = crowd.CrowdCount;
        for (int i = 0; i < len; i++)
        {
            EntityId eid = crowd.GetCrowdEntityId(i);
            if (!selfId.IsNone && eid == selfId) continue;

            float sqr = Float3.DistanceSqr(crowd.GetCrowdPosition(i), position);
            if (sqr <= personalSpaceSqr)
            {
                count++;
                if (count >= minNeighbors)
                    return true;
            }
        }
        return false;
    }

}
