/// <summary>
/// Pure arbitration contract. Receives proposals, returns a decision.
/// <para>
/// IMPORTANT — <see cref="Arbitrate"/> must be a pure function: proposals + time → decision.
/// No side effects, no dispatch, no registry reads.
/// </para>
/// </summary>
public interface IArbiter
{
    /// <summary>
    /// Selects the active domain based on proposals and current time.
    /// Pure function — no side effects.
    /// </summary>
    /// <param name="proposals">Domain proposals from this tick's Evaluate pass.</param>
    /// <param name="now">Current <c>Time.time</c>.</param>
    /// <returns>The arbitration decision for the router to execute.</returns>
    ArbiterDecision Arbitrate(OrchestrationArbiterProposals proposals, float now);
}
