/// <summary>
/// Transitional StrategyCombat combat route executor.
/// Ownership is in StrategyCombat (not RuntimeHost) during C04A.
/// </summary>
public sealed class StrategyCombatCombatExecutionRoute
{
    readonly StrategyCombatRouteExecutionProfile _profile;

    public StrategyCombatCombatExecutionRoute(StrategyCombatRouteExecutionPolicyAsset policy = null)
    {
        _profile = new StrategyCombatRouteExecutionProfile(policy);
    }

    public void Execute(IExecutionRouteHost host, ArbiterDecision decision, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        if (host == null)
            return;

        PublishCombatCommandForAll(host, ctx.CombatCommand, world);
        if (decision.ModeChanged && _profile.Combat.EmitIdleHoldOnModeChange)
            PublishIdleHoldForAll(host, world);
    }

    void PublishCombatCommandForAll(IExecutionRouteHost host, CombatCommand cmd, OrchestrationWorldCache world)
    {
        int count = world.CombatReceiverCount;
        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetCombatReceiverEntityId(i);
            if (eid.IsNone)
                continue;

            host.PublishCommand(new DispatchCombatCommand
            {
                ReceiverEntityId = eid,
                Payload = cmd,
                ReceiverRoleId = world.GetCombatReceiverRoleId(i)
            });
        }
    }

    void PublishIdleHoldForAll(IExecutionRouteHost host, OrchestrationWorldCache world)
    {
        IdleCommand hold = IdleCommand.Hold();
        int count = world.IdleReceiverCount;
        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetIdleReceiverEntityId(i);
            if (eid.IsNone)
                continue;

            host.PublishCommand(new DispatchIdleCommand
            {
                ReceiverEntityId = eid,
                Payload = hold,
                ReceiverRoleId = world.GetIdleReceiverRoleId(i)
            });
        }
    }
}
