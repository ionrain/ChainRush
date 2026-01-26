using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public class UnitList : ItemList<AllUnitsData, UnitItem>, MMEventListener<UnitEvent> {
    [Header("Unit List")]
    [SerializeField] protected UnitType unitType = UnitType.Normal;
    [SerializeField] protected UnitListType listType = UnitListType.All;
    [SerializeField] protected bool reactToUnitStateChange;
    [SerializeField] protected bool autoSelectNewElement;
    [SerializeField] protected List<UnitState> priorityStates = new();
    [SerializeField] protected UnitDataEvent OnAction;

    protected UnitData _lastSelected;

    protected override void InstantiateItems() {
        var list = data.Get(unitType, listType);
        int count = list.Count;
        if (count > 0) {
            UnitItem selected = null;
            for (int i = 0; i < count; i++) {
                UnitData unitData = list[i];
                InstantiateItem(unitData);
                if (selectable && selected == null && unitData == _lastSelected)
                    selected = _items[_items.Count - 1];
            }

            if (selectable && selectDefault) {
                if (selected == null)
                    selected = GetInitialSelected();
                selected.OnClicked();
            }
        }
        base.InstantiateItems();
    }

    protected virtual UnitItem InstantiateItem(UnitData unitData) {
        UnitItem item = Instantiate(prefab, root);
        item.OnClick += OnItemClick;
        item.Setup(unitData);
        _items.Add(item);
        return item;
    }

    protected virtual UnitItem GetInitialSelected() {
        if (priorityStates != null && priorityStates.Count > 0) {
            foreach (UnitState state in priorityStates) {
                UnitItem item = _items.Find(t => t.Data != null && t.Data.State == state);
                if (item != null)
                    return item;
            }
        }
        return _items[0];
    }

    protected override void OnItemClick(UnitItem item) {
        if (item != null) {
            if (selectable)
                _lastSelected = item.Data;
            OnAction?.Invoke(item.Data);
        }
        base.OnItemClick(item);
    }

    protected bool MatchesListCriteria(UnitData unitData) {
        if (unitData == null || !unitType.HasFlag(unitData.type))
            return false;

        return listType switch {
            UnitListType.All => true,
            UnitListType.Unlocked => unitData.Unlocked,
            UnitListType.Available => unitData.State == UnitState.Available,
            UnitListType.Selected => unitData.State == UnitState.Selected,
            UnitListType.NotSelected => unitData.State != UnitState.Selected,
            _ => false
        };
    }

    protected UnitItem FindItem(UnitData unitData) {
        return _items.Find(t => t.Data == unitData);
    }

    public void OnMMEvent(UnitEvent e) {
        if (!reactToUnitStateChange || root == null || e.Stage != EventStage.End || e.Data == null)
            return;

        if (e.Type != UnitEventType.ChangeState)
            return;

        bool shouldBeInList = MatchesListCriteria(e.Data);
        UnitItem existingItem = FindItem(e.Data);
        bool isInList = existingItem != null;

        if (shouldBeInList && !isInList) {
            if (fillBlanks && _blanks.Count > 0) {
                Destroy(_blanks[_blanks.Count - 1]);
                _blanks.RemoveAt(_blanks.Count - 1);
            }
            var item = InstantiateItem(e.Data);
            if (autoSelectNewElement && selectable)
                OnItemClick(item);
            _items[_items.Count - 1].transform.SetSiblingIndex(_items.Count - 1);
            InstatiateBlanks();
        }
        else if (!shouldBeInList && isInList) {
            existingItem.OnClick -= OnItemClick;
            _items.Remove(existingItem);
            Destroy(existingItem.gameObject);
            InstatiateBlanks();
        }
    }

    protected virtual void OnEnable() {
        this.MMEventStartListening<UnitEvent>();
    }

    protected virtual void OnDisable() {
        this.MMEventStopListening<UnitEvent>();
    }
}
