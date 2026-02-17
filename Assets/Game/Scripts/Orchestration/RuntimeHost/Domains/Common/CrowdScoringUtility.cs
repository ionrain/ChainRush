using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared static utility for crowd-aware scoring and deterministic hashing.
/// Consumed by idle policies (<see cref="IdleFillAreaPolicyAsset"/>) and combat
/// constraint spread logic (<see cref="UnitCombatCommandExecutor"/>).
/// <para>
/// PERF: All methods use index loops only — no LINQ, no allocations.
/// </para>
/// <para>
/// IMPORTANT: Does not use <c>UnityEngine.Random</c>. Does not touch global random state.
/// All randomness is deterministic via FNV-1a hashing.
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
    /// Z is ignored. Handles zero-size axes safely (clamps to center on that axis).
    /// </summary>
    public static Vector2 RandomPointInBounds(Bounds b, int seed)
    {
        float tx = Hash01(seed);
        float ty = Hash01(seed ^ 0x6C62272E);
        float x = b.size.x > 0f ? b.min.x + tx * b.size.x : b.center.x;
        float y = b.size.y > 0f ? b.min.y + ty * b.size.y : b.center.y;
        return new Vector2(x, y);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Crowd scoring
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Counts transforms within <paramref name="radiusSqr"/> of <paramref name="position"/>,
    /// skipping <paramref name="selfTransform"/>.
    /// </summary>
    public static int CountNear(
        IReadOnlyList<Transform> transforms,
        Transform selfTransform,
        Vector2 position,
        float radiusSqr)
    {
        int count = 0;
        int len = transforms.Count;
        for (int i = 0; i < len; i++)
        {
            Transform t = transforms[i];
            if (t == null || t == selfTransform) continue;
            float sqr = ((Vector2)t.position - position).sqrMagnitude;
            if (sqr <= radiusSqr)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Returns true if <paramref name="minNeighbors"/>+ transforms are within
    /// <paramref name="personalSpaceSqr"/> of <paramref name="position"/>.
    /// </summary>
    public static bool IsCrowded(
        IReadOnlyList<Transform> transforms,
        Transform selfTransform,
        Vector2 position,
        float personalSpaceSqr,
        int minNeighbors = 2)
    {
        int count = 0;
        int len = transforms.Count;
        for (int i = 0; i < len; i++)
        {
            Transform t = transforms[i];
            if (t == null || t == selfTransform) continue;
            float sqr = ((Vector2)t.position - position).sqrMagnitude;
            if (sqr <= personalSpaceSqr)
            {
                count++;
                if (count >= minNeighbors)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns a crowd penalty score for a candidate position.
    /// Applies hard penalty (<paramref name="personalSpacePenalty"/>) for personal-space
    /// violations, soft +1 for each transform within <paramref name="crowdRadiusSqr"/>.
    /// <para>
    /// IMPORTANT: This is a penalty score, not a reject. Even in extreme crowding
    /// the position remains valid — just penalized. Only "outside leash" is a hard reject.
    /// </para>
    /// </summary>
    public static float ScoreCrowdPenalty(
        IReadOnlyList<Transform> transforms,
        Transform selfTransform,
        Vector2 candidatePosition,
        float personalSpaceSqr,
        float crowdRadiusSqr,
        float personalSpacePenalty = 1000f)
    {
        float score = 0f;
        int len = transforms.Count;
        for (int i = 0; i < len; i++)
        {
            Transform t = transforms[i];
            if (t == null || t == selfTransform) continue;

            float sqrDist = ((Vector2)t.position - candidatePosition).sqrMagnitude;

            // Hard penalty: personal space violation
            if (sqrDist < personalSpaceSqr)
                score += personalSpacePenalty;

            // Soft penalty: crowding
            if (sqrDist < crowdRadiusSqr)
                score += 1f;
        }
        return score;
    }
}
