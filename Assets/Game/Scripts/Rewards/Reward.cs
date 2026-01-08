using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public enum RewardEventType { Show, Transfer }
public enum RewardItemType { LevelGoalReward, LevelReward, Loot, DailyReward, IdleReward }

public interface IRewardItem : IRewardList {
    public RewardState State { get; }
    public List<Reward> Rewards { get; }
    public RewardItemType Type { get; }

    public bool SetState(RewardState state);
}

public interface IRewardList {
    public List<Reward> GetRewards(RewardType rewardType = RewardType.Any);
}

public class RewardItem : IRewardItem {
    [SerializeField] protected RewardItemType type;
    [SerializeField] protected List<Reward> rewards = new();

    public RewardState State { get; protected set; }
    public List<Reward> Rewards => rewards;
    public RewardItemType Type => type;

    public RewardItem(RewardItemType itemType, List<Reward> rewardList, RewardState state = RewardState.Locked) {
        type = itemType;
        rewards = rewardList;
        State = state;
    }

    public bool SetState(RewardState state) {
        if (State != state) {
            State = state;
            return true;
        }
        return false;
    }

    public List<Reward> GetRewards(RewardType rewardType = RewardType.Any) {
        List<Reward> result = new();
        if (rewards != null) {
            if (rewardType != RewardType.Any)
                result.AddRange(rewards?.FindAll(t => t.Type == rewardType));
            else
                result.AddRange(rewards);
        }
        return result;
    }
}

public struct RewardEvent {
    public EventStage Stage { get; private set; }
    public RewardEventType Type { get; private set; }
    public IRewardItem Item { get; private set; }
    public string Source { get; private set;}

    static RewardEvent e;
    public static void Trigger(EventStage stage, RewardEventType eventType, IRewardItem item, string source = "") {
        e.Source = source;
        e.Type = eventType;
        e.Stage = stage;
        e.Item = item;
        MMEventManager.TriggerEvent(e);
    }
}

public enum RewardType { Any, Resource, InventoryItem, Unit, UnitCard }
public enum RewardState { Locked, Unlocked, Ready, Taken }

public class Reward {
    [HideInInspector]
    [SerializeField] protected RewardType rewardType;
    [SerializeField] protected int amount;

    public virtual bool IsValid => amount > 0;
    public RewardType Type => rewardType;
    public int Amount => amount;

    public void SetAmount(int value) {
        amount = value;
    }
}