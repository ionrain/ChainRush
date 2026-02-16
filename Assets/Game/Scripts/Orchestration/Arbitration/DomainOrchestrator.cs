using UnityEngine;

/// <summary>
/// Abstract base for orchestration domain evaluators polled by
/// <see cref="OrchestrationArbiter"/>. Exists to restrict the arbiter's
/// inspector slot to valid domain implementations only.
/// <para>
/// IMPORTANT — Subclasses must not run their own Update loops or allocate
/// per-tick. The arbiter owns the tick cadence and calls <see cref="Evaluate"/>.
/// </para>
/// </summary>
public abstract class DomainOrchestrator : MonoBehaviour, IOrchestrationDomain
{
    /// <inheritdoc/>
    public abstract void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals);
}
