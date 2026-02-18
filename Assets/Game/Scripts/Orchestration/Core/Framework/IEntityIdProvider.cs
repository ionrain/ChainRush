/// <summary>
/// Provides a stable <see cref="EntityId"/> for an orchestration entity.
/// Separate from <see cref="IOrchestrationActor"/> — not every actor-ish thing
/// needs stable identity. Used by execution routing and state reporting.
/// <para>
/// IMPORTANT — The EntityId MUST be stable for the entire lifetime of the
/// underlying object (including across OnEnable/OnDisable for pooled objects).
/// Implementations resolve EntityId from a sibling identity component
/// (<c>UnitOrchestrationIdentity</c> / <c>EnemyOrchestrationIdentity</c>).
/// Missing identity is fatal: warn once + return <see cref="EntityId.None"/>.
/// </para>
/// </summary>
public interface IEntityIdProvider
{
    EntityId GetEntityId();
}
