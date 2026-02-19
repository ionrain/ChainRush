
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public class RewardList: ItemList<IRewardList, RewardListItem> {
    [SerializeField] RewardsData rewardsData;
    [SerializeField] LocalizedString indexPattern;
    
    protected override void InstantiateItems() {
        string pattern = indexPattern != null && !indexPattern.IsEmpty ? indexPattern.GetLocalizedString() : "{0}";
        if (rewardsData != null) {
            List<Reward> rewards = data.GetRewards();
            for (int i = 0; i < rewards.Count; i++) {
                Reward reward = rewards[i];
                Sprite icon = rewardsData.GetIcon(reward);
                RewardListItem item = Instantiate(prefab, root);
                item.Setup(new RewardListItemData(i, icon, reward.Amount, string.Format(pattern, i + 1)));
            }
            base.InstantiateItems();
        } else
            Debug.LogError("RewardList InstantiateItems: RewardsData is NULL");
    }
}
