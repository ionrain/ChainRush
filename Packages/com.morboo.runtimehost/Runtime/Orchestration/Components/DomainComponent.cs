using UnityEngine;

/// <summary>
/// Generic orchestration domain-component contract.
/// A <see cref="DomainOrchestratorComponent"/> delegates domain identity, proposal evaluation,
/// arbiter bindings and route contribution to one component implementing this contract.
/// IMPORTANT: RuntimeHost owns the generic form; genre-specific implementations
/// (e.g. CombatDomainComponent, IdleDomainComponent) live in StrategyCombat.
/// </summary>
public interface IDomainComponent : IDomainRouteExecutionPolicyConsumer
{
    OrchestrationDomainId DomainId { get; }
    bool StickyPrimaryArbitration { get; }

    void EvaluateDomain(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals);
    IDomainArbiterBindingContributor CreateArbiterBindingContributor();
    IDomainExecutionRouteContributor CreateExecutionRouteContributor();
}

/// <summary>
/// Abstract base for orchestration domain components used by <see cref="DomainOrchestratorComponent"/>.
/// IMPORTANT: No <c>Base</c> suffix per C04D naming rule for abstract types in upper layers.
/// </summary>
public abstract class DomainComponent : MonoBehaviour, IDomainComponent
{
    public abstract OrchestrationDomainId DomainId { get; }
    public abstract bool StickyPrimaryArbitration { get; }

    public abstract void EvaluateDomain(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals);

    public virtual IDomainArbiterBindingContributor CreateArbiterBindingContributor()
    {
        return null;
    }

    public virtual IDomainExecutionRouteContributor CreateExecutionRouteContributor()
    {
        return null;
    }

    public virtual void ApplyRouteExecutionPolicy(DomainRouteExecutionPolicy policy)
    {
    }
}
