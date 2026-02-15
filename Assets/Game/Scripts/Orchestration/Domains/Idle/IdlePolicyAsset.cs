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
    /// <param name="self">The unit's own transform.</param>
    /// <param name="anchor">Orchestrator-defined anchor point.</param>
    /// <param name="now"><see cref="Time.time"/> at tick start.</param>
    /// <param name="roleSeed">Stable seed for the role (typically <c>RoleAsset.GetInstanceID()</c>).
    /// Session-stable only. If cross-session determinism is needed, use a serialized
    /// seed on RoleAsset or hash RoleAsset.Id.</param>
    /// <param name="entitySeed">Stable per-entity seed (from <see cref="IRoleContextProvider.GetEntitySeed"/>).</param>
    /// <param name="debugInfo">Short debug string (null is fine; avoid allocations).</param>
    public virtual IdleCommand ChooseCommand(
        Transform self, Vector2 anchor, float now,
        int roleSeed, int entitySeed,
        out string debugInfo)
    {
        return ChooseCommand(self, anchor, now, out debugInfo);
    }

    /// <summary>
    /// Context-aware overload that receives the arbiter's per-tick context.
    /// Policies that need world state (e.g. crowd avoidance) should override this.
    /// <para>
    /// RATIONALE: Passing <see cref="OrchestrationArbiterContext"/> is an intentional
    /// dependency — domain policies can use the arbiter's world cache for scoring.
    /// </para>
    /// </summary>
    // TODO: If layering becomes an issue, extract a minimal IWorldQuery interface
    // to decouple policies from arbiter context.
    public virtual IdleCommand ChooseCommand(
        Transform self, Vector2 anchor, float now,
        int roleSeed, int entitySeed,
        OrchestrationArbiterContext ctx,
        out string debugInfo)
    {
        return ChooseCommand(self, anchor, now, roleSeed, entitySeed, out debugInfo);
    }
}
