using System.Collections.Generic;
using NUnit.Framework;

public sealed class Phase2KernelRuntimeStoreSmokeTests
{
    [Test]
    public void SessionStore_SetGetRemove_Works()
    {
        var store = new InMemorySessionStateStore();

        store.Set("run.seed", 42);

        bool found = store.TryGet("run.seed", out int value);
        Assert.That(found, Is.True);
        Assert.That(value, Is.EqualTo(42));

        bool removed = store.Remove("run.seed");
        Assert.That(removed, Is.True);
        Assert.That(store.TryGet("run.seed", out int _), Is.False);
    }

    [Test]
    public void ProfileStore_SetGetRemove_Works()
    {
        var store = new InMemoryProfileStateStore();

        store.Set("profile.level", "gold");

        bool found = store.TryGet("profile.level", out string value);
        Assert.That(found, Is.True);
        Assert.That(value, Is.EqualTo("gold"));

        bool removed = store.Remove("profile.level");
        Assert.That(removed, Is.True);
        Assert.That(store.TryGet("profile.level", out string _), Is.False);
    }

    [Test]
    public void EntitySnapshotStore_WriteRead_WorksAndCopiesOnWrite()
    {
        var store = new InMemoryEntitySnapshotStore();
        var entityId = new EntityId(123);

        var source = new Dictionary<string, string>
        {
            ["hp"] = "10"
        };

        store.Write(entityId, source);
        source["hp"] = "99";

        bool found = store.TryRead(entityId, out IReadOnlyDictionary<string, string> snapshot);
        Assert.That(found, Is.True);
        Assert.That(snapshot, Is.Not.Null);
        Assert.That(snapshot["hp"], Is.EqualTo("10"));

        store.Write(entityId, null);
        Assert.That(store.TryRead(entityId, out IReadOnlyDictionary<string, string> _), Is.False);
    }
}
