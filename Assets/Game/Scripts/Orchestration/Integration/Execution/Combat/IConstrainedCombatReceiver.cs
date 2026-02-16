/// <summary>
/// Optional extension for <see cref="ICombatCommandReceiver"/> that accepts runtime
/// movement constraints and world context from the arbiter.
/// <para>
/// IMPORTANT: Called each tick BEFORE <see cref="ICombatCommandReceiver.ApplyCombatCommand"/>
/// if the arbiter has a bound constraints map for the receiver's role. Also called with
/// null constraints when no map/role match, to ensure the executor transitions cleanly
/// to unconstrained mode.
/// </para>
/// <para>
/// IMPORTANT: Constraints and world are tick-scoped. Executor must not cache references
/// across ticks beyond what the arbiter refreshes.
/// </para>
/// </summary>
public interface IConstrainedCombatReceiver
{
    /// <summary>
    /// Injects runtime constraints and world cache for this tick.
    /// Called by arbiter before <c>ApplyCombatCommand</c>.
    /// Null constraints = unconstrained movement (executor delegates to original path).
    /// </summary>
    void SetRuntimeContext(CombatMoveConstraintsAsset constraints, OrchestrationWorldCache world);
}
