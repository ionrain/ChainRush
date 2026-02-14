/// <summary>
/// Executor contract for the Combat domain.
/// Implementors translate a <see cref="CombatCommand"/> into concrete
/// gameplay actions (e.g., setting an AIBrain target on a Unit).
/// <para>
/// IMPORTANT: The receiver only provides target plumbing.
/// Movement and attack transitions remain the responsibility of the
/// underlying AI state machine (AIBrain / AIDecisions).
/// </para>
/// </summary>
public interface ICombatCommandReceiver
{
    /// <summary>
    /// Applies a single combat command.
    /// Called on demand by the orchestration layer — not per-frame.
    /// </summary>
    void ApplyCombatCommand(CombatCommand command);
}
