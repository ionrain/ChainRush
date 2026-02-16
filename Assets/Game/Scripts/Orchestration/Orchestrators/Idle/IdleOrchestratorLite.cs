using UnityEngine;

/// <summary>
/// Idle domain evaluator. Owns the <see cref="IdleRolePolicyMapAsset"/> reference
/// and exposes it via <see cref="IIdleRolePolicyMapSource"/> so the arbiter can
/// pull the map each tick. Signals "idle domain active" via proposals.
/// <para>
/// IMPORTANT — This class does NOT tick itself. It implements
/// <see cref="IOrchestrationDomain"/> and is polled by
/// <see cref="OrchestrationArbiter"/> each tick.
/// </para>
/// <para>
/// IMPORTANT — Does NOT dispatch commands to receivers. Only writes proposals.
/// The arbiter owns all dispatch logic including per-unit command generation.
/// </para>
/// </summary>
public sealed class IdleOrchestratorLite : DomainOrchestrator, IIdleRolePolicyMapSource
{
    [Header("Per-Role Policies")]
    [Tooltip("Data-driven mapping of RoleAssets to idle policies.")]
    [SerializeField] IdleRolePolicyMapAsset rolePolicyMap;

    [Header("Debug")]
    [SerializeField] bool debugLog;

    // ──────────────────────────────────────────────────────────────────
    //  IIdleRolePolicyMapSource
    // ──────────────────────────────────────────────────────────────────

    public IdleRolePolicyMapAsset GetIdleRolePolicyMap() => rolePolicyMap;

    // ──────────────────────────────────────────────────────────────────
    //  IOrchestrationDomain
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Signals "idle domain is active this tick". The arbiter pulls the map
    /// reference via <see cref="IIdleRolePolicyMapSource"/> after all domains evaluate.
    /// <para>
    /// IMPORTANT — <c>proposals.SetIdle(null)</c> means "idle domain present and wants
    /// idle considered", NOT "here is a policy payload". The <c>IdlePolicies</c> field
    /// in proposals is unused; <c>HasIdle</c> is the sole dispatch trigger.
    /// </para>
    /// </summary>
    public override void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals)
    {
        proposals.SetIdle(null);
    }
}
