using System;
using System.Collections.Generic;

/// <summary>
/// Stable archetype key for entity creation requests.
/// </summary>
[Serializable]
public readonly struct EntityArchetypeId : IEquatable<EntityArchetypeId>
{
    readonly string _value;

    public EntityArchetypeId(string value)
    {
        _value = value ?? string.Empty;
    }

    public string Value => _value;

    public bool Equals(EntityArchetypeId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is EntityArchetypeId other && Equals(other);
    public override int GetHashCode() => _value == null ? 0 : _value.GetHashCode();
    public override string ToString() => _value ?? string.Empty;

    public static bool operator ==(EntityArchetypeId left, EntityArchetypeId right) => left.Equals(right);
    public static bool operator !=(EntityArchetypeId left, EntityArchetypeId right) => !left.Equals(right);
}

/// <summary>
/// Engine-agnostic view handle.
/// </summary>
[Serializable]
public readonly struct EntityViewId : IEquatable<EntityViewId>
{
    readonly int _value;

    public EntityViewId(int value)
    {
        _value = value;
    }

    public int Value => _value;
    public bool IsNone => _value == 0;

    public bool Equals(EntityViewId other) => _value == other._value;
    public override bool Equals(object obj) => obj is EntityViewId other && Equals(other);
    public override int GetHashCode() => _value;
    public override string ToString() => _value == 0 ? "EntityViewId.None" : _value.ToString();

    public static bool operator ==(EntityViewId left, EntityViewId right) => left.Equals(right);
    public static bool operator !=(EntityViewId left, EntityViewId right) => !left.Equals(right);
}

/// <summary>
/// Minimal entity model contract used by kernel/runtime services.
/// </summary>
public interface IEntityModel
{
    EntityId EntityId { get; }
    EntityArchetypeId ArchetypeId { get; }
    bool IsAlive { get; }
    IReadOnlyCollection<string> Tags { get; }
}

/// <summary>
/// Central entity lookup ownership seam.
/// </summary>
public interface IEntityRegistry
{
    bool Contains(EntityId entityId);
    bool TryGet(EntityId entityId, out IEntityModel entity);
    IReadOnlyCollection<EntityId> GetAllIds();
}

/// <summary>
/// Entity lifecycle creation/destruction ownership seam.
/// </summary>
public interface IEntityFactory
{
    EntityId Create(EntityArchetypeId archetypeId);
    bool Destroy(EntityId entityId);
}

/// <summary>
/// Lifecycle event stream for entity ownership changes.
/// </summary>
public interface IEntityLifecycleService
{
    event Action<EntityId> EntityCreated;
    event Action<EntityId> EntityDestroyed;
}

/// <summary>
/// Snapshot seam for save/replay and diagnostics.
/// </summary>
public interface IEntitySnapshotStore
{
    bool TryRead(EntityId entityId, out IReadOnlyDictionary<string, string> snapshot);
    void Write(EntityId entityId, IReadOnlyDictionary<string, string> snapshot);
}

/// <summary>
/// Adapter contract for entity-to-view binding (implemented in experience layer).
/// </summary>
public interface IEntityViewBinder
{
    bool TryGetBoundView(EntityId entityId, out EntityViewId viewId);
    void Bind(EntityId entityId, EntityViewId viewId);
    void Unbind(EntityId entityId);
}
