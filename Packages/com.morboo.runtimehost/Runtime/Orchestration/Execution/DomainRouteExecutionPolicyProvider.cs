using UnityEngine;

/// <summary>
/// Generic route-execution policy provider component.
/// Keeps route-policy asset ownership in a reusable domain component instead of duplicating
/// a serialized policy field inside each domain orchestrator.
/// IMPORTANT: RuntimeHost owns the generic form; the serialized policy asset reference
/// can be any <see cref="DomainRouteExecutionPolicy"/> subclass.
/// </summary>
public sealed class DomainRouteExecutionPolicyProvider : MonoBehaviour, IDomainRouteExecutionPolicyConsumer
{
    [Tooltip("Optional route-execution policy. Null preserves current behavior.")]
    [SerializeField] DomainRouteExecutionPolicy routeExecutionPolicy;

    public DomainRouteExecutionPolicy CurrentPolicy => routeExecutionPolicy;

    public void ApplyRouteExecutionPolicy(DomainRouteExecutionPolicy policy)
    {
        routeExecutionPolicy = policy;
    }
}
