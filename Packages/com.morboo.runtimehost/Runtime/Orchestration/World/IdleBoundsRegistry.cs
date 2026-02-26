using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry mapping <see cref="RoleId"/> → <see cref="IIdleBoundsProvider"/>.
/// One zone per role (MVP). Duplicate registrations warn and "last wins" deterministically.
/// <para>
/// IMPORTANT — Role is resolved generically via <see cref="IRoleAssetProvider"/> at registration
/// time, converted to <see cref="RoleId"/>, and stored as a value type.
/// RuntimeHost code sees only RoleId, never RoleAsset.
/// </para>
/// <para>
/// PERF: No LINQ, no per-call allocations. Dead-entry pruning only in Register/Unregister,
/// not in TryGet (keeps lookup cheap and predictable).
/// </para>
/// </summary>
public static class IdleBoundsRegistry
{
    private struct Entry
    {
        public RoleId RoleId;
        public IIdleBoundsProvider Provider;
    }

    private static readonly List<Entry> _entries = new List<Entry>(8);

    // ──────────────────────────────────────────────────────────────────
    //  Register
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a bounds provider keyed by its <see cref="RoleId"/>.
    /// Role is resolved via <see cref="IRoleAssetProvider"/> interface (Integration boundary),
    /// then converted to RoleId for storage.
    /// If role is null or RoleId is None, registration is skipped with a warning.
    /// </summary>
    public static void Register(IIdleBoundsProvider provider)
    {
        if (provider == null) return;

        // ── Resolve role generically at Integration boundary ─────────
        RoleId roleId = RoleId.None;
        string debugName = null;

        if (provider is IRoleAssetProvider rp)
        {
            var role = rp.GetRoleAsset();
            if (role != null)
            {
                roleId = role.RoleId;
                debugName = role.Id;
            }
        }
        else if (provider is Component c)
        {
            IRoleAssetProvider comp = c.GetComponent<IRoleAssetProvider>();
            if (comp != null)
            {
                var role = comp.GetRoleAsset();
                if (role != null)
                {
                    roleId = role.RoleId;
                    debugName = role.Id;
                }
            }
        }

        if (roleId.IsNone)
        {
            Debug.LogWarning("[IdleBoundsRegistry] Provider has no valid RoleId; skipping registration.",
                             provider as Object);
            return;
        }

        // ── Backward scan: prune dead, detect duplicate ───────────────
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry e = _entries[i];

            // Prune Unity-null (destroyed) providers
            if (e.Provider is Component comp && comp == null)
            {
                _entries.RemoveAt(i);
                continue;
            }

            // Duplicate role: last wins
            if (e.RoleId == roleId)
            {
                if (!ReferenceEquals(e.Provider, provider))
                {
                    Debug.LogWarning(string.Concat(
                        "[IdleBoundsRegistry] Duplicate provider for role '",
                        debugName ?? roleId.ToString(),
                        "'; replacing previous (last wins)."), provider as Object);
                }

                e.Provider = provider;
                _entries[i] = e;
                return;
            }
        }

        // ── New entry ─────────────────────────────────────────────────
        _entries.Add(new Entry { RoleId = roleId, Provider = provider });
    }

    // ──────────────────────────────────────────────────────────────────
    //  Unregister
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes a provider by reference. Also prunes any Unity-null entries encountered.
    /// Does not need to recompute role — matches by provider reference directly.
    /// </summary>
    public static void Unregister(IIdleBoundsProvider provider)
    {
        if (provider == null) return;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry e = _entries[i];

            // Prune Unity-null providers
            if (e.Provider is Component comp && comp == null)
            {
                _entries.RemoveAt(i);
                continue;
            }

            // Match by provider reference
            if (ReferenceEquals(e.Provider, provider))
            {
                _entries.RemoveAt(i);
                // Don't break — continue to prune remaining dead entries
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  TryGet
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Looks up the <see cref="IIdleBoundsProvider"/> for a given <see cref="RoleId"/>.
    /// Skips Unity-null (destroyed) providers but does NOT prune them (keeps TryGet cheap).
    /// Dead entries are cleaned on next Register/Unregister call.
    /// </summary>
    public static bool TryGet(RoleId roleId, out IIdleBoundsProvider provider)
    {
        provider = null;
        if (roleId.IsNone) return false;

        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];

            if (e.RoleId != roleId) continue;

            // Skip destroyed providers
            if (e.Provider is Component comp && comp == null) continue;

            provider = e.Provider;
            return true;
        }

        return false;
    }

    // ──────────────────────────────────────────────────────────────────
    //  FillResolvedBounds — arbiter-only, per-tick
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fills the target dictionary with resolved bounds for all registered roles.
    /// Called by arbiter during BuildWorldCache. Domains MUST NOT call this directly.
    /// IMPORTANT: Output keyed by RoleId, values converted to AABB2D at this boundary.
    /// PERF: One pass over _entries (typically 1-4). Skips destroyed providers.
    /// Last wins on duplicate roles; warns once per duplicate.
    /// </summary>
    public static void FillResolvedBounds(Dictionary<RoleId, AABB3D> target)
    {
        target.Clear();
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];
            if (e.Provider is Component comp && comp == null) continue;
            Bounds b;
            if (e.Provider != null && e.Provider.TryGetIdleBounds(out b))
            {
                if (target.ContainsKey(e.RoleId))
                {
                    Debug.LogWarning(string.Concat(
                        "[IdleBoundsRegistry] Duplicate bounds for role '", e.RoleId.ToString(),
                        "'; last wins. Check scene for conflicting providers."));
                }
                target[e.RoleId] = b.ToAABB3D();
            }
        }
    }
}
