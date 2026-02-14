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
/// </summary>
public abstract class CombatTargetingPolicyAsset : ScriptableObject
{
    [SerializeField] string id = "PrimaryTarget";

    /// <summary>
    /// Display/log identifier. Falls back to asset name if <see cref="id"/> is empty.
    /// </summary>
    public string Id => string.IsNullOrEmpty(id) ? name : id;

    /// <summary>
    /// Selects a target for the unit.
    /// PERF: Must be allocation-free. No LINQ. Index loops only.
    /// </summary>
    /// <param name="self">The unit's own transform.</param>
    /// <param name="primaryTarget">The orchestrator-chosen primary target.</param>
    /// <param name="targetSet">Shared Top-K candidate set (may be null).</param>
    /// <param name="debugInfo">Short debug string for logging (no allocations besides the literal).</param>
    /// <returns>The chosen target transform, or null to indicate "no target".</returns>
    public abstract Transform ChooseTarget(
        Transform self,
        Transform primaryTarget,
        CombatTargetSet targetSet,
        out string debugInfo);
}
