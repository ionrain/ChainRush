using System;

[Serializable]
public struct FactionKey : IEquatable<FactionKey>
{
    public string Id;

    public static FactionKey Of(string id)
    {
        return new FactionKey { Id = id };
    }

    public override string ToString()
    {
        return Id ?? string.Empty;
    }

    public bool Equals(FactionKey other)
    {
        return string.Equals(Id, other.Id);
    }

    public override bool Equals(object obj)
    {
        return obj is FactionKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Id != null ? Id.GetHashCode() : 0;
    }

    public static bool operator ==(FactionKey a, FactionKey b) => a.Equals(b);
    public static bool operator !=(FactionKey a, FactionKey b) => !a.Equals(b);
}

public enum FactionRelation
{
    Friendly,
    Neutral,
    Hostile
}

public static class FactionDefaults
{
    public static readonly FactionKey Player = FactionKey.Of("Player");
    public static readonly FactionKey Enemy = FactionKey.Of("Enemy");
}
