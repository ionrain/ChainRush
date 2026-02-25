using System;

/// <summary>
/// Engine-agnostic orchestration command struct.
/// IMPORTANT: Contains ONLY engine-agnostic value types (TargetRef, enums, primitives, string).
/// Zero Unity types and no behavior/policy objects.
/// EntityId→Transform resolution happens strictly in Integration adapters.
/// </summary>
[Serializable]
public struct OrchestrationCommand
{
    public OrchestrationCommandType Type;
    public OrchestrationTargetRef Target;
    public string DebugLabel;

    public static OrchestrationCommand None => new OrchestrationCommand
    {
        Type = OrchestrationCommandType.None,
        Target = OrchestrationTargetRef.None,
        DebugLabel = null
    };

    public static OrchestrationCommand Engage(OrchestrationTargetRef target, string debugLabel = null)
    {
        return new OrchestrationCommand
        {
            Type = OrchestrationCommandType.Engage,
            Target = target,
            DebugLabel = debugLabel
        };
    }

    public static OrchestrationCommand Cancel(string debugLabel = null)
    {
        return new OrchestrationCommand
        {
            Type = OrchestrationCommandType.Cancel,
            Target = OrchestrationTargetRef.None,
            DebugLabel = debugLabel
        };
    }

    public bool IsNone => Type == OrchestrationCommandType.None;
}
