/// <summary>
/// Published after a pipeline tick completes (arbiter + router + command flush).
/// Queued after command bus flush, delivered on <see cref="InProcessEventBus.Flush"/>.
/// </summary>
public struct OrchestrationTickExecutedEvent : IDomainEvent
{
    public OrchestrationDomainId Domain;
    public int CommandCount;
    public float Timestamp;
}
