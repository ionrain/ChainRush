using UnityEngine;

/// <summary>
/// Reports a <see cref="CapabilitySnapshot"/> for a unit to the orchestration layer.
/// IMPORTANT: Read-only, data-driven provider. No gameplay control, no Update loop.
/// Resolves capabilities from a role-to-capabilities map via
/// the unit's <see cref="UnitOrchestrationIdentity"/> role, or a default profile.
/// </summary>
public sealed class UnitCapabilityProvider : MonoBehaviour, ICapabilityProvider
{
    [SerializeField] Unit _unit;
    [SerializeField] UnitOrchestrationIdentity _identity;

    [Header("Capabilities Source")]
    [SerializeField] CapabilitiesProfile defaultCapabilities;
    [SerializeField] RoleCapabilitiesMapAssetBase roleMap;

    // One-shot warning flag (avoids log spam with pooling)
    bool _warnedMissingIdentity;

    void OnEnable()
    {
        // PERF: Cache identity for pooled instances that may not have it at author time.
        if (_identity == null)
            _identity = GetComponent<UnitOrchestrationIdentity>();

        OrchestrationRegistry.Register((ICapabilityProvider)this);
    }

    void OnDisable() => OrchestrationRegistry.Unregister((ICapabilityProvider)this);

    void Reset()
    {
        _unit = GetComponent<Unit>();
        _identity = GetComponent<UnitOrchestrationIdentity>();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Runtime setters (for UnitAISetup bridge)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the role-to-capabilities map at runtime.
    /// Pass null to clear a stale reference on pooled instances.
    /// </summary>
    public void SetRoleMap(RoleCapabilitiesMapAssetBase m) { roleMap = m; }

    /// <summary>
    /// Sets the default capabilities profile at runtime.
    /// Used as fallback when roleMap has no entry for the unit's role.
    /// </summary>
    public void SetDefaultCapabilities(CapabilitiesProfile p) { defaultCapabilities = p; }

    // ──────────────────────────────────────────────────────────────────
    //  Capability reporting
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a capability snapshot for the unit.
    /// Resolution order: roleMap lookup by RoleAsset → defaultCapabilities → empty snapshot.
    /// </summary>
    public CapabilitySnapshot ReportCapabilities()
    {
        CapabilitiesProfile profile = null;

        if (_identity == null)
        {
            if (!_warnedMissingIdentity)
            {
                _warnedMissingIdentity = true;
                Debug.LogWarning("[UnitCapabilityProvider] Missing UnitOrchestrationIdentity; " +
                                 "falling back to defaultCapabilities.", this);
            }
        }
        else if (roleMap != null)
        {
            RoleAsset role = _identity.GetRoleAsset();
            if (role != null)
                roleMap.TryGet(role, out profile);
        }

        if (profile == null)
            profile = defaultCapabilities;

        if (profile == null)
            return default;

        return profile.ToSnapshot();
    }
}
