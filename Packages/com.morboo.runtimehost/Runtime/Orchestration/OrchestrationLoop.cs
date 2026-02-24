using UnityEngine;

/// <summary>
/// Drives the orchestration tick lifecycle: subscribes to <see cref="ITickSource"/>,
/// calls <see cref="OrchestrationArbiter.ProduceTick"/> to get a decision, then
/// passes it to <see cref="ExecutionRouter.Execute"/> for command emission,
/// and finally flushes the <see cref="InProcessCommandBus"/> to Integration adapters.
/// <para>
/// IMPORTANT — This is the single entry point for the orchestration pipeline.
/// The arbiter produces decisions only; the router emits commands to the bus;
/// Integration adapters subscribe and apply commands to MonoBehaviour receivers.
/// </para>
/// <para>
/// IMPORTANT — Subscribe/Unsubscribe during a tick callback is forbidden by
/// <see cref="ITickSource"/> contract. This component does not subscribe/unsubscribe
/// other components during its tick handler.
/// </para>
/// </summary>
public sealed class OrchestrationLoop : MonoBehaviour, IEventBusProvider, ICommandBusProvider
{
    // ──────────────────────────────────────────────────────────────────
    //  Serialized
    // ──────────────────────────────────────────────────────────────────

    [Header("Tick Source")]
    [Tooltip("ITickSource that drives the loop. Must be a MonoBehaviour implementing ITickSource (e.g. RealtimeScheduler).")]
    [SerializeField] MonoBehaviour tickSourceComponent;

    [Header("Pipeline")]
    [Tooltip("Ordered pipeline composition components (each component owns arbiter + faction + domain composition). " +
             "Index 0 is the compatibility primary pipeline used by current adapters via OrchestrationLoop.CommandBus/CurrentWorld/CurrentExecContext.")]
    [SerializeField] OrchestrationPipelineComponent[] pipelines;

    [Header("Faction Relations (Global)")]
    [Tooltip("Shared typed faction relation table used by all orchestration pipelines in this scene. " +
             "Faction identity is per-pipeline; relation semantics are global to avoid conflicting tables.")]
    [SerializeField] FactionRelationTableAsset sharedRelations;

    ITickSource _tickSource;
    readonly InProcessCommandBus _commandBus = new InProcessCommandBus();
    readonly InProcessEventBus _eventBus = new InProcessEventBus();
    OrchestrationPipeline[] _runtimePipelines;
    int _runtimePipelineCount;
    OrchestrationPipeline _primaryPipeline;
    OrchestrationWorldCache _currentDispatchWorld;
    ExecutionContext _currentDispatchExecContext;
    bool _hasCurrentDispatchContext;
    bool _warnedInvalidConfiguredPipelines;
    bool _warnedInvalidConfiguredDomains;
    bool _warnedDuplicateConfiguredDomainKeys;

    // ──────────────────────────────────────────────────────────────────
    //  Public — For Integration adapters to subscribe and read per-tick context
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Command bus for adapter subscription. Set before first tick.</summary>
    public InProcessCommandBus CommandBus => _primaryPipeline?.CommandBus ?? _commandBus;

    // ── ICommandBusProvider / IEventBusProvider (C05) ────────────────
    ICommandBus ICommandBusProvider.CommandBus => _commandBus;
    IEventBus IEventBusProvider.EventBus => _eventBus;

    /// <summary>Per-tick world cache, set before bus Flush. Integration adapters read this.</summary>
    public OrchestrationWorldCache CurrentWorld => _hasCurrentDispatchContext ? _currentDispatchWorld : _primaryPipeline?.CurrentWorld;

    /// <summary>Per-tick execution context, set before bus Flush. Integration adapters read this.</summary>
    public ExecutionContext CurrentExecContext => _hasCurrentDispatchContext
        ? _currentDispatchExecContext
        : _primaryPipeline != null ? _primaryPipeline.CurrentExecContext : default;

    /// <summary>
    /// Flattened scene-level domain composition view across configured pipelines (in pipeline order, then domain order).
    /// Bridge composition helpers may read this list during early Awake to apply project-level route policies
    /// before <see cref="BuildAndApplyConfiguredPipelines"/> builds route registrations.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<DomainOrchestrator> ConfiguredDomainOrchestrators
        => BuildConfiguredDomainView();

