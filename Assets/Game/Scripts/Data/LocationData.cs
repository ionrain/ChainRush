using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Sirenix.OdinInspector;
using MoreMountains.Tools;

public enum LocationState { Locked = -1, Unlocked = 0, Completed = 1 }
public enum LocationEventType { Change, Complete }

public struct LocationEvent {
    public EventStage Stage { get; private set; }
    public LocationEventType Type { get; private set; }
    public LocationData Data { get; private set; }

    static LocationEvent e;
    public static void Trigger(EventStage stage, LocationEventType eventType, LocationData data) {
        e.Stage = stage;
        e.Type = eventType;
        e.Data = data;
        MMEventManager.TriggerEvent(e);
    }
}

[System.Serializable]
public class LocationRewardState {
    public LocationState locationState;
    public RewardState rewardState;

    public LocationRewardState(LocationState location, RewardState reward) {
        locationState = location;
        rewardState = reward;
    }
}

[System.Serializable]
public class LocationStateData {
    public string location;
    public LocationState state;
    public List<LevelStateData> levels = new List<LevelStateData>();
    public int currentIndex;
}

[CreateAssetMenu(fileName = "New LocationData", menuName = "Game/LocationData", order = 0)]
public class LocationData : SerializedScriptableObject {
    public LocalizedString title;

    [Header("Battle UI")]
    public GameObject backPrefab;
    public LocationIcon iconPrefab;
    public Sprite levelBack;
    public List<LevelData> levels = new List<LevelData>();

    public string Title => title != null && !title.IsEmpty ? title.GetLocalizedString() : string.Empty;
    public LocationState State { get; private set; }
    public LevelData Current => GetLevel(CurrentIndex);
    public LevelData Next => GetLevel(CurrentIndex + 1);
    public int Difficulty { get; private set; }
    public bool HasLevels => levels != null && levels.Count > 0;
    public bool HasPreviousLevel => HasLevels && CurrentIndex > 0;
    public bool HasNextLevel => HasLevels && CurrentIndex < levels.Count - 1;
    public int CurrentIndex { get; private set; }
    public int LevelsCount => HasLevels ? levels.Count : 0;
    public int PassedCount => HasLevels ? levels.FindAll(t => t.State > LevelState.Unlocked).Count : 0;

    public LevelData GetLevel(int index) => index >= 0 && index < levels.Count ? levels[index] : null;
    public LevelData LastLevel => HasLevels ? levels[levels.Count - 1] : null;

    public int CompletedLevelsCount => HasLevels ? levels.FindAll(t => t.State >= LevelState.Passed).Count : 0;

    public void UpdateLevelStates() {
        if (HasLevels) {
            levels.ForEach(t => t.TryUnlock());
            if (State == LocationState.Locked && levels[0].State == LevelState.Unlocked)
                SetState(LocationState.Unlocked);
        }
    }

    public void SetCurrentIndex(int index) {
        if (HasLevels && index >= 0 && index < levels.Count)
            CurrentIndex = index;
    }

    public void Reset() {
        State = LocationState.Locked;
        CurrentIndex = 0;
        if (levels != null && levels.Count > 0)
            levels.ForEach(t => t?.Reset());
    }

    public void MoveForward() {
        CurrentIndex = HasLevels && CurrentIndex < levels.Count - 1 ? CurrentIndex + 1 : 0;
    }

    public void MoveBackward() {
        if (HasLevels && levels.Count > 2) {
            CurrentIndex = CurrentIndex > 1 ? CurrentIndex - 1 : levels.Count - 2;
            return;
        } 
        CurrentIndex = 0;
    }

    public void MoveToIndex(int index) {
        if (HasLevels && index >= 0 && index < levels.Count)
            CurrentIndex = index;
    }

    public bool SetState(LocationState newState) {
        if (State != newState) {
            State = newState;
            if (State == LocationState.Unlocked)
                UpdateLevelStates();
            return true;
        }
        return false;
    }

    public LocationStateData GetStateData() {
        LocationStateData result = new LocationStateData() { location = name, state = State, currentIndex = CurrentIndex };

        if (HasLevels)
            levels.ForEach(t => result.levels.Add(t.GetStateData()));

        return result;
    }

    public void SetStateData(LocationStateData stateData) {
        State = stateData.state;
        CurrentIndex = stateData.currentIndex;

        if (HasLevels) {
            foreach (LevelStateData levelState in stateData.levels) {
                LevelData level = levels.Find(t => t.name.Equals(levelState.level));
                if (level != null)
                    level.SetStateData(levelState);
            }
        }
    }
}
