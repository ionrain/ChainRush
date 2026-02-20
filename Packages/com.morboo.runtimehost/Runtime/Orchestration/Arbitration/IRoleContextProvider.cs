/// <summary>
/// Provides <see cref="RoleId"/> and a stable <see cref="EntityId"/> for
/// per-role command routing in the orchestration arbiter pipeline.
/// <para>
/// IMPORTANT: RuntimeHost code should use <see cref="IRoleIdProvider.GetRoleId"/> (Framework type).
/// <see cref="IEntityIdProvider.GetEntityId"/> must return a value
/// that is stable for the object's entire lifetime (assigned by identity component in Awake).
/// </para>
/// </summary>
public interface IRoleContextProvider : IRoleIdProvider, IEntityIdProvider
{
}
