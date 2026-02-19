using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum LevelState { Locked = -1, Unlocked = 0, Passed = 1, Completed = 2 }

[System.Serializable]
public class LevelStateData {
    public string level;
    public LevelState state;
    public int playsCount;
}

[CreateAssetMenu(fileName = "New LevelData", menuName = "Game/LevelData", order = 1)]
public class LevelData : SerializedScriptableObject {
    [Header("General Settings")]
    public string id;
    public LevelData required;
    public LevelGoalReward goal;
    public LevelDifficultyData difficulty;
    public EnemyGenerationData enemyData;
    public Dictionary<Attribute, float> enemyMultipliers = new Dictionary<Attribute, float>();
    public Vector2Int boardSize = new Vector2Int(6, 6);
    
    public string Id => id;
    public int Best { get; set; }
    public int Index { get; set; }
    public LevelGoalReward Goal => goal;
    public int PlaysCount { get; set; }    
    public LevelState State { get; private set; }
    public bool IsRewardValid => goal != null && goal.rewards != null && goal.rewards.Count > 0;
    public RewardItem GetReward() => IsRewardValid ? new RewardItem(RewardItemType.LevelReward, new List<Reward>(goal.rewards), RewardState.Ready) : null;
    public Reward GetRewardByType(RewardType type) => IsRewardValid ? goal.rewards.Find(t => t.Type == type) : null;

    public bool TryUnlock(bool force = false) {
        if (force || (State == LevelState.Locked && (required == null || required.State >= LevelState.Passed))) {
            State = LevelState.Unlocked;
            Best = 0;
            return true;
        }
        return false;
    }

    public void UpdateBestScore(int value) {
        if (Best < value)
            Best = value;
    }

    public void Reset() {
        State = LevelState.Locked;
        Best = 0;
        PlaysCount = 0;        
    }

    public void SetPassed() {
        if (State < LevelState.Passed)
            State = LevelState.Passed;
    }

    public void SetCompleted() {
        State = LevelState.Completed;
    }

    public Dictionary<GameObject, int> GetEnemyPoolData() {
        Dictionary<GameObject, int> result = new Dictionary<GameObject, int>();
        if (enemyData != null) {
            Dictionary<GameObject, int> poolData = enemyData.GetEnemyList();
            foreach (var poolItem in poolData) {
                int value = poolItem.Value;
                if (result.GetValueOrDefault(poolItem.Key, 0) < value)
                    result[poolItem.Key] = value;
            }       
        }
        return result;
    }

    public LevelStateData GetStateData() {
        LevelStateData result = new LevelStateData();
        result.level = name;
        result.state = State;
        result.playsCount = PlaysCount;
        return result;
    }

    public void SetStateData(LevelStateData stateData) {
        State = stateData.state;
        PlaysCount = stateData.playsCount;
    }
}
