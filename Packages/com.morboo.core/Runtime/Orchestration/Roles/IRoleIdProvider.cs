/// <summary>
/// Provides a stable <see cref="RoleId"/> for orchestration routing.
/// IMPORTANT: RuntimeHost and Framework contracts use RoleId only.
/// Any asset-based identity mapping is an Integration-layer concern.
/// </summary>
public interface IRoleIdProvider
{
    RoleId GetRoleId();
}
