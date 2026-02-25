/// <summary>
/// Unified receiver contract for orchestration commands.
/// Actors and groups may both implement this interface.
/// </summary>
public interface IOrchestrationCommandReceiver
{
    /// <summary>
    /// Applies a single orchestration command.
    /// Called on demand by the orchestration layer — not per-frame.
    /// </summary>
    void ApplyCommand(OrchestrationCommand command);
}
