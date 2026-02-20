using System.Linq;
using NUnit.Framework;

public sealed class Phase3EntityBackboneSmokeTests
{
    [Test]
    public void EntityState_TracksTagsTraitsAndCapabilities()
    {
        var entity = new EntityState(new EntityId(1), new EntityArchetypeId("unit.base"), tags: new[] { "ground" });

        Assert.That(entity.Tags, Does.Contain("ground"));
        Assert.That(entity.AddTag("ally"), Is.True);
        Assert.That(entity.Tags, Does.Contain("ally"));
        Assert.That(entity.AddCapability("attack.melee"), Is.True);
        Assert.That(entity.Capabilities, Does.Contain("attack.melee"));

        entity.SetTrait("faction", "player");
        Assert.That(entity.TryGetTrait("faction", out string value), Is.True);
        Assert.That(value, Is.EqualTo("player"));
    }

    [Test]
    public void Factory_Create_RegistersEntity_AndRaisesCreatedEvent()
    {
        var registry = new InMemoryEntityRegistry();
        var lifecycle = new InMemoryEntityLifecycleService();
        var factory = new InMemoryEntityFactory(registry, lifecycle, () => new EntityId(101));

        EntityId created = EntityId.None;
        lifecycle.EntityCreated += id => created = id;

        EntityId entityId = factory.Create(new EntityArchetypeId("unit.base"));

        Assert.That(entityId.IsNone, Is.False);
        Assert.That(created, Is.EqualTo(entityId));
        Assert.That(registry.Contains(entityId), Is.True);
        Assert.That(registry.TryGet(entityId, out IEntityModel model), Is.True);
        Assert.That(model.ArchetypeId.Value, Is.EqualTo("unit.base"));
        Assert.That(model.IsAlive, Is.True);
    }

    [Test]
    public void Factory_Destroy_UnregistersEntity_AndRaisesDestroyedEvent()
    {
        var registry = new InMemoryEntityRegistry();
        var lifecycle = new InMemoryEntityLifecycleService();
        var factory = new InMemoryEntityFactory(registry, lifecycle, () => new EntityId(202));

        EntityId destroyed = EntityId.None;
        lifecycle.EntityDestroyed += id => destroyed = id;

        EntityId entityId = factory.Create(new EntityArchetypeId("enemy.base"));
        bool result = factory.Destroy(entityId);

        Assert.That(result, Is.True);
        Assert.That(destroyed, Is.EqualTo(entityId));
        Assert.That(registry.Contains(entityId), Is.False);
        Assert.That(registry.TryGet(entityId, out IEntityModel _), Is.False);
    }

    [Test]
    public void Registry_GetAllIds_ReturnsCurrentEntitySet()
    {
        var registry = new InMemoryEntityRegistry();
        var lifecycle = new InMemoryEntityLifecycleService();
        int nextId = 303;
        var factory = new InMemoryEntityFactory(registry, lifecycle, () => new EntityId(nextId++));

        EntityId first = factory.Create(new EntityArchetypeId("unit.base"));
        EntityId second = factory.Create(new EntityArchetypeId("unit.base"));

        var ids = registry.GetAllIds();
        Assert.That(ids.Count, Is.EqualTo(2));
        Assert.That(ids.Contains(first), Is.True);
        Assert.That(ids.Contains(second), Is.True);
    }

    [Test]
    public void ViewBinder_BindGetUnbind_Works()
    {
        var binder = new InMemoryEntityViewBinder();
        var entityId = new EntityId(7);
        var viewId = new EntityViewId(9001);

        binder.Bind(entityId, viewId);
        bool found = binder.TryGetBoundView(entityId, out EntityViewId resolved);

        Assert.That(found, Is.True);
        Assert.That(resolved, Is.EqualTo(viewId));

        binder.Unbind(entityId);
        Assert.That(binder.TryGetBoundView(entityId, out EntityViewId _), Is.False);
    }
}
