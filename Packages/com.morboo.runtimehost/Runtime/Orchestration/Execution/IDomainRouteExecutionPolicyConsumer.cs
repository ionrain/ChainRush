/// <summary>
/// Generic orchestration-facing contract for components that accept route-execution policy updates.
/// Domain orchestrators and domain components implement this to receive policy configuration
/// from composition bridges before the orchestration loop starts.
/// </summary>
public interface IDomainRouteExecutionPolicyConsumer
{
    void ApplyRouteExecutionPolicy(DomainRouteExecutionPolicy policy);
}
