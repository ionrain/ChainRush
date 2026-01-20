using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public class RewardManager : MMSingleton<RewardManager>, MMEventListener<RewardEvent> {
    [SerializeField] protected AllUnitsData units;

    public void OnMMEvent(RewardEvent e) {
        if (e.Stage == EventStage.Start && e.Type == RewardEventType.Transfer && e.Item != null ) {
            string source = e.Item.Type.ToString();
            List<Reward> rewards = new List<Reward>(e.Item.Rewards);
            foreach (Reward reward in e.Item.Rewards) {
                if (reward.IsValid) {
                    if (reward.Type == RewardType.Resource && reward is ResourceReward resourceReward && resourceReward.Resource != ResourceType.UnitCard) {
                        EarnResourceEvent.Trigger(EventStage.Start, resourceReward.Resource, ResourceSource.Reward, source, resourceReward.Amount);
                    } if (reward.Type == RewardType.InventoryItem && reward is InventoryReward inventoryReward ) {
                        inventoryReward.Item.SetOwner(ItemOwner.Inventory);
                        ItemEvent.Trigger(EventStage.Start, ItemEventType.Get, inventoryReward.Item);
                    } else if (reward.Type == RewardType.Unit && reward is UnitReward unitReward) {
                        unitReward.Unit.SetState(UnitState.ReadyToBeUnlocked);
                        UnitEvent.Trigger(EventStage.Start, UnitEventType.ChangeState, unitReward.Unit);
                    } else if ((reward.Type == RewardType.UnitCard && reward is UnitCardReward) || (reward.Type == RewardType.Resource && 
                                reward is ResourceReward resourceCardReward && resourceCardReward.Resource == ResourceType.UnitCard)) {
                        rewards.Remove(reward);
                        List<UnitCardReward> cardRewards = GenerateUnitCardReward(reward.Amount);
                        foreach (var cardReward in cardRewards) {
                            cardReward.TransferCards();;
                            UnitEvent.Trigger(EventStage.End, UnitEventType.CardBalanceChange, cardReward.Unit);
                        }
                        rewards.AddRange(cardRewards);
                    }                        
                }
            }
            e.Item.Rewards.Clear();
            e.Item.Rewards.AddRange(rewards);
            RewardEvent.Trigger(EventStage.End, e.Type, e.Item);
        }
    }

    List<UnitCardReward> GenerateUnitCardReward(int amount) {   
        List<UnitCardReward> result = new ();
        List<UnitData> unlocked = units.Get(units.upgradable, UnitListType.Unlocked);
        unlocked.Sort((a, b) => a.CardBalance.CompareTo(b.CardBalance));        
        if (unlocked.Count <= 1 || amount < 5) {
            result.Add(new UnitCardReward(unlocked[0], amount));
        } else {
            int total = unlocked[0].CardBalance + unlocked[1].CardBalance;
            float probability = total > 0 ? (float)unlocked[0].CardBalance / total : 0.5f;
            int unlocked0Cards = 0;
            int unlocked1Cards = 0;
            for (int i = 0; i < amount; i++) {
                if (Random.value > probability)
                    unlocked0Cards++;
                else
                    unlocked1Cards++;
            }
            if (unlocked0Cards > 0)
                result.Add(new UnitCardReward(unlocked[0], unlocked0Cards));
            if (unlocked1Cards > 0)
                result.Add(new UnitCardReward(unlocked[1], unlocked1Cards));
        }

        return result;
    }

    void OnEnable() {
        this.MMEventStartListening<RewardEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<RewardEvent>();
    }
}
