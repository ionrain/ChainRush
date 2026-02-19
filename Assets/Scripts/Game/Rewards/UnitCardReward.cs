using UnityEngine;

public class UnitCardReward : Reward {
    public UnitData Unit { get; private set; } 

    public UnitCardReward() {
        rewardType = RewardType.UnitCard;
    }

    public UnitCardReward(UnitData unitData, int cardsAmount) {
        rewardType = RewardType.UnitCard;
        Unit = unitData;
        amount = cardsAmount;
    }

    public void TransferCards() {
        if (IsValid)
            Unit.AddCards(amount);
    }
}
