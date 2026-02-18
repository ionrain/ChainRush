/// <summary>
/// Source of proposals built from immutable world snapshots.
/// </summary>
public interface IProposalSource
{
    bool TryBuildProposal(in WorldSnapshot snapshot, out Proposal proposal);
}
