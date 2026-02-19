using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Per-unit idle policy selection. Allows prefab defaults with optional per-instance overrides.
/// Arbiter injects a runtime default per role each tick; per-unit override wins.
/// </summary>
public sealed class UnitIdlePolicySelector : MonoBehaviour, IIdlePolicySelector
{
    [Header("Policies")]
    [Tooltip("Per-unit override. Takes priority over runtime default and defaultPolicy.")]
    [FormerlySerializedAs("policy")]
    [SerializeField] IdlePolicyAsset overridePolicy;

    [Tooltip("Prefab/scene default. Used when overridePolicy and runtime default are both null.")]
    [SerializeField] IdlePolicyAsset defaultPolicy;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runtime default policy injected by arbiter per-role dispatch.
    /// Cleared in OnEnable for pooling safety.
    /// </summary>
    IdlePolicyAsset _runtimeDefaultPolicy;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _runtimeDefaultPolicy = null;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the per-unit override policy at runtime (e.g. from <see cref="UnitAISetup"/>).
    /// Highest priority in resolution: overridePolicy > runtimeDefault > defaultPolicy.
    /// </summary>
    public void SetOverridePolicy(IdlePolicyAsset p) { overridePolicy = p; }

    /// <summary>
    /// Sets the runtime default policy injected by the arbiter per-role dispatch.
    /// Does not override a per-unit inspector policy; only fills the gap between
    /// per-unit override and scene default.
    /// </summary>
    public void SetRuntimeDefaultPolicy(IdlePolicyAsset p)
    {
        _runtimeDefaultPolicy = p;
    }

    /// <summary>
    /// Resolves the active policy using strict precedence:
    /// overridePolicy → runtime default (arbiter-injected) → defaultPolicy → null.
    /// </summary>
    public IdlePolicyAsset ResolvePolicy()
    {
        if (overridePolicy != null) return overridePolicy;
        if (_runtimeDefaultPolicy != null) return _runtimeDefaultPolicy;
        return defaultPolicy;
    }
}
