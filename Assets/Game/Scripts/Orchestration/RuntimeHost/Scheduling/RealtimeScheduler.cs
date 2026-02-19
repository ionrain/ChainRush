using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonoBehaviour implementing <see cref="ITickSource"/> backed by a coroutine loop.
/// Extracted from <see cref="OrchestrationArbiter"/> so the tick rate is configurable
/// and subscribers are decoupled from the arbiter lifecycle.
/// <para>
/// IMPORTANT: Subscribe/Unsubscribe during a tick callback is forbidden —
/// the subscriber list is iterated without copy. Violating this will throw.
/// Tick delivery order among subscribers is registration order.
/// </para>
/// </summary>
public sealed class RealtimeScheduler : MonoBehaviour, ITickSource
{
    [Tooltip("Seconds between ticks. Default 0.25s matches the arbiter's original interval.")]
    [SerializeField] float tickInterval = 0.25f;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime
    // ──────────────────────────────────────────────────────────────────

    readonly List<Action<TickContext>> _subscribers = new List<Action<TickContext>>(4);
    Coroutine _loop;
    WaitForSeconds _wait;
    int _tickIndex;

    // Guard against subscribe/unsubscribe during dispatch
    bool _dispatching;

    // ──────────────────────────────────────────────────────────────────
    //  ITickSource
    // ──────────────────────────────────────────────────────────────────

    public void Subscribe(Action<TickContext> callback)
    {
        if (callback == null) return;
        if (_dispatching)
            throw new InvalidOperationException("[RealtimeScheduler] Subscribe during tick callback is forbidden.");
        if (!_subscribers.Contains(callback))
            _subscribers.Add(callback);
    }

    public void Unsubscribe(Action<TickContext> callback)
    {
        if (callback == null) return;
        if (_dispatching)
            throw new InvalidOperationException("[RealtimeScheduler] Unsubscribe during tick callback is forbidden.");
        _subscribers.Remove(callback);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _wait = new WaitForSeconds(tickInterval);
        _tickIndex = 0;
        _loop = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
    }

    IEnumerator Loop()
    {
        while (true)
        {
            yield return _wait;

            float now = Time.time;
            TickContext ctx = new TickContext(now, tickInterval, _tickIndex++);

            _dispatching = true;
            try
            {
                for (int i = 0; i < _subscribers.Count; i++)
                    _subscribers[i](ctx);
            }
            finally
            {
                _dispatching = false;
            }
        }
    }
}
