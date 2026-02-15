using System.Collections.Generic;

/// <summary>
/// Reusable per-tick payload carrying idle policy assignments per role.
/// Built by <see cref="IdleOrchestratorLite"/>, consumed by <see cref="OrchestrationArbiter"/>.
/// <para>
/// IMPORTANT: Carries <see cref="IdlePolicyAsset"/> references, NOT precomputed commands.
/// The arbiter computes commands per-unit at dispatch time using each receiver's
/// <see cref="IRoleContextProvider.GetEntitySeed"/>.
/// </para>
/// PERF: Pre-allocated list, reused via <see cref="Clear"/>+<see cref="Add"/>. No per-tick allocation.
/// </summary>
public sealed class IdleRolePolicySet
{
    public struct Entry
    {
        public RoleAsset Role;
        public IdlePolicyAsset Policy;
    }

    readonly List<Entry> _entries = new List<Entry>(8);

    public int Count => _entries.Count;

    public void Clear() => _entries.Clear();

    public void Add(RoleAsset role, IdlePolicyAsset policy)
    {
        _entries.Add(new Entry
        {
            Role = role,
            Policy = policy
        });
    }

    /// <summary>
    /// Linear scan for matching role asset. First match wins.
    /// PERF: O(N) where N = number of roles (small).
    /// IMPORTANT: Returns false if role is null to avoid accidental match with null entries.
    /// </summary>
    public bool TryGetPolicy(RoleAsset role, out IdlePolicyAsset policy)
    {
        if (role == null)
        {
            policy = null;
            return false;
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];
            if (ReferenceEquals(e.Role, role))
            {
                policy = e.Policy;
                return true;
            }
        }

        policy = null;
        return false;
    }
}
