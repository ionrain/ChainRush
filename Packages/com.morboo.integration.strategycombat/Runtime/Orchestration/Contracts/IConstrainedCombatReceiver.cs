/// <summary>
/// Optional extension for constrained combat-capable orchestration receivers that accepts runtime
/// movement constraints and world context from the Integration adapter.
/// <para>
/// IMPORTANT: Called each tick BEFORE <see cref="IOrchestrationCommandReceiver.ApplyCommand"/>
/// by <see cref="OrchestrationCommandAdapter"/> if the constraints map has an entry for the
/// receiver's role. Also called with null constraints when no map/role match, to ensure
/// the executor transitions cleanly to unconstrained mode.
/// </para>
/// <para>
/// IMPORTANT: Constraints and world are tick-scoped. Executor must not cache references
/// across ticks beyond what the adapter refreshes.
/// </para>
/// </summary>
public interface IConstrainedCombatReceiver
{
    /// <summary>
    /// Injects runtime constraints and world cache for this tick.
    /// Called by <see cref="OrchestrationCommandAdapter"/> before <c>ApplyCommand</c>.
    /// Null constraints = unconstrained movement (executor delegates to original path).
    /// </summary>
    void SetRuntimeContext(CombatMoveConstraintsAsset constraints, IWorldQuery world);
}
