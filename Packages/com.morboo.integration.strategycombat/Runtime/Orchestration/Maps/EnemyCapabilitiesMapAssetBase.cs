using UnityEngine;

/// <summary>
/// Base contract for resolving EnemyType to CapabilitiesProfile in project-type integration.
/// Concrete maps can live in project assets and derive from this type.
/// </summary>
public abstract class EnemyCapabilitiesMapAssetBase : ScriptableObject
{
    public abstract bool TryGetProfile(EnemyType type, out CapabilitiesProfile profile);
}
