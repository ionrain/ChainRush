using System;

[Serializable]
public struct CapabilityId : IEquatable<CapabilityId>
{
    public string Id;

    public static CapabilityId Of(string id)
    {
        return new CapabilityId { Id = id };
    }

    public override string ToString()
    {
        return Id ?? string.Empty;
    }

    public bool Equals(CapabilityId other)
    {
        return string.Equals(Id, other.Id, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is CapabilityId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id != null ? Id.GetHashCode() : 0;
    }

    public static bool operator ==(CapabilityId a, CapabilityId b) => a.Equals(b);
    public static bool operator !=(CapabilityId a, CapabilityId b) => !a.Equals(b);
}
