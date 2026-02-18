/// <summary>
/// Engine-agnostic idle command struct.
/// IMPORTANT: Contains ONLY Float2, enums, primitives, string. Zero Unity types.
/// </summary>
/// <remarks>
/// IMPORTANT: <see cref="StopDistance"/> is data-only in Step R1. The executor does NOT enforce
/// distance checks — AIBrain handles approach via its own state machine. Future steps may add
/// per-frame proximity checks in a dedicated component, not the executor.
/// </remarks>
[System.Serializable]
public struct IdleCommand
{
    public IdleCommandType Type;
    public Float2 TargetPoint;
    /// <summary>Data-only in Step R1. Not enforced by executor. See remarks.</summary>
    public float StopDistance;
    public string DebugLabel;

    public static IdleCommand None => new IdleCommand { Type = IdleCommandType.None, StopDistance = 0f };

    public static IdleCommand Hold(string debugLabel = null)
    {
        return new IdleCommand
        {
            Type = IdleCommandType.Hold,
            StopDistance = 0f,
            DebugLabel = debugLabel
        };
    }

    public static IdleCommand MoveToPoint(Float2 point, float stopDistance = 0.2f, string debugLabel = null)
    {
        return new IdleCommand
        {
            Type = IdleCommandType.MoveToPoint,
            TargetPoint = point,
            StopDistance = stopDistance,
            DebugLabel = debugLabel
        };
    }
}
