/// <summary>
/// Receiver contract for idle domain commands.
/// Called on demand by the Idle domain (<see cref="IdleDomainComponent"/> via <see cref="DomainOrchestratorComponent"/>) — not per-frame.
/// </summary>
public interface IIdleCommandReceiver
{
    void ApplyIdleCommand(IdleCommand command);
}
