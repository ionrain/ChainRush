/// <summary>
/// Domain-agnostic target scoring seam. Higher score = better target.
/// </summary>
public interface IOrchestrationTargetScorer
{
    bool TryScore(
        IWorldQuery world,
        EntityId seekerEntityId,
        in OrchestrationTargetRef candidate,
        out float score);
}
