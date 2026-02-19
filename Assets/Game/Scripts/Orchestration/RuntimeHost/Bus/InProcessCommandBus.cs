using System.Collections.Generic;

/// <summary>
/// In-process command bus: enqueues commands during router dispatch, then
/// flushes all queued commands to subscribed handlers in one batch.
/// <para>
/// IMPORTANT: Typed queues avoid boxing. Only known command types
/// (<see cref="DispatchCombatCommand"/>, <see cref="DispatchIdleCommand"/>) are supported.
/// Use the typed <see cref="PublishCombat"/>/<see cref="PublishIdle"/> overloads from
/// RuntimeHost for zero-GC dispatch. The generic <see cref="ICommandBus.Publish{TCommand}"/>
/// is provided for contract compliance.
/// </para>
/// </summary>
public sealed class InProcessCommandBus : ICommandBus
{
    // PERF: Typed lists — no boxing. Pre-sized, reused each tick.
    readonly List<DispatchCombatCommand> _combatQueue = new List<DispatchCombatCommand>(128);
    readonly List<DispatchIdleCommand> _idleQueue = new List<DispatchIdleCommand>(128);

    System.Action<DispatchCombatCommand> _combatHandler;
    System.Action<DispatchIdleCommand> _idleHandler;

    // ──────────────────────────────────────────────────────────────────
    //  Typed subscribe — Integration adapters use these
    // ──────────────────────────────────────────────────────────────────

    public void SubscribeCombat(System.Action<DispatchCombatCommand> handler) { _combatHandler = handler; }
    public void SubscribeIdle(System.Action<DispatchIdleCommand> handler) { _idleHandler = handler; }

    // ──────────────────────────────────────────────────────────────────
    //  ICommandBus — generic publish (minor boxing for is-check)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generic publish from <see cref="ICommandBus"/>. Routes to typed queues.
    /// PERF: Prefer <see cref="PublishCombat"/>/<see cref="PublishIdle"/> from RuntimeHost.
    /// </summary>
    public void Publish<TCommand>(TCommand command) where TCommand : struct, ICommand
    {
        if (command is DispatchCombatCommand cc)
            _combatQueue.Add(cc);
        else if (command is DispatchIdleCommand ic)
            _idleQueue.Add(ic);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Typed publish — PERF: zero boxing when called directly
    // ──────────────────────────────────────────────────────────────────

    public void PublishCombat(DispatchCombatCommand cmd) { _combatQueue.Add(cmd); }
    public void PublishIdle(DispatchIdleCommand cmd) { _idleQueue.Add(cmd); }

    // ──────────────────────────────────────────────────────────────────
    //  Flush — dispatches all queued commands to handlers
    //  IMPORTANT: Called by OrchestrationLoop after router.Execute.
    // ──────────────────────────────────────────────────────────────────

    public void Flush()
    {
        if (_combatHandler != null)
        {
            for (int i = 0; i < _combatQueue.Count; i++)
                _combatHandler(_combatQueue[i]);
        }
        _combatQueue.Clear();

        if (_idleHandler != null)
        {
            for (int i = 0; i < _idleQueue.Count; i++)
                _idleHandler(_idleQueue[i]);
        }
        _idleQueue.Clear();
    }
}
