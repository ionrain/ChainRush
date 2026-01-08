using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEngine;

public class LevelGoalReward : IRewardItem {
    public LevelGoal goal;
    public List<Reward> rewards = new List<Reward>();
    
    public RewardState State { get; set; }

    public List<Reward> Rewards => rewards;
    public RewardItemType Type => RewardItemType.LevelGoalReward;
    public bool Completed => State >= RewardState.Ready;
    public bool Achieved => goal != null ? goal.Achieved : false;

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
            goal.Achieved = value;
            goal.CurrentAmount = 0;
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
    public EventStage EventStage {get; private set; }
    public LevelGoal Goal { get; private set; }

    static LevelGoalResultEvent e;
    public static void Trigger(EventStage eventStage, LevelGoal goal) {
        e.EventStage = eventStage;
        e.Goal = goal;
        MMEventManager.TriggerEvent(e);
    }
}

public class LevelGoalManager : SerializedMonoBehaviour, MMEventListener<LevelStageStateEvent>, MMEventListener<LevelStageProgressEvent>,
    MMEventListener<LevelGoalProgressEvent>, MMEventListener<LevelLoadEvent>, MMEventListener<LevelGoalEvent>, MMEventListener<CellEvent>,
    MMEventListener<EnemySpawnEvent>, MMEventListener<LevelResultEvent> {

    static string stageGoal = "StageGoal";

    Dictionary<string, LevelGoal> _goals = new Dictionary<string, LevelGoal>();
    List<LevelGoalType> _goalTypes = new List<LevelGoalType>();

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start) {
            ClearGoals();
            Subscribe();
        }
    }

    void ClearGoals() {
        _goals.Clear();
        _goalTypes.Clear();
        
        Unsubscribe();
    }

    public void OnMMEvent(LevelResultEvent e) {
        if (e.Result == LevelResult.Failure)
            ClearGoals();
    }

    public void OnMMEvent(LevelStageStateEvent e) {
        if (e.EventStage == EventStage.End && e.Data != null) {
            LevelStageStateData data = e.Data;
            if (e.State == LevelStageState.Start)
                _goals[stageGoal] = new LevelGoal(LevelGoalType.Traps, data.Stage.TrapsCount);
            else if (e.State == LevelStageState.Battle)
                _goals[stageGoal] = new LevelGoal(LevelGoalType.Survive);
        
            Unsubscribe();
            Subscribe();
        }
    }

    public void FinishCurrentGoals() {
        _goals.ForEach(t => FinishGoal(t.Value, true));
    }

    void FinishGoal(LevelGoalType goalType, bool result) {
        foreach (var pair in _goals) {
            if (pair.Value.Type == goalType) {
                FinishGoal(pair.Value, result);
                break;
            }
        }
    }

    void FinishGoal(LevelGoal goal, bool result) {
        goal.Achieved = result;
        LevelGoalResultEvent.Trigger(EventStage.Start, goal);
        Unsubscribe();
        Subscribe();
    }

    void ManageGoal(string id, int value) {
        if (_goals.ContainsKey(id)) {
            LevelGoal goal = _goals[id];
            SetGoalValue(goal, MathAction.Add, value);

            if (goal.CanBeCompleted)
                FinishGoal(goal, true);

            LevelGoalProgressEvent.Trigger(EventStage.End, id, goal.CurrentAmount, goal.Amount);
        }
    }

    void ManageGoals(LevelGoalType goalType, MathAction action, int value, string id = "") {
        foreach (var pair in _goals) {
            LevelGoal goal = pair.Value;

            if (goal.Suitable(goalType, id)) {
                SetGoalValue(goal, action, value);

                if (goal.CanBeCompleted)
                    FinishGoal(goal, true);

                LevelGoalProgressEvent.Trigger(EventStage.End, pair.Key, goal.CurrentAmount, goal.Amount);
            }
        }
    }

    void SetGoalValue(LevelGoal goal, MathAction action, int value) {
        goal.CurrentAmount = goal.CurrentAmount.ApplyMathAction(action, value);
    }

    public void OnMMEvent(LevelStageProgressEvent e) {
        ManageGoals(LevelGoalType.Survive, MathAction.Set, e.Time);
    }

    public void OnMMEvent(LevelGoalProgressEvent e) {
        if (e.Stage == EventStage.Start)
            ManageGoal(e.GoalId, e.Progress);
    }

    public void OnMMEvent(LevelGoalEvent e) {
        ManageGoals(e.Type, MathAction.Add, e.Amount);
    }

    public void OnMMEvent(CellEvent e) {
        if (e.Type != CellEventType.Tap && e.Cell.Type == CellType.Trap && e.Cell.Item != null && e.Cell.Item is CellTrap cellTrap) {
            if (e.Type == CellEventType.Open && cellTrap.TrapType == TrapType.Alarm)
                FinishGoal(LevelGoalType.Traps, false);
            else
                ManageGoals(LevelGoalType.Traps, MathAction.Add, 1);                
        }
    }

    public void OnMMEvent(EnemySpawnEvent e) {
        if (e.EventType == EnemySpawnEventType.Cleared)
            ManageGoals(LevelGoalType.Survive, MathAction.Add, 1);
    }    

    void OnEnable() {
        this.MMEventStartListening<LevelResultEvent>();
        Subscribe();
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<LevelStageStateEvent>();
        this.MMEventStartListening<LevelGoalEvent>();
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
            else if (goalType == LevelGoalType.Traps)
                this.MMEventStartListening<CellEvent>();
            _goalTypes.Add(goalType);
        }
    }

    void Unsubscribe() {
        _goalTypes.Clear();
        this.MMEventStopListening<LevelStageProgressEvent>();
        this.MMEventStopListening<CellEvent>();
        this.MMEventStopListening<EnemySpawnEvent>(); 

    }

    void OnDisable() {
        Unsubscribe();
        this.MMEventStopListening<LevelResultEvent>(); 
        this.MMEventStopListening<LevelGoalEvent>();
        this.MMEventStopListening<LevelGoalProgressEvent>();
        this.MMEventStopListening<LevelStageStateEvent>();
        this.MMEventStopListening<LevelLoadEvent>();
    }
}
