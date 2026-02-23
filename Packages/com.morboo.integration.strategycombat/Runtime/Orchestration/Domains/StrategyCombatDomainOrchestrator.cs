using UnityEngine;

/// <summary>
/// Shared StrategyCombat domain orchestrator entrypoint.
/// Owns the <see cref="DomainOrchestrator"/> runtime contract while delegating
/// domain-specific behavior to a component/provider graph
/// (<see cref="StrategyCombatDomainComponentBase"/> + target providers + data assets).
/// </summary>
public sealed class StrategyCombatDomainOrchestrator : DomainOrchestrator, IDomainArbitrationProfileSource, IStrategyCombatRouteExecutionPolicyConsumer
{
    [Header("Domain Component")]
    [Tooltip("StrategyCombat domain component that owns domain-specific evaluation/bindings/routes.")]
    [SerializeField] StrategyCombatDomainComponentBase domainComponent;

    [Header("Debug")]
    [SerializeField] bool warnOnInvalidDomainComponent = true;

    bool _warnedMissingDomainComponent;
    bool _warnedInvalidDomainId;

    public override OrchestrationDomainId DomainId
    {
        get
        {
            StrategyCombatDomainComponentBase component = domainComponent;
            if (component == null)
                return OrchestrationDomainId.None;

            return component.DomainId;
        }
    }

    public DomainArbitrationProfile GetArbitrationProfile()
    {
        StrategyCombatDomainComponentBase component = domainComponent;
        bool stickyPrimary = component != null && component.StickyPrimaryArbitration;
        return StrategyCombatDomainOrchestratorCommon.CreateArbitrationProfile(stickyPrimary);
    }

    protected override IDomainArbiterBindingContributor CreateArbiterBindingContributor()
    {
        StrategyCombatDomainComponentBase component = GetValidDomainComponentForComposition();
        return component != null ? component.CreateArbiterBindingContributor() : null;
    }

    protected override IDomainExecutionRouteContributor CreateExecutionRouteContributor()
    {
        StrategyCombatDomainComponentBase component = GetValidDomainComponentForComposition();
        return component != null ? component.CreateExecutionRouteContributor() : null;
    }

    public void ApplyRouteExecutionPolicy(StrategyCombatRouteExecutionPolicyAsset policy)
    {
        StrategyCombatDomainComponentBase component = GetValidDomainComponentForComposition();
        if (component != null)
            component.ApplyRouteExecutionPolicy(policy);
    }

    public override void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals)
    {
        StrategyCombatDomainComponentBase component = GetValidDomainComponentForComposition();
        if (component == null)
            return;

        component.EvaluateDomain(ctx, proposals);
    }

    StrategyCombatDomainComponentBase GetValidDomainComponentForComposition()
    {
        StrategyCombatDomainComponentBase component = domainComponent;
        if (component == null)
        {
            WarnMissingDomainComponentOnce();
            return null;
        }

        if (component.DomainId == OrchestrationDomainId.None)
        {
            WarnInvalidDomainIdOnce();
            return null;
        }

        return component;
    }

    void WarnMissingDomainComponentOnce()
    {
        if (!warnOnInvalidDomainComponent || _warnedMissingDomainComponent)
            return;

        _warnedMissingDomainComponent = true;
        Debug.LogError(
            "[StrategyCombatDomainOrchestrator] Missing StrategyCombatDomainComponentBase. " +
            "Assign a domain component (Combat/Idle/...) explicitly.",
            this);
    }

    void WarnInvalidDomainIdOnce()
    {
        if (!warnOnInvalidDomainComponent || _warnedInvalidDomainId)
            return;

        _warnedInvalidDomainId = true;
        Debug.LogError(
            "[StrategyCombatDomainOrchestrator] Domain component returned OrchestrationDomainId.None. " +
            "Domain components must provide an explicit DomainId.",
            this);
    }
}
