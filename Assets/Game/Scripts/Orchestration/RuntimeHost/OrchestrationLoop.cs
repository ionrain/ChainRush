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
public sealed class OrchestrationLoop : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────
    //  Serialized
    // ──────────────────────────────────────────────────────────────────

    [Header("Tick Source")]
    [Tooltip("ITickSource that drives the loop. Must be a MonoBehaviour implementing ITickSource (e.g. RealtimeScheduler).")]
    [SerializeField] MonoBehaviour tickSourceComponent;

    [Header("Pipeline")]
    [Tooltip("The arbiter that produces decisions each tick.")]
    [SerializeField] OrchestrationArbiter arbiter;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Bus + Router
    //  IMPORTANT: Bus is created here. Integration adapters subscribe
    //  via CommandBus property. Flush() dispatches after router emits.
    // ──────────────────────────────────────────────────────────────────

    ITickSource _tickSource;
    readonly InProcessCommandBus _commandBus = new InProcessCommandBus();
    ExecutionRouter _router;

    // ──────────────────────────────────────────────────────────────────
    //  Public — For Integration adapters to subscribe and read per-tick context
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Command bus for adapter subscription. Set before first tick.</summary>
    public InProcessCommandBus CommandBus => _commandBus;

    /// <summary>Per-tick world cache, set before bus Flush. Integration adapters read this.</summary>
    public OrchestrationWorldCache CurrentWorld { get; private set; }

    /// <summary>Per-tick execution context, set before bus Flush. Integration adapters read this.</summary>
    public ExecutionContext CurrentExecContext { get; private set; }

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void Awake()
    {
        _router = new ExecutionRouter(_commandBus);
    }

    void OnEnable()
    {
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

        if (arbiter == null)
            Debug.LogWarning("[OrchestrationLoop] arbiter is not assigned.", this);
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
        if (arbiter == null) return;

        OrchestrationTickResult result = arbiter.ProduceTick(tickCtx.Now);

        if (result.Skipped) return;

        // Router emits commands to bus (no Apply calls)
        _router.Execute(result.Decision, result.World, result.ExecContext);

        // Set per-tick context for adapters before flush
        CurrentWorld = result.World;
        CurrentExecContext = result.ExecContext;

        // Flush dispatches queued commands to Integration adapters
        _commandBus.Flush();
    }
}
