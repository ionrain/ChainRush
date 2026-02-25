using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven mapping from <see cref="EnemyType"/> to <see cref="ActorCapabilityProfile"/>.
/// Used by <see cref="EnemyActorCapabilityProvider"/> to resolve capabilities per enemy type.
/// PERF: Linear scan over a small list; no LINQ, no allocations.
/// </summary>
[CreateAssetMenu(fileName = "EnemyTypeActorCapabilitiesMap", menuName = "Game/Orchestration/Enemy Type Actor Capabilities Map")]
public class EnemyTypeActorCapabilitiesMap : EnemyActorCapabilitiesMapAssetBase
{
    [Serializable]
    public struct Entry
    {
        public EnemyType Type;
        public ActorCapabilityProfile Profile;
    }

    public List<Entry> Entries;

    /// <summary>
    /// Looks up the <see cref="ActorCapabilityProfile"/> for the given enemy type.
    /// Returns false if no matching entry is found or Entries is null/empty.
    /// </summary>
    public override bool TryGetProfile(EnemyType type, out ActorCapabilityProfile profile)
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
