using UnityEngine;

/// <summary>
/// Generic orchestration domain orchestrator wrapper.
/// Owns the <see cref="DomainOrchestrator"/> runtime contract while delegating
/// domain-specific behavior to a component/provider graph
/// (<see cref="DomainComponent"/> + target providers + data assets).
/// IMPORTANT: RuntimeHost owns the generic form; genre-specific components live in StrategyCombat.
/// </summary>
public sealed class DomainOrchestratorComponent : DomainOrchestrator, IDomainArbitrationProfileSource, IDomainRouteExecutionPolicyConsumer
{
    [Header("Domain Component")]
    [Tooltip("Domain component that owns domain-specific evaluation/bindings/routes.")]
    [SerializeField] DomainComponent domainComponent;

    [Header("Debug")]
    [SerializeField] bool warnOnInvalidDomainComponent = true;

    bool _warnedMissingDomainComponent;
    bool _warnedInvalidDomainId;

    public override OrchestrationDomainId DomainId
    {
        get
        {
            DomainComponent component = domainComponent;
            if (component == null)
                return OrchestrationDomainId.None;

            return component.DomainId;
        }
    }

    public DomainArbitrationProfile GetArbitrationProfile()
    {
        DomainComponent component = domainComponent;
        bool stickyPrimary = component != null && component.StickyPrimaryArbitration;
        return DomainOrchestratorComposition.CreateArbitrationProfile(stickyPrimary);
    }

    protected override IDomainArbiterBindingContributor CreateArbiterBindingContributor()
    {
        DomainComponent component = GetValidDomainComponentForComposition();
        return component != null ? component.CreateArbiterBindingContributor() : null;
    }

    protected override IDomainExecutionRouteContributor CreateExecutionRouteContributor()
    {
        DomainComponent component = GetValidDomainComponentForComposition();
        return component != null ? component.CreateExecutionRouteContributor() : null;
    }

    public void ApplyRouteExecutionPolicy(DomainRouteExecutionPolicy policy)
    {
        DomainComponent component = GetValidDomainComponentForComposition();
        if (component != null)
            component.ApplyRouteExecutionPolicy(policy);
    }

    public override void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals)
    {
        DomainComponent component = GetValidDomainComponentForComposition();
        if (component == null)
            return;

        component.EvaluateDomain(ctx, proposals);
    }

    DomainComponent GetValidDomainComponentForComposition()
    {
        DomainComponent component = domainComponent;
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
            "[DomainOrchestratorComponent] Missing DomainComponent. " +
            "Assign a domain component (Combat/Idle/...) explicitly.",
            this);
    }

    void WarnInvalidDomainIdOnce()
    {
        if (!warnOnInvalidDomainComponent || _warnedInvalidDomainId)
            return;

        _warnedInvalidDomainId = true;
        Debug.LogError(
            "[DomainOrchestratorComponent] Domain component returned OrchestrationDomainId.None. " +
            "Domain components must provide an explicit DomainId.",
            this);
    }
}
