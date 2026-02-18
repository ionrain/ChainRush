using System;

/// <summary>
/// Stable role identity value.
/// IMPORTANT: This is a Framework type — no dependency on RoleAsset (ScriptableObject).
/// RoleAsset → RoleId conversion happens at the Integration boundary only.
/// </summary>
[Serializable]
public readonly struct RoleId : IEquatable<RoleId>
{
    public static readonly RoleId None = default;

    readonly int _value;

    public RoleId(int value)
    {
        _value = value;
    }

    public int ToStableInt() => _value;

    public bool IsNone => _value == 0;

    public bool Equals(RoleId other) => _value == other._value;
    public override bool Equals(object obj) => obj is RoleId other && Equals(other);
    public override int GetHashCode() => _value;

    public static bool operator ==(RoleId a, RoleId b) => a._value == b._value;
    public static bool operator !=(RoleId a, RoleId b) => a._value != b._value;

    public override string ToString() => _value == 0 ? "RoleId.None" : $"Role({_value})";
}
