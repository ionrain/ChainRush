/// <summary>
/// Provides a stable <see cref="RoleId"/> for orchestration routing.
/// IMPORTANT: RuntimeHost and Framework contracts use RoleId, never RoleAsset.
/// RoleAsset → RoleId conversion happens at Integration boundary via
/// <see cref="IRoleAssetProvider.GetRoleAsset()"/>.RoleId.
/// </summary>
public interface IRoleIdProvider
{
    RoleId GetRoleId();
}
