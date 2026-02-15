/// <summary>
/// Provides a typed <see cref="RoleAsset"/> identity for orchestration routing.
/// Can return null for entities that have no role configured.
/// </summary>
public interface IRoleAssetProvider
{
    RoleAsset GetRoleAsset();
}
