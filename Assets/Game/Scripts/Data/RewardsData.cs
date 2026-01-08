using UnityEngine;

[CreateAssetMenu(fileName = "New RewardsData", menuName = "Game/RewardsData", order = 19)]
public class RewardsData : ScriptableObject {
    public ResourcesData resourcesData;

    public Sprite GetIcon(Reward reward) {
        if (reward.Type == RewardType.Resource && reward is ResourceReward resourceReward && resourcesData != null) {
            ResourceData resourceData = resourcesData.Get(resourceReward.Resource);
            if (resourceData != null)
                return resourceData.icon;
        } else if (reward.Type == RewardType.InventoryItem && reward is InventoryReward inventoryReward && inventoryReward.Item != null)
            return inventoryReward.Item.Icon;
        else if (reward.Type == RewardType.Unit && reward is UnitReward unitReward && unitReward.Unit != null)
            return unitReward.Unit.Icon;
        else if (reward.Type == RewardType.UnitCard && reward is UnitCardReward unitCardReward && unitCardReward.Unit != null)
            return unitCardReward.Unit.Icon;
        return null;
    }
}
