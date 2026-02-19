using System;
using System.Collections.Generic;

/// <summary>
/// In-process command bus: commands are queued during routing and flushed in batch.
/// Supports any struct command implementing <see cref="ICommand"/> without depending
/// on feature-specific command types.
/// </summary>
public sealed class InProcessCommandBus : ICommandBus
{
    interface ICommandQueue
    {
        void FlushTo(Delegate handler);
        void Clear();
    }

    sealed class CommandQueue<TCommand> : ICommandQueue where TCommand : struct, ICommand
    {
        readonly List<TCommand> _items = new List<TCommand>(64);

        public void Enqueue(TCommand command) => _items.Add(command);

        public void FlushTo(Delegate handler)
        {
            if (handler is not Action<TCommand> typedHandler)
                return;

            for (int i = 0; i < _items.Count; i++)
                typedHandler(_items[i]);
        }

        public void Clear() => _items.Clear();
    }

    readonly Dictionary<Type, ICommandQueue> _queues = new Dictionary<Type, ICommandQueue>(8);
    readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>(8);

    public void Subscribe<TCommand>(Action<TCommand> handler) where TCommand : struct, ICommand
    {
        _handlers[typeof(TCommand)] = handler;
    }

    public void Unsubscribe<TCommand>() where TCommand : struct, ICommand
    {
        _handlers.Remove(typeof(TCommand));
    }

    public void Publish<TCommand>(TCommand command) where TCommand : struct, ICommand
    {
        Type type = typeof(TCommand);
        if (!_queues.TryGetValue(type, out ICommandQueue rawQueue))
        {
            rawQueue = new CommandQueue<TCommand>();
            _queues[type] = rawQueue;
        }

        ((CommandQueue<TCommand>)rawQueue).Enqueue(command);
    }

    public void Flush()
    {
        foreach (KeyValuePair<Type, ICommandQueue> entry in _queues)
        {
            if (_handlers.TryGetValue(entry.Key, out Delegate handler))
                entry.Value.FlushTo(handler);
            entry.Value.Clear();
        }
    }
}
