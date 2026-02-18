/// <summary>
/// Domain event publish/subscribe abstraction.
/// </summary>
public interface IEventBus
{
    void Publish<TEvent>(TEvent domainEvent) where TEvent : struct, IDomainEvent;
}
