using UnityEngine;

public class SocialEventTrigger : EventTrigger<SocialEventType> {
    protected override void OnInvoke() {
        SocialEvent.Trigger(eventType);
        base.OnInvoke();
    }
}
