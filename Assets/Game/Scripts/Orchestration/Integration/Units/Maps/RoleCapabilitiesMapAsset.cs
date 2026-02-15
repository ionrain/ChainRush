using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps <see cref="RoleAsset"/> to <see cref="CapabilitiesProfile"/>.
/// Single source of truth for role-based capability resolution.
/// <para>
/// IMPORTANT: Matching uses ReferenceEquals on <see cref="RoleAsset"/> —
/// each distinct role must be a separate asset instance.
/// </para>
/// <para>
/// PERF: Linear scan over a small list; no LINQ, no allocations.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "RoleCapabilitiesMap", menuName = "Game/Orchestration/Role Capabilities Map")]
public sealed class RoleCapabilitiesMapAsset : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public RoleAsset Role;
        public CapabilitiesProfile Profile;
    }

    [SerializeField] List<Entry> entries;

    /// <summary>
    /// Looks up the <see cref="CapabilitiesProfile"/> for a given <see cref="RoleAsset"/>.
    /// Returns false if <paramref name="role"/> is null, entries are empty, or no match is found.
    /// IMPORTANT: Matching uses ReferenceEquals — never string comparison.
    /// </summary>
    public bool TryGet(RoleAsset role, out CapabilitiesProfile profile)
    {
        profile = null;
        if (role == null) return false;
        if (entries == null || entries.Count == 0) return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (ReferenceEquals(entries[i].Role, role) && entries[i].Profile != null)
            {
                profile = entries[i].Profile;
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Role == null)
                Debug.LogWarning($"[RoleCapabilitiesMap] Entry {i} has null RoleAsset.", this);
            if (entries[i].Profile == null)
                Debug.LogWarning($"[RoleCapabilitiesMap] Entry {i} has null CapabilitiesProfile.", this);

            // Duplicate check
            for (int j = i + 1; j < entries.Count; j++)
            {
                if (entries[i].Role != null && ReferenceEquals(entries[i].Role, entries[j].Role))
                    Debug.LogWarning($"[RoleCapabilitiesMap] Duplicate RoleAsset '{entries[i].Role.Id}' at entries {i} and {j}.", this);
            }
        }
    }
#endif
}
