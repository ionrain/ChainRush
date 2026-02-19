using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class IdlePopup : Popup<ResourcesData> {
    [Header("Idle Popup")]
    [SerializeField] GameObject notification;
    [SerializeField] AllLocationsData locations;
    [SerializeField] Transform itemsRoot;
    [SerializeField] IconTextItem itemPrefab;
    [SerializeField] TextMeshProUGUI timeLabel;
    [SerializeField] LocalizedString timePattern;
    [SerializeField] Dictionary<ResourceType, TextMeshProUGUI> resourceLabels = new();
    [SerializeField] LocalizedString resourcePattern;

    string _resourcePattern = "{0} / h";
    string _timePattern = "{0}h {1:D2}m";
    int _completedLocations;


    protected override void Awake() {
        base.Awake();
        if (locations != null)
            _completedLocations = locations.CompletedCount;
        UpdateNotification();
        if (timePattern != null && !timePattern.IsEmpty)
            _timePattern = timePattern.GetLocalizedString();
        if (resourcePattern != null && !resourcePattern.IsEmpty)
            _resourcePattern = resourcePattern.GetLocalizedString();
    }

    void UpdateNotification() {
        if (notification != null)
            notification.SetActive(GetIdleResources().Count > 0);
    }

    Dictionary<ResourceType, int> GetIdleResources() {
        return data != null ? data.GetIdleResources(_completedLocations) : new Dictionary<ResourceType, int>();
    }

    void UpdateTime(int hours, int minutes) {
        if (timeLabel != null)
            timeLabel.SetText(string.Format(_timePattern, hours, minutes));
    }

    public override bool Setup(ResourcesData value) {
        if (base.Setup(value) && itemsRoot != null && itemPrefab != null) {
            var resources = GetIdleResources();

            itemsRoot.DestroyChildren();
            foreach (var pair in resources) {
                ResourceData resourceData = value.Get(pair.Key);
                if (resourceData != null) {
                    if (resourceLabels.TryGetValue(pair.Key, out TextMeshProUGUI label))
                        label?.SetText(string.Format(_resourcePattern, resourceData.GetProduction(_completedLocations)));
                    IconTextItem item = Instantiate(itemPrefab, itemsRoot);
                    item.Setup(resourceData.icon, pair.Value.ToShortString());
                }
            }

            var time = data.ProductionTimeSpan;
            UpdateTime((int)time.TotalHours, time.Minutes);

            return true;
        }
        return false;
    }

    public void TransferResources() {
        if (locations != null && data != null) {
            var resources = GetIdleResources();
            if (resources.Count > 0) {
                List<Reward> rewards = new();
                foreach (var pair in resources)
                    rewards.Add(new ResourceReward(pair.Key, pair.Value));
                data.ResetProduction();
                RewardEvent.Trigger(EventStage.Start, RewardEventType.Transfer, new RewardItem(RewardItemType.IdleReward, rewards));
            }

            if (itemsRoot != null)
                itemsRoot.DestroyChildren();

            UpdateTime(0, 0);
            UpdateNotification();
            SetVisibility(false);
        }
    }

    public override void SetVisibility(bool visible) {
        if (visible)
            Setup();
        base.SetVisibility(visible);
    }
}
