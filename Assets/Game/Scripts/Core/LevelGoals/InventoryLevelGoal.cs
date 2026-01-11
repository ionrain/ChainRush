using System.Collections.Generic;
using UnityEngine;

public class InventoryLevelGoal : LevelGoal {
    [SerializeField] ItemData item;

    public ItemData Item => item;
    public override bool IsValid => base.IsValid && item != null && item.owner != ItemOwner.Unit && item.owner != ItemOwner.Inventory;

    public InventoryLevelGoal() {
        goalType = LevelGoalType.Inventory;
    }

    public override void Setup() {
        base.Setup();
        if (item != null)
            item.Transferable = false;
    }

    public override void Complete() {
        if (IsValid) {
            item.Transferable = true;
            item.SetOwner(ItemOwner.Inventory);
        }
    }

    public override bool Suitable(LevelGoalType targetGoalType, string id) {
        return !Achieved && goalType == targetGoalType && item != null && item.name.Equals(id);
    }
}
