/// <summary>
/// Tick scheduling contract. Subscribers receive <see cref="TickContext"/> each tick.
/// <para>
/// IMPORTANT: Subscribe/Unsubscribe during a tick callback is forbidden.
/// Tick delivery order among subscribers is registration order.
/// </para>
/// </summary>
public interface ITickSource
{
    void Subscribe(System.Action<TickContext> callback);
    void Unsubscribe(System.Action<TickContext> callback);
}
