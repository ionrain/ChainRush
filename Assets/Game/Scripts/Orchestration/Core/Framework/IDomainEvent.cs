/// <summary>
/// Marker interface for orchestration domain events.
/// Phase 2B hook — the <c>ExecutionRouter</c> will emit these via
/// <c>ExecutionResult</c> for consumption by an EventBus.
/// <para>
/// IMPORTANT — Phase 2B MUST: connect ExecutionRouter to EventBus via this seam.
/// </para>
/// </summary>
public interface IDomainEvent
{
}
