using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

public class LevelGoalReward : IRewardItem {
    public LevelGoal goal;
    public List<Reward> rewards = new List<Reward>();
    
    public RewardState State { get; set; }

    public List<Reward> Rewards => rewards;
    public RewardItemType Type => RewardItemType.LevelGoalReward;
    public bool Completed => State >= RewardState.Ready;
    public bool Achieved => goal != null ? goal.Achieved : false;
    public LevelGoalType GoalType => goal != null ? goal.Type : LevelGoalType.Survive;
    public int GoalAmount => goal != null ? goal.Amount : 0;

    public List<Reward> GetRewards(RewardType rewardType) {
        return rewards.FindAll(r => rewardType == RewardType.Any || r.Type == rewardType);
    }

    public bool SetState(RewardState value) {
        if (State != value) {
            State = value;
            if (State == RewardState.Ready && goal != null)
                goal.Complete();
            return true;
        }
        return false;
    }

    public void SetAchieved(bool value) {
        if (goal != null) {
            goal.SetAchieved(value);
            goal.SetCurrentAmount(0);
        }
    }
}

public struct LevelGoalProgressEvent {
    public EventStage Stage { get; private set; }
    public string GoalId { get; private set; }
    public int Progress { get; private set; }
    public int Total { get; private set; }

    static LevelGoalProgressEvent e;
    public static void Trigger(EventStage stage, string goalId, int progress, int total = 0) {
        e.Stage = stage;
        e.GoalId = goalId;
        e.Progress = progress;
        e.Total = total;
        MMEventManager.TriggerEvent(e);
    }
}

public struct LevelGoalResultEvent {
    public EventStage Stage {get; private set; }
    public LevelGoal Goal { get; private set; }

    static LevelGoalResultEvent e;
    public static void Trigger(EventStage stage, LevelGoal goal) {
        e.Stage = stage;
        e.Goal = goal;
        MMEventManager.TriggerEvent(e);
    }
}

