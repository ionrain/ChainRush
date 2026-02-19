using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitReward : Reward {
    [SerializeField] protected UnitData unit;

    public UnitData Unit => unit;
    public override bool IsValid => base.IsValid && unit != null;

    public UnitReward() {
        rewardType = RewardType.Unit;
    }

    public UnitReward(UnitData unitData) {
        rewardType = RewardType.Unit;
        unit = unitData;
    }
}
