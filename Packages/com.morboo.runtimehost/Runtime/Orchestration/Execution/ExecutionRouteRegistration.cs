/// <summary>
/// Route executor delegate used by <see cref="ExecutionRouter"/> during C04A
/// onboarding simplification. Domain execution behavior is selected by domain key
/// via registration, not hardcoded branching in the router entrypoint.
/// </summary>
public delegate void DomainExecutionRouteExecutor(
    ArbiterDecision decision,
    OrchestrationWorldCache world,
    ExecutionContext ctx);

/// <summary>
/// Host-runtime execution route registration record.
/// C04A bootstrap: allows router behavior to be extended/replaced through
/// registration touchpoints before moving full execution logic out of RuntimeHost.
/// </summary>
public readonly struct ExecutionRouteRegistration
{
    public readonly OrchestrationDomainId DomainKey;
    public readonly DomainExecutionRouteExecutor Execute;

    public ExecutionRouteRegistration(OrchestrationDomainId domainKey, DomainExecutionRouteExecutor execute)
    {
        DomainKey = domainKey;
        Execute = execute;
    }
}