public class LevelGoalManager : SerializedMonoBehaviour, MMEventListener<EarnResourceEvent>, MMEventListener<LevelGoalProgressEvent>, MMEventListener<LevelLoadEvent>,
    MMEventListener<ItemEvent>, MMEventListener<EnemyDeathEvent>, MMEventListener<LevelGoalEvent>, MMEventListener<EnemySpawnEvent>,
    MMEventListener<LevelProgressEvent> {
    
    Dictionary<string, LevelGoal> _goals = new Dictionary<string, LevelGoal>();
    List<LevelGoalType> _goalTypes = new List<LevelGoalType>();

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start) {
            _goals.Clear();
            _goalTypes.Clear();

            LevelGoalReward goalReward = e.Data.Goal;
            if (goalReward != null && !goalReward.Completed) {
                LevelGoal goal = goalReward.goal;
                goal.Setup();
                _goals.Add(string.Format("LevelGoal{0}", goal.Type), goal);
            }
            
            Unsubscribe();
            Subscribe();
        }
    }

    void CompleteGoal(LevelGoal goal) {
        goal.SetAchieved(true);
        LevelGoalResultEvent.Trigger(EventStage.Start, goal);
        Unsubscribe();
        Subscribe();
    }

    void ManageGoal(string id, int value) {
        if (_goals.ContainsKey(id)) {
            LevelGoal goal = _goals[id];
            SetGoalValue(goal, MathAction.Add, value);

            if (goal.CanBeCompleted)
                CompleteGoal(goal);

            LevelGoalProgressEvent.Trigger(EventStage.End, id, goal.CurrentAmount, goal.Amount);
        }
    }

    void ManageGoals(LevelGoalType goalType, MathAction action, int value, string id = "") {
        foreach (var pair in _goals) {
            LevelGoal goal = pair.Value;

            if (goal.Suitable(goalType, id)) {
                SetGoalValue(goal, action, value);

                if (goal.CanBeCompleted)
                    CompleteGoal(goal);

                LevelGoalProgressEvent.Trigger(EventStage.End, pair.Key, goal.CurrentAmount, goal.Amount);
            }
        }

    }

    void SetGoalValue(LevelGoal goal, MathAction action, int value) {
        goal.SetCurrentAmount(goal.CurrentAmount.ApplyMathAction(action, value));
    }

    public void OnMMEvent(EnemySpawnEvent e) {
        if (e.EventType == EnemySpawnEventType.Cleared)
            ManageGoals(LevelGoalType.Survive, MathAction.Set, int.MaxValue);
    }

    public void OnMMEvent(EarnResourceEvent e) {
        if (e.Source == ResourceSource.Gameplay && e.Stage == EventStage.Process)
            ManageGoals(LevelGoalType.CollectResource, MathAction.Add, e.Value, e.Resource.ToString());
    }

    public void OnMMEvent(LevelGoalProgressEvent e) {
        if (e.Stage == EventStage.Start)
            ManageGoal(e.GoalId, e.Progress);
    }

    public void OnMMEvent(ItemEvent e) {
        if (e.Stage == EventStage.Start && e.Type == ItemEventType.Pick)
            ManageGoals(LevelGoalType.Inventory, MathAction.Add, 1, e.Value.name);
    }

    public void OnMMEvent(EnemyDeathEvent e) {
        if (e.Stage == EventStage.Start && e.Enemy != null) {
            string enemyName = e.Enemy.EnemyName;
            EnemyType enemyType = e.Enemy.Type;
            if (enemyName.Length > 0) {
                ManageGoals(LevelGoalType.EnemyType, MathAction.Add, 1, enemyType.ToString());
                ManageGoals(LevelGoalType.Enemy, MathAction.Add, 1, enemyName);
            }
        }
    }

    public void OnMMEvent(LevelGoalEvent e) {
        ManageGoals(e.Type, MathAction.Add, e.Amount);
    }

    public void OnMMEvent(LevelProgressEvent e) {
        ManageGoals(LevelGoalType.Distance, MathAction.Set, Mathf.RoundToInt(e.Distance));
    }

    void OnEnable() {
        Subscribe();
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<LevelGoalProgressEvent>();
    }

    void Subscribe() {
        foreach (var pair in _goals) {
            LevelGoal goal = pair.Value;
            if (!goal.Achieved)
                SubscribeByGoalType(goal.Type);
        }
    }

    void SubscribeByGoalType(LevelGoalType goalType) {
        if (!_goalTypes.Contains(goalType)) {
            if (goalType == LevelGoalType.Survive) 
                this.MMEventStartListening<EnemySpawnEvent>();
            else if (goalType == LevelGoalType.CollectResource)
                this.MMEventStartListening<EarnResourceEvent>();
            else if (goalType == LevelGoalType.Inventory)
                this.MMEventStartListening<ItemEvent>();
            else if (goalType == LevelGoalType.Enemy || goalType == LevelGoalType.EnemyType)
                this.MMEventStartListening<EnemyDeathEvent>();
            else if (goalType == LevelGoalType.Distance)
                this.MMEventStartListening<LevelProgressEvent>();
            _goalTypes.Add(goalType);
        }
    }

    void Unsubscribe() {
        _goalTypes.Clear();
        this.MMEventStopListening<EarnResourceEvent>();
        this.MMEventStopListening<ItemEvent>();
        this.MMEventStopListening<LevelGoalEvent>();
        this.MMEventStopListening<EnemyDeathEvent>();
        this.MMEventStopListening<EnemySpawnEvent>();
        this.MMEventStopListening<LevelProgressEvent>();
    }

    void OnDisable() {
        Unsubscribe();
        this.MMEventStopListening<LevelGoalProgressEvent>();
        this.MMEventStopListening<LevelLoadEvent>();
    }


}
