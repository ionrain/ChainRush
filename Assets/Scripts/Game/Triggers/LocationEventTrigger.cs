using UnityEngine;

public class LocationEventTrigger : EventTrigger<LocationEventType> {
    [SerializeField] EventStage eventStage;
    [SerializeField] LocationData data;

    protected override void OnInvoke() {
        LocationEvent.Trigger(eventStage, eventType, data);
        base.OnInvoke();
    }
}
