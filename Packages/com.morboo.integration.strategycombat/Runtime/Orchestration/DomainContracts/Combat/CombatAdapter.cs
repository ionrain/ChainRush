using UnityEngine;

/// <summary>
/// Compiles <see cref="Instruction"/> into engine-agnostic <see cref="OrchestrationCommand"/>.
/// IMPORTANT: Resolves Instruction.Target (UnityEngine.Object) to EntityId via IEntityIdProvider.
/// Instruction.TargetPoint (Vector2) is converted into a synthetic point target via
/// <see cref="OrchestrationTargetPointRegistry"/> at this boundary.
/// </summary>
public static class CombatAdapter
{
    public static bool TryCompileToCommand(in Instruction ins, out OrchestrationCommand command)
    {
        if (ins.Domain != DomainId.Combat && ins.Domain != DomainId.Generic)
        {
            command = OrchestrationCommand.None;
            return false;
        }

        Float2 point = new Float2(ins.TargetPoint.x, ins.TargetPoint.y);
        EntityId targetEntityId = ResolveEntityId(ins.Target);

        switch (ins.ActionId)
        {
            case "Hold":
            case "Defend":
                command = OrchestrationCommand.Cancel(ins.Tag);
                return true;

            case "MoveTo":
            case "Advance":
                command = OrchestrationCommand.Engage(
                    OrchestrationTargetRef.Point(OrchestrationTargetPointRegistry.RegisterPointTarget(point)),
                    ins.Tag);
                return true;

            case "Attack":
            case "Eliminate":
                command = targetEntityId.IsNone
                    ? OrchestrationCommand.None
                    : OrchestrationCommand.Engage(OrchestrationTargetRef.Entity(targetEntityId), ins.Tag);
                return true;

            case "Support":
                command = targetEntityId.IsNone
                    ? OrchestrationCommand.None
                    : OrchestrationCommand.Engage(OrchestrationTargetRef.Entity(targetEntityId), ins.Tag);
                return true;

            default:
                command = OrchestrationCommand.None;
                return false;
        }
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
