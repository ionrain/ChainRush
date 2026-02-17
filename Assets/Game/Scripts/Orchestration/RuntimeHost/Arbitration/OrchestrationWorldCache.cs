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

    /// <summary>
    /// Pre-filtered friendly transforms for crowd scoring.
    /// IMPORTANT: Built from friendly combat + idle receiver transforms (deduped via HashSet).
    /// No game-type dependency. Built once per tick in arbiter BuildWorldCache.
    /// Consumed by <see cref="CrowdScoringUtility"/>.
    /// </summary>
    public readonly List<Transform> FriendlyCrowdTransforms = new List<Transform>(128);

    /// <summary>
    /// Reusable dedup set for building FriendlyCrowdTransforms.
    /// PERF: Avoids O(n²) Contains on List. Cleared each tick, no allocations after warmup.
    /// </summary>
    internal readonly HashSet<Transform> CrowdDedup = new HashSet<Transform>(256);

    /// <summary>
    /// Per-transform role lookup, resolved once per tick from friendly receivers.
    /// IMPORTANT: Policies use this instead of GetComponentInParent&lt;IRoleAssetProvider&gt;().
    /// </summary>
    public readonly Dictionary<Transform, RoleAsset> RoleByTransform = new Dictionary<Transform, RoleAsset>(128);

    /// <summary>
    /// Per-role idle bounds, resolved from IdleBoundsRegistry once per tick.
    /// IMPORTANT: Domains/policies read bounds ONLY through this map, never via registry.
    /// Last-wins on duplicate roles (with one-shot warning).
    /// </summary>
    public readonly Dictionary<RoleAsset, Bounds> ResolvedIdleBounds = new Dictionary<RoleAsset, Bounds>(4);

    /// <summary>
    /// Combat target set resolved from OrchestrationRegistry once per tick.
    /// IMPORTANT: Domains read this ONLY through ctx.World, never via registry.
    /// </summary>
    public CombatTargetSet ResolvedCombatTargetSet;

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
        FriendlyCrowdTransforms.Clear();
        CrowdDedup.Clear();
        RoleByTransform.Clear();
        ResolvedIdleBounds.Clear();
        ResolvedCombatTargetSet = null;
    }
}
