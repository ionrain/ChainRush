using UnityEngine;

/// <summary>
/// Shared StrategyCombat domain-component contract.
/// A single <see cref="StrategyCombatDomainOrchestrator"/> delegates domain identity,
/// proposal evaluation, arbiter bindings and route contribution to one component that
/// implements this contract.
/// </summary>
public interface IStrategyCombatDomainComponent : IStrategyCombatRouteExecutionPolicyConsumer
{
    OrchestrationDomainId DomainId { get; }
    bool StickyPrimaryArbitration { get; }

    void EvaluateDomain(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals);
    IDomainArbiterBindingContributor CreateArbiterBindingContributor();
    IDomainExecutionRouteContributor CreateExecutionRouteContributor();
}

/// <summary>
/// Base class for StrategyCombat domain components used by the shared
/// <see cref="StrategyCombatDomainOrchestrator"/>.
/// </summary>
public abstract class StrategyCombatDomainComponentBase : MonoBehaviour, IStrategyCombatDomainComponent
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

    public virtual void ApplyRouteExecutionPolicy(StrategyCombatRouteExecutionPolicyAsset policy)
    {
    }
}
