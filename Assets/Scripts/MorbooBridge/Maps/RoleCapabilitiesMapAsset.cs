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
public sealed class RoleCapabilitiesMapAsset : RoleCapabilitiesMapAssetBase
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
    public override bool TryGet(RoleAsset role, out CapabilitiesProfile profile)
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

    // ── Lazy RoleId-keyed cache for RuntimeHost consumption ──────────

    Dictionary<RoleId, CapabilitiesProfile> _byRoleId;

    /// <summary>
    /// Lookup by <see cref="RoleId"/> for RuntimeHost code.
    /// IMPORTANT: RuntimeHost must use this overload, never the RoleAsset one.
    /// </summary>
    public override bool TryGet(RoleId roleId, out CapabilitiesProfile profile)
    {
        if (roleId.IsNone) { profile = null; return false; }

        if (_byRoleId == null)
            RebuildRoleIdCache();

        return _byRoleId.TryGetValue(roleId, out profile);
    }

    void RebuildRoleIdCache()
    {
        _byRoleId = new Dictionary<RoleId, CapabilitiesProfile>(entries != null ? entries.Count : 0);
        if (entries == null) return;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Role == null || entries[i].Profile == null) continue;
            RoleId rid = entries[i].Role.RoleId;
            if (rid.IsNone) continue;
            if (!_byRoleId.ContainsKey(rid))
                _byRoleId[rid] = entries[i].Profile;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        _byRoleId = null; // Invalidate lazy cache on inspector changes
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
