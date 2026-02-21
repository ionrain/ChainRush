/// <summary>
/// Minimal actor runtime boundary used by orchestration-facing systems.
/// IMPORTANT: Engine-agnostic contract — no Unity types.
/// </summary>
public interface IActorRuntimeHandle : IEntityIdProvider
{
    /// <summary>
    /// Reads universal entity lifecycle state from Entity contracts.
    /// </summary>
    EntityLifecycleState GetLifecycleState();
}
