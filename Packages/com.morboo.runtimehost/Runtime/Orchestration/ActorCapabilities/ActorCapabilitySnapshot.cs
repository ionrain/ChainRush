using System;
using System.Collections.Generic;

/// <summary>
/// Per-actor capability snapshot. Base = from profile; Add = runtime additions.
/// IMPORTANT: Has() checks Add first, then Base (reference equality).
/// PERF: Linear scan — intentional for small N (typical 3–8 capabilities per actor).
/// </summary>
[Serializable]
public struct ActorCapabilitySnapshot
{
    public List<ActorCapability> Base;
    public List<ActorCapability> Add;

    public bool Has(ActorCapability cap)
    {
        if (cap == null) return false;
        if (Add != null)
        {
            for (int i = 0; i < Add.Count; i++)
                if (ReferenceEquals(Add[i], cap)) return true;
        }
        if (Base != null)
        {
            for (int i = 0; i < Base.Count; i++)
                if (ReferenceEquals(Base[i], cap)) return true;
        }
        return false;
    }

    public bool HasAny(IReadOnlyList<ActorCapability> caps)
    {
        if (caps == null || caps.Count == 0) return true;
        for (int i = 0; i < caps.Count; i++)
            if (Has(caps[i])) return true;
        return false;
    }

    /// <summary>
    /// Unions another snapshot into this one (adds all caps from other into this.Add).
    /// IMPORTANT: Used by consumers to build aggregate capability sets.
    /// PERF: Allocates on first call (Add list creation). Call once per tick, not per actor.
    /// </summary>
    public void MergeFrom(in ActorCapabilitySnapshot other)
    {
        MergeList(other.Base);
        MergeList(other.Add);
    }

    void MergeList(List<ActorCapability> source)
    {
        if (source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            ActorCapability cap = source[i];
            if (cap == null) continue;
            if (Has(cap)) continue;
            if (Add == null) Add = new List<ActorCapability>(8);
            Add.Add(cap);
        }
    }
}
