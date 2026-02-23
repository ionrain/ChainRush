using UnityEngine;

/// <summary>
/// Abstract base for orchestration domain evaluators polled by
/// <see cref="OrchestrationArbiter"/>. Exists to restrict the arbiter's
/// inspector slot to valid domain implementations only.
/// <para>
/// IMPORTANT — Subclasses must not run their own Update loops or allocate
/// per-tick. The arbiter owns the tick cadence and calls <see cref="Evaluate"/>.
/// </para>
/// <para>
/// C03 compatibility seam: the arbiter may call <see cref="CollectProposals"/>
/// which adapts legacy fixed proposal output into the host proposal collector.
/// </para>
/// </summary>
public abstract class DomainOrchestrator : MonoBehaviour, IOrchestrationDomain
{
    /// <summary>
    /// Canonical domain identity for this orchestrator.
    /// Used by project/loop composition to validate duplicates and preserve explicit
    /// domain ownership without relying on external mapping tables.
    /// </summary>
    public abstract OrchestrationDomainId DomainId { get; }

    /// <inheritdoc/>
    public abstract void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals);

    /// <summary>
    /// C04A registration seam bootstrap: provides a cached host-facing domain registration
    /// record (arbitration metadata + optional policy-map providers).
    /// Override only when a domain needs custom registration behavior.
    /// </summary>
    public virtual DomainRegistration GetRegistration()
    {
        DomainArbitrationProfile profile = new DomainArbitrationProfile(stickyPrimary: false);
        IDomainArbitrationProfileSource profileSource = this as IDomainArbitrationProfileSource;
        if (profileSource != null)
        {
            DomainArbitrationProfile sourceProfile = profileSource.GetArbitrationProfile();
            // Domain identity is owned by the orchestrator itself; profile contributes
            // arbitration metadata (e.g. sticky/fallback behavior).
            profile = new DomainArbitrationProfile(sourceProfile.StickyPrimary);
        }

        IDomainArbiterBindingContributor arbiterBindingContributor =
            CreateArbiterBindingContributor();
        IDomainExecutionRouteContributor executionRouteContributor =
            CreateExecutionRouteContributor();

        return new DomainRegistration(
            orchestrator: this,
            arbitrationProfile: profile,
            arbiterBindingContributor: arbiterBindingContributor,
            executionRouteContributor: executionRouteContributor);
    }

    /// <summary>
    /// C04A transitional seam: domains may provide cached arbiter-binding contributors
    /// (for example legacy policy-map providers) without forcing the base class to know
    /// domain-specific provider interfaces.
    /// </summary>
    protected virtual IDomainArbiterBindingContributor CreateArbiterBindingContributor()
    {
        return null;
    }

    /// <summary>
    /// C04A route onboarding seam: domains may contribute execution route registrations
    /// (for example RuntimeHost built-in routes during migration) without editing
    /// <see cref="ExecutionRouter"/> constructor/entrypoint.
    /// </summary>
    protected virtual IDomainExecutionRouteContributor CreateExecutionRouteContributor()
    {
        return null;
    }

    /// <summary>
    /// Producer-format bridge used during C03 migration. Default implementation
    /// preserves legacy domain behavior by evaluating into a reusable legacy
    /// proposal scratch buffer and importing it into the unified collector.
    /// </summary>
    public virtual void CollectProposals(
        OrchestrationArbiterContext ctx,
        OrchestrationProposalCollector collector,
        OrchestrationArbiterProposals legacyProposalScratch)
    {
        if (collector == null || legacyProposalScratch == null)
            return;

        legacyProposalScratch.Clear();
        Evaluate(ctx, legacyProposalScratch);
        collector.ImportLegacy(legacyProposalScratch);
    }
}
