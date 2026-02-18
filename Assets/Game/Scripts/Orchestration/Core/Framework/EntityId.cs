using System;

/// <summary>
/// Stable identity for orchestration entities. Wraps a monotonically-increasing int
/// issued by <see cref="EntityIdAllocator"/> (RuntimeHost). Pool-safe: assigned once
/// in identity component's <c>Awake()</c>, stable across enable/disable cycles.
/// <para>
/// IMPORTANT — EntityId assignment is the responsibility of Identity components
/// (<c>UnitOrchestrationIdentity</c> / <c>EnemyOrchestrationIdentity</c>).
/// All other components MUST treat missing identity as fatal: warn once + fallback
/// to <see cref="None"/>.
/// </para>
/// <para>
/// IMPORTANT — Do not construct EntityId directly. Use <see cref="EntityIdAllocator.Create"/>
/// in RuntimeHost. The internal constructor is accessible only within the Core assembly
/// and the RuntimeHost assembly (via InternalsVisibleTo if needed) or via the allocator.
/// </para>
/// </summary>
[Serializable]
public readonly struct EntityId : IEquatable<EntityId>
{
    /// <summary>
    /// Default value representing "no entity". Value is 0.
    /// </summary>
    public static readonly EntityId None = default;

    readonly int _value;

    /// <summary>
    /// Constructor. Only <see cref="EntityIdAllocator"/> should call this.
    /// IMPORTANT: Do not construct EntityId manually — use EntityIdAllocator.Create().
    /// </summary>
    public EntityId(int value)
    {
        _value = value;
    }

    /// <summary>
    /// Returns the underlying int for use as a deterministic seed (hashing, anti-clumping).
    /// IMPORTANT: Use this method explicitly when an int seed is needed. Do not access
    /// the raw value for any other purpose.
    /// </summary>
    public int ToStableInt() => _value;

    public bool IsNone => _value == 0;

    public bool Equals(EntityId other) => _value == other._value;
    public override bool Equals(object obj) => obj is EntityId other && Equals(other);
    public override int GetHashCode() => _value;

    public static bool operator ==(EntityId a, EntityId b) => a._value == b._value;
    public static bool operator !=(EntityId a, EntityId b) => a._value != b._value;

    public override string ToString() => _value == 0 ? "EntityId.None" : _value.ToString();
}
