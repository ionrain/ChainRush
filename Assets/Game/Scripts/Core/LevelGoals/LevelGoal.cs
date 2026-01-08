using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public enum LevelGoalType { Traps, Survive, Stage }

public struct LevelGoalEvent {
    public LevelGoalType Type { get; private set; }
    public int Amount { get; private set; }

    static LevelGoalEvent e;
    public static void Trigger(LevelGoalType goalType, int amount) {
        e.Type = goalType;
        e.Amount = amount;
        MMEventManager.TriggerEvent(e);
    }
}

public class LevelGoal {
    [SerializeField] protected LevelGoalType goalType;
    [SerializeField] protected int amount;

    public LevelGoalType Type => goalType;
    public bool Achieved { get; set; }
    public virtual int Amount => amount;
    public virtual int ContainersCount => 1;
    public virtual float MinContainerDistance => 20;
    public virtual List<int> Containers => null;
    public virtual bool CanBeCompleted => !Achieved && CurrentAmount >= Amount;
    public int CurrentAmount { get; set; }


    public LevelGoal() { }

    public LevelGoal(LevelGoalType goalType, int amount = 1) {
        this.amount = amount;
        this.goalType = goalType;
    }

    public virtual void Setup() {
        CurrentAmount = 0;
    }

    public virtual void Complete() { }

    public virtual bool Suitable(LevelGoalType targetGoalType, string id) {
        return !Achieved && goalType == targetGoalType;
    }
}