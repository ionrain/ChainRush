using UnityEngine;

/// <summary>
/// Read-only carrier of Top-K hostile candidates, updated once per orchestrator tick.
/// Units reference this via <see cref="UnitCombatTargetSelector"/> to pick per-unit
/// targets from the candidate pool without re-scanning the world.
/// <para>
/// IMPORTANT — This component holds no gameplay logic. It is a shared data buffer:
/// - Written by <see cref="CombatOrchestratorLite"/> each tick.
/// - Read by <see cref="UnitCombatTargetSelector"/> on demand.
/// - Without it, selectors fall back to the orchestrator-chosen primary target.
/// </para>
/// <para>
/// Variant B binding: Registers itself in <see cref="OrchestrationRegistry"/> by
/// <see cref="faction"/> on enable. Consumers resolve via
/// <see cref="OrchestrationRegistry.TryGetCombatTargetSet"/> without inspector wiring.
/// </para>
/// <para>
/// No Update loop. TTL is checked lazily in <see cref="TryGetTargets"/>.
/// No allocations after the first <see cref="SetTargets"/> call.
/// </para>
/// </summary>
public sealed class CombatTargetSet : MonoBehaviour, IFactionAssetProvider
{
    // ──────────────────────────────────────────────────────────────────
    //  Serialized
    // ──────────────────────────────────────────────────────────────────

    [Tooltip("Maximum number of candidates stored. Orchestrator may push fewer.")]
    [SerializeField] int capacity = 6;

    [Tooltip("How long the set remains valid after being written. " +
             "Should be >= orchestrator tickInterval to avoid inter-tick expiry.")]
    [SerializeField] float ttlSeconds = 0.9f;

    [SerializeField] FactionAsset faction;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime
    // ──────────────────────────────────────────────────────────────────

    Transform[] _targets;
    int _count;
    float _expiresAt;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle — Variant B registry binding
    // ──────────────────────────────────────────────────────────────────

    void OnEnable() => OrchestrationRegistry.Register(this);
    void OnDisable() => OrchestrationRegistry.Unregister(this);

    // ──────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Effective capacity (always >= 1).</summary>
    public int Capacity => capacity > 1 ? capacity : 1;

    /// <summary>Effective TTL (always >= 0.1s).</summary>
    public float TtlSeconds => ttlSeconds > 0.1f ? ttlSeconds : 0.1f;

    /// <summary>
    /// Returns the typed faction asset, or null if not assigned.
    /// IMPORTANT: Combat orchestration runtime uses this for registry binding.
    /// </summary>
    public FactionAsset GetFactionAsset() => faction;

    /// <summary>
    /// Overwrites the candidate set from a raw array.
    /// Called by <see cref="CombatOrchestratorLite"/> each tick.
    /// <paramref name="count"/> is the number of valid entries in <paramref name="src"/>
    /// (starting at index 0). Entries beyond <paramref name="count"/> are ignored.
    /// </summary>
    public void SetTargets(Transform[] src, int count)
    {
        int cap = Capacity;
        if (_targets == null || _targets.Length < cap)
            _targets = new Transform[cap];

        int toCopy = count < cap ? count : cap;
        if (toCopy > src.Length) toCopy = src.Length;

        for (int i = 0; i < toCopy; i++)
            _targets[i] = src[i];

        // Clear leftover slots to avoid stale refs
        for (int i = toCopy; i < _targets.Length; i++)
            _targets[i] = null;

        _count = toCopy;
        _expiresAt = Time.time + TtlSeconds;
    }

    /// <summary>
    /// Tries to retrieve the current candidate set.
    /// Returns false if expired or empty. Callers should fall back to primary target.
    /// <para>
    /// IMPORTANT: The returned array is internal storage — do not cache or mutate it.
    /// Only indices 0..<paramref name="count"/> are valid.
    /// </para>
    /// </summary>
    public bool TryGetTargets(out Transform[] targets, out int count)
    {
        if (_count == 0 || Time.time > _expiresAt)
        {
            targets = null;
            count = 0;
            return false;
        }

        targets = _targets;
        count = _count;
        return true;
    }

    void OnValidate()
    {
        if (faction == null)
            Debug.LogWarning($"[{name}] CombatTargetSet has no FactionAsset assigned.", this);
    }
}
