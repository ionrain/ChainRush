using System;
using UnityEngine;

[Serializable]
public struct Instruction
{
    public DomainId Domain;
    public string ActionId;
    public ControlLevel Control;
    public FactionKey Faction;
    public int Priority;
    public float DeadlineSeconds;
    public UnityEngine.Object Target;
    public Vector2 TargetPoint;
    public ParamSet Params;
    public string Tag;

    public static Instruction Create(
        DomainId domain,
        string actionId,
        FactionKey faction,
        ControlLevel control = ControlLevel.Guided,
        int priority = 50,
        float deadlineSeconds = -1f,
        UnityEngine.Object target = null,
        Vector2 targetPoint = default,
        ParamSet @params = default,
        string tag = null)
    {
        return new Instruction
        {
            Domain = domain,
            ActionId = actionId,
            Control = control,
            Faction = faction,
            Priority = priority,
            DeadlineSeconds = deadlineSeconds,
            Target = target,
            TargetPoint = targetPoint,
            Params = @params,
            Tag = tag
        };
    }

    public bool HasDeadline => DeadlineSeconds > 0f;
    public bool HasTarget => Target != null;
}
