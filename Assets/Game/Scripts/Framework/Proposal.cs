/// <summary>
/// Payload-agnostic proposal metadata.
/// </summary>
public readonly struct Proposal
{
    public readonly int DomainKey;
    public readonly int ProposalKey;
    public readonly int Priority;
    public readonly float Score;

    public Proposal(int domainKey, int proposalKey, int priority, float score)
    {
        DomainKey = domainKey;
        ProposalKey = proposalKey;
        Priority = priority;
        Score = score;
    }
}
