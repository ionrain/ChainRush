using UnityEngine;

/// <summary>
/// Reports a <see cref="CapabilitySnapshot"/> for an enemy to the orchestration layer.
/// IMPORTANT: Read-only, data-driven provider. No gameplay control, no Update loop.
/// Resolves capabilities from an <see cref="EnemyTypeCapabilitiesMap"/> or a default profile.
/// Will be extended later to include dynamic capabilities.
/// </summary>
public sealed class EnemyCapabilityProvider : MonoBehaviour, ICapabilityProvider
{
    [SerializeField] Enemy _enemy;

    [Header("Capabilities Source")]
    [SerializeField] CapabilitiesProfile defaultCapabilities;
    [SerializeField] EnemyTypeCapabilitiesMap typeMap;

    void OnEnable() => OrchestrationRegistry.Register((ICapabilityProvider)this);
    void OnDisable() => OrchestrationRegistry.Unregister((ICapabilityProvider)this);

    void Reset()
    {
        _enemy = GetComponent<Enemy>();
    }

    /// <summary>
    /// Returns a capability snapshot for the enemy.
    /// Resolution order: typeMap lookup by EnemyType → defaultCapabilities → empty snapshot.
    /// RATIONALE: If the map exists but has no entry for this enemy's type, we fall
    /// back to defaultCapabilities rather than returning empty.
    /// </summary>
    public CapabilitySnapshot ReportCapabilities()
    {
        CapabilitiesProfile profile = null;

        if (typeMap != null && _enemy != null)
            typeMap.TryGetProfile(_enemy.Type, out profile);

        if (profile == null)
            profile = defaultCapabilities;

        if (profile == null)
            return default;

        return profile.ToSnapshot();
    }
}
