using System;
using System.Collections.Generic;

/// <summary>
/// In-process event bus: events are queued during publish and flushed in batch.
/// Supports multiple handlers per event type.
/// <para>
/// IMPORTANT: Events are dispatched on Flush, not on Publish — deferred model
/// matching <see cref="InProcessCommandBus"/> for consistency.
/// PERF: struct events, pre-allocated queues, linear scan on flush — zero per-frame allocation.
/// </para>
/// </summary>
public sealed class InProcessEventBus : IEventBus
{
    interface IEventQueue
    {
        void FlushTo(List<Delegate> handlers);
        void Clear();
        int Count { get; }
    }

    sealed class EventQueue<TEvent> : IEventQueue where TEvent : struct, IDomainEvent
    {
        readonly List<TEvent> _items = new List<TEvent>(16);

        public int Count => _items.Count;

        public void Enqueue(TEvent domainEvent) => _items.Add(domainEvent);

        public void FlushTo(List<Delegate> handlers)
        {
            if (handlers == null || handlers.Count == 0)
                return;

            for (int i = 0; i < _items.Count; i++)
            {
                TEvent item = _items[i];
                for (int h = 0; h < handlers.Count; h++)
                {
                    if (handlers[h] is Action<TEvent> typedHandler)
                        typedHandler(item);
                }
            }
        }

        public void Clear() => _items.Clear();
    }

    readonly Dictionary<Type, IEventQueue> _queues = new Dictionary<Type, IEventQueue>(8);
    readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>(8);

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IDomainEvent
    {
        if (handler == null)
            return;

        Type type = typeof(TEvent);
        if (!_handlers.TryGetValue(type, out List<Delegate> list))
        {
            list = new List<Delegate>(4);
            _handlers[type] = list;
        }

        list.Add(handler);
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IDomainEvent
    {
        if (handler == null)
            return;

        Type type = typeof(TEvent);
        if (_handlers.TryGetValue(type, out List<Delegate> list))
        {
            list.Remove(handler);
            if (list.Count == 0)
                _handlers.Remove(type);
        }
    }

    public void Publish<TEvent>(TEvent domainEvent) where TEvent : struct, IDomainEvent
    {
        Type type = typeof(TEvent);
        if (!_queues.TryGetValue(type, out IEventQueue rawQueue))
        {
            rawQueue = new EventQueue<TEvent>();
            _queues[type] = rawQueue;
        }

        ((EventQueue<TEvent>)rawQueue).Enqueue(domainEvent);
    }

    /// <summary>
    /// Flush all queued events to subscribed handlers, then clear queues.
    /// Returns total number of events flushed.
    /// </summary>
    public int Flush()
    {
        int total = 0;

        foreach (KeyValuePair<Type, IEventQueue> entry in _queues)
        {
            total += entry.Value.Count;
            _handlers.TryGetValue(entry.Key, out List<Delegate> handlers);
            entry.Value.FlushTo(handlers);
            entry.Value.Clear();
        }

        return total;
    }
}
