using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

public class UnitPanelList : UnitList, MMEventListener<BalanceResourcesEvent> {
    [Header("Unit Panel List")]
    [SerializeField] protected MMF_Player selectSFX;

    public void OnMMEvent(BalanceResourcesEvent e) {
        if (e.Stage == EventStage.End) {
            int soft = e.Balance.GetValueOrDefault(ResourceType.SoftCurrency).Value;
            foreach (UnitItem item in _items)
                item.CheckNotification(soft);
        }
    }

    protected override void OnItemClick(UnitItem item) {
        selectSFX?.PlayFeedbacks();
        if (item != null)
            UnitEvent.Trigger(EventStage.Start, UnitEventType.Select, item.Data);
        base.OnItemClick(item);
    }

    protected override UnitItem GetInitialSelected() {
        UnitItem result = _items.Find(t => t.Data.State == UnitState.ReadyToBeUnlocked);
        if (result != null)
            return result;
        return base.GetInitialSelected();
    }

    void OnEnable() {
        this.MMEventStartListening<BalanceResourcesEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<BalanceResourcesEvent>();
    }
}
