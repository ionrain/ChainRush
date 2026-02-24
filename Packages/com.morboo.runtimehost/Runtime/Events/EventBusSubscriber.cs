using UnityEngine;

/// <summary>
/// Universal MonoBehaviour base for scene-level event bus subscribers.
/// Not tied to orchestration — any system owning an <see cref="IEventBusProvider"/>
/// can serve as event source.
/// <para>
/// IMPORTANT — Subclasses must implement <see cref="SubscribeEvents"/> and
/// <see cref="UnsubscribeEvents"/>. The base handles provider resolution
/// and lifecycle (OnEnable/OnDisable).
/// </para>
/// </summary>
public abstract class EventBusSubscriber : MonoBehaviour
{
    [Tooltip("Any MonoBehaviour implementing IEventBusProvider (e.g. OrchestrationLoop).")]
    [SerializeField] MonoBehaviour eventBusSource;

    InProcessEventBus _bus;

    void OnEnable()
    {
        IEventBusProvider provider = eventBusSource as IEventBusProvider;
        if (provider == null)
        {
            Debug.LogWarning(string.Concat(
                "[EventBusSubscriber] eventBusSource on '", name,
                "' does not implement IEventBusProvider. Subscriber inactive."), this);
            return;
        }

        // IMPORTANT: IEventBusProvider.EventBus returns IEventBus (publish-only).
        // Subscribe/Unsubscribe require the concrete InProcessEventBus.
        _bus = provider.EventBus as InProcessEventBus;
        if (_bus == null)
        {
            Debug.LogWarning(string.Concat(
                "[EventBusSubscriber] eventBusSource on '", name,
                "' provides an IEventBus that is not InProcessEventBus. Subscriber inactive."), this);
            return;
        }

        SubscribeEvents(_bus);
    }

    void OnDisable()
    {
        if (_bus != null)
        {
            UnsubscribeEvents(_bus);
            _bus = null;
        }
    }

    /// <summary>Subscribe to events on the resolved bus. Called from OnEnable.</summary>
    protected abstract void SubscribeEvents(InProcessEventBus bus);

    /// <summary>Unsubscribe from events. Called from OnDisable.</summary>
    protected abstract void UnsubscribeEvents(InProcessEventBus bus);
}
