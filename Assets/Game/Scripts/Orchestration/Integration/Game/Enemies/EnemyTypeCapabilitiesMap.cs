using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven mapping from <see cref="EnemyType"/> to <see cref="CapabilitiesProfile"/>.
/// Used by <see cref="EnemyCapabilityProvider"/> to resolve capabilities per enemy type.
/// PERF: Linear scan over a small list; no LINQ, no allocations.
/// </summary>
[CreateAssetMenu(fileName = "EnemyTypeCapabilitiesMap", menuName = "Game/Orchestration/Enemy Type Capabilities Map")]
public class EnemyTypeCapabilitiesMap : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public EnemyType Type;
        public CapabilitiesProfile Profile;
    }

    public List<Entry> Entries;

    /// <summary>
    /// Looks up the <see cref="CapabilitiesProfile"/> for the given enemy type.
    /// Returns false if no matching entry is found or Entries is null/empty.
    /// </summary>
    public bool TryGetProfile(EnemyType type, out CapabilitiesProfile profile)
    {
        profile = null;
        if (Entries == null || Entries.Count == 0) return false;

        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Type == type && Entries[i].Profile != null)
            {
                profile = Entries[i].Profile;
                return true;
            }
        }

        return false;
    }
}
