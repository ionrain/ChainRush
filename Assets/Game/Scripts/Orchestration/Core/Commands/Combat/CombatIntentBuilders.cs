using UnityEngine;

public static class CombatIntentBuilders
{
    public static string ToGoalString(CombatGoalId goal)
    {
        switch (goal)
        {
            case CombatGoalId.Hold:      return "Hold";
            case CombatGoalId.Advance:   return "Advance";
            case CombatGoalId.Defend:    return "Defend";
            case CombatGoalId.Eliminate: return "Eliminate";
            case CombatGoalId.Support:   return "Support";
            case CombatGoalId.Retreat:   return "Retreat";
            default:                     return "Hold";
        }
    }

    public static bool TryParseGoal(string goalId, out CombatGoalId goal)
    {
        switch (goalId)
        {
            case "Hold":      goal = CombatGoalId.Hold;      return true;
            case "Advance":   goal = CombatGoalId.Advance;   return true;
            case "Defend":    goal = CombatGoalId.Defend;     return true;
            case "Eliminate": goal = CombatGoalId.Eliminate;  return true;
            case "Support":   goal = CombatGoalId.Support;    return true;
            case "Retreat":   goal = CombatGoalId.Retreat;    return true;
            default:          goal = default;                  return false;
        }
    }

    public static Intent Create(
        CombatGoalId goal,
        FactionAsset faction,
        int priority = 50,
        float deadlineSeconds = -1f,
        UnityEngine.Object target = null,
        Vector2 targetPoint = default,
        string tag = null)
    {
        return Intent.Create(
            DomainId.Combat,
            ToGoalString(goal),
            faction,
            priority,
            deadlineSeconds,
            target,
            targetPoint,
            tag);
    }

    public static Intent Hold(FactionAsset faction, int priority = 50, string tag = null)
    {
        return Create(CombatGoalId.Hold, faction, priority, tag: tag);
    }

    public static Intent AdvanceTo(FactionAsset faction, Vector2 point, int priority = 50, float deadlineSeconds = -1f, string tag = null)
    {
        return Create(CombatGoalId.Advance, faction, priority, deadlineSeconds, targetPoint: point, tag: tag);
    }

    public static Intent Defend(FactionAsset faction, Vector2 point, int priority = 60, float deadlineSeconds = -1f, string tag = null)
    {
        return Create(CombatGoalId.Defend, faction, priority, deadlineSeconds, targetPoint: point, tag: tag);
    }

    public static Intent Eliminate(FactionAsset faction, UnityEngine.Object target, int priority = 70, float deadlineSeconds = -1f, string tag = null)
    {
        return Create(CombatGoalId.Eliminate, faction, priority, deadlineSeconds, target: target, tag: tag);
    }

    public static Intent Support(FactionAsset faction, UnityEngine.Object target, int priority = 50, string tag = null)
    {
        return Create(CombatGoalId.Support, faction, priority, target: target, tag: tag);
    }

    public static Intent Retreat(FactionAsset faction, Vector2 point, int priority = 80, string tag = null)
    {
        return Create(CombatGoalId.Retreat, faction, priority, targetPoint: point, tag: tag);
    }
}
