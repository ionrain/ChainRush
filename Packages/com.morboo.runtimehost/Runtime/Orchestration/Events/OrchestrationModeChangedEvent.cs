/// <summary>
/// Published when the arbiter switches active domain (e.g. Combat → Idle).
/// Queued during arbiter tick, delivered on <see cref="InProcessEventBus.Flush"/>.
/// </summary>
public struct OrchestrationModeChangedEvent : IDomainEvent
{
    public OrchestrationDomainId PreviousDomain;
    public OrchestrationDomainId CurrentDomain;
    public float Timestamp;
}
