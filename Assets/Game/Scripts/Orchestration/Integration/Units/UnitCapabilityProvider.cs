using UnityEngine;

/// <summary>
/// Reports a <see cref="CapabilitySnapshot"/> for a unit to the orchestration layer.
/// IMPORTANT: Read-only, data-driven provider. No gameplay control, no Update loop.
/// Resolves capabilities from a <see cref="UnitClassCapabilitiesMap"/> or a default profile.
/// Will be extended later to include dynamic capabilities from skills/merge state.
/// </summary>
public sealed class UnitCapabilityProvider : MonoBehaviour, ICapabilityProvider
{
    [SerializeField] Unit _unit;

    [Header("Capabilities Source")]
    [SerializeField] CapabilitiesProfile defaultCapabilities;
    [SerializeField] UnitClassCapabilitiesMap classMap;

    void OnEnable() => OrchestrationRegistry.Register((ICapabilityProvider)this);
    void OnDisable() => OrchestrationRegistry.Unregister((ICapabilityProvider)this);

    void Reset()
    {
        _unit = GetComponent<Unit>();
    }

    /// <summary>
    /// Returns a capability snapshot for the unit.
    /// Resolution order: classMap lookup by UnitClass → defaultCapabilities → empty snapshot.
    /// </summary>
    public CapabilitySnapshot ReportCapabilities()
    {
        CapabilitiesProfile profile = null;

        if (classMap != null && _unit != null && _unit.Data != null)
            classMap.TryGetProfile(_unit.Data.unitClass, out profile);

        if (profile == null)
            profile = defaultCapabilities;

        if (profile == null)
            return default;

        return profile.ToSnapshot();
    }
}
