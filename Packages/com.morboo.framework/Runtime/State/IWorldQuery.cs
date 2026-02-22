/// <summary>
/// Read-only world view composed from snapshot sub-interfaces.
/// Contracts expose only value snapshots and keys.
/// IMPORTANT: Framework type — no UnityEngine dependency.
/// </summary>
public interface IWorldQueryBase
{
    Float2 Anchor { get; }
    float Now { get; }

    int ActorCount { get; }
    EntityId GetActorEntityId(int index);
    Float2 GetActorPosition(int index);
    bool GetActorIsAlive(int index);
    bool GetActorIsHostile(int index);

    /// <summary>
    /// Resolves an actor's position by <see cref="EntityId"/>. Returns false if not found.
    /// IMPORTANT: O(1) lookup via internal index map. Used by targeting policies
    /// to resolve candidate positions from the target set.
    /// </summary>
    bool TryGetActorPosition(EntityId entityId, out Float2 position);
}

/// <summary>
/// Optional 3D world query seam.
/// IMPORTANT: Added as compatibility extension; existing 2D query contracts stay valid.
/// </summary>
public interface IWorldQueryBase3D
{
    Float3 Anchor3D { get; }

    Float3 GetActorPosition3D(int index);

    /// <summary>
    /// Resolves an actor's 3D position by <see cref="EntityId"/>. Returns false if not found.
    /// </summary>
    bool TryGetActorPosition3D(EntityId entityId, out Float3 position);
}

public interface ICrowdQuery
{
    int CrowdCount { get; }
    Float2 GetCrowdPosition(int index);
    EntityId GetCrowdEntityId(int index);
}

/// <summary>
/// Optional 3D crowd query seam.
/// </summary>
public interface ICrowdQuery3D
{
    Float3 GetCrowdPosition3D(int index);
}

public interface IRoleQuery
{
    bool TryGetRoleId(EntityId entityId, out RoleId roleId);
}

public interface IIdleBoundsQuery
{
    bool TryGetIdleBounds(RoleId roleId, out AABB2D bounds);
}

public interface IWorldQuery : IWorldQueryBase, ICrowdQuery, IRoleQuery, IIdleBoundsQuery
{
}

public interface IWorldQuery3D : IWorldQueryBase3D, ICrowdQuery3D
{
}
