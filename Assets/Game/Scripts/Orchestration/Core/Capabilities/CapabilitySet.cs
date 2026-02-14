using System;
using System.Collections.Generic;

[Serializable]
public struct CapabilitySet
{
    public List<CapabilityEntry> Items;

    public bool Has(CapabilityId id)
    {
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == id) return true;
        }
        return false;
    }

    public bool Has(string id)
    {
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (string.Equals(Items[i].Id.Id, id, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    public bool TryGet(CapabilityId id, out CapabilityEntry entry)
    {
        entry = default;
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == id)
            {
                entry = Items[i];
                return true;
            }
        }
        return false;
    }

    public bool TryGetFloat(CapabilityId id, string key, out float v)
    {
        v = default;
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == id)
            {
                return Items[i].TryGetFloat(key, out v);
            }
        }
        return false;
    }

    public bool TryGetBool(CapabilityId id, string key, out bool v)
    {
        v = default;
        if (Items == null || Items.Count == 0) return false;
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == id)
            {
                return Items[i].TryGetBool(key, out v);
            }
        }
        return false;
    }
}
