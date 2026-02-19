using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryReward : Reward {
    [SerializeField] protected ItemData item;

    public ItemData Item => item;
    public override bool IsValid => base.IsValid && item != null;

    public InventoryReward() {
        rewardType = RewardType.InventoryItem;
    }

    public InventoryReward(ItemData itemData) {
        rewardType = RewardType.InventoryItem;
        item = itemData;
    }
}
