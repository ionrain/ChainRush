using System;

/// <summary>
/// Stable per-actor read projection consumed by orchestration runtime paths.
/// IMPORTANT: Core contract — no Unity types.
/// </summary>
[Serializable]
public readonly struct ActorReadProjection
{
    public ActorReadProjection(EntityId entityId, Float2 position, EntityLifecycleState lifecycleState, RoleId roleId)
        : this(entityId, new Float3(position.X, position.Y, 0f), lifecycleState, roleId)
    {
    }

    public ActorReadProjection(EntityId entityId, Float3 position, EntityLifecycleState lifecycleState, RoleId roleId)
    {
        EntityId = entityId;
        WorldPosition = position;
        Position = new Float2(position.X, position.Y);
        LifecycleState = lifecycleState;
        RoleId = roleId;
    }

    public EntityId EntityId { get; }
    public Float2 Position { get; }
    public Float3 WorldPosition { get; }
    public EntityLifecycleState LifecycleState { get; }
    public RoleId RoleId { get; }
}

/// <summary>
/// Typed query seam for actor projections by <see cref="EntityId"/>.
/// </summary>
public interface IActorReadProjectionQuery
{
    bool TryGetActorReadProjection(EntityId entityId, out ActorReadProjection projection);
}
