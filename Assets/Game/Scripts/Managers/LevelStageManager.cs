using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public enum LevelStageState { Start, Battle, Complete, ClearBonus }

[System.Serializable]
public class LevelStageEvent : UnityEvent<LevelStageStateData> { }

public class LevelStageStateData {
    public LevelStage Stage { get; private set; }
    public string LevelName { get; private set; }
    public int StageIndex { get ; private set; }
    public int TotalStages { get ; private set; }
    public int Time { get ; private set; }
    public float Delay { get; private set; }

    public LevelStageStateData(LevelStage stage, string levelName, int stageIndex, int totalStages, float delay, int time) {
        Stage = stage;
        LevelName = levelName;
        StageIndex = stageIndex;
        TotalStages = totalStages;
        Delay = delay;
        Time = time;
    }

    public bool IsLastStage => StageIndex == TotalStages - 1;
}

public struct LevelStageStateEvent {
    public EventStage EventStage { get; private set; }
    public LevelStageState State { get; private set; }
    public LevelStageStateData Data { get; private set; }
    public bool IsValid => Data != null && Data.Stage != null;

    static LevelStageStateEvent e;
    public static void Trigger(EventStage eventStage, LevelStageState state, LevelStageStateData data) {
        e.EventStage = eventStage;
        e.State = state;
        e.Data = data;
        MMEventManager.TriggerEvent(e);
    }
}

public struct LevelStageStartRequestEvent {
    public string StageId { get; private set; }

    static LevelStageStartRequestEvent e;
    public static void Trigger(string stageId) {
        e.StageId = stageId;
        MMEventManager.TriggerEvent(e);
    }
}

public struct LevelStageCompleteRequestEvent {
    static LevelStageCompleteRequestEvent e;
    public static void Trigger() {
        MMEventManager.TriggerEvent(e);
    }
}

public struct LevelStageProgressEvent {
    public int Time { get; private set; }
    public int RemainingTime { get; private set; }
    public int RealTime { get; private set; }
    public float Progress { get; private set; }
    public float LevelProgress { get; private set; }

    static LevelStageProgressEvent e;
    public static void Trigger(int time, int remainingTime, int realTime, float progress, float levelProgress) {
        e.Time = time;
        e.RemainingTime = remainingTime;
        e.RealTime = realTime;
        e.Progress = progress;
        e.LevelProgress = levelProgress;
        MMEventManager.TriggerEvent(e);
    }
}

public struct LevelStageStateDelay {
    public float Before;
    public float InProcess;
    public float After;
}

