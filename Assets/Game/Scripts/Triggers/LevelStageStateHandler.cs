using System;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine.Events;

public class LevelStageStateHandler : SerializedMonoBehaviour, MMEventListener<LevelStageStateEvent> {
    public Dictionary<EventStage, Dictionary<LevelStageState, UnityEvent>> stageStates = new();

    public void OnMMEvent(LevelStageStateEvent e) {
        if (stageStates != null && e.Data != null) {
            var events = stageStates.GetValueOrDefault(e.EventStage, null);
            if (events != null)
                foreach (var stageState in events)
                    if (e.State == stageState.Key) {
                        stageState.Value?.Invoke();
                        break;
                    }
        }
    }

    void OnEnable() {
        this.MMEventStartListening<LevelStageStateEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelStageStateEvent>();
    }
}
