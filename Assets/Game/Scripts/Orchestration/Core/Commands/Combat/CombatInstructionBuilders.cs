using UnityEngine;

public static class CombatInstructionBuilders
{
    public static string ToActionString(CombatActionId action)
    {
        switch (action)
        {
            case CombatActionId.Hold:      return "Hold";
            case CombatActionId.MoveTo:    return "MoveTo";
            case CombatActionId.Eliminate: return "Eliminate";
            case CombatActionId.Attack:    return "Attack";
            case CombatActionId.Defend:    return "Defend";
            case CombatActionId.Support:   return "Support";
            default:                       return "Hold";
        }
    }

    public static bool TryParseAction(string actionId, out CombatActionId action)
    {
        switch (actionId)
        {
            case "Hold":      action = CombatActionId.Hold;      return true;
            case "MoveTo":    action = CombatActionId.MoveTo;    return true;
            case "Advance":   action = CombatActionId.MoveTo;    return true;
            case "Eliminate": action = CombatActionId.Eliminate; return true;
            case "Attack":    action = CombatActionId.Attack;    return true;
            case "Defend":    action = CombatActionId.Defend;    return true;
            case "Support":   action = CombatActionId.Support;   return true;
            default:          action = default;                   return false;
        }
    }

    public static Instruction Create(
        CombatActionId action,
        FactionAsset faction,
        ControlLevel control = ControlLevel.Guided,
        int priority = 50,
        float deadlineSeconds = -1f,
        UnityEngine.Object target = null,
        Vector2 targetPoint = default,
        ParamSet @params = default,
        string tag = null)
    {
        return Instruction.Create(
            DomainId.Combat,
            ToActionString(action),
            faction,
            control,
            priority,
            deadlineSeconds,
            target,
            targetPoint,
            @params,
            tag);
    }

    public static Instruction Hold(FactionAsset faction, int priority = 50, string tag = null)
    {
        return Instruction.Create(
            DomainId.Combat,
            "Hold",
            faction,
            ControlLevel.IntentOnly,
            priority,
            tag: tag);
    }

    public static Instruction MoveToPoint(
        FactionAsset faction,
        Vector2 point,
        int priority = 50,
        float deadlineSeconds = -1f,
        string tag = null)
    {
        return Instruction.Create(
            DomainId.Combat,
            "MoveTo",
            faction,
            ControlLevel.Guided,
            priority,
            deadlineSeconds,
            targetPoint: point,
            tag: tag);
    }

    public static Instruction Eliminate(
        FactionAsset faction,
        UnityEngine.Object target,
        int priority = 70,
        float deadlineSeconds = -1f,
        string tag = null)
    {
        return Instruction.Create(
            DomainId.Combat,
            "Eliminate",
            faction,
            ControlLevel.Guided,
            priority,
            deadlineSeconds,
            target: target,
            tag: tag);
    }

    public static Instruction Attack(
        FactionAsset faction,
        UnityEngine.Object target,
        int priority = 60,
        float deadlineSeconds = -1f,
        string tag = null)
    {
        return Instruction.Create(
            DomainId.Combat,
            "Attack",
            faction,
            ControlLevel.Guided,
            priority,
            deadlineSeconds,
            target: target,
            tag: tag);
    }
}
