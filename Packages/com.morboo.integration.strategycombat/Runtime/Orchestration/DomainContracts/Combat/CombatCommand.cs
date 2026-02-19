using System;

/// <summary>
/// Engine-agnostic combat command struct.
/// IMPORTANT: Contains ONLY EntityId, Float2, enums, primitives, string. Zero Unity types.
/// EntityId→Transform resolution happens strictly in Integration/Game.
/// </summary>
[Serializable]
public struct CombatCommand
{
    public CombatCommandType Type;
    public EntityId TargetEntityId;
    public Float2 TargetPoint;
    public float StopDistance;
    public float DesiredRangeMin;
    public float DesiredRangeMax;
    public float Urgency;
    public string DebugLabel;

    public static CombatCommand None => new CombatCommand
    {
        Type = CombatCommandType.None,
        TargetEntityId = EntityId.None,
        TargetPoint = Float2.Zero,
        StopDistance = 0f,
        DesiredRangeMin = -1f,
        DesiredRangeMax = -1f,
        Urgency = 0f,
        DebugLabel = null
    };

    public static CombatCommand Create(
        CombatCommandType type,
        EntityId targetEntityId = default,
        Float2 targetPoint = default,
        float stopDistance = 0f,
        float desiredRangeMin = -1f,
        float desiredRangeMax = -1f,
        float urgency = 0f,
        string debugLabel = null)
    {
        return new CombatCommand
        {
            Type = type,
            TargetEntityId = targetEntityId,
            TargetPoint = targetPoint,
            StopDistance = stopDistance,
            DesiredRangeMin = desiredRangeMin,
            DesiredRangeMax = desiredRangeMax,
            Urgency = urgency,
            DebugLabel = debugLabel
        };
    }

    public bool IsNone => Type == CombatCommandType.None;

    /// <summary>True when an entity target is specified (not None).</summary>
    public bool HasEntityTarget => !TargetEntityId.IsNone;

    public bool HasRange => DesiredRangeMin >= 0f || DesiredRangeMax >= 0f;
}
