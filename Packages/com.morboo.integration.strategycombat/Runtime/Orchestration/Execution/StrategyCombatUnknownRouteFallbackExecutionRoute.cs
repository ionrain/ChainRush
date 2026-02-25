using System.Collections.Generic;

/// <summary>
/// StrategyCombat-owned unknown-route fallback executor (unified cancel fallback).
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

        if (_profile.UnknownRouteFallback.EmitCombatHoldOnModeChange || _profile.UnknownRouteFallback.EmitIdleHoldOnModeChange)
            PublishCancelForAll(host, world, ResolveDebugLabel(
                _profile.UnknownRouteFallback.CombatHoldDebugLabelOverride,
                "Router=UnknownRoute"));
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
