using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class UnityStringEvent : UnityEvent<string> { }

public class StageStateEventTrigger : EventTrigger<LevelStageState>, MMEventListener<LevelStageStateEvent> {
    [Header("Stage State Event Trigger")]
    [SerializeField] EventStage eventStage = EventStage.Start;
    [SerializeField] UnityStringEvent OnTriggerStageId;

    string _stageId;
    
    public void OnMMEvent(LevelStageStateEvent e) {
        if (e.State == eventType && e.EventStage == eventStage && e.Data != null && e.Data.Stage != null) {
            _stageId = e.Data.Stage.id;
            Trigger();
        }
    }

    protected override void OnInvoke() {
        OnTriggerStageId?.Invoke(_stageId);
        base.OnInvoke();
    }

    protected override void OnEnable() {
        if (triggerMode.HasFlag(TriggerMode.OnEvent))
            this.MMEventStartListening<LevelStageStateEvent>();
        base.OnEnable();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelStageStateEvent>();
    }
}
