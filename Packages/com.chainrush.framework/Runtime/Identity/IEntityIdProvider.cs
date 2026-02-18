/// <summary>
/// Provides a stable <see cref="EntityId"/> for an orchestration entity.
/// </summary>
public interface IEntityIdProvider
{
    EntityId GetEntityId();
}
