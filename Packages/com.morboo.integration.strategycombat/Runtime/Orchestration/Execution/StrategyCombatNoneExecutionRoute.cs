/// <summary>
/// StrategyCombat none-route executor (mode-change cancel-all fallback).
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
            if (_profile.NoneRoute.EmitCombatHoldOnModeChange || _profile.NoneRoute.EmitIdleHoldOnModeChange)
                PublishCancelForAll(host, world, ResolveDebugLabel(
                    _profile.NoneRoute.CombatHoldDebugLabelOverride,
                    "Router=None"));
        }
    }

    static string ResolveDebugLabel(string overrideLabel, string fallback)
    {
        return string.IsNullOrEmpty(overrideLabel) ? fallback : overrideLabel;
    }

    static void PublishCancelForAll(IExecutionRouteHost host, OrchestrationWorldCache world, string debugLabel)
    {
        OrchestrationCommand cmd = OrchestrationCommand.Cancel(debugLabel);
        int count = world.OperatorCount;
        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetOperatorEntityId(i);
            if (eid.IsNone)
                continue;

            host.PublishCommand(new DispatchOrchestrationCommand
            {
                ReceiverEntityId = eid,
                Payload = cmd
            });
        }
    }
}
