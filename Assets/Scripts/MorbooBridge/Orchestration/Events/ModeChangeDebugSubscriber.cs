using UnityEngine;

/// <summary>
/// Debug subscriber: logs orchestration mode changes to Unity console.
/// Attach to any GameObject with an <see cref="IEventBusProvider"/> reference
/// (e.g. OrchestrationLoop) assigned to the eventBusSource field.
/// </summary>
public sealed class ModeChangeDebugSubscriber : EventBusSubscriber
{
    protected override void SubscribeEvents(InProcessEventBus bus)
    {
        bus.Subscribe<OrchestrationModeChangedEvent>(OnModeChanged);
    }

    protected override void UnsubscribeEvents(InProcessEventBus bus)
    {
        bus.Unsubscribe<OrchestrationModeChangedEvent>(OnModeChanged);
    }

    void OnModeChanged(OrchestrationModeChangedEvent e)
    {
        Debug.Log(string.Concat(
            "[ModeChange] ", e.PreviousDomain.ToString(),
            " → ", e.CurrentDomain.ToString(),
            " t=", e.Timestamp.ToString("F2")), this);
    }
}
