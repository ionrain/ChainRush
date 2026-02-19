/// <summary>
/// Interface for orchestration domains polled by <see cref="OrchestrationArbiter"/>.
/// Domains evaluate the world state and write proposals; they must NOT dispatch
/// commands to receivers directly.
/// </summary>
public interface IOrchestrationDomain
{
    /// <summary>
    /// Called by <see cref="OrchestrationArbiter"/> each tick.
    /// Implementations should read world state and write into <paramref name="proposals"/>.
    /// IMPORTANT: Do not dispatch commands or allocate per-tick objects here.
    /// </summary>
    void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals);
}
