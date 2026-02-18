/// <summary>
/// Payload-agnostic arbitration result.
/// </summary>
public struct ArbiterDecision
{
    /// <summary>
    /// Selected domain key. 0 means no active domain.
    /// Domain IDs are defined outside of Framework.
    /// </summary>
    public int DomainKey;

    /// <summary>
    /// Selected proposal key within the active domain.
    /// 0 means no selected proposal.
    /// </summary>
    public int ProposalKey;

    /// <summary>
    /// True when active domain changed since previous tick.
    /// </summary>
    public bool ModeChanged;
}
