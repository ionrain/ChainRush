/// <summary>
/// Domain/integration seam: enumerates target candidates for a seeker/operator.
/// Examples: hostile actors, rest spots, mining nodes, interactable buildings.
/// </summary>
public interface IOrchestrationTargetProvider
{
    /// <summary>
    /// Writes candidate targets into <paramref name="outCandidates"/> and returns count.
    /// Caller owns buffer allocation/reuse.
    /// </summary>
    int FillCandidates(
        IWorldQuery world,
        EntityId seekerEntityId,
        OrchestrationTargetRef[] outCandidates);
}
