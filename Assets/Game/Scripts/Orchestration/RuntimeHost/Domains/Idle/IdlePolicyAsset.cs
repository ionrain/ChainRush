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
public abstract class IdlePolicyAsset : ScriptableObject
{
    [SerializeField] string id;

    /// <summary>
    /// Display/log identifier. Falls back to asset name if <see cref="id"/> is empty.
    /// RATIONALE: Used for debug logging only, never for behavior selection.
    /// </summary>
    public string Id => string.IsNullOrEmpty(id) ? name : id;

    /// <summary>
    /// Computes the idle command for a unit.
    /// PERF: Must be allocation-free. No LINQ. Index loops only.
    /// </summary>
    /// <param name="self">The unit's own transform.</param>
    /// <param name="anchor">Orchestrator-defined anchor point (e.g., hero position).</param>
    /// <param name="now"><see cref="Time.time"/> at tick start.</param>
    /// <param name="debugInfo">Short debug string for logging (null is fine; avoid allocations).</param>
    /// <returns>The idle command to apply.</returns>
    public abstract IdleCommand ChooseCommand(Transform self, Vector2 anchor, float now, out string debugInfo);

    /// <summary>
    /// Per-role dispatch overload used by <see cref="OrchestrationArbiter"/>.
    /// Provides a stable role seed and per-entity seed so policies can generate
    /// deterministic but unique positions for each unit within a role.
    /// <para>
    /// Default implementation ignores both seeds and delegates to the original
    /// abstract method. Override in policies that need role/entity awareness
    /// (e.g. <see cref="IdleRingSlotPolicyAsset"/>).
    /// </para>
    /// </summary>
    public virtual IdleCommand ChooseCommand(
        Transform self, Vector2 anchor, float now,
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
    public virtual IdleCommand ChooseCommand(
        Float2 selfPosition, EntityId selfId,
        Float2 anchor, float now,
        int roleSeed, int entitySeed,
        IWorldQuery world,
        out string debugInfo)
    {
        // Default: delegate to seed-based overload (ignores world).
        // Policies that need crowd data override this directly.
        return ChooseCommand(null, anchor.ToVector2(), now, roleSeed, entitySeed, out debugInfo);
    }
}
