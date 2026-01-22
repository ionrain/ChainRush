using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.Utilities;
using UnityEngine;

public class RewardManager : MMSingleton<RewardManager>, MMEventListener<RewardEvent> {
    [SerializeField] protected AllUnitsData units;

    public void OnMMEvent(RewardEvent e) {
        if (e.Stage == EventStage.Start && e.Type == RewardEventType.Transfer && e.Item != null ) {
            string source = e.Item.Type.ToString();
            List<Reward> rewards = new List<Reward>(e.Item.Rewards);
            Dictionary<UnitType, int> cards = new();
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
                    } else if (reward.Type == RewardType.Resource && reward is ResourceReward resourceUnitCardReward && resourceUnitCardReward.Resource == ResourceType.UnitCard) {
                        rewards.Remove(reward);
                        cards[UnitType.Normal] = cards.GetValueOrDefault(UnitType.Normal) + reward.Amount;
                        rewards.AddRange(GenerateUnitCardReward(UnitType.Normal, reward.Amount));
                    } else if (reward.Type == RewardType.Resource &&  reward is ResourceReward resourceHeroCardReward && resourceHeroCardReward.Resource == ResourceType.HeroCard) {
                        rewards.Remove(reward);
                        cards[UnitType.Hero] = cards.GetValueOrDefault(UnitType.Hero) + reward.Amount;
                    }
                }
            }

            foreach (var pair in cards)
                rewards.AddRange(GenerateUnitCardReward(pair.Key, pair.Value));

            e.Item.Rewards.Clear();
            e.Item.Rewards.AddRange(rewards);
            RewardEvent.Trigger(EventStage.End, e.Type, e.Item);
        }
    }

    List<UnitCardReward> GenerateUnitCardReward(UnitType unitType, int amount) {   
        List<UnitCardReward> result = new ();
        List<UnitData> unlocked = units.Get(unitType, UnitListType.Unlocked);
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

        foreach (var reward in result) {
            reward.TransferCards();;
            UnitEvent.Trigger(EventStage.End, UnitEventType.CardBalanceChange, reward.Unit);
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
