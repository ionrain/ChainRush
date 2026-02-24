/// <summary>
/// Bus instance provider for decoupled command subscriber access.
/// Any system owning an <see cref="ICommandBus"/> instance can implement this
/// to expose the bus without coupling consumers to a specific owner type.
/// </summary>
public interface ICommandBusProvider
{
    ICommandBus CommandBus { get; }
}
