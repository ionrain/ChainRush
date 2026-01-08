using MoreMountains.Tools;
using UnityEngine;

public class TutorialStageTrigger : TutorialStepTrigger, MMEventListener<LevelStageStateEvent> {
    public void OnMMEvent(LevelStageStateEvent e) {
        if (e.EventStage == EventStage.Start && e.Data.Stage.id.Equals(step.ToString()))
            Trigger();
    }

    protected override void OnEnable() {
        if (triggerMode.HasFlag(TriggerMode.OnEvent))
            this.MMEventStartListening<LevelStageStateEvent>();
        base.OnEnable();
    }

    protected override void OnDisable() {
        this.MMEventStopListening<LevelStageStateEvent>();
        base.OnDisable();
    }
}
