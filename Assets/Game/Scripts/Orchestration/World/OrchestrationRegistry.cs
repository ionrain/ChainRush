using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight static registry for orchestration reporters and providers.
/// Populated via OnEnable/OnDisable hooks on integration components.
/// IMPORTANT: This is read-only aggregation only — no gameplay ownership, no Update loop.
/// PERF: Linear Contains is acceptable given small N. Safe for pooled objects
/// calling OnEnable multiple times (idempotent).
/// </summary>
public static class OrchestrationRegistry
{
    static readonly List<IStateReporter> _stateReporters = new List<IStateReporter>(256);
    static readonly List<ICapabilityProvider> _capabilityProviders = new List<ICapabilityProvider>(256);
    static readonly List<ICombatCommandReceiver> _combatReceivers = new List<ICombatCommandReceiver>(64);

    public static IReadOnlyList<IStateReporter> StateReporters => _stateReporters;
    public static IReadOnlyList<ICapabilityProvider> CapabilityProviders => _capabilityProviders;
    public static IReadOnlyList<ICombatCommandReceiver> CombatReceivers => _combatReceivers;

    /// <summary>Returns the underlying mutable list for internal query iteration.</summary>
    internal static List<IStateReporter> StateReportersList => _stateReporters;

    public static void Register(IStateReporter reporter)
    {
        if (reporter == null) return;
        if (!_stateReporters.Contains(reporter))
            _stateReporters.Add(reporter);
    }

    public static void Unregister(IStateReporter reporter)
    {
        if (reporter == null) return;
        _stateReporters.Remove(reporter);
    }

    public static void Register(ICapabilityProvider provider)
    {
        if (provider == null) return;
        if (!_capabilityProviders.Contains(provider))
            _capabilityProviders.Add(provider);
    }

    public static void Unregister(ICapabilityProvider provider)
    {
        if (provider == null) return;
        _capabilityProviders.Remove(provider);
    }

    public static void Register(ICombatCommandReceiver receiver)
    {
        if (receiver == null) return;
        if (!_combatReceivers.Contains(receiver))
            _combatReceivers.Add(receiver);
    }

    public static void Unregister(ICombatCommandReceiver receiver)
    {
        if (receiver == null) return;
        _combatReceivers.Remove(receiver);
    }

    // ──────────────────────────────────────────────────────────────────
    //  CombatTargetSet — Variant B registry binding (typed FactionAsset)
    // ──────────────────────────────────────────────────────────────────

    struct CombatTargetSetEntry
    {
        public FactionAsset Faction;
        public CombatTargetSet Set;
    }

    static readonly List<CombatTargetSetEntry> _combatTargetSets = new List<CombatTargetSetEntry>(8);

    public static void Register(CombatTargetSet set)
    {
        if (set == null) return;

        // Prune Unity-null entries and check for duplicates
        for (int i = _combatTargetSets.Count - 1; i >= 0; i--)
        {
            CombatTargetSetEntry entry = _combatTargetSets[i];
            if (entry.Set == null)
            {
                _combatTargetSets.RemoveAt(i);
                continue;
            }
            if (ReferenceEquals(entry.Set, set))
                return; // already registered
        }

        _combatTargetSets.Add(new CombatTargetSetEntry
        {
            Faction = set.GetFactionAsset(),
            Set = set
        });
    }

    public static void Unregister(CombatTargetSet set)
    {
        for (int i = _combatTargetSets.Count - 1; i >= 0; i--)
        {
            CombatTargetSetEntry entry = _combatTargetSets[i];
            if (ReferenceEquals(entry.Set, set) || entry.Set == null)
                _combatTargetSets.RemoveAt(i);
        }
    }

    /// <summary>
    /// Resolves the first alive <see cref="CombatTargetSet"/> matching the given
    /// <paramref name="faction"/> by reference equality.
    /// Opportunistically prunes Unity-null entries.
    /// Null <paramref name="faction"/> never matches.
    /// </summary>
    public static bool TryGetCombatTargetSet(FactionAsset faction, out CombatTargetSet set)
    {
        set = null;

        if (faction == null)
            return false;

        for (int i = _combatTargetSets.Count - 1; i >= 0; i--)
        {
            CombatTargetSetEntry entry = _combatTargetSets[i];
            if (entry.Set == null)
            {
                _combatTargetSets.RemoveAt(i);
                continue;
            }

            if (set == null && ReferenceEquals(entry.Faction, faction))
                set = entry.Set;
        }

        return set != null;
    }
}
