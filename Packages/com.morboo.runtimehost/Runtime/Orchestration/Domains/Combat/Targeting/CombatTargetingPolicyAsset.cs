using UnityEngine;

/// <summary>
/// Abstract base for typed targeting policies. Concrete subclasses define how a unit
/// picks its combat target from the available candidates.
/// <para>
/// IMPORTANT — Must be allocation-free. <paramref name="debugInfo"/> is for logging only.
/// </para>
/// <para>
/// IMPORTANT — All targeting policy code lives under
/// <c>Orchestration/Domains/Combat/Targeting/</c>. Do not add combat-specific fields
/// to Core types. Future parameters (Step 9.1) go as typed serialized fields on
/// concrete policy assets.
/// </para>
/// <para>
/// IMPORTANT — Policies operate on EntityId + Float2 via IWorldQueryBase. No Transform.
/// EntityId → Transform resolution happens at the Integration boundary
/// (UnitCombatTargetSelector).
/// </para>
/// </summary>
public abstract class CombatTargetingPolicyAsset : ScriptableObject
{
    [SerializeField] string id = "PrimaryTarget";

    /// <summary>
    /// Display/log identifier. Falls back to asset name if <see cref="id"/> is empty.
    /// </summary>
    public string Id => string.IsNullOrEmpty(id) ? name : id;

    /// <summary>
    /// Selects a target for the unit. Returns the chosen EntityId, or EntityId.None for "no target".
    /// PERF: Must be allocation-free. No LINQ. Index loops only.
    /// </summary>
    /// <param name="selfEntityId">The unit's own EntityId.</param>
    /// <param name="selfPosition">The unit's current position.</param>
    /// <param name="primaryTarget">The orchestrator-chosen primary target EntityId.</param>
    /// <param name="targetSet">Shared Top-K candidate set (may be null).</param>
    /// <param name="now">Current time for TTL checks on <paramref name="targetSet"/>.</param>
    /// <param name="world">Read-only world snapshot for position lookups.</param>
    /// <param name="debugInfo">Short debug string for logging (no allocations besides the literal).</param>
    /// <returns>The chosen target EntityId, or EntityId.None to indicate "no target".</returns>
    public abstract EntityId ChooseTarget(
        EntityId selfEntityId,
        Float2 selfPosition,
        EntityId primaryTarget,
        CombatTargetSet targetSet,
        float now,
        IWorldQueryBase world,
        out string debugInfo);
}
