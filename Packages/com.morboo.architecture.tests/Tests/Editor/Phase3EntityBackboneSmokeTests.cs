using System.Linq;
using NUnit.Framework;

public sealed class Phase3EntityBackboneSmokeTests
{
    [TearDown]
    public void TearDown()
    {
        EntityBackboneRuntimeContext.Clear();
    }

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
    public void EntityState_ImplementsAuthoritativeAccessorContract()
    {
        IEntityStateAccessor state = new EntityState(new EntityId(11), new EntityArchetypeId("unit.base"));

        state.AddTag("unit");
        state.AddCapability("attack.ranged");
        state.SetTrait("faction", "player");

        Assert.That(state.EntityId, Is.EqualTo(new EntityId(11)));
        Assert.That(state.ArchetypeId.Value, Is.EqualTo("unit.base"));
        Assert.That(state.Tags, Does.Contain("unit"));
        Assert.That(state.Capabilities, Does.Contain("attack.ranged"));
        Assert.That(state.TryGetTrait("faction", out string faction), Is.True);
        Assert.That(faction, Is.EqualTo("player"));
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
    public void Registry_TryGetState_ReturnsTypedAuthoritativeState()
    {
        var registry = new InMemoryEntityRegistry();
        var lifecycle = new InMemoryEntityLifecycleService();
        var factory = new InMemoryEntityFactory(registry, lifecycle, () => new EntityId(404));

        EntityId entityId = factory.Create(new EntityArchetypeId("enemy.base"));
        bool found = registry.TryGetState(entityId, out IEntityStateAccessor state);

        Assert.That(found, Is.True);
        Assert.That(state, Is.Not.Null);
        Assert.That(state.EntityId, Is.EqualTo(entityId));
        Assert.That(state.ArchetypeId.Value, Is.EqualTo("enemy.base"));
        Assert.That(state.IsAlive, Is.True);
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

    [Test]
    public void RuntimeContext_Install_ExposesTypedStateQuery()
    {
        var registry = new InMemoryEntityRegistry();
        var lifecycle = new InMemoryEntityLifecycleService();
        var factory = new InMemoryEntityFactory(registry, lifecycle, () => new EntityId(505));
        var binder = new InMemoryEntityViewBinder();

        EntityBackboneRuntimeContext.Install(registry, factory, lifecycle, binder);
        EntityId entityId = factory.Create(new EntityArchetypeId("unit.base"));

        Assert.That(EntityBackboneRuntimeContext.IsInstalled, Is.True);
        Assert.That(EntityBackboneRuntimeContext.StateQuery, Is.Not.Null);
        Assert.That(EntityBackboneRuntimeContext.StateQuery.TryGetState(entityId, out IEntityStateAccessor state), Is.True);
        Assert.That(state.ArchetypeId.Value, Is.EqualTo("unit.base"));
    }

    [Test]
    public void RuntimeContext_Clear_WithNonOwnerRegistry_DoesNothing()
    {
        var registryOwner = new InMemoryEntityRegistry();
        var lifecycleOwner = new InMemoryEntityLifecycleService();
        var factoryOwner = new InMemoryEntityFactory(registryOwner, lifecycleOwner, () => new EntityId(601));
        var binderOwner = new InMemoryEntityViewBinder();

        var foreignRegistry = new InMemoryEntityRegistry();
        EntityBackboneRuntimeContext.Install(registryOwner, factoryOwner, lifecycleOwner, binderOwner);

        EntityBackboneRuntimeContext.Clear(foreignRegistry);

        Assert.That(EntityBackboneRuntimeContext.IsInstalled, Is.True);
        Assert.That(EntityBackboneRuntimeContext.Registry, Is.SameAs(registryOwner));
    }
}
