using UnityEngine;

/// <summary>
/// Domain-neutral read/write carrier for a target candidate set (EntityId Top-K + TTL).
/// Written by a domain producer during orchestration ticks and read by selector/policy code
/// without rescanning the world.
/// <para>
/// IMPORTANT: Stores EntityId[], not Transform[]. Consumers resolve EntityId → Transform
/// at the Integration boundary.
/// </para>
/// <para>
/// No Update loop. TTL is checked lazily in <see cref="TryGetTargets"/>.
/// No allocations after the first <see cref="SetTargets"/> call.
/// </para>
/// </summary>
public class DomainTargetSet : MonoBehaviour
{
    [Tooltip("Maximum number of candidates stored. Orchestrator/domain may push fewer.")]
    [SerializeField] int capacity = 6;

    [Tooltip("How long the set remains valid after being written. " +
             "Should be >= orchestrator tickInterval to avoid inter-tick expiry.")]
    [SerializeField] float ttlSeconds = 0.9f;

    EntityId[] _targets;
    int _count;
    float _expiresAt;

    /// <summary>Effective capacity (always >= 1).</summary>
    public int Capacity => capacity > 1 ? capacity : 1;

    /// <summary>Effective TTL (always >= 0.1s).</summary>
    public float TtlSeconds => ttlSeconds > 0.1f ? ttlSeconds : 0.1f;

    /// <summary>
    /// Overwrites the candidate set from a raw array.
    /// <paramref name="count"/> is the number of valid entries in <paramref name="src"/>
    /// (starting at index 0). Entries beyond <paramref name="count"/> are ignored.
    /// <paramref name="now"/> is the current tick time.
    /// </summary>
    public void SetTargets(EntityId[] src, int count, float now)
    {
        int cap = Capacity;
        if (_targets == null || _targets.Length < cap)
            _targets = new EntityId[cap];

        int toCopy = count < cap ? count : cap;
        if (src == null)
            toCopy = 0;
        else if (toCopy > src.Length)
            toCopy = src.Length;

        for (int i = 0; i < toCopy; i++)
            _targets[i] = src[i];

        for (int i = toCopy; i < _targets.Length; i++)
            _targets[i] = EntityId.None;

        _count = toCopy;
        _expiresAt = now + TtlSeconds;
    }

    /// <summary>
    /// Tries to retrieve the current candidate set.
    /// Returns false if expired or empty. Callers should fall back to primary target.
    /// The returned array is internal storage — do not cache or mutate it.
    /// </summary>
    public bool TryGetTargets(out EntityId[] targets, out int count, float now)
    {
        if (_count == 0 || now > _expiresAt)
        {
            targets = null;
            count = 0;
            return false;
        }

        targets = _targets;
        count = _count;
        return true;
    }
}
