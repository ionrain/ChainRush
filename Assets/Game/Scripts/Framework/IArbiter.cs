/// <summary>
/// Pure arbitration contract. Receives proposal availability input and returns a decision.
/// </summary>
public interface IArbiter
{
    /// <summary>
    /// Selects an active domain and proposal in a side-effect-free manner.
    /// </summary>
    ArbiterDecision Arbitrate(in ArbitrationInput input, float now);
}
