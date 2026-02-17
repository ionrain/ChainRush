using UnityEngine;

public static class CombatAdapter
{
    public static bool TryCompileToCommand(in Instruction ins, out CombatCommand command)
    {
        if (ins.Domain != DomainId.Combat && ins.Domain != DomainId.Generic)
        {
            command = CombatCommand.None;
            return false;
        }

        switch (ins.ActionId)
        {
            case "Hold":
                command = CombatCommand.Create(
                    CombatCommandType.Hold,
                    urgency: ins.Priority / 100f,
                    debugLabel: ins.Tag);
                return true;

            case "MoveTo":
            case "Advance":
                command = CombatCommand.Create(
                    CombatCommandType.MoveToPoint,
                    targetPoint: ins.TargetPoint,
                    stopDistance: GetDirectFloat(in ins, "StopDistance", 0f),
                    desiredRangeMin: GetDirectFloat(in ins, "DesiredRangeMin", -1f),
                    desiredRangeMax: GetDirectFloat(in ins, "DesiredRangeMax", -1f),
                    urgency: ins.Priority / 100f,
                    debugLabel: ins.Tag);
                return true;

            case "Attack":
            case "Eliminate":
                command = CombatCommand.Create(
                    CombatCommandType.AttackTarget,
                    targetTransform: ResolveTransform(ins.Target),
                    targetPoint: ins.TargetPoint,
                    stopDistance: GetDirectFloat(in ins, "StopDistance", 0f),
                    desiredRangeMin: GetDirectFloat(in ins, "DesiredRangeMin", -1f),
                    desiredRangeMax: GetDirectFloat(in ins, "DesiredRangeMax", -1f),
                    urgency: ins.Priority / 100f,
                    debugLabel: ins.Tag);
                return true;

            case "Defend":
                command = CombatCommand.Create(
                    CombatCommandType.Hold,
                    urgency: ins.Priority / 100f,
                    debugLabel: ins.Tag);
                return true;

            case "Support":
                command = CombatCommand.Create(
                    CombatCommandType.Assist,
                    targetTransform: ResolveTransform(ins.Target),
                    targetPoint: ins.TargetPoint,
                    stopDistance: GetDirectFloat(in ins, "StopDistance", 0f),
                    desiredRangeMin: GetDirectFloat(in ins, "DesiredRangeMin", -1f),
                    desiredRangeMax: GetDirectFloat(in ins, "DesiredRangeMax", -1f),
                    urgency: ins.Priority / 100f,
                    debugLabel: ins.Tag);
                return true;

            default:
                command = CombatCommand.None;
                return false;
        }
    }

    static float GetDirectFloat(in Instruction ins, string key, float fallback)
    {
        if (ins.Control != ControlLevel.Direct) return fallback;
        var p = ins.Params;
        return p.TryGetFloat(key, out float val) ? val : fallback;
    }

    static Transform ResolveTransform(UnityEngine.Object target)
    {
        if (target == null) return null;
        if (target is Component comp) return comp.transform;
        if (target is GameObject go) return go.transform;
        return null;
    }
}
