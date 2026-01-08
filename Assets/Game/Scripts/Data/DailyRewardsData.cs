using System.Collections.Generic;
using UnityEngine;

public class DailyReward : IRewardItem {
    public Reward reward;
    
    public RewardState State { get; set; }

    public List<Reward> Rewards => new List<Reward>() { reward };
    public RewardItemType Type => RewardItemType.DailyReward;
    public bool Completed => State >= RewardState.Ready;

    public List<Reward> GetRewards(RewardType rewardType) {
        List<Reward> result = new List<Reward>();
        if (rewardType == RewardType.Any || reward.Type == rewardType)
            result.Add(reward);
        return result;
    }

    public bool SetState(RewardState value) {
        if (State != value) {
            State = value;
            return true;
        }
        return false;
    }
}

[System.Serializable]
public class DailyRewardsStateData {
    public long lastClaimed;
    public List<RewardState> states = new();
}

[CreateAssetMenu(fileName = "New DailyRewardsData", menuName = "Game/DailyRewardsData", order = 24)]
public class DailyRewardsData : GameSettings, IRewardList {
    public List<DailyReward> rewards = new();

    public long LastClaimed { get; private set; }

    public List<Reward> GetRewards(RewardType rewardType) {
        List<Reward> result = new List<Reward>();
        rewards.ForEach(t => result.AddRange(t.GetRewards(rewardType)));
        return result;
    }

    public Reward Claim(int day) {
        LastClaimed = System.DateTime.Now.ToBinary();
        if (day >= 0 && day < rewards.Count) {
            DailyReward dailyReward = rewards[day];
            dailyReward?.SetState(RewardState.Taken);
            return dailyReward.reward;
        }
        return null;
    }

    public override void Reset() {
        LastClaimed = 0;
        rewards.ForEach(r => r.State = RewardState.Locked);
        if (rewards.Count > 0)
            rewards[0].State = RewardState.Ready;
    }

    public override void Load(GameData data) {
        LastClaimed = data.dailyRewards.lastClaimed;
        for (int i = 0; i < rewards.Count; i++) {
            RewardState state = data.dailyRewards.states[i];
            rewards[i]?.SetState(state);
        }
    }

    public override void Save(GameData data) {
        data.dailyRewards.lastClaimed = LastClaimed;
        rewards.ForEach(r => data.dailyRewards.states.Add(r.State));
    }
}
