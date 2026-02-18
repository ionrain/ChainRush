/// <summary>
/// Provides typed <see cref="RoleAsset"/> and a stable <see cref="EntityId"/> for
/// per-role idle dispatch. Implemented by executors that participate in
/// role-based command routing via <see cref="OrchestrationArbiter"/>.
/// <para>
/// IMPORTANT: <see cref="IRoleAssetProvider.GetRoleAsset"/> may return null
/// for unconfigured entities. <see cref="IEntityIdProvider.GetEntityId"/> must return a value
/// that is stable for the object's entire lifetime (assigned by identity component in Awake).
/// </para>
/// </summary>
public interface IRoleContextProvider : IRoleAssetProvider, IEntityIdProvider
{
}
