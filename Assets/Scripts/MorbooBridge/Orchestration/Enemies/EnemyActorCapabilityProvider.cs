using UnityEngine;

/// <summary>
/// Reports an <see cref="ActorCapabilitySnapshot"/> for an enemy to the orchestration layer.
/// IMPORTANT: Read-only, data-driven provider. No gameplay control, no Update loop.
/// Resolves capabilities from a type-to-capabilities map or a default profile.
/// </summary>
public sealed class EnemyActorCapabilityProvider : MonoBehaviour, IActorCapabilityProvider
{
    [SerializeField] Enemy _enemy;

    [Header("Capabilities Source")]
    [SerializeField] ActorCapabilityProfile defaultCapabilities;
    [SerializeField] EnemyActorCapabilitiesMapAssetBase typeMap;

    void OnEnable() => OrchestrationRegistry.Register((IActorCapabilityProvider)this);
    void OnDisable() => OrchestrationRegistry.Unregister((IActorCapabilityProvider)this);

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
    public ActorCapabilitySnapshot ReportCapabilities()
    {
        ActorCapabilityProfile profile = null;

        if (typeMap != null && _enemy != null)
            typeMap.TryGetProfile(_enemy.Type, out profile);

        if (profile == null)
            profile = defaultCapabilities;

        if (profile == null)
            return default;

        return profile.ToSnapshot();
    }
}
