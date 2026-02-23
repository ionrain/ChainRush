/// <summary>
/// Shared orchestration-facing helper/contract set for StrategyCombat domain orchestrators.
/// C04C intent: keep `Combat/Idle` on the same structural pattern without introducing an
/// additional orchestrator inheritance layer beyond <see cref="DomainOrchestrator"/>.
/// </summary>
public interface IStrategyCombatRouteExecutionPolicyConsumer
{
    void ApplyRouteExecutionPolicy(StrategyCombatRouteExecutionPolicyAsset policy);
}

public static class StrategyCombatDomainOrchestratorCommon
{
    public static DomainArbitrationProfile CreateArbitrationProfile(bool stickyPrimary)
    {
        return new DomainArbitrationProfile(stickyPrimary);
    }

    public static IDomainExecutionRouteContributor CreateFixedRouteContributorWithUnknownFallback(
        OrchestrationDomainId domainId,
        StrategyCombatRouteExecutionPolicyAsset routeExecutionPolicy,
        DomainExecutionRouteExecutor executor)
    {
        return DomainExecutionRouteContributors.CreateFixedWithUnknownRouteFallback(
            StrategyCombatUnknownRouteFallbackExecutionRoute.GetShared(routeExecutionPolicy).Execute,
            new ExecutionRouteRegistration(domainId, executor));
    }

    public static bool ShouldRebuildRouteExecutorForPolicyChange(
        StrategyCombatRouteExecutionPolicyAsset currentPolicy,
        StrategyCombatRouteExecutionPolicyAsset nextPolicy)
    {
        return currentPolicy != nextPolicy;
    }
}
