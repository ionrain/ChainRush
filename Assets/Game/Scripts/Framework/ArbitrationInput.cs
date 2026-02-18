/// <summary>
/// Minimal arbitration input model shared across orchestration implementations.
/// </summary>
public readonly struct ArbitrationInput
{
    public readonly bool HasPrimaryProposal;
    public readonly bool HasSecondaryProposal;
    public readonly bool ThreatPresent;

    public ArbitrationInput(bool hasPrimaryProposal, bool hasSecondaryProposal, bool threatPresent)
    {
        HasPrimaryProposal = hasPrimaryProposal;
        HasSecondaryProposal = hasSecondaryProposal;
        ThreatPresent = threatPresent;
    }
}
