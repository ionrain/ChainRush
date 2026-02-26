using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abstract base for typed idle policies. Concrete subclasses define how a unit
/// chooses its idle position/behavior when no combat is active.
/// <para>
/// IMPORTANT — Must be allocation-free and completely stateless. Per-unit timers,
/// last-known positions, or any mutable state belong in per-unit components
/// (selector/executor), never in ScriptableObject assets.
/// </para>
/// <para>
/// IMPORTANT — All idle policy code lives under
/// <c>Orchestration/Domains/Idle/Policies/</c>. Do not add idle-specific fields
/// to Core types.
/// </para>
/// </summary>
public abstract class IdlePolicyAsset : ScriptableObject, IActorCapabilityGatedPolicy
{
    [SerializeField] string id;

    [Tooltip("C06: Actor must have all of these capabilities for this policy to apply. Empty = no gate.")]
    [SerializeField] List<ActorCapability> requiredCapabilities;

    /// <summary>C06: Capability gate — empty list means no restriction.</summary>
    public IReadOnlyList<ActorCapability> RequiredCapabilities => requiredCapabilities;

    /// <summary>
    /// Display/log identifier. Falls back to asset name if <see cref="id"/> is empty.
    /// RATIONALE: Used for debug logging only, never for behavior selection.
    /// </summary>
    public string Id => string.IsNullOrEmpty(id) ? name : id;

    /// <summary>
    /// Computes the orchestration command for a unit on the legacy transform path.
    /// PERF: Must be allocation-free. No LINQ. Index loops only.
    /// </summary>
    /// <param name="self">The unit's own transform.</param>
    /// <param name="anchor">Orchestrator-defined anchor point (e.g., hero position).</param>
    /// <param name="now">Current time from <see cref="TickContext.Now"/> or <see cref="IWorldQuery.Now"/>.</param>
    /// <param name="debugInfo">Short debug string for logging (null is fine; avoid allocations).</param>
    /// <returns>The orchestration command to apply.</returns>
    public abstract OrchestrationCommand ChooseCommand(Transform self, Vector3 anchor, float now, out string debugInfo);

    /// <summary>
    /// Per-role dispatch overload used by <see cref="OrchestrationArbiter"/>.
    /// Provides a stable role seed and per-entity seed so policies can generate
    /// deterministic but unique positions for each unit within a role.
    /// <para>
    /// Default implementation ignores both seeds and delegates to the original
    /// abstract method. Override in policies that need role/entity awareness
    /// (e.g. <see cref="IdleRingSlotPolicy2DAsset"/>).
    /// </para>
    /// </summary>
    public virtual OrchestrationCommand ChooseCommand(
        Transform self, Vector3 anchor, float now,
        int roleSeed, int entitySeed,
        out string debugInfo)
    {
        return ChooseCommand(self, anchor, now, out debugInfo);
    }

    /// <summary>
    /// IWorldQuery-based overload. Policies that need world state (e.g. crowd avoidance)
    /// should override this. Receives position and EntityId instead of Transform.
    /// <para>
    /// IMPORTANT — This is the preferred overload for new policies. The arbiter calls
    /// this when dispatching per-unit idle commands.
    /// </para>
    /// </summary>
    /// <param name="selfPosition">The unit's current position (Float2).</param>
    /// <param name="selfId">The unit's stable <see cref="EntityId"/>.</param>
    /// <param name="anchor">Orchestrator-defined anchor point (Float2).</param>
    /// <param name="now">Tick time.</param>
    /// <param name="roleSeed">Stable seed for the role.</param>
    /// <param name="entitySeed">Stable per-entity seed (from EntityId.ToStableInt).</param>
    /// <param name="world">Read-only world snapshot for crowd scoring and role lookups.</param>
    /// <param name="debugInfo">Short debug string (null is fine; avoid allocations).</param>
    public virtual OrchestrationCommand ChooseCommand(
        Float3 selfPosition, EntityId selfId,
        Float3 anchor, float now,
        int roleSeed, int entitySeed,
        IWorldQuery world,
        out string debugInfo)
    {
        // Default: delegate to seed-based overload (ignores world).
        // Policies that need crowd data override this directly.
        return ChooseCommand(null, anchor.ToVector3(), now, roleSeed, entitySeed, out debugInfo);
    }
}
