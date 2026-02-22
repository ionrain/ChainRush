using System.Collections.Generic;

/// <summary>
/// Pure arbitration contract.
/// </summary>
public interface IArbiter
{
    /// <summary>
    /// Canonical C04 arbitration seam: selects an active domain/proposal
    /// from proposal metadata entries and explicit threat signal.
    /// </summary>
    ArbiterDecision Arbitrate(IReadOnlyList<Proposal> proposals, bool threatPresent, float now);

    /// <summary>
    /// Legacy compatibility overload kept during migration from fixed
    /// arbitration input to proposal-list arbitration.
    /// </summary>
    ArbiterDecision Arbitrate(in ArbitrationInput input, float now);

    // NOTE: Do not add runtime-layer types here (collector implementations belong to RuntimeHost).
}
