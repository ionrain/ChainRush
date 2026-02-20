using System.Collections.Generic;

/// <summary>
/// In-memory implementation of <see cref="IEntitySnapshotStore"/>.
/// Keeps per-entity string snapshots, copied on write.
/// </summary>
public sealed class InMemoryEntitySnapshotStore : IEntitySnapshotStore
{
    readonly Dictionary<EntityId, IReadOnlyDictionary<string, string>> _snapshots =
        new Dictionary<EntityId, IReadOnlyDictionary<string, string>>();

    public bool TryRead(EntityId entityId, out IReadOnlyDictionary<string, string> snapshot)
    {
        return _snapshots.TryGetValue(entityId, out snapshot);
    }

    public void Write(EntityId entityId, IReadOnlyDictionary<string, string> snapshot)
    {
        if (snapshot == null)
        {
            _snapshots.Remove(entityId);
            return;
        }

        var copy = new Dictionary<string, string>(snapshot.Count);
        foreach (KeyValuePair<string, string> pair in snapshot)
            copy[pair.Key] = pair.Value;

        _snapshots[entityId] = copy;
    }
}