    /// <summary>
    /// Ordered scene pipeline components (source-of-truth host composition in C04B/B3-B4).
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<OrchestrationPipelineComponent> ConfiguredPipelines
        => pipelines ?? System.Array.Empty<OrchestrationPipelineComponent>();

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void Awake()
    {
        BuildAndApplyConfiguredPipelines();
    }

    void OnEnable()
    {
        // Rebuild once more on enable to reduce sensitivity to cross-object Awake ordering
        // in scene bootstrap (pipeline composition owners / bridge route-policy application).
        if (_runtimePipelineCount == 0)
            BuildAndApplyConfiguredPipelines();

        _tickSource = tickSourceComponent as ITickSource;
        if (_tickSource != null)
        {
            _tickSource.Subscribe(OnTick);
        }
        else
        {
            Debug.LogWarning("[OrchestrationLoop] tickSourceComponent does not implement ITickSource. " +
                             "Loop will not tick.", this);
        }

        if (_runtimePipelineCount == 0)
            Debug.LogWarning("[OrchestrationLoop] no valid pipelines configured.", this);
    }

    void OnDisable()
    {
        if (_tickSource != null)
        {
            _tickSource.Unsubscribe(OnTick);
            _tickSource = null;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Tick handler
    //  IMPORTANT: Sequence is arbiter.ProduceTick → router.Execute (emits
    //  to bus) → set context → bus.Flush (dispatches to adapters).
    // ──────────────────────────────────────────────────────────────────

    void OnTick(TickContext tickCtx)
    {
        if (_runtimePipelineCount == 0 || _runtimePipelines == null)
            return;

        for (int i = 0; i < _runtimePipelineCount; i++)
        {
            _runtimePipelines[i].Tick(tickCtx.Now);
            ClearCurrentDispatchContext();
        }
    }

    void BuildAndApplyConfiguredPipelines()
    {
        _runtimePipelines = null;
        _runtimePipelineCount = 0;
        _primaryPipeline = null;

        if (pipelines == null || pipelines.Length == 0)
            return;

        var runtime = new System.Collections.Generic.List<OrchestrationPipeline>(pipelines.Length);

        for (int pipelineIndex = 0; pipelineIndex < pipelines.Length; pipelineIndex++)
        {
            OrchestrationPipelineComponent pipelineComponent = pipelines[pipelineIndex];
            if (pipelineComponent is Object ucp && ucp == null)
                pipelineComponent = null;

            if (pipelineComponent == null)
                continue;

            // Composition owner is data/config, not an active ticking behavior.
            // We only require the host GameObject to be active in hierarchy.
            if (!pipelineComponent.gameObject.activeInHierarchy)
            {
                if (!_warnedInvalidConfiguredPipelines)
                {
                    _warnedInvalidConfiguredPipelines = true;
                    Debug.LogWarning(string.Concat(
                        "[OrchestrationLoop] pipelines[", pipelineIndex.ToString(),
                        "] GameObject is inactive. Skipping pipeline component."), this);
                }
                continue;
            }

            OrchestrationArbiter pipelineArbiter = pipelineComponent.Arbiter;
            if (pipelineArbiter is Object uo && uo == null)
                pipelineArbiter = null;

            if (pipelineArbiter == null)
            {
                if (!_warnedInvalidConfiguredPipelines)
                {
                    _warnedInvalidConfiguredPipelines = true;
                    Debug.LogWarning(string.Concat(
                        "[OrchestrationLoop] pipelines[", pipelineIndex.ToString(),
                        "] has no arbiter. Skipping pipeline component."), this);
                }
                continue;
            }

            var pipeline = new OrchestrationPipeline(pipelineArbiter, _commandBus, _eventBus);
            pipeline.SetDispatchContextSink(SetCurrentDispatchContext);
            pipeline.ApplyFactionContext(pipelineComponent.OrchestratorFaction, sharedRelations);
            DomainOrchestrator[] resolvedDomains = ResolveConfiguredDomainsForPipeline(pipelineIndex, pipelineComponent.GetConfiguredDomainArrayOrEmpty());
            pipeline.ApplyConfiguredDomains(resolvedDomains);

            runtime.Add(pipeline);
            if (_primaryPipeline == null)
                _primaryPipeline = pipeline;
        }

        if (runtime.Count == 0)
            return;

        _runtimePipelines = runtime.ToArray();
        _runtimePipelineCount = _runtimePipelines.Length;
    }

    void SetCurrentDispatchContext(OrchestrationWorldCache world, ExecutionContext execContext)
    {
        _currentDispatchWorld = world;
        _currentDispatchExecContext = execContext;
        _hasCurrentDispatchContext = true;
    }

    void ClearCurrentDispatchContext()
    {
        _currentDispatchWorld = null;
        _currentDispatchExecContext = default;
        _hasCurrentDispatchContext = false;
    }

    DomainOrchestrator[] ResolveConfiguredDomainsForPipeline(int pipelineIndex, DomainOrchestrator[] configuredDomains)
    {
        if (configuredDomains == null || configuredDomains.Length == 0)
            return System.Array.Empty<DomainOrchestrator>();

        var resolved = new System.Collections.Generic.List<DomainOrchestrator>(configuredDomains.Length);
        var usedKeys = new System.Collections.Generic.HashSet<OrchestrationDomainId>();

        for (int i = 0; i < configuredDomains.Length; i++)
        {
            DomainOrchestrator domain = configuredDomains[i];
            if (domain is Object uo && uo == null)
                domain = null;

            if (domain == null)
            {
                if (!_warnedInvalidConfiguredDomains)
                {
                    _warnedInvalidConfiguredDomains = true;
                    Debug.LogWarning(string.Concat(
                        "[OrchestrationLoop] pipelines[", pipelineIndex.ToString(),
                        "].DomainOrchestrators[", i.ToString(),
                        "] is null. Skipping."), this);
                }
                continue;
            }

            OrchestrationDomainId key = domain.DomainId;
            if (key == OrchestrationDomainId.None)
            {
                if (!_warnedInvalidConfiguredDomains)
                {
                    _warnedInvalidConfiguredDomains = true;
                    Debug.LogWarning(string.Concat(
                        "[OrchestrationLoop] pipelines[", pipelineIndex.ToString(),
                        "].DomainOrchestrators[", i.ToString(),
                        "] reports DomainId=None. Skipping."), this);
                }
                continue;
            }

            if (!usedKeys.Add(key))
            {
                if (!_warnedDuplicateConfiguredDomainKeys)
                {
                    _warnedDuplicateConfiguredDomainKeys = true;
                    Debug.LogWarning(string.Concat(
                        "[OrchestrationLoop] Duplicate DomainId in pipelines[", pipelineIndex.ToString(),
                        "].DomainOrchestrators: ", key.ToString(),
                        ". First entry wins per pipeline."), this);
                }
                continue;
            }

            resolved.Add(domain);
        }

        return resolved.ToArray();
    }

    DomainOrchestrator[] BuildConfiguredDomainView()
    {
        if (pipelines == null || pipelines.Length == 0)
            return System.Array.Empty<DomainOrchestrator>();

        var domains = new System.Collections.Generic.List<DomainOrchestrator>();
        for (int i = 0; i < pipelines.Length; i++)
        {
            OrchestrationPipelineComponent pipelineComponent = pipelines[i];
            if (pipelineComponent is Object ucp && ucp == null)
                pipelineComponent = null;
            if (pipelineComponent == null || !pipelineComponent.isActiveAndEnabled)
                continue;

            var configuredDomains = pipelineComponent.ConfiguredDomainOrchestrators;
            if (configuredDomains == null || configuredDomains.Count == 0)
                continue;

            for (int j = 0; j < configuredDomains.Count; j++)
            {
                DomainOrchestrator domain = configuredDomains[j];
                if (domain is Object uo && uo == null)
                    domain = null;
                if (domain != null)
                    domains.Add(domain);
            }
        }

        return domains.Count == 0 ? System.Array.Empty<DomainOrchestrator>() : domains.ToArray();
    }
}
