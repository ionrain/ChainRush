using System;
using System.Collections.Generic;

/// <summary>
/// In-memory implementation of <see cref="ISessionStateStore"/>.
/// Process-lifetime only; no persistence.
/// </summary>
public sealed class InMemorySessionStateStore : ISessionStateStore
{
    readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);

    public bool TryGet<TState>(string key, out TState value)
    {
        if (_values.TryGetValue(key, out object raw) && raw is TState typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void Set<TState>(string key, TState value)
    {
        _values[key] = value;
    }

    public bool Remove(string key)
    {
        return _values.Remove(key);
    }
}
