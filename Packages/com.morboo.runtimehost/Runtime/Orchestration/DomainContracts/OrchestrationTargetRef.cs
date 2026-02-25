using System;

/// <summary>
/// Universal target kinds for orchestration intent.
/// Entity = any world entity (actor, tree, mine, building, station).
/// Point/Area/Route = target entities representing anchor/zone/route semantics.
/// </summary>
public enum OrchestrationTargetKind
{
    None,
    Entity,
    Point,
    Area,
    Route
}

/// <summary>
/// Engine-agnostic tagged target reference used by <see cref="OrchestrationCommand"/>.
/// IMPORTANT: Reference-only value data (kind + target entity id).
/// Position/anchor/area/route resolution is handled by query/resolver seams keyed by EntityId.
/// </summary>
[Serializable]
public struct OrchestrationTargetRef
{
    public OrchestrationTargetKind Kind;
    public EntityId TargetEntityId;

    public static OrchestrationTargetRef None => default;

    public static OrchestrationTargetRef Entity(EntityId targetEntityId)
    {
        return new OrchestrationTargetRef
        {
            Kind = OrchestrationTargetKind.Entity,
            TargetEntityId = targetEntityId
        };
    }

    public static OrchestrationTargetRef Point(EntityId pointTargetEntityId)
    {
        return new OrchestrationTargetRef
        {
            Kind = OrchestrationTargetKind.Point,
            TargetEntityId = pointTargetEntityId
        };
    }

    public static OrchestrationTargetRef Area(EntityId areaTargetEntityId)
    {
        return new OrchestrationTargetRef
        {
            Kind = OrchestrationTargetKind.Area,
            TargetEntityId = areaTargetEntityId
        };
    }

    public static OrchestrationTargetRef Route(EntityId routeTargetEntityId)
    {
        return new OrchestrationTargetRef
        {
            Kind = OrchestrationTargetKind.Route,
            TargetEntityId = routeTargetEntityId
        };
    }

    public bool IsNone => Kind == OrchestrationTargetKind.None;
}
