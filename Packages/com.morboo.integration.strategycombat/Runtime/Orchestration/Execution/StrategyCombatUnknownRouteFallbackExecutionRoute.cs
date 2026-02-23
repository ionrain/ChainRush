using System.Collections.Generic;

/// <summary>
/// Transitional StrategyCombat-owned unknown-route fallback executor.
/// Preserves legacy defensive behavior while removing domain-shaped fallback from RuntimeHost router.
/// </summary>
public sealed class StrategyCombatUnknownRouteFallbackExecutionRoute
{
    public static readonly StrategyCombatUnknownRouteFallbackExecutionRoute Shared = new StrategyCombatUnknownRouteFallbackExecutionRoute();
    static readonly Dictionary<StrategyCombatRouteExecutionPolicyAsset, StrategyCombatUnknownRouteFallbackExecutionRoute> SharedByPolicy
        = new Dictionary<StrategyCombatRouteExecutionPolicyAsset, StrategyCombatUnknownRouteFallbackExecutionRoute>();

    readonly StrategyCombatRouteExecutionProfile _profile;

    public StrategyCombatUnknownRouteFallbackExecutionRoute(StrategyCombatRouteExecutionPolicyAsset policy = null)
    {
        _profile = new StrategyCombatRouteExecutionProfile(policy);
    }

    public static StrategyCombatUnknownRouteFallbackExecutionRoute GetShared(StrategyCombatRouteExecutionPolicyAsset policy)
    {
        if (policy == null)
            return Shared;

        if (SharedByPolicy.TryGetValue(policy, out StrategyCombatUnknownRouteFallbackExecutionRoute cached))
            return cached;

        cached = new StrategyCombatUnknownRouteFallbackExecutionRoute(policy);
        SharedByPolicy[policy] = cached;
        return cached;
    }

    public void Execute(IExecutionRouteHost host, ArbiterDecision decision, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        if (host == null || !decision.ModeChanged)
            return;

        if (_profile.UnknownRouteFallback.EmitCombatHoldOnModeChange)
            PublishCombatCommandForAll(host, CombatCommand.Create(
                CombatCommandType.Hold,
                debugLabel: ResolveDebugLabel(
                    _profile.UnknownRouteFallback.CombatHoldDebugLabelOverride,
                    "Router=UnknownRoute")), world);

        if (_profile.UnknownRouteFallback.EmitIdleHoldOnModeChange)
            PublishIdleHoldForAll(host, world);
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
