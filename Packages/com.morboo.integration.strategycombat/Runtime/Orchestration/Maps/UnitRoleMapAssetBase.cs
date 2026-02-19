using UnityEngine;

/// <summary>
/// Base contract for resolving UnitClass to RoleAsset in project-type integration.
/// Concrete maps can live in project assets and derive from this type.
/// </summary>
public abstract class UnitRoleMapAssetBase : ScriptableObject
{
    public abstract bool TryGet(UnitClass unitClass, out RoleAsset role);
}
