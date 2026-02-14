using System;

[Serializable]
public struct CapabilitySnapshot
{
    public CapabilitySet Base;
    public CapabilitySet Add;

    public bool Has(CapabilityId id)
    {
        return Add.Has(id) || Base.Has(id);
    }

    public bool Has(string id)
    {
        return Add.Has(id) || Base.Has(id);
    }

    public bool TryGet(CapabilityId id, out CapabilityEntry entry)
    {
        if (Add.TryGet(id, out entry)) return true;
        return Base.TryGet(id, out entry);
    }

    public bool TryGetFloat(CapabilityId id, string key, out float v)
    {
        if (Add.TryGetFloat(id, key, out v)) return true;
        return Base.TryGetFloat(id, key, out v);
    }

    public bool TryGetBool(CapabilityId id, string key, out bool v)
    {
        if (Add.TryGetBool(id, key, out v)) return true;
        return Base.TryGetBool(id, key, out v);
    }
}
