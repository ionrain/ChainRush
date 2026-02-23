using UnityEngine;

/// <summary>
/// Command emitter for orchestration decisions.
/// Consumes <see cref="ArbiterDecision"/> and emits <see cref="DispatchCombatCommand"/>
/// / <see cref="DispatchIdleCommand"/> into the <see cref="InProcessCommandBus"/>.
/// <para>
/// IMPORTANT — The router does NOT call ApplyCombatCommand / ApplyIdleCommand.
/// Integration adapters (<see cref="CombatCommandAdapter"/>, <see cref="IdleCommandAdapter"/>)
/// subscribe to the bus, resolve EntityId → receiver, inject policies, and call Apply.
/// </para>
/// <para>
/// IMPORTANT — Iterates receiver identity snapshots from <see cref="OrchestrationWorldCache"/>
/// (EntityId + RoleId), not MonoBehaviour receiver lists. Zero direct receiver access.
/// </para>
/// </summary>
public sealed class ExecutionRouter : IExecutionRouteHost
{
    readonly InProcessCommandBus _bus;
    ExecutionRouteRegistration[] _routes;
    int _routeCount;
    DomainExecutionRouteExecutor _unknownRouteFallback;

    // ──────────────────────────────────────────────────────────────────
    //  One-shot warning flags
    // ──────────────────────────────────────────────────────────────────

    bool _warnedDuplicateRouteKeys;
    bool _warnedDuplicateUnknownRouteFallback;

    // ──────────────────────────────────────────────────────────────────
    //  Constructor
    // ──────────────────────────────────────────────────────────────────

    public ExecutionRouter(InProcessCommandBus bus)
    {
        _bus = bus;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Emits dispatch commands into the bus based on the arbiter's decision.
    /// IMPORTANT: Does NOT call Apply on receivers. Bus subscribers handle that.
    /// </summary>
    public ExecutionResult Execute(
        ArbiterDecision decision,
        OrchestrationWorldCache world,
        ExecutionContext ctx)
    {
        TryExecuteRegisteredRoute(decision, world, ctx);

        if (ctx.DebugLog)
        {
            // Golden test debug output
            string firstRxIds = BuildFirstReceiverIds(world, 3);
            string firstCrowdIds = BuildFirstCrowdIds(world, 3);
            Debug.Log(string.Concat(
                "[Router] domain=", decision.DomainKey.ToString(),
                " combatRx=", world.CombatReceiverCount.ToString(),
                " idleRx=", world.IdleReceiverCount.ToString(),
                " firstRxIds=", firstRxIds,
                " firstCrowdIds=", firstCrowdIds));
        }

        return new ExecutionResult { EventCount = 0 };
    }

    /// <summary>
    /// C04A route registration seam. Later domain modules can supply route behavior
    /// without editing the router entrypoint. Current bootstrap registers built-ins.
    /// Last registration for the same DomainKey wins (warn once).
    /// </summary>
    public void RegisterRoute(in ExecutionRouteRegistration registration)
    {
        if (_routes == null)
            _routes = new ExecutionRouteRegistration[4];
        else if (_routeCount >= _routes.Length)
            System.Array.Resize(ref _routes, _routes.Length * 2);

        for (int i = 0; i < _routeCount; i++)
        {
            if (_routes[i].DomainKey != registration.DomainKey)
                continue;

            if (!_warnedDuplicateRouteKeys)
            {
                _warnedDuplicateRouteKeys = true;
                Debug.LogWarning(string.Concat(
                    "[ExecutionRouter] Duplicate route registration for DomainKey=",
                    registration.DomainKey.ToString(),
                    ". Last registration wins."));
            }

            _routes[i] = registration;
            return;
        }

        _routes[_routeCount++] = registration;
    }

    /// <summary>
    /// Registers a fallback executor used when no route is registered for a decision domain key.
    /// Last registration wins (warn once).
    /// </summary>
    public void RegisterUnknownRouteFallback(DomainExecutionRouteExecutor fallback)
    {
        if (fallback == null)
            return;

        if (_unknownRouteFallback == fallback)
            return;

        if (_unknownRouteFallback != null && !_warnedDuplicateUnknownRouteFallback)
        {
            _warnedDuplicateUnknownRouteFallback = true;
            Debug.LogWarning("[ExecutionRouter] Duplicate unknown-route fallback registration. Last registration wins.");
        }

        _unknownRouteFallback = fallback;
    }

    bool TryExecuteRegisteredRoute(ArbiterDecision decision, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        OrchestrationDomainId decisionDomain = (OrchestrationDomainId)decision.DomainKey;

        for (int i = 0; i < _routeCount; i++)
        {
            if (_routes[i].DomainKey != decisionDomain)
                continue;

            DomainExecutionRouteExecutor execute = _routes[i].Execute;
            if (execute == null)
                return false;

            execute(this, decision, world, ctx);
            return true;
        }

        // Defensive unknown-route fallback is configurable via route-contributor seam.
        _unknownRouteFallback?.Invoke(this, decision, world, ctx);

        return false;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Debug helpers — golden test output
    // ──────────────────────────────────────────────────────────────────

    static string BuildFirstReceiverIds(OrchestrationWorldCache world, int max)
    {
        int total = world.CombatReceiverCount;
        if (total == 0) return "-";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(32);
        int count = total < max ? total : max;
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(world.GetCombatReceiverEntityId(i).ToStableInt().ToString());
        }
        return sb.ToString();
    }

    static string BuildFirstCrowdIds(OrchestrationWorldCache world, int max)
    {
        int crowdCount = world.CrowdCount;
        if (crowdCount == 0) return "-";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(32);
        int count = crowdCount < max ? crowdCount : max;
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(world.GetCrowdEntityId(i).ToStableInt().ToString());
        }
        return sb.ToString();
    }

    void IExecutionRouteHost.PublishCommand<TCommand>(TCommand command) => PublishCommand(command);

    void PublishCommand<TCommand>(TCommand command) where TCommand : struct, ICommand => _bus.Publish(command);
}
