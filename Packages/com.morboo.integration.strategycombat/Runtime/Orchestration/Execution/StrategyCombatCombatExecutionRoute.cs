/// <summary>
/// StrategyCombat combat route executor (C06A unified dispatch path).
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

        PublishCommandForAll(host, ctx.Command, world);
    }

    static void PublishCommandForAll(IExecutionRouteHost host, OrchestrationCommand cmd, OrchestrationWorldCache world)
    {
        if (cmd.IsNone)
            return;

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