public class LevelStageManager : SerializedMonoBehaviour, MMEventListener<LevelLoadEvent>, MMEventListener<LevelStageStartRequestEvent>, 
                                 MMEventListener<LevelGoalResultEvent>, MMEventListener<LevelStageCompleteRequestEvent>, 
                                 MMEventListener<PartyUnitEvent>, MMEventListener<LevelStageStateEvent>, MMEventListener<ItemMagnetEvent>,
                                 MMEventListener<PartyBoostEvent>, MMEventListener<TrapEvent> {
                                    
    [SerializeField] Dictionary<LevelStageState, LevelStageStateDelay> delays = new ();

    List<LevelStage> _stages = new List<LevelStage>();
    int _currentIndex = 0;
    int _stagesCount;
    string _levelName = string.Empty;
    LevelStage _current;
    bool _isMerging;
    bool _isCollectingCoins;
    bool _isBoosting;
    bool _isTrapping;
    System.DateTime _start;

    LevelStage GetByIndex(int index) {
        return index >= 0 && index < _stagesCount ? _stages[index] : null;
    }

    void SwitchStage() {
        if (_currentIndex < _stagesCount - 1) {
            _currentIndex++;
            _current = GetByIndex(_currentIndex);
            _current.ChooseClearBonus();
            _isMerging = false;
            _isCollectingCoins = false;
            _isBoosting = false;
            _isTrapping = false;
            _start = System.DateTime.Now;
        }
    }

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Data != null && e.Data.stages != null) {
            _levelName = e.Data.name;
            _currentIndex = -1;
            _stages = e.Data.stages;
            _stagesCount = _stages.Count;
            SwitchStage();
            SetState(LevelStageState.Start);
        }
    }

    public void OnMMEvent(LevelStageStartRequestEvent e) {
        if (e.StageId.Length == 0 || (_current != null && e.StageId.Equals(_current.id)))
            SetState(LevelStageState.Start);
    }

    void SendStageActionEvent(EventStage eventStage, LevelStageState action, float delay = 0) {
        if (_current != null) {
            double time = 0;
            if (eventStage == EventStage.Start && action == LevelStageState.Complete)
                time = (System.DateTime.Now - _start).TotalSeconds;
            var data = new LevelStageStateData(_current, _levelName, _currentIndex, _stagesCount, delay, (int)time);
            LevelStageStateEvent.Trigger(eventStage, action, data);
        } else 
            Debug.LogWarningFormat("LevelStageManager SendStageActionEvent: Current stage is NULL for Action = {0} and EventStage = {1}", action, eventStage);
    }

    public void OnMMEvent(LevelGoalResultEvent e) {
        if (e.EventStage == EventStage.Start && e.Goal != null) {
            if (e.Goal.Achieved && e.Goal.Type == LevelGoalType.Survive)
                SetState(LevelStageState.Complete);
            else {
                if (e.Goal.Achieved)
                    SetState(LevelStageState.ClearBonus, _current.clearBonuses.Count == 0);
                else
                    TrySetBattleState();
            }
        }
    }

    void SetState(LevelStageState state, bool skipDelay = false) {
        StartCoroutine(SetStateCo(state, skipDelay));
    }

    void TrySetBattleState() {
        if (_current != null) {
            if (_current.enemyData != null)
                SetState(LevelStageState.Battle);
            else
                SetState(LevelStageState.Complete, true);
        }
    }

    IEnumerator SetStateCo(LevelStageState state, bool skipDelay = false) {
        float delta = 0.1f;
        yield return new WaitForSeconds(delta);
        float threshold = 2;
        float elapsed = 0;
        while ((_isMerging || _isCollectingCoins || _isBoosting || _isTrapping) && elapsed < threshold) {
            elapsed += delta;
            if (elapsed >= threshold - delta && state == LevelStageState.ClearBonus) {
                if (_isMerging) {
                    _isMerging = false;
                    Debug.LogError("LevelStageManager LevelStageState: still merging...");
                } else if (_isCollectingCoins) {
                    _isCollectingCoins = false;
                    Debug.LogError("LevelStageManager LevelStageState: still collecting coins...");
                } else if (_isBoosting) {
                    _isBoosting = false;
                    Debug.LogError("LevelStageManager LevelStageState: still boosting...");
                } else if (_isTrapping) {
                    _isTrapping = false;
                    Debug.LogError("LevelStageManager LevelStageState: still trapping...");
                }
            }
            yield return new WaitForSeconds(delta);
        }
        
        LevelStageStateDelay delay = skipDelay ? new LevelStageStateDelay() : delays.GetValueOrDefault(state, new LevelStageStateDelay());
        SendStageActionEvent(EventStage.Start, state, delay.Before);
        yield return new WaitForSeconds(delay.Before);
        SendStageActionEvent(EventStage.Process, state, delay.InProcess);
        yield return new WaitForSeconds(delay.InProcess);
        SendStageActionEvent(EventStage.End, state, delay.After);
        yield return new WaitForSeconds(delay.After);
    }

    public void OnMMEvent(LevelStageCompleteRequestEvent e) {
        if (_current != null)
            SetState(LevelStageState.Complete);
    }

    public void OnMMEvent(PartyUnitEvent e) {
        if (e.Type == PartyUnitEventType.Merge)
            _isMerging = e.EventStage == EventStage.Start;
    }

    public void OnMMEvent(LevelStageStateEvent e) {
        if (e.EventStage == EventStage.End) {
            if (e.State == LevelStageState.ClearBonus)
                TrySetBattleState();
            else if (e.State == LevelStageState.Complete)
                StartCoroutine(AfterCompleteCo());
        }
    }

    IEnumerator AfterCompleteCo() {
        while (_isCollectingCoins)
            yield return new WaitForSecondsRealtime(0.1f);
        if (_currentIndex < _stagesCount - 1) {
            LevelStageStateDelay delay = delays.GetValueOrDefault(LevelStageState.Complete, new LevelStageStateDelay());
            yield return new WaitForSeconds(delay.After);
            SwitchStage();
            SetState(LevelStageState.Start);
        } else 
            LevelActionEvent.Trigger(EventStage.Start, LevelActionType.Succeed);
    }

    public void OnMMEvent(ItemMagnetEvent e) {
        if (e.Id == 0)
            _isCollectingCoins = e.EventStage == EventStage.Start;
    }


    public void OnMMEvent(PartyBoostEvent e) {
        Debug.LogFormat("LevelStageManager PartyBoostEvent: Stage {0} for Type {1}", e.EventStage, e.Booster);
        if (e.EventStage == EventStage.Start)
            _isBoosting = true;
        else if (e.EventStage == EventStage.End)
            _isBoosting = false;
    }

    public void OnMMEvent(TrapEvent e) {
        Debug.LogFormat("LevelStageManager TrapEvent: Stage {0} for Type {1}", e.EventStage, e.Type);
        if (e.Type != TrapType.Alarm) {
            if (e.EventStage == EventStage.Start)
                _isTrapping = true;
            else if (e.EventStage == EventStage.End)
                _isTrapping = false;
        }
    }

    void OnEnable() {
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<LevelStageStateEvent>();
        this.MMEventStartListening<LevelStageStartRequestEvent>();
        this.MMEventStartListening<LevelStageCompleteRequestEvent>();
        this.MMEventStartListening<ItemMagnetEvent>();
        this.MMEventStartListening<PartyBoostEvent>();
        this.MMEventStartListening<TrapEvent>();
        this.MMEventStartListening<LevelGoalResultEvent>();        
    }

    void OnDisable() {
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<LevelStageStateEvent>();
        this.MMEventStopListening<LevelStageStartRequestEvent>();
        this.MMEventStopListening<LevelStageCompleteRequestEvent>();
        this.MMEventStopListening<ItemMagnetEvent>();
        this.MMEventStopListening<TrapEvent>();
        this.MMEventStopListening<PartyBoostEvent>();
        this.MMEventStopListening<LevelGoalResultEvent>();
    }
}
