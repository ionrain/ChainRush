/// <summary>
/// Bus instance provider for decoupled event subscriber access.
/// Any system owning an <see cref="IEventBus"/> instance can implement this
/// to expose the bus without coupling consumers to a specific owner type.
/// </summary>
public interface IEventBusProvider
{
    IEventBus EventBus { get; }
}
