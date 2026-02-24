using UnityEngine;

/// <summary>
/// Idle domain component. Owns the <see cref="IdleRolePolicyMapAsset"/> reference
/// and contributes it to the arbiter via cached domain registration bindings
/// through <see cref="DomainOrchestratorComponent"/>.
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
public sealed class IdleDomainComponent : DomainComponent
{
    StrategyCombatIdleExecutionRoute _idleExecutionRoute;

    public override OrchestrationDomainId DomainId => OrchestrationDomainId.Idle;
    public override bool StickyPrimaryArbitration => false;

    [Header("Per-Role Policies")]
    [Tooltip("Data-driven mapping of RoleAssets to idle policies.")]
    [SerializeField] IdleRolePolicyMapAsset rolePolicyMap;

    [Header("Idle Targeting")]
    [Tooltip("Domain-owned provider for idle self-position/target resolution during route execution.")]
    [SerializeField] IdleTargetProvider idleTargetProvider;

    [Header("Route Execution")]
    [Tooltip("Shared route-execution policy provider component (optional; null preserves current behavior).")]
    [SerializeField] DomainRouteExecutionPolicyProvider routeExecutionPolicyProvider;

    [Header("Debug")]
    [SerializeField] bool debugLog;
    bool _warnedMissingRoutePolicyProviderForPolicy;

    public override IDomainArbiterBindingContributor CreateArbiterBindingContributor()
    {
        return DomainArbiterBindingContributors.CreateFixedContributor(
            new DomainArbiterBindingRegistration(
                StrategyCombatArbiterBindingKeys.IdleRolePolicyMap,
                StrategyCombatArbiterBindingAppliers.IdleRolePolicyMap,
                rolePolicyMap),
            default,
            default);
    }

    public override IDomainExecutionRouteContributor CreateExecutionRouteContributor()
    {
        StrategyCombatRouteExecutionPolicyAsset routePolicy = GetRouteExecutionPolicyAsset();
        _idleExecutionRoute ??= new StrategyCombatIdleExecutionRoute(idleTargetProvider, routePolicy);
        return DomainOrchestratorComposition.CreateFixedRouteContributorWithUnknownFallback(
            DomainId,
            _idleExecutionRoute.Execute,
            StrategyCombatUnknownRouteFallbackExecutionRoute.GetShared(routePolicy).Execute);
    }

    public override void ApplyRouteExecutionPolicy(DomainRouteExecutionPolicy policy)
    {
        StrategyCombatRouteExecutionPolicyAsset currentPolicy = GetRouteExecutionPolicyAsset();
        if (!DomainOrchestratorComposition.ShouldRebuildRouteExecutorForPolicyChange(currentPolicy, policy))
            return;

        if (routeExecutionPolicyProvider != null)
        {
            routeExecutionPolicyProvider.ApplyRouteExecutionPolicy(policy);
        }
        else if (!_warnedMissingRoutePolicyProviderForPolicy)
        {
            _warnedMissingRoutePolicyProviderForPolicy = true;
            Debug.LogError(
                "[IdleDomainComponent] Missing DomainRouteExecutionPolicyProvider. " +
                "Route-policy bridge cannot apply policy configuration to this domain without the shared provider component.",
                this);
        }

        _idleExecutionRoute = null;
    }

    StrategyCombatRouteExecutionPolicyAsset GetRouteExecutionPolicyAsset()
    {
        return routeExecutionPolicyProvider != null ? routeExecutionPolicyProvider.CurrentPolicy as StrategyCombatRouteExecutionPolicyAsset : null;
    }

    // ──────────────────────────────────────────────────────────────────
    //  IOrchestrationDomain
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Signals "idle domain is active this tick" by setting <c>HasIdle = true</c>.
    /// The arbiter pulls cached domain bindings after all domains evaluate.
    /// </summary>
    public override void EvaluateDomain(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals)
    {
        proposals.SetIdle();
    }
}
