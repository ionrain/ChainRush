using UnityEngine;

/// <summary>
/// Base contract for resolving EnemyType to ActorCapabilityProfile in project-type integration.
/// Concrete maps can live in project assets and derive from this type.
/// </summary>
public abstract class EnemyActorCapabilitiesMapAssetBase : ScriptableObject
{
    public abstract bool TryGetProfile(EnemyType type, out ActorCapabilityProfile profile);
}
