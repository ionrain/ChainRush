/// <summary>
/// Read-only world view composed from snapshot sub-interfaces.
/// Contracts expose only value snapshots and keys.
/// IMPORTANT: Framework type — no UnityEngine dependency.
/// </summary>
public interface IWorldQueryBase
{
    Float3 Anchor { get; }
    float Now { get; }

    int ActorCount { get; }
    EntityId GetActorEntityId(int index);
    Float3 GetActorPosition(int index);
    bool GetActorIsAlive(int index);
    bool GetActorIsHostile(int index);

    /// <summary>
    /// Resolves an actor's position by <see cref="EntityId"/>. Returns false if not found.
    /// IMPORTANT: O(1) lookup via internal index map. Used by targeting policies
    /// to resolve candidate positions from the target set.
    /// </summary>
    bool TryGetActorPosition(EntityId entityId, out Float3 position);
}

public interface ICrowdQuery
{
    int CrowdCount { get; }
    Float3 GetCrowdPosition(int index);
    EntityId GetCrowdEntityId(int index);
}

public interface IRoleQuery
{
    bool TryGetRoleId(EntityId entityId, out RoleId roleId);
}

public interface IIdleBoundsQuery
{
    bool TryGetIdleBounds(RoleId roleId, out AABB3D bounds);
}

public interface IWorldQuery : IWorldQueryBase, ICrowdQuery, IRoleQuery, IIdleBoundsQuery
{
}
