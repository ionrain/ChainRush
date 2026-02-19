using System;
using System.Collections.Generic;

/// <summary>
/// In-process event bus: immediate publish to subscribed handlers.
/// <para>
/// IMPORTANT: Events are dispatched synchronously on Publish — no queuing.
/// Phase 2B seam; currently unused. Will carry domain events in future commits.
/// </para>
/// </summary>
public sealed class InProcessEventBus : IEventBus
{
    readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>(8);

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IDomainEvent
    {
        _handlers[typeof(TEvent)] = handler;
    }

    public void Unsubscribe<TEvent>() where TEvent : struct, IDomainEvent
    {
        _handlers.Remove(typeof(TEvent));
    }

    public void Publish<TEvent>(TEvent domainEvent) where TEvent : struct, IDomainEvent
    {
        if (_handlers.TryGetValue(typeof(TEvent), out Delegate del))
            ((Action<TEvent>)del)(domainEvent);
    }
}
