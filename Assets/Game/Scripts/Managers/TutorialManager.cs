using System;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

public enum TutorialStep { None, TutorialLevel, FirstTap, BlankCells, Numbers, UnitSpawn, UnitMerge, TryYourself, TutorialLevelReward, UnitLevelUp, 
                           SecondLevel, SecondLevelReward, CitadelLevelUp, UnitUnlock, Boosters }
public enum TutorialEventType { Start, Complete, Continue, Cancel, CancelSubstep, Status }
public enum TutorialStepStatus { None, Active, Substep, Complete }

public struct TutorialStepEventData {
    public TutorialStep Step { get; set; }
    public TutorialStepStatus Status { get; set; }
    public int Substep { get; set; }

    public TutorialStepEventData(TutorialStep step = TutorialStep.None, TutorialStepStatus status = TutorialStepStatus.None, int substep = -100) {
        Step = step;
        Status = status;
        Substep = substep;
    }
}

public struct TutorialEvent {
    public string CallerId { get; private set; }
    public EventStage Stage { get; private set; }
    public TutorialEventType Type { get; private set; }
    public TutorialStepEventData Triggered { get; private set; }
    public TutorialStepEventData Completed { get; private set; }
    public TutorialStepEventData NonCompleted { get; private set; }

    static TutorialEvent e;
    public static void Trigger(string callerId, EventStage stage, TutorialEventType eventType,
        TutorialStepEventData triggered,
        TutorialStepEventData completed = new TutorialStepEventData(),
        TutorialStepEventData nonCompleted = new TutorialStepEventData()) {
        e.CallerId = callerId;
        e.Stage = stage;
        e.Type = eventType;
        e.Triggered = triggered;
        e.Completed = completed;
        e.NonCompleted = nonCompleted;
        MMEventManager.TriggerEvent(e);
    }
}

public class TutorialManager : SerializedMonoBehaviour, MMEventListener<TutorialEvent>, MMEventListener<GameSettingsEvent> {
    public static string TutorialParamPattern = "TutorialStep{0}";
    public static string TutorialParamName => "ShowTutorial";

    public static bool ShowTutorial => PlayerPrefs.GetInt(TutorialParamName, 0) == 1;

    [SerializeField] Dictionary<TutorialStep, TutorialPanel> steps = new Dictionary<TutorialStep, TutorialPanel>();
    [SerializeField] bool markComplete;

    void ClearPlayerPrefs() {
        Array tutorialSteps = Enum.GetValues(typeof(TutorialStep));
        foreach (TutorialStep step in tutorialSteps) {
            string key = string.Format(TutorialParamPattern, step);
            if (PlayerPrefs.HasKey(key))
                PlayerPrefs.DeleteKey(key);
        }
    }

    public bool IsStepComplete(TutorialStep step) {
        return GetStepEventData(step).Status == TutorialStepStatus.Complete;
    }

    public void OnMMEvent(TutorialEvent e) {
        if (e.Stage == EventStage.Start && e.CallerId != string.Empty && e.Triggered.Step != TutorialStep.None) {
            TutorialStep step = e.Triggered.Step;
            string key = string.Format(TutorialParamPattern, step);
            bool hasPanel = steps.ContainsKey(step) && steps[step] != null;
            TutorialPanel panel = hasPanel ? steps[step] : null;

            if (e.Type == TutorialEventType.Start) {
                int substep = e.Triggered.Substep;
                PlayerPrefs.SetInt(key, substep);
                PlayerPrefs.Save();
                if (hasPanel && !panel.Visible) {
                    panel.gameObject.SetActive(true);
                    panel.SetSubstep(substep);
                    panel.Show();
                }
            } else if (e.Type == TutorialEventType.Continue) {
                PlayerPrefs.SetInt(key, panel.Substep);
                PlayerPrefs.Save();                
                if (hasPanel)
                    panel.Forward();
            } else if (e.Type == TutorialEventType.Complete) {
                if (markComplete) {
                    PlayerPrefs.SetInt(key, -1);
                    PlayerPrefs.Save();
                }
                if (hasPanel)
                    panel.Hide();
            } else if (e.Type == TutorialEventType.Cancel) {
                PlayerPrefs.SetInt(key, -100);
                PlayerPrefs.Save();
                if (hasPanel)
                    panel.Hide();
            } else if (e.Type == TutorialEventType.CancelSubstep && hasPanel) {
                panel.Backwards();
                PlayerPrefs.SetInt(key, panel.Substep);
                PlayerPrefs.Save();
            }

            TutorialEvent.Trigger(e.CallerId, EventStage.End, e.Type, GetStepEventData(step),
                                  GetStepEventData(e.Completed.Step), GetStepEventData(e.NonCompleted.Step));
        }
    }

    void CheckComplete() {
        foreach (var pair in steps) {
            TutorialStepEventData data = GetStepEventData(pair.Key);
            if (data.Status != TutorialStepStatus.Complete)
                return;
        }
        GameSettingsEvent.Trigger(EventStage.Start, GameSettingsAction.TurnOffTutorial);
    }

    TutorialStepEventData GetStepEventData(TutorialStep step) {
        if (step != TutorialStep.None) {
            int substep = PlayerPrefs.GetInt(string.Format(TutorialParamPattern, step), -100);
            TutorialStepStatus status = GetStatus(substep);
            return new TutorialStepEventData(step, status, substep);
        }
        return new TutorialStepEventData();
    }

    TutorialStepStatus GetStatus(int substep) {
        if (substep == -1)
            return TutorialStepStatus.Complete;
        else if (substep == 0)
            return TutorialStepStatus.Active;
        else if (substep > 0)
            return TutorialStepStatus.Substep;
        return TutorialStepStatus.None;
    }

    void Awake() {
        foreach (var pair in steps) {
            string key = string.Format(TutorialParamPattern, pair.Key);
            int substep = PlayerPrefs.GetInt(key, -100);
            if (substep >= 0)
                PlayerPrefs.SetInt(key, -100);
        }
    }

    public void OnMMEvent(GameSettingsEvent e) {
        if (e.Action == GameSettingsAction.Reset)
            ClearPlayerPrefs();
    }

    void OnEnable() {
        this.MMEventStartListening<GameSettingsEvent>();
        if (ShowTutorial)
            this.MMEventStartListening<TutorialEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<TutorialEvent>();
        this.MMEventStopListening<GameSettingsEvent>();
    }
}
