using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven mapping from <see cref="UnitClass"/> to <see cref="CapabilitiesProfile"/>.
/// Used by <see cref="UnitCapabilityProvider"/> to resolve capabilities per unit class.
/// PERF: Linear scan over a small list; no LINQ, no allocations.
/// </summary>
[CreateAssetMenu(fileName = "UnitClassCapabilitiesMap", menuName = "Game/Orchestration/UnitClass Capabilities Map")]
public class UnitClassCapabilitiesMap : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public UnitClass UnitClass;
        public CapabilitiesProfile Profile;
    }

    public List<Entry> Entries;

    /// <summary>
    /// Looks up the <see cref="CapabilitiesProfile"/> for the given unit class.
    /// Returns false if no matching entry is found or Entries is null/empty.
    /// </summary>
    public bool TryGetProfile(UnitClass unitClass, out CapabilitiesProfile profile)
    {
        profile = null;
        if (Entries == null || Entries.Count == 0) return false;

        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].UnitClass == unitClass && Entries[i].Profile != null)
            {
                profile = Entries[i].Profile;
                return true;
            }
        }

        return false;
    }
}
