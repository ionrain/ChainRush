using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public class SelectUnitPopup : Popup<AllUnitsData>, MMEventListener<PartyUnitEvent> {
    [SerializeField] UnitItem itemPrefab;
    [SerializeField] Transform itemRoot;

    public override bool Setup(AllUnitsData value) {
        if (base.Setup(value) && itemPrefab != null && itemRoot != null) {
            itemRoot.DestroyChildren();
            List<UnitData> selected = data.Get(UnitListType.Selected);
            if (selected.Count > 0) {
                foreach (UnitData unit in selected) {
                    UnitItem item = Instantiate(itemPrefab, itemRoot);
                    item.Setup(unit);
                    item.OnClick += OnItemClick;
                }
                return true;
            }
        }
        return false;
    }

    void OnItemClick(UnitItem item) {
        PartyUnitEvent.Trigger(EventStage.Start, PartyUnitEventType.Create, item.Data);
        SetVisibility(false);
    }

    public void OnMMEvent(PartyUnitEvent e) {
        if (e.Type == PartyUnitEventType.Create && e.EventStage == EventStage.Process && Setup())
            SetVisibility(true);
    }

    void OnEnable() {
        this.MMEventStartListening<PartyUnitEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<PartyUnitEvent>();
    }
}
