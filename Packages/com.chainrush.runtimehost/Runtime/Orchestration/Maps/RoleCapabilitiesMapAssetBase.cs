using UnityEngine;

/// <summary>
/// Base contract for role-to-capabilities map assets used by integration bindings.
/// Concrete assets can be project-specific and derive from this base.
/// </summary>
public abstract class RoleCapabilitiesMapAssetBase : ScriptableObject
{
    public abstract bool TryGet(RoleAsset role, out CapabilitiesProfile profile);
    public abstract bool TryGet(RoleId roleId, out CapabilitiesProfile profile);
}
