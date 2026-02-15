/// <summary>
/// Receiver contract for idle domain commands.
/// Called on demand by <see cref="IdleOrchestratorLite"/> — not per-frame.
/// </summary>
public interface IIdleCommandReceiver
{
    void ApplyIdleCommand(IdleCommand command);
}
