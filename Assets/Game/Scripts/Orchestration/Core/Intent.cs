using System;
using UnityEngine;

[Serializable]
public struct Intent
{
    public DomainId Domain;
    public string GoalId;
    public FactionKey Faction;
    public int Priority;
    public float DeadlineSeconds;
    public UnityEngine.Object Target;
    public Vector2 TargetPoint;
    public string Tag;

    public static Intent Create(
        DomainId domain,
        string goalId,
        FactionKey faction,
        int priority = 50,
        float deadlineSeconds = -1f,
        UnityEngine.Object target = null,
        Vector2 targetPoint = default,
        string tag = null)
    {
        return new Intent
        {
            Domain = domain,
            GoalId = goalId,
            Faction = faction,
            Priority = priority,
            DeadlineSeconds = deadlineSeconds,
            Target = target,
            TargetPoint = targetPoint,
            Tag = tag
        };
    }

    public bool HasDeadline => DeadlineSeconds > 0f;
    public bool HasTarget => Target != null;
}
