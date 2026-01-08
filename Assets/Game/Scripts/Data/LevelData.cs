using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

public enum LevelState { Locked = -1, Unlocked = 0, Passed = 1, Completed = 2 }
public enum LevelStageDifficulty { None, Easy, Normal, Hard, Hardest }

public class StageClearBonus { }

public class UnitStageClearBonus : StageClearBonus {
    public UnitData Data { get; private set; }

    public void SetData(UnitData value) {
        Data = value;
    }
}

public class BoosterStageClearBonus : StageClearBonus {
    [SerializeField] PartyBoosterType booster;

    public PartyBoosterType Booster => booster;
}

public class LevelStage {
    public string id;
    public int battleDuration;
    public LevelStageDifficulty difficulty;
    public EnemyGenerationData enemyData;
    public List<StageClearBonus> clearBonuses = new();

    [Header("Board data")]
    public Vector2Int boardSize = new Vector2Int(16, 16);
    [Range(0, 1)]
    public float openCellShare = 0;    
    public Dictionary<Vector2Int, CellType> predefinedCells = new();

    [Header("Cell Items")]
    public int unitsCount = 5;
    public Dictionary<TrapType, int> traps = new();
    public Dictionary<PartyBoosterType, int> boosters = new ();

    public int BoostersCount => boosters != null ? boosters.Sum(t => t.Value) : 0;
    public int TrapsCount => traps != null ? traps.Sum(t => t.Value) : 0;
    public int BlocksCount => predefinedCells != null ? predefinedCells.Count(t => t.Value == CellType.Block) : 0;
    public int CellsCount => boardSize.x * boardSize.y;
    public StageClearBonus ClearBonus { get; private set; }
    public string Id => id;

    public void ChooseClearBonus() {
        if (clearBonuses != null && clearBonuses.Count > 0)
            ClearBonus = clearBonuses[Random.Range(0, clearBonuses.Count)];
    }

    public int GetBlockAndOpenCount() => predefinedCells == null ? 0 : predefinedCells.Count(t => t.Value == CellType.Block || t.Value == CellType.Open);
}


[System.Serializable]
public class LevelStateData {
    public string level;
    public LevelState state;
    public int playsCount;
}

[CreateAssetMenu(fileName = "New LevelData", menuName = "Game/LevelData", order = 1)]
public class LevelData : SerializedScriptableObject {
    [Header("General Settings")]
    public LevelData required;

    [Header("Multipliers")]
    public Dictionary<Attribute, float> attributeMultipliers = new ();
    public Dictionary<ResourceType, float> collectMultipliers = new();

    [Header("Chests Data")]
    public int healAmount = 20;
    public int damageAmount = 20;
    public Dictionary<Attribute, float> debuffsValues = new ();

    [Header("Stages")]
    public float cellSize = 2;
    public bool startWithRandomOpener = true;
    public List<LevelStage> stages = new List<LevelStage>();

    [Header("Goals")]
    public List<Reward> rewards = new();
    
    public int Best { get; set; }
    public int Index { get; set; }
    public int PlaysCount { get; set; }    
    public int StagesCount => stages != null ? stages.Count : 0;
    public LevelState State { get; private set; }
    public bool IsRewardValid => rewards != null && rewards.Count > 0;
    public RewardItem GetReward() => new RewardItem(RewardItemType.LevelReward, new List<Reward>(rewards), RewardState.Ready);

    public Reward GetRewardByType(RewardType type) {
        return rewards.Find(t => t.Type == type);
    }

    public int GetTotalCellsCount() {
        int total = 0;
        stages.ForEach(t => total += t != null ? t.boardSize.x * t.boardSize.y - t.GetBlockAndOpenCount() - t.TrapsCount : 0);
        return total;
    }

    public int GetTotalTrapsCount() {
        int total = 0;
        stages.ForEach(t => total += t != null ? t.TrapsCount : 0);
        return total;
    }

    public int GetTotalBoostersCount() {
        int total = 0;
        stages.ForEach(t => total += t != null ? t.BoostersCount : 0);
        return total;
    }

    public bool ContainsBooster(PartyBoosterType booster) {
        foreach (LevelStage stage in stages)
            if (stage != null && stage.boosters != null && stage.boosters.ContainsKey(booster))
                return true;
        return false;
    }

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
        if (State < LevelState.Passed) {
            State = LevelState.Passed;
            Best = stages.Count;
        }
    }

    public void SetCompleted() {
        State = LevelState.Completed;
    }

    public Dictionary<GameObject, int> GetEnemyPoolData() {
        Dictionary<GameObject, int> result = new Dictionary<GameObject, int>();
        foreach (LevelStage stage in stages) {
            if (stage != null && stage.enemyData != null) {
                Dictionary<GameObject, int> poolData = stage.enemyData.GetEnemyList();
                foreach (var poolItem in poolData) {
                    int value = poolItem.Value;
                    if (result.GetValueOrDefault(poolItem.Key, 0) < value)
                        result[poolItem.Key] = value;
                }       
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
