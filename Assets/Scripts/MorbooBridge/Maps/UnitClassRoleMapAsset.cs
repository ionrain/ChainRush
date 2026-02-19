using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maps <see cref="UnitClass"/> to <see cref="RoleAsset"/>.
/// Single source of truth for UnitClass → Role assignment.
/// <para>
/// PERF: Linear scan, no LINQ, no per-call allocations.
/// Same pattern as <see cref="RoleCapabilitiesMapAsset"/>.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "UnitClassRoleMap", menuName = "Game/Orchestration/UnitClass Role Map")]
public sealed class UnitClassRoleMapAsset : UnitRoleMapAssetBase
{
    [Serializable]
    public struct Entry
    {
        public UnitClass UnitClass;
        public RoleAsset Role;
    }

    [SerializeField] List<Entry> entries;

    /// <summary>
    /// Looks up the <see cref="RoleAsset"/> for a given <see cref="UnitClass"/>.
    /// Returns first match (linear scan).
    /// </summary>
    public override bool TryGet(UnitClass unitClass, out RoleAsset role)
    {
        role = null;
        if (entries == null || entries.Count == 0) return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].UnitClass == unitClass && entries[i].Role != null)
            {
                role = entries[i].Role;
                return true;
            }
        }
        return false;
    }
}
