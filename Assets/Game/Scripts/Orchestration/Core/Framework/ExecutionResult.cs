/// <summary>
/// Return value from <c>ExecutionRouter.Execute</c>.
/// Phase 2B seam — will carry event counts and domain event references
/// for consumption by an EventBus.
/// <para>
/// IMPORTANT — Phase 2B MUST: ExecutionRouter emits <see cref="IDomainEvent"/>s
/// via this struct.
/// </para>
/// </summary>
public struct ExecutionResult
{
    /// <summary>
    /// Number of domain events emitted during execution.
    /// Phase 2A: always 0. Phase 2B: populated by ExecutionRouter.
    /// </summary>
    public int EventCount;
}
