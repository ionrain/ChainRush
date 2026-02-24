/// <summary>
/// Shared orchestration composition helpers for domain orchestrators.
/// Provides common patterns (arbitration profile, route contributor, policy-change detection)
/// so genre-specific domain components use one structural pattern.
/// IMPORTANT: RuntimeHost owns the generic form; strategy-specific route bodies live in StrategyCombat.
/// </summary>
public static class DomainOrchestratorComposition
{
    public static DomainArbitrationProfile CreateArbitrationProfile(bool stickyPrimary)
    {
        return new DomainArbitrationProfile(stickyPrimary);
    }

    public static IDomainExecutionRouteContributor CreateFixedRouteContributorWithUnknownFallback(
        OrchestrationDomainId domainId,
        DomainExecutionRouteExecutor executor,
        DomainExecutionRouteExecutor unknownRouteFallbackExecutor)
    {
        return DomainExecutionRouteContributors.CreateFixedWithUnknownRouteFallback(
            unknownRouteFallbackExecutor,
            new ExecutionRouteRegistration(domainId, executor));
    }

    public static bool ShouldRebuildRouteExecutorForPolicyChange(
        DomainRouteExecutionPolicy currentPolicy,
        DomainRouteExecutionPolicy nextPolicy)
    {
        return currentPolicy != nextPolicy;
    }
}
