using UnityEngine;

/// <summary>
/// Read-only world view composed from snapshot sub-interfaces.
/// Contracts expose only value snapshots and keys.
/// </summary>
public interface IWorldQueryBase
{
    Vector2 Anchor { get; }
    float Now { get; }

    int ActorCount { get; }
    EntityId GetActorEntityId(int index);
    Vector2 GetActorPosition(int index);
    bool GetActorIsAlive(int index);
    bool GetActorIsHostile(int index);
}

public interface ICrowdQuery
{
    int CrowdCount { get; }
    Vector2 GetCrowdPosition(int index);
    EntityId GetCrowdEntityId(int index);
}

public interface IRoleQuery
{
    bool TryGetRoleKey(EntityId entityId, out int roleKey);
}

public interface IIdleBoundsQuery
{
    bool TryGetIdleBounds(int roleKey, out Bounds bounds);
}

public interface IWorldQuery : IWorldQueryBase, ICrowdQuery, IRoleQuery, IIdleBoundsQuery
{
}
