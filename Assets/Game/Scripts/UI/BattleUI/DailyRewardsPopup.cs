using System.Collections.Generic;
using UnityEngine;

public class DailyRewardsPopup : Popup<DailyRewardsData> {
    const string DailyReward = "DailyReward";

    [Header("Daily Rewards Popup")]
    [SerializeField] RewardList rewardList;

    public override bool Setup(DailyRewardsData value) {
        if (base.Setup(value) && rewardList != null) {
            rewardList.Setup(data);
            rewardList.OnClick += OnRewardClick;
            return true;
        }
        return false;
    }

    public void OnRewardClick(RewardListItem item) {
        if (item != null && item.Data != null)
            RewardEvent.Trigger(EventStage.Start, RewardEventType.Transfer, 
                                 new RewardItem(RewardItemType.DailyReward, new List<Reward>() { data.Claim(item.Data.Index) }));
    }

    public override void SetVisibility(bool visible) {
        if (!visible || Setup())
            base.SetVisibility(visible);
    }
}
