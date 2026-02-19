using System;

/// <summary>
/// Stable entity identity value.
/// </summary>
[Serializable]
public readonly struct EntityId : IEquatable<EntityId>
{
    public static readonly EntityId None = default;

    readonly int _value;

    public EntityId(int value)
    {
        _value = value;
    }

    public int ToStableInt() => _value;

    public bool IsNone => _value == 0;

    public bool Equals(EntityId other) => _value == other._value;
    public override bool Equals(object obj) => obj is EntityId other && Equals(other);
    public override int GetHashCode() => _value;

    public static bool operator ==(EntityId a, EntityId b) => a._value == b._value;
    public static bool operator !=(EntityId a, EntityId b) => a._value != b._value;

    public override string ToString() => _value == 0 ? "EntityId.None" : _value.ToString();
}
