using UnityEngine;

/// <summary>
/// Domain-owned Idle target/self-position provider (StrategyCombat).
/// Encapsulates how Idle route execution resolves actor position for policy evaluation.
/// </summary>
public sealed class IdleTargetProvider : DomainTargetProviderBase
{
    [Tooltip("When true, prefer actor read projection from world snapshot; otherwise always fall back to anchor.")]
    [SerializeField] bool useActorReadProjection = true;

    public override OrchestrationDomainId DomainId => OrchestrationDomainId.Idle;

    public Float2 ResolveSelfPosition(EntityId entityId, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        return ResolveSelfPositionInternal(entityId, world, ctx, useActorReadProjection);
    }

    static Float2 ResolveSelfPositionInternal(EntityId entityId, OrchestrationWorldCache world, ExecutionContext ctx, bool useActorReadProjection)
    {
        if (useActorReadProjection && world != null)
        {
            ActorReadProjection projection;
            if (world.TryGetActorReadProjection(entityId, out projection))
                return projection.Position;
        }

        return ctx.Anchor;
    }
}
