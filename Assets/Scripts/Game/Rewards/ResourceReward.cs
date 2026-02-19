using UnityEngine;

public class ResourceReward : Reward {
    [SerializeField] protected ResourceType resource;

    public ResourceType Resource => resource;

    public ResourceReward(ResourceType resourceType, int resourceAmount) {
        rewardType = RewardType.Resource;
        amount = resourceAmount;
        resource = resourceType;
    }

    public ResourceReward() {
        rewardType = RewardType.Resource;
    }
}
