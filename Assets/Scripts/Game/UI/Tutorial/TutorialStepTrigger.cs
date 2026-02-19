using System.Collections;
using UnityEngine;
using MoreMountains.Tools;
using UnityEngine.Events;

public class TutorialStepTrigger : MonoBehaviour, MMEventListener<TutorialEvent> {
    [SerializeField] protected TutorialEventType eventType;
    [SerializeField] protected TutorialStep step;
    
    [MMEnumCondition("eventType", (int)TutorialEventType.Start)]
    [SerializeField] protected int substep;

    [Header("Conditions")]
    [MMEnumCondition("eventType", (int)TutorialEventType.Cancel)]
    [SerializeField] protected bool performIfCompleted = true;

    [MMEnumCondition("eventType", (int)TutorialEventType.Start)]
    [SerializeField] protected TutorialStep completedStep;
    
    [MMEnumCondition("eventType", (int)TutorialEventType.Start, (int)TutorialEventType.Cancel)]
    [SerializeField] protected TutorialStep nonCompletedStep;
    
    [MMEnumCondition("eventType", (int)TutorialEventType.Continue, (int)TutorialEventType.CancelSubstep)]
    [SerializeField] protected int requiredSubstep;
    
    [Header("Trigger params")]
    [SerializeField] protected float initialDelay;
    [SerializeField] protected TriggerMode triggerMode;
    [SerializeField] protected float triggerDelay;
    
    [Header("Events")]
    [SerializeField] protected UnityEvent OnTrigger;
    [SerializeField] protected UnityEvent OnCompleted;
    [SerializeField] protected UnityEvent OnNonCompleted;

    protected int _substep = -1;

    public void SetStepWithString(string value) {
        TutorialStep parsed;
        bool succeded = System.Enum.TryParse<TutorialStep>(value, out parsed);
        if (succeded)
            step = parsed;
    }

    public virtual void OnMMEvent(TutorialEvent e) {
        if (e.Type == TutorialEventType.Status && e.CallerId.Equals(gameObject.name) &&
            e.Triggered.Step == step && e.Stage == EventStage.End) {

            Debug.LogFormat("TutorialStepTrigger TutorialEvent: status is {0} for {1} in {2}",e.Triggered.Status.ToString(), e.Triggered.Step.ToString(), gameObject.name);
            bool cantBeRestarted = e.Triggered.Status == TutorialStepStatus.Active || e.Triggered.Status == TutorialStepStatus.Substep;
            bool stepCompleted = e.Triggered.Status == TutorialStepStatus.Complete;
            bool completedStepCompleted = completedStep == TutorialStep.None || e.Completed.Status == TutorialStepStatus.Complete;
            bool nonCompletedStepCompleted = nonCompletedStep != TutorialStep.None && e.NonCompleted.Status == TutorialStepStatus.Complete;
            bool requiredSubstepCompleted = e.Triggered.Substep == requiredSubstep;

            if (stepCompleted)
                OnCompleted?.Invoke();
            else
                OnNonCompleted?.Invoke();

            if (eventType == TutorialEventType.Start) {
                if (_substep < 0)
                    _substep = substep;

                if (cantBeRestarted || stepCompleted || !completedStepCompleted || nonCompletedStepCompleted)
                    return;

            } else if (eventType == TutorialEventType.Continue) {
                if (!requiredSubstepCompleted || e.Triggered.Status == TutorialStepStatus.None ||
                    e.Triggered.Status == TutorialStepStatus.Complete)
                    return;

            } else if (eventType == TutorialEventType.Cancel) {
                if (e.Triggered.Status == TutorialStepStatus.None ||
                    (!performIfCompleted && e.Triggered.Status == TutorialStepStatus.Complete) || nonCompletedStepCompleted)
                    return;

            } else if (eventType == TutorialEventType.CancelSubstep) {
                if (!requiredSubstepCompleted || e.Triggered.Status != TutorialStepStatus.Substep)
                    return;

            } else if (eventType == TutorialEventType.Complete && (e.Triggered.Status == TutorialStepStatus.None ||
                e.Triggered.Status == TutorialStepStatus.Complete)) {
                return;
            }

            if (_substep < 0)
                _substep = e.Triggered.Substep;

            if (eventType != TutorialEventType.Status) {
                if (triggerDelay > 0)
                    StartCoroutine(TriggerCoroutine());
                else
                    TriggerInstantly();
            }
        }
    }

    public virtual void SetTriggerDelay(float delay) {
        triggerDelay = delay;
    }

    protected virtual IEnumerator TriggerCoroutine() {
        yield return new WaitForSeconds(triggerDelay);
        TriggerInstantly();
    }

    protected virtual void TriggerInstantly() {
        if (gameObject.activeInHierarchy) {
            Debug.LogFormat("TutorialStepTrigger TutorialEvent: triggering {0} for {1} in {2}",
                eventType.ToString(), step.ToString(), gameObject.name);
            TutorialEvent.Trigger(gameObject.name, EventStage.Start, eventType, new TutorialStepEventData(step, substep: _substep),
            new TutorialStepEventData(completedStep), new TutorialStepEventData(nonCompletedStep));
            _substep = -1;
            OnTrigger?.Invoke();
        }
    }

    protected virtual void TriggerWithMode(TriggerMode mode) {
        if ((PlayerPrefs.GetInt(TutorialManager.TutorialParamName, 0) == 1) && triggerMode.HasFlag(mode)) {
            Debug.LogFormat("TutorialStepTrigger TriggerWithMode: {0} for {1}", mode.ToString(), gameObject.name);
            Trigger();
        }
    }

    public virtual void Trigger() {
        if (gameObject.activeInHierarchy)
            Trigger(substep);
    }

    public virtual void Trigger(int customSubstep) {
        _substep = customSubstep;

        if (initialDelay > 0)
            StartCoroutine(CheckStatusCoroutine());
        else
            CheckStatusInstantly();
    }

    protected virtual IEnumerator CheckStatusCoroutine() {
        yield return new WaitForSeconds(initialDelay);
        CheckStatusInstantly();
    }

    protected virtual void CheckStatusInstantly() {
        TutorialEvent.Trigger(gameObject.name, EventStage.Start, TutorialEventType.Status, new TutorialStepEventData(step),
            new TutorialStepEventData(completedStep), new TutorialStepEventData(nonCompletedStep));
    }

    protected virtual void Awake() {
        TriggerWithMode(TriggerMode.OnAwake);
    }

    protected virtual void Start() {
        TriggerWithMode(TriggerMode.OnStart);
    }

    protected virtual void OnEnable() {
        if (TutorialManager.ShowTutorial) {
            this.MMEventStartListening<TutorialEvent>();
            TriggerWithMode(TriggerMode.OnEnable);
        } else
            gameObject.SetActive(false);
    }

    protected virtual void OnDisable() {
        this.MMEventStopListening<TutorialEvent>();
    }
}
