using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

public class ResourcesBalancePanel : SerializedMonoBehaviour, MMEventListener<BalanceResourcesEvent>, MMEventListener<UnitEvent> {
    [SerializeField] Dictionary<ResourceType, IconTextItem> items = new Dictionary<ResourceType, IconTextItem>();
    [SerializeField] RectTransform itemsRoot;
    [SerializeField] ResourcesData resourcesData;
    [SerializeField] IconTextItem prefab;

    string _soloPattern = "{0}";
    string _capacityPattern = "{0}/{1}";

    void Start() {
        bool canInstantiate = itemsRoot != null && resourcesData != null && prefab != null;
        foreach (var pair in items) {
            if (pair.Value == null && canInstantiate) {
                IconTextItem item = Instantiate(prefab, itemsRoot);
                items[pair.Key] = item;
                ResourceData data = resourcesData.Get(pair.Key);
                if (data != null)
                    item.Setup(data.icon, string.Empty);
            }
        }
        RequestBalance(new List<ResourceType>(items.Keys));
    }

    void RequestBalance(List<ResourceType> balanceList) {
        BalanceResourcesEvent.Trigger(EventStage.Start, balanceList);
    }

    public void OnMMEvent(BalanceResourcesEvent e) {
        if (e.Stage == EventStage.End) {
            foreach (var balance in e.Balance) {
                IconTextItem item = items.GetValueOrDefault(balance.Key, null);
                if (item != null) {
                    var t = balance.Value;
                    item.SetText(string.Format(t.Capacity > 0 ? _capacityPattern : _soloPattern, t.Value.ToShortString(), t.Capacity.ToShortString()));
                }
            }
        }
    }

    public void OnMMEvent(UnitEvent e) {
        if (e.Data != null)
            UpdateCardBalance(e.Data);
    }

    public void UpdateCardBalance(UnitData data) {
        ResourceType resourceType = data.type == UnitType.Hero ? ResourceType.HeroCard : ResourceType.UnitCard;
        IconTextItem item = items.GetValueOrDefault(resourceType, null);

        if (item != null)
            item.SetText(string.Format(_soloPattern, data.CardBalance.ToShortString()));
    }

    void OnEnable() {
        this.MMEventStartListening<BalanceResourcesEvent>();
        this.MMEventStartListening<UnitEvent>();
        
    }

    void OnDisable() {
        this.MMEventStopListening<BalanceResourcesEvent>();
        this.MMEventStopListening<UnitEvent>();
    }
}
