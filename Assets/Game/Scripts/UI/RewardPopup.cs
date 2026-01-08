using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;

public class RewardPopup : Popup<IRewardList>, MMEventListener<RewardEvent> {
    [SerializeField] protected UnityEvent OnRewardsShown;

    [Header("Reward Popup")]
    [SerializeField] protected float initialDelay;
    [SerializeField] protected float spawnCooldown;
    [SerializeField] protected Transform itemsRoot;
    [SerializeField] protected Transform unitRewardRoot;
    [SerializeField] protected RewardsData rewardsData;
    [SerializeField] protected Dictionary<RewardType, IconTextItem> itemPrefabs = new Dictionary<RewardType, IconTextItem>();
    [SerializeField] protected LocalizedString inventoryPattern;
    [SerializeField] protected bool spawnUnitRewardSeparately;

    protected string _inventoryPattern = "{0}";

    public void OnMMEvent(RewardEvent e) {
        if (e.Type == RewardEventType.Transfer && e.Stage == EventStage.End && e.Item != null && Setup(e.Item))
            SetVisibility(true);
    }

    public override bool Setup(IRewardList value) {
        if (inventoryPattern != null && !inventoryPattern.IsEmpty)
            _inventoryPattern = inventoryPattern.GetLocalizedString();
        if (value != null && rewardsData != null && itemsRoot != null) {
            itemsRoot.DestroyChildren();
            if (unitRewardRoot != null)
                unitRewardRoot.DestroyChildren();            
            StartCoroutine(SpawnRewards(value.GetRewards(RewardType.Any)));
            return base.Setup(value);
        }
        return false;
    }

    protected virtual IEnumerator SpawnRewards(List<Reward> rewards) {
        yield return new WaitForSecondsRealtime(initialDelay);
        foreach (var reward in rewards) {
            IconTextItem prefab = itemPrefabs.ContainsKey(reward.Type) ? itemPrefabs[reward.Type] : null;
            if (prefab != null) {
                bool isUnitReward = reward.Type == RewardType.Unit && spawnUnitRewardSeparately && unitRewardRoot != null;                
                IconTextItem item = Instantiate(prefab, isUnitReward ? unitRewardRoot : itemsRoot);
                string text = reward.Type == RewardType.InventoryItem ? string.Format(_inventoryPattern,
                    ((InventoryReward)reward).Item.level + 1) : reward.Amount.ToShortString();
                item.Setup(rewardsData.GetIcon(reward), text);
                MMFeedbacks feedback = item.GetComponent<MMFeedbacks>();
                if (feedback != null)
                    feedback.PlayFeedbacks();
                yield return new WaitForSecondsRealtime(spawnCooldown);
                RewardFlyEvent.Trigger(EventStage.Start, reward, item.transform.position);
            }
        }
        OnRewardsShown?.Invoke();
    }

    protected virtual void OnEnable() {
        this.MMEventStartListening<RewardEvent>();
    }

    protected virtual void OnDisable() {
        this.MMEventStopListening<RewardEvent>();
    }
}
