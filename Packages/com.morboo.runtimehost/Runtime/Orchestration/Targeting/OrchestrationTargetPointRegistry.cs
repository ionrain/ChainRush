using System.Collections.Generic;

/// <summary>
/// Runtime registry for point targets addressed via <see cref="OrchestrationTargetRef"/> (`Kind=Point`).
/// Stores point coordinates under synthetic EntityIds derived from operator EntityId.
/// <para>
/// RATIONALE: C06A command payload is reference-only (`Kind + EntityId`), while existing idle policies
/// still produce raw points. This registry provides the resolver seam without adding inline point data
/// back into <see cref="OrchestrationCommand"/>.
/// </para>
/// </summary>
public static class OrchestrationTargetPointRegistry
{
    // Synthetic point-target namespace bit. Assumes gameplay entity ids are non-negative.
    const int PointNamespaceMask = unchecked((int)0x80000000);

    static readonly Dictionary<EntityId, Float3> _pointsByTargetId = new Dictionary<EntityId, Float3>(256);

    public static EntityId SetOperatorPointTarget(EntityId operatorEntityId, in Float3 point)
    {
        EntityId targetId = GetPointTargetEntityId(operatorEntityId);
        if (targetId.IsNone)
            return EntityId.None;

        _pointsByTargetId[targetId] = point;
        return targetId;
    }

    public static EntityId GetPointTargetEntityId(EntityId operatorEntityId)
    {
        if (operatorEntityId.IsNone)
            return EntityId.None;

        int raw = operatorEntityId.ToStableInt();
        if (raw <= 0)
            return EntityId.None;

        return new EntityId(raw | PointNamespaceMask);
    }

    public static bool TryGetPointTarget(EntityId targetEntityId, out Float3 point)
    {
        return _pointsByTargetId.TryGetValue(targetEntityId, out point);
    }

    public static void RemoveOperatorPointTarget(EntityId operatorEntityId)
    {
        EntityId targetId = GetPointTargetEntityId(operatorEntityId);
        if (!targetId.IsNone)
            _pointsByTargetId.Remove(targetId);
    }
}
