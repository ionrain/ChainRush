using System;
using System.Collections.Generic;

/// <summary>
/// In-memory implementation of <see cref="IEntityLifecycleService"/>.
/// </summary>
public sealed class InMemoryEntityLifecycleService : IEntityLifecycleService
{
    public event Action<EntityId> EntityCreated;
    public event Action<EntityId> EntityDestroyed;

    public void NotifyCreated(EntityId entityId)
    {
        EntityCreated?.Invoke(entityId);
    }

    public void NotifyDestroyed(EntityId entityId)
    {
        EntityDestroyed?.Invoke(entityId);
    }
}

/// <summary>
/// In-memory implementation of <see cref="IEntityRegistry"/>.
/// </summary>
public sealed class InMemoryEntityRegistry : IEntityRegistry
{
    readonly Dictionary<EntityId, EntityState> _entities = new Dictionary<EntityId, EntityState>();

    public bool Contains(EntityId entityId)
    {
        return _entities.ContainsKey(entityId);
    }

    public bool TryGet(EntityId entityId, out IEntityModel entity)
    {
        if (_entities.TryGetValue(entityId, out EntityState state))
        {
            entity = state;
            return true;
        }

        entity = null;
        return false;
    }

    public bool TryGetState(EntityId entityId, out IEntityStateAccessor entityState)
    {
        if (_entities.TryGetValue(entityId, out EntityState state))
        {
            entityState = state;
            return true;
        }

        entityState = null;
        return false;
    }

    public IReadOnlyCollection<EntityId> GetAllIds()
    {
        return new List<EntityId>(_entities.Keys);
    }

    public bool TryAdd(EntityState entity)
    {
        if (entity == null || entity.EntityId.IsNone)
            return false;
        if (_entities.ContainsKey(entity.EntityId))
            return false;

        _entities.Add(entity.EntityId, entity);
        return true;
    }

    public bool TryRemove(EntityId entityId, out EntityState removed)
    {
        if (_entities.TryGetValue(entityId, out removed))
        {
            _entities.Remove(entityId);
            return true;
        }

        removed = null;
        return false;
    }
}

/// <summary>
/// In-memory implementation of <see cref="IEntityFactory"/>.
/// </summary>
public sealed class InMemoryEntityFactory : IEntityFactory
{
    readonly InMemoryEntityRegistry _registry;
    readonly InMemoryEntityLifecycleService _lifecycle;
    readonly Func<EntityId> _idFactory;

    public InMemoryEntityFactory(
        InMemoryEntityRegistry registry,
        InMemoryEntityLifecycleService lifecycle,
        Func<EntityId> idFactory = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _idFactory = idFactory ?? EntityIdAllocator.Create;
    }

    public EntityId Create(EntityArchetypeId archetypeId)
    {
        if (string.IsNullOrWhiteSpace(archetypeId.Value))
            return EntityId.None;

        EntityId entityId = AllocateUniqueEntityId();
        if (entityId.IsNone)
            return EntityId.None;

        var entity = new EntityState(entityId, archetypeId, isAlive: true);
        if (!_registry.TryAdd(entity))
            return EntityId.None;

        _lifecycle.NotifyCreated(entityId);
        return entityId;
    }

    public bool Destroy(EntityId entityId)
    {
        if (entityId.IsNone)
            return false;
        if (!_registry.TryRemove(entityId, out EntityState removed))
            return false;

        removed.SetAlive(false);
        _lifecycle.NotifyDestroyed(entityId);
        return true;
    }

    EntityId AllocateUniqueEntityId()
    {
        const int maxAttempts = 16;

        for (int i = 0; i < maxAttempts; i++)
        {
            EntityId entityId = _idFactory();
            if (!entityId.IsNone && !_registry.Contains(entityId))
                return entityId;
        }

        return EntityId.None;
    }
}

/// <summary>
/// In-memory implementation of <see cref="IEntityViewBinder"/>.
/// </summary>
public sealed class InMemoryEntityViewBinder : IEntityViewBinder
{
    readonly Dictionary<EntityId, EntityViewId> _bindings = new Dictionary<EntityId, EntityViewId>();

    public bool TryGetBoundView(EntityId entityId, out EntityViewId viewId)
    {
        return _bindings.TryGetValue(entityId, out viewId);
    }

    public void Bind(EntityId entityId, EntityViewId viewId)
    {
        if (entityId.IsNone || viewId.IsNone)
            return;

        _bindings[entityId] = viewId;
    }

    public void Unbind(EntityId entityId)
    {
        if (entityId.IsNone)
            return;

        _bindings.Remove(entityId);
    }
}
