/// <summary>
/// Provides typed <see cref="RoleAsset"/> and a stable per-entity seed for
/// per-role idle dispatch. Implemented by executors that participate in
/// role-based command routing via <see cref="OrchestrationArbiter"/>.
/// <para>
/// IMPORTANT: <see cref="IRoleAssetProvider.GetRoleAsset"/> may return null
/// for unconfigured entities. <see cref="GetEntitySeed"/> must return a value
/// that is stable for the object's entire lifetime (e.g. <c>GetInstanceID()</c>).
/// </para>
/// </summary>
public interface IRoleContextProvider : IRoleAssetProvider
{
    /// <summary>
    /// Stable per-entity seed for deterministic but unique command computation.
    /// Must not change during the object's lifetime.
    /// Typical implementation: <c>return GetInstanceID();</c>
    /// </summary>
    int GetEntitySeed();
}
