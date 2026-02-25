/// <summary>
/// Read-only capability query over a frozen world snapshot.
/// IMPORTANT: Domain-agnostic — no receiver-specific or domain-specific methods.
/// Consumer gets EntityId from IWorldQuery and queries capabilities here.
/// Aggregate capability sets are computed by consumers, not by this interface.
/// </summary>
public interface IActorCapabilityQuery
{
    /// <summary>
    /// Capability snapshot for actor at the given index (parallel to IWorldQueryBase actor list).
    /// Returns default(ActorCapabilitySnapshot) if no provider on the actor.
    /// IMPORTANT: Index in [0, ActorCount). Same indexing as IWorldQueryBase.
    /// </summary>
    ActorCapabilitySnapshot GetActorCapabilities(int index);

    /// <summary>
    /// Capability snapshot by EntityId. O(1) via internal index map.
    /// Returns false if entity is not in the current snapshot.
    /// </summary>
    bool TryGetActorCapabilities(EntityId entityId, out ActorCapabilitySnapshot snapshot);
}
