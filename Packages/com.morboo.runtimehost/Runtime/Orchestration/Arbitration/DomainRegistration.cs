/// <summary>
/// Cached host-runtime domain registration record.
/// C04A bootstrap: centralizes arbiter-facing domain capabilities discovered once
/// during cache rebuild instead of repeated host-side type checks each tick.
/// TRANSITIONAL: ArbiterBindingContributor currently adapts StrategyCombat-specific
/// policy map sources. It is a migration bridge only and must be replaced by
/// DomainModule/generic execution/policy bindings so RuntimeHost no longer carries
/// Idle/Combat-specific provider shapes.
/// </summary>
public readonly struct DomainRegistration
{
    public readonly DomainOrchestrator Orchestrator;
    public readonly DomainArbitrationProfile ArbitrationProfile;
    public readonly IDomainArbiterBindingContributor ArbiterBindingContributor;
    public readonly IDomainExecutionRouteContributor ExecutionRouteContributor;

    public DomainRegistration(
        DomainOrchestrator orchestrator,
        in DomainArbitrationProfile arbitrationProfile,
        IDomainArbiterBindingContributor arbiterBindingContributor,
        IDomainExecutionRouteContributor executionRouteContributor)
    {
        Orchestrator = orchestrator;
        ArbitrationProfile = arbitrationProfile;
        ArbiterBindingContributor = arbiterBindingContributor;
        ExecutionRouteContributor = executionRouteContributor;
    }
}
