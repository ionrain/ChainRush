using UnityEngine;

/// <summary>
/// Compiles <see cref="Instruction"/> into engine-agnostic <see cref="CombatCommand"/>.
/// IMPORTANT: Resolves Instruction.Target (UnityEngine.Object) to EntityId via IEntityIdProvider.
/// Instruction.TargetPoint (Vector2) is converted to Float2 at this boundary.
/// </summary>
public static class CombatAdapter
{
    public static bool TryCompileToCommand(in Instruction ins, out CombatCommand command)
    {
        if (ins.Domain != DomainId.Combat && ins.Domain != DomainId.Generic)
        {
            command = CombatCommand.None;
            return false;
        }

        Float2 tp = new Float2(ins.TargetPoint.x, ins.TargetPoint.y);

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
                    targetPoint: tp,
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
                    targetEntityId: ResolveEntityId(ins.Target),
                    targetPoint: tp,
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
                    targetEntityId: ResolveEntityId(ins.Target),
                    targetPoint: tp,
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

    /// <summary>
    /// Resolves an Instruction target (UnityEngine.Object) to EntityId via IEntityIdProvider.
    /// Returns EntityId.None if target is null or does not implement IEntityIdProvider.
    /// </summary>
    static EntityId ResolveEntityId(UnityEngine.Object target)
    {
        if (target == null) return EntityId.None;
        if (target is IEntityIdProvider idp) return idp.GetEntityId();
        if (target is Component comp)
        {
            IEntityIdProvider provider = comp.GetComponent<IEntityIdProvider>();
            if (provider != null) return provider.GetEntityId();
        }
        if (target is GameObject go)
        {
            IEntityIdProvider provider = go.GetComponent<IEntityIdProvider>();
            if (provider != null) return provider.GetEntityId();
        }
        return EntityId.None;
    }
}
