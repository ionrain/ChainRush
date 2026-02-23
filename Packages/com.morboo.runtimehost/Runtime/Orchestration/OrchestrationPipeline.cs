using System;
using System.Collections.Generic;

/// <summary>
/// Runtime container for one orchestration pipeline (one arbiter + router + command bus + domain composition).
/// C04B/B2 extraction step: current scene still uses a single pipeline instance via <see cref="OrchestrationLoop"/>.
/// </summary>
public sealed class OrchestrationPipeline
{
    public delegate void DispatchContextSink(OrchestrationWorldCache world, ExecutionContext execContext);

    readonly InProcessCommandBus _commandBus;
    readonly ExecutionRouter _router;
    DispatchContextSink _dispatchContextSink;

    OrchestrationArbiter _arbiter;
    FactionAsset _orchestratorFaction;
    OrchestrationWorldCache _currentWorld;
    ExecutionContext _currentExecContext;
    DomainOrchestrator[] _configuredDomains;

    public OrchestrationPipeline(OrchestrationArbiter arbiter, InProcessCommandBus commandBus = null)
    {
        _commandBus = commandBus ?? new InProcessCommandBus();
        _router = new ExecutionRouter(_commandBus);
        _arbiter = arbiter;
        _configuredDomains = Array.Empty<DomainOrchestrator>();
    }

    public InProcessCommandBus CommandBus => _commandBus;
    public OrchestrationWorldCache CurrentWorld => _currentWorld;
    public ExecutionContext CurrentExecContext => _currentExecContext;
    public IReadOnlyList<DomainOrchestrator> ConfiguredDomainOrchestrators => _configuredDomains ?? Array.Empty<DomainOrchestrator>();
    public FactionAsset OrchestratorFaction => _orchestratorFaction;

    public void SetArbiter(OrchestrationArbiter arbiter)
    {
        _arbiter = arbiter;
    }

    public void SetDispatchContextSink(DispatchContextSink sink)
    {
        _dispatchContextSink = sink;
    }

    /// <summary>
    /// Applies faction-first pipeline identity into the arbiter (C04B/B5).
    /// </summary>
    public void ApplyFactionContext(FactionAsset orchestratorFaction, FactionRelationTableAsset relations)
    {
        _orchestratorFaction = orchestratorFaction;

        if (_arbiter == null)
            return;

        _arbiter.SetFactionContextForComposition(_orchestratorFaction, relations);
    }

    /// <summary>
    /// Applies resolved domain composition into the arbiter and registers route contributors into this pipeline router.
    /// </summary>
    public void ApplyConfiguredDomains(DomainOrchestrator[] resolvedDomains)
    {
        _configuredDomains = resolvedDomains ?? Array.Empty<DomainOrchestrator>();

        if (_arbiter == null)
            return;

        _arbiter.SetDomainOrchestratorsForComposition(_configuredDomains);
        RegisterDomainRoutes(_configuredDomains);
    }

    /// <summary>
    /// Executes one pipeline tick: arbiter produce → router execute → publish context → bus flush.
    /// </summary>
    public void Tick(float now)
    {
        if (_arbiter == null)
            return;

        OrchestrationTickResult result = _arbiter.ProduceTick(now);
        if (result.Skipped)
            return;

        _router.Execute(result.Decision, result.World, result.ExecContext);

        _currentWorld = result.World;
        _currentExecContext = result.ExecContext;
        _dispatchContextSink?.Invoke(_currentWorld, _currentExecContext);

        _commandBus.Flush();
    }

    void RegisterDomainRoutes(DomainOrchestrator[] resolvedDomains)
    {
        if (resolvedDomains == null || resolvedDomains.Length == 0)
            return;

        for (int i = 0; i < resolvedDomains.Length; i++)
        {
            DomainOrchestrator domain = resolvedDomains[i];
            if (domain == null)
                continue;

            DomainRegistration registration = domain.GetRegistration();
            IDomainExecutionRouteContributor routeContributor = registration.ExecutionRouteContributor;
            if (routeContributor == null)
                continue;

            routeContributor.RegisterRoutes(_router);
        }
    }
}
