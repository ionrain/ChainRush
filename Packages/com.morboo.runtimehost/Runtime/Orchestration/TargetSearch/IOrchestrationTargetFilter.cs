/// <summary>
/// Optional candidate filter for orchestration target search.
/// </summary>
public interface IOrchestrationTargetFilter
{
    bool Accept(
        IWorldQuery world,
        EntityId seekerEntityId,
        in OrchestrationTargetRef candidate);
}
