using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven mapping of <see cref="RoleAsset"/> to <see cref="CombatMoveConstraintsAsset"/>.
/// Assigned to <see cref="CombatOrchestratorLite"/> and pulled by the arbiter each tick
/// via domain arbiter bindings contributed by combat domains.
/// <para>
/// IMPORTANT: Routing uses <c>ReferenceEquals</c> on <see cref="RoleAsset"/>.
/// First-match strategy on <see cref="TryGet"/>. Duplicate roles
/// are warned in <see cref="OnValidate"/>.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "CombatRoleConstraintsMap", menuName = "Game/Orchestration/Combat/Role Constraints Map")]
public sealed class CombatRoleConstraintsMapAsset : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public RoleAsset role;
        public CombatMoveConstraintsAsset constraints;
    }

    [SerializeField] List<Entry> entries = new List<Entry>();

    public int Count => entries != null ? entries.Count : 0;

    /// <summary>
    /// Index-based access for iteration.
    /// </summary>
    public Entry GetEntry(int index) => entries[index];

    /// <summary>
    /// First-match linear scan for the given role asset (Integration boundary only).
    /// PERF: O(N) where N = number of roles (typically 3-6).
    /// </summary>
    public bool TryGet(RoleAsset role, out CombatMoveConstraintsAsset constraints)
    {
        if (role == null)
        {
            constraints = null;
            return false;
        }

        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i].role, role))
                {
                    constraints = entries[i].constraints;
                    return true;
                }
            }
        }

        constraints = null;
        return false;
    }

    // ── Lazy RoleId-keyed cache for RuntimeHost consumption ──────────

    Dictionary<RoleId, CombatMoveConstraintsAsset> _byRoleId;

    /// <summary>
    /// Lookup by <see cref="RoleId"/> for RuntimeHost code.
    /// IMPORTANT: RuntimeHost must use this overload, never the RoleAsset one.
    /// Builds a lazy cache on first access; invalidated in OnValidate.
    /// </summary>
    public bool TryGet(RoleId roleId, out CombatMoveConstraintsAsset constraints)
    {
        if (roleId.IsNone) { constraints = null; return false; }

        if (_byRoleId == null)
            RebuildRoleIdCache();

        return _byRoleId.TryGetValue(roleId, out constraints);
    }

    void RebuildRoleIdCache()
    {
        _byRoleId = new Dictionary<RoleId, CombatMoveConstraintsAsset>(entries != null ? entries.Count : 0);
        if (entries == null) return;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].role == null) continue;
            RoleId rid = entries[i].role.RoleId;
            if (rid.IsNone) continue;
            if (!_byRoleId.ContainsKey(rid))
                _byRoleId[rid] = entries[i].constraints;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        _byRoleId = null; // Invalidate lazy cache on inspector changes
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            RoleAsset role = entries[i].role;

            if (role == null)
            {
                Debug.LogWarning(
                    "[CombatRoleConstraintsMapAsset] Null role at index " + i + ".", this);
                continue;
            }

            if (entries[i].constraints == null)
            {
                Debug.LogWarning(
                    "[CombatRoleConstraintsMapAsset] Null constraints for role '" + role.Id +
                    "' at index " + i + ".", this);
            }

            for (int j = i + 1; j < entries.Count; j++)
            {
                if (ReferenceEquals(entries[j].role, role))
                {
                    Debug.LogWarning(
                        "[CombatRoleConstraintsMapAsset] Duplicate role '" + role.Id +
                        "' at indices " + i + " and " + j +
                        ". First match will be used.", this);
                }
                else if (entries[j].role != null && entries[j].role.Id == role.Id)
                {
                    Debug.LogWarning(
                        "[CombatRoleConstraintsMapAsset] Different RoleAssets with same Id '" + role.Id +
                        "' at indices " + i + " and " + j +
                        ". This may be unintentional.", this);
                }
            }
        }
    }
#endif
}
