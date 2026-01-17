using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;

public enum LevelResult { None, Success, Failure, Reload, Quit }
public enum LevelActionType { Pause, Unpause, RequestResult, Succeed, Fail, Reload, Quit }
public enum LevelExp { None = 0, Smallest = 3, Small = 6, Medium = 20, Large = 100, Largest = 200 }

public class LevelResultData : IRewardList {
    public LevelData LevelData { get; private set; }
    public List<Reward> RewardList { get; private set; }
    public int LastStage { get; private set; }
    public int TotalStages { get; private set; }
    public int Time { get; private set; }

    public bool Valid => LevelData != null && RewardList != null;

    public LevelResultData(LevelData levelData, List<Reward> rewardsList, int lastStage, int time) {
        LevelData = levelData;
        RewardList = rewardsList;
        LastStage = lastStage;
        Time = time;
    }

    public List<Reward> GetRewards(RewardType rewardType = RewardType.Any) => RewardList;
}

public struct LevelActionEvent {
    public EventStage Stage { get; private set; }
    public LevelActionType Action { get; private set; }

    static LevelActionEvent e;
    public static void Trigger(EventStage stage, LevelActionType action) {
        e.Stage = stage;
        e.Action = action;
        MMEventManager.TriggerEvent(e);
    }
}

public struct LevelResultEvent {
    public LevelResult Result { get; private set; }
    public LevelResultData Data { get; private set; }

    static LevelResultEvent e;
    public static void Trigger(LevelResult result, LevelResultData data) {
        e.Result = result;
        e.Data = data;
        MMEventManager.TriggerEvent(e);
    }
}

public struct LevelLoadEvent {
    public EventStage Stage { get; private set; }
    public LevelData Data { get; private set; }

    static LevelLoadEvent e;
    public static void Trigger(EventStage stage, LevelData data) {
        e.Stage = stage;
        e.Data = data;
        MMEventManager.TriggerEvent(e);
    }
}

public struct LevelProgressEvent {
    public float Progress { get; private set; }
    public int Time { get; private set; }

    static LevelProgressEvent e;
    public static void Trigger(float progress, int time) {
        e.Progress = progress;
        e.Time = time;
        MMEventManager.TriggerEvent(e);
    }
}

public class LevelManager : MonoBehaviour, MMEventListener<LevelActionEvent>, MMEventListener<LevelGoalResultEvent> {
    [SerializeField] SpriteRenderer background;
    [SerializeField] AllLocationsData locations;
    [SerializeField] UnityEvent OnLevelLoaded;

    LevelData _data;
    int _stageIndex;
    float _time;
    float _duration;
    LevelResult _result = LevelResult.None;
    int _timeInSeconds;

    void Start() {
        Setup();
    }

    void FixedUpdate() {
        if (_time < _duration) {
            float deltaTime = Time.fixedDeltaTime;
            _time += deltaTime;
            int intTime = (int)Mathf.Floor(_time);

            if (_timeInSeconds < intTime) {
                _timeInSeconds++;
                LevelProgressEvent.Trigger(Mathf.Clamp01(_time / _duration), _timeInSeconds);
            }
        }
    }

    public void Setup() {
        if (locations != null && locations.Current != null && locations.Current.Current != null) {
            _data = locations.Current.Current;
            _data.PlaysCount++;
            _duration = _data.duration;
            _timeInSeconds = 0;
            _time = 0;
            if (background != null)
                background.sprite = locations.Current.levelBack;
            LevelLoadEvent.Trigger(EventStage.Start, _data);
            OnLevelLoaded?.Invoke();
        }
    }

    protected IEnumerator EndLevel(LevelResult result, float delay = 0) {
        _result = result;
        if (_data != null) {
            if (delay > 0)
                yield return new WaitForSeconds(delay);

            if (result == LevelResult.Success) {
                _data.SetPassed();
                LevelData next = locations.Current.Next;
                if (next != null)
                    next.TryUnlock();
                locations.Current.MoveForward();
            }

            yield return null;
            TriggerLevelResultEvent(result);
        } else
            Debug.Log("GameLevelManager EndLevel: LevelData or Party or LootManager is NULL");
    }

    void CancelNextLevel() {
        if (locations != null)
            locations.Current?.MoveBackward();
    }

    public void OnMMEvent(LevelActionEvent e) {
        if (e.Stage == EventStage.Start) {
            if (e.Action == LevelActionType.RequestResult)
                TriggerLevelResultEvent(LevelResult.None);
            else if (e.Action == LevelActionType.Reload) {
                if (_result == LevelResult.Success)
                    CancelNextLevel();
                TriggerLevelResultEvent(LevelResult.Reload);
            } else if (e.Action == LevelActionType.Quit && _result == LevelResult.None)
                TriggerLevelResultEvent(LevelResult.Quit);
            else if (e.Action == LevelActionType.Succeed)
                StartCoroutine(EndLevel(LevelResult.Success, 1));
            else if (e.Action == LevelActionType.Fail)
                StartCoroutine(EndLevel(LevelResult.Failure));
        }
    }

    /*public void OnMMEvent(LevelStageStateEvent e) {
        if (e.EventStage == EventStage.Start && e.State == LevelStageState.Start)
            _stageIndex = e.Data.StageIndex;
    }*/

    public void Pause() {
        LevelActionEvent.Trigger(EventStage.Start, LevelActionType.RequestResult);
    }

    void OnApplicationPause(bool paused) {
        if (paused && Time.timeScale > 0)
            Pause();
    }

    protected void TriggerLevelResultEvent(LevelResult result) {
        LevelResultEvent.Trigger(result, new LevelResultData(_data, new List<Reward>(), _stageIndex, (int)_time));
    }

    public void OnMMEvent(LevelGoalResultEvent e) {
        if (e.Stage == EventStage.Start && e.Goal != null) {
            bool achieved = e.Goal.Achieved;
            TriggerLevelResultEvent(e.Goal.Achieved ? LevelResult.Success : LevelResult.Failure);
            if (achieved && _data.Goal.State < RewardState.Ready)
                _data.Goal.SetState(RewardState.Ready);
            e.Goal.SetAchieved(false);
        } else
            Debug.LogError("LevelManager LevelGoalResultEvent: Something went wrong");
    }

    void OnEnable() {
        this.MMEventStartListening<LevelActionEvent>();
        this.MMEventStartListening<LevelGoalResultEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelActionEvent>();
        this.MMEventStopListening<LevelGoalResultEvent>();
    }

}
