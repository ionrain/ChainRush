using UnityEngine;

/// <summary>
/// Idle domain evaluator. Owns the <see cref="IdleRolePolicyMapAsset"/> reference
/// and contributes it to the arbiter via cached domain registration bindings.
/// Signals "idle domain active" via proposals.
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
public sealed class IdleOrchestratorLite : DomainOrchestrator, IDomainArbitrationProfileSource
{
    public override OrchestrationDomainId DomainId => OrchestrationDomainId.Idle;

    [Header("Per-Role Policies")]
    [Tooltip("Data-driven mapping of RoleAssets to idle policies.")]
    [SerializeField] IdleRolePolicyMapAsset rolePolicyMap;

    [Header("Debug")]
    [SerializeField] bool debugLog;

    // ──────────────────────────────────────────────────────────────────
    //  IDomainArbitrationProfileSource
    // ──────────────────────────────────────────────────────────────────

    public DomainArbitrationProfile GetArbitrationProfile()
    {
        return new DomainArbitrationProfile(stickyPrimary: false);
    }

    protected override IDomainArbiterBindingContributor CreateArbiterBindingContributor()
    {
        return DomainArbiterBindingContributors.CreatePolicyMapContributor(
            idleRolePolicyMapKey: StrategyCombatArbiterBindingKeys.IdleRolePolicyMap,
            idleRolePolicyMapApply: StrategyCombatArbiterBindingAppliers.IdleRolePolicyMap,
            idleRolePolicyMap: rolePolicyMap,
            combatRolePolicyMapKey: default,
            combatRolePolicyMapApply: null,
            combatRolePolicyMap: null,
            combatRoleConstraintsMapKey: default,
            combatRoleConstraintsMapApply: null,
            combatRoleConstraintsMap: null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  IOrchestrationDomain
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Signals "idle domain is active this tick" by setting <c>HasIdle = true</c>.
    /// The arbiter pulls cached domain bindings after all domains evaluate.
    /// </summary>
    public override void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals)
    {
        proposals.SetIdle();
    }
}
