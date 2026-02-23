/// <summary>
/// Transitional StrategyCombat none-route executor (mode-change hold-all fallback).
/// Ownership is in StrategyCombat (not RuntimeHost) during C04A.
/// </summary>
public sealed class StrategyCombatNoneExecutionRoute
{
    readonly StrategyCombatRouteExecutionProfile _profile;

    public StrategyCombatNoneExecutionRoute(StrategyCombatRouteExecutionPolicyAsset policy = null)
    {
        _profile = new StrategyCombatRouteExecutionProfile(policy);
    }

    public void Execute(IExecutionRouteHost host, ArbiterDecision decision, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        if (host == null)
            return;

        if (decision.ModeChanged)
        {
            if (_profile.NoneRoute.EmitCombatHoldOnModeChange)
                PublishCombatCommandForAll(host, CombatCommand.Create(
                    CombatCommandType.Hold,
                    debugLabel: ResolveDebugLabel(
                        _profile.NoneRoute.CombatHoldDebugLabelOverride,
                        "Router=None")), world);

            if (_profile.NoneRoute.EmitIdleHoldOnModeChange)
                PublishIdleHoldForAll(host, world);
        }
    }

    static string ResolveDebugLabel(string overrideLabel, string fallback)
    {
        return string.IsNullOrEmpty(overrideLabel) ? fallback : overrideLabel;
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
