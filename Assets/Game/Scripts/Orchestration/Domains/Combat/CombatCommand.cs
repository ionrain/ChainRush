using System;
using UnityEngine;

[Serializable]
public struct CombatCommand
{
    public CombatCommandType Type;
    public Transform TargetTransform;
    public Vector2 TargetPoint;
    public float StopDistance;
    public float DesiredRangeMin;
    public float DesiredRangeMax;
    public float Urgency;
    public string DebugLabel;

    public static CombatCommand None => new CombatCommand
    {
        Type = CombatCommandType.None,
        TargetTransform = null,
        TargetPoint = Vector2.zero,
        StopDistance = 0f,
        DesiredRangeMin = -1f,
        DesiredRangeMax = -1f,
        Urgency = 0f,
        DebugLabel = null
    };

    public static CombatCommand Create(
        CombatCommandType type,
        Transform targetTransform = null,
        Vector2 targetPoint = default,
        float stopDistance = 0f,
        float desiredRangeMin = -1f,
        float desiredRangeMax = -1f,
        float urgency = 0f,
        string debugLabel = null)
    {
        return new CombatCommand
        {
            Type = type,
            TargetTransform = targetTransform,
            TargetPoint = targetPoint,
            StopDistance = stopDistance,
            DesiredRangeMin = desiredRangeMin,
            DesiredRangeMax = desiredRangeMax,
            Urgency = urgency,
            DebugLabel = debugLabel
        };
    }

    public bool IsNone => Type == CombatCommandType.None;
    public bool HasTarget => TargetTransform != null;
    public bool HasRange => DesiredRangeMin >= 0f || DesiredRangeMax >= 0f;
}
