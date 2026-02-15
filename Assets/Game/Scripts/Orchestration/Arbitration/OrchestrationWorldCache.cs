using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-tick world snapshot built by <see cref="OrchestrationArbiter"/> before polling domains.
/// Contains pre-filtered lists of alive actors and friendly receivers so that domains
/// and dispatch methods can iterate without re-querying <see cref="OrchestrationRegistry"/>.
/// <para>
/// IMPORTANT — Single instance, reused each tick. Lists are <see cref="Clear"/>ed and
/// refilled; no per-tick allocations.
/// </para>
/// <para>
/// IMPORTANT — Actors list contains only alive <see cref="IOrchestrationActor"/>
/// entries that also implement <see cref="IFactionAssetProvider"/> with a non-null faction.
/// Unity-null entries are pruned during build.
/// </para>
/// </summary>
public sealed class OrchestrationWorldCache
{
    // PERF: Pre-sized, reused each tick. Clear() does not shrink capacity.
    public readonly List<IOrchestrationActor> Actors = new List<IOrchestrationActor>(256);
    public readonly List<ICombatCommandReceiver> FriendlyCombatReceivers = new List<ICombatCommandReceiver>(128);
    public readonly List<IIdleCommandReceiver> FriendlyIdleReceivers = new List<IIdleCommandReceiver>(128);

    public Vector2 Anchor;
    public float Now;

    /// <summary>
    /// Clears all cached lists. Called at the start of each arbiter tick.
    /// </summary>
    public void Clear()
    {
        Actors.Clear();
        FriendlyCombatReceivers.Clear();
        FriendlyIdleReceivers.Clear();
    }
}
