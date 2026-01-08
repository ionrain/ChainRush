using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelActionEventTrigger : EventTrigger<LevelActionType> {

    protected override void OnInvoke() {
        LevelActionEvent.Trigger(EventStage.Start, eventType);
    }
}
