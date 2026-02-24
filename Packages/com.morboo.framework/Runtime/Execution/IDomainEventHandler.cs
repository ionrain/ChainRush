/// <summary>
/// Typed handler contract for domain event consumers.
/// No Unity dependency — usable in tests, non-MonoBehaviour services, and pure C# classes.
/// </summary>
public interface IDomainEventHandler<TEvent> where TEvent : struct, IDomainEvent
{
    void HandleEvent(TEvent domainEvent);
}
