using MoreMountains.Tools;
using UnityEngine;

public class TutorialTabUnlockCondition : TabUnlockCondition, MMEventListener<TutorialEvent> {
    [SerializeField] TutorialStep completedStep;

    protected override string PlayerPrefsId => $"TutorialTabUnlockCondition_{completedStep}";

    public override void Check() {
        if (TutorialManager.ShowTutorial && !WasShown)
            TutorialEvent.Trigger(gameObject.name, EventStage.Start, TutorialEventType.Status, new TutorialStepEventData(completedStep));
    }

    public void OnMMEvent(TutorialEvent e) {
        if (e.Stage == EventStage.End && e.Triggered.Step == completedStep) {
            if (e.Type == TutorialEventType.Complete && triggerMode.HasFlag(TriggerMode.OnEvent))
                InvokeOnCheck(true);
            else if (e.Type == TutorialEventType.Status && e.CallerId.Equals(gameObject.name))
                InvokeOnCheck(e.Triggered.Status == TutorialStepStatus.Complete);
        }
    }

    protected override void OnEnable() {
        if (TutorialManager.ShowTutorial && !WasShown)
            this.MMEventStartListening<TutorialEvent>();
        base.OnEnable();
    }

    protected override void OnDisable() {
        this.MMEventStopListening<TutorialEvent>();
        base.OnDisable();
    }
}
