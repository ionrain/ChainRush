/// <summary>
/// Provides <see cref="RoleId"/>, <see cref="RoleAsset"/>, and a stable <see cref="EntityId"/> for
/// per-role command routing via <see cref="OrchestrationArbiter"/>.
/// <para>
/// IMPORTANT: RuntimeHost code should use <see cref="IRoleIdProvider.GetRoleId"/> (Framework type).
/// <see cref="IRoleAssetProvider.GetRoleAsset"/> is kept for Integration boundary only (inspector binding).
/// <see cref="IEntityIdProvider.GetEntityId"/> must return a value
/// that is stable for the object's entire lifetime (assigned by identity component in Awake).
/// </para>
/// </summary>
public interface IRoleContextProvider : IRoleAssetProvider, IRoleIdProvider, IEntityIdProvider
{
}
