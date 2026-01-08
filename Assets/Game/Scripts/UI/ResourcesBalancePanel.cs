using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

public class ResourcesBalancePanel : SerializedMonoBehaviour, MMEventListener<BalanceResourcesEvent> {
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

    void OnEnable() {
        this.MMEventStartListening<BalanceResourcesEvent>();
        
    }

    void OnDisable() {
        this.MMEventStopListening<BalanceResourcesEvent>();
    }
}
