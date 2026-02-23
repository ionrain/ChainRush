using UnityEngine;

/// <summary>
/// Shared StrategyCombat route-execution policy provider component.
/// Keeps route-policy asset ownership in a reusable domain component instead of duplicating
/// a serialized policy field inside each domain orchestrator.
/// </summary>
public sealed class StrategyCombatRouteExecutionPolicyProvider : MonoBehaviour, IStrategyCombatRouteExecutionPolicyConsumer
{
    [Tooltip("Optional StrategyCombat route-execution policy. Null preserves current behavior.")]
    [SerializeField] StrategyCombatRouteExecutionPolicyAsset routeExecutionPolicy;

    public StrategyCombatRouteExecutionPolicyAsset CurrentPolicy => routeExecutionPolicy;

    public void ApplyRouteExecutionPolicy(StrategyCombatRouteExecutionPolicyAsset policy)
    {
        routeExecutionPolicy = policy;
    }
}
