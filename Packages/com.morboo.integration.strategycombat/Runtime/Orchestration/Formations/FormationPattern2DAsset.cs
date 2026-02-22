using UnityEngine;

/// <summary>
/// Abstract base for formation patterns. Concrete subclasses define how units
/// are arranged around an anchor point (ring, grid, wedge, etc.).
/// <para>
/// Returns local offset around anchor. If <paramref name="slotCount"/> is 0 or 1,
/// returns <see cref="Vector2.zero"/> (single unit goes directly on anchor).
/// </para>
/// <para>
/// IMPORTANT — Must be allocation-free and stateless. No per-call allocations.
/// </para>
/// </summary>
public abstract class FormationPattern2DAsset : ScriptableObject
{
    [SerializeField] string id;

    /// <summary>
    /// Display/log identifier. Falls back to asset name if empty.
    /// </summary>
    public string Id => string.IsNullOrEmpty(id) ? name : id;

    /// <summary>
    /// Returns the local offset for a given slot within the formation.
    /// Offset is relative to anchor (add to anchor position for world position).
    /// <para>
    /// If <paramref name="slotCount"/> &lt;= 1, returns <see cref="Vector2.zero"/>.
    /// </para>
    /// </summary>
    /// <param name="slotIndex">Zero-based index of the slot.</param>
    /// <param name="slotCount">Total number of slots in the formation.</param>
    /// <param name="seed">Deterministic seed for jitter/variation.</param>
    /// <returns>Local 2D offset from anchor.</returns>
    public Vector2 GetSlotOffset(int slotIndex, int slotCount, int seed)
    {
        if (slotCount <= 1)
            return Vector2.zero;

        return ComputeSlotOffset(slotIndex, slotCount, seed);
    }

    /// <summary>
    /// Override in concrete patterns to compute the actual slot offset.
    /// Called only when <paramref name="slotCount"/> &gt; 1.
    /// </summary>
    protected abstract Vector2 ComputeSlotOffset(int slotIndex, int slotCount, int seed);

    // ──────────────────────────────────────────────────────────────────
    //  Shared hash helper (FNV-1a, deterministic)
    // ──────────────────────────────────────────────────────────────────

    const int FNV_OFFSET = unchecked((int)2166136261);
    const int FNV_PRIME = 16777619;

    /// <summary>
    /// Stable FNV-1a hash normalized to [0, 1]. Deterministic.
    /// </summary>
    protected static float Hash01(int x)
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
}
