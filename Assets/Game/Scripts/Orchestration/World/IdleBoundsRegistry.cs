using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static registry mapping <see cref="RoleAsset"/> → <see cref="IIdleBoundsProvider"/>.
/// One zone per role (MVP). Duplicate registrations warn and "last wins" deterministically.
/// <para>
/// IMPORTANT — Role is resolved generically via <see cref="IRoleAssetProvider"/> at registration
/// time, not by casting to a concrete type. Any <see cref="IIdleBoundsProvider"/> that also
/// implements <see cref="IRoleAssetProvider"/> (or has one on the same GameObject) can register.
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
        public RoleAsset Role;
        public IIdleBoundsProvider Provider;
    }

    private static readonly List<Entry> _entries = new List<Entry>(8);

    // ──────────────────────────────────────────────────────────────────
    //  Register
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a bounds provider keyed by its <see cref="RoleAsset"/>.
    /// Role is resolved via <see cref="IRoleAssetProvider"/> interface, then
    /// via <c>GetComponent&lt;IRoleAssetProvider&gt;</c> as fallback.
    /// If role is null, registration is skipped with a warning.
    /// </summary>
    public static void Register(IIdleBoundsProvider provider)
    {
        if (provider == null) return;

        // ── Resolve role generically ──────────────────────────────────
        RoleAsset role = null;

        if (provider is IRoleAssetProvider rp)
        {
            role = rp.GetRoleAsset();
        }
        else if (provider is Component c)
        {
            IRoleAssetProvider comp = c.GetComponent<IRoleAssetProvider>();
            if (comp != null)
                role = comp.GetRoleAsset();
        }

        if (role == null)
        {
            Debug.LogWarning("[IdleBoundsRegistry] Provider has no RoleAsset; skipping registration.",
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
            if (ReferenceEquals(e.Role, role))
            {
                if (!ReferenceEquals(e.Provider, provider))
                {
                    Debug.LogWarning(string.Concat(
                        "[IdleBoundsRegistry] Duplicate provider for role '", role.Id,
                        "'; replacing previous (last wins)."), provider as Object);
                }

                e.Provider = provider;
                _entries[i] = e;
                return;
            }
        }

        // ── New entry ─────────────────────────────────────────────────
        _entries.Add(new Entry { Role = role, Provider = provider });
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
    /// Looks up the <see cref="IIdleBoundsProvider"/> for a given <see cref="RoleAsset"/>.
    /// Skips Unity-null (destroyed) providers but does NOT prune them (keeps TryGet cheap).
    /// Dead entries are cleaned on next Register/Unregister call.
    /// </summary>
    public static bool TryGet(RoleAsset role, out IIdleBoundsProvider provider)
    {
        provider = null;
        if (role == null) return false;

        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];

            if (!ReferenceEquals(e.Role, role)) continue;

            // Skip destroyed providers
            if (e.Provider is Component comp && comp == null) continue;

            provider = e.Provider;
            return true;
        }

        return false;
    }
}
