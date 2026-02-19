/// <summary>
/// Output of <see cref="OrchestrationArbiter.ProduceTick"/>. Contains the arbiter's
/// decision, the execution context for the router, and the world cache reference.
/// <para>
/// IMPORTANT: Struct — stack-allocated, zero GC. The OrchestrationLoop consumes this
/// to drive <see cref="ExecutionRouter.Execute"/>.
/// </para>
/// </summary>
public struct OrchestrationTickResult
{
    public ArbiterDecision Decision;
    public ExecutionContext ExecContext;
    public OrchestrationWorldCache World;

    /// <summary>True if the arbiter skipped this tick (e.g. missing setup).</summary>
    public bool Skipped;
}
