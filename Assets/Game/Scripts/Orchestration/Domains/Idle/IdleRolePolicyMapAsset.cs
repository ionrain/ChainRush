using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven mapping of <see cref="RoleAsset"/> to idle policies.
/// Assigned to <see cref="IdleOrchestratorLite"/> to define which policy
/// each unit role uses during idle.
/// <para>
/// IMPORTANT: Routing uses <c>ReferenceEquals</c> on <see cref="RoleAsset"/>.
/// First-match strategy on <see cref="TryGet"/>. Duplicate roles
/// are warned in <see cref="OnValidate"/>.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "IdleRolePolicyMap", menuName = "Game/Orchestration/Idle/Role Policy Map")]
public sealed class IdleRolePolicyMapAsset : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public RoleAsset role;
        public IdlePolicyAsset policy;
    }

    [SerializeField] List<Entry> entries = new List<Entry>();

    public int Count => entries != null ? entries.Count : 0;

    /// <summary>
    /// Index-based access for orchestrator iteration.
    /// </summary>
    public Entry GetEntry(int index) => entries[index];

    /// <summary>
    /// First-match linear scan for the given role asset.
    /// PERF: O(N) where N = number of roles (typically 3-6).
    /// </summary>
    public bool TryGet(RoleAsset role, out IdlePolicyAsset policy)
    {
        if (role == null)
        {
            policy = null;
            return false;
        }

        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i].role, role))
                {
                    policy = entries[i].policy;
                    return true;
                }
            }
        }

        policy = null;
        return false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            RoleAsset role = entries[i].role;

            if (role == null)
            {
                Debug.LogWarning(
                    "[IdleRolePolicyMapAsset] Null role at index " + i + ".", this);
                continue;
            }

            if (entries[i].policy == null)
            {
                Debug.LogWarning(
                    "[IdleRolePolicyMapAsset] Null policy for role '" + role.Id +
                    "' at index " + i + ".", this);
            }

            for (int j = i + 1; j < entries.Count; j++)
            {
                if (ReferenceEquals(entries[j].role, role))
                {
                    Debug.LogWarning(
                        "[IdleRolePolicyMapAsset] Duplicate role '" + role.Id +
                        "' at indices " + i + " and " + j +
                        ". First match will be used.", this);
                }
                else if (entries[j].role != null && entries[j].role.Id == role.Id)
                {
                    Debug.LogWarning(
                        "[IdleRolePolicyMapAsset] Different RoleAssets with same Id '" + role.Id +
                        "' at indices " + i + " and " + j +
                        ". This may be unintentional.", this);
                }
            }
        }
    }
#endif
}
