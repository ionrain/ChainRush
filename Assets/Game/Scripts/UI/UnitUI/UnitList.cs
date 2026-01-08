using UnityEngine;

public class UnitList : ItemList<AllUnitsData, UnitItem> {
    [Header("Unit List")]
    [SerializeField] protected UnitListType listType = UnitListType.All;

    protected UnitData _lastSelected;

    protected override void InstantiateItems() {
        var list = data.Get(listType);
        int count = list.Count;
        if (count > 0) {
            UnitItem selected = null;
            for (int i = 0; i < count; i++) {
                UnitData unitoData = list[i];
                UnitItem item = Instantiate(prefab, root);
                item.OnClick += OnItemClick;
                item.Setup(unitoData);
                _items.Add(item);
                if (selectable && selected == null && unitoData == _lastSelected)
                    selected = item;
            }

            if (selectable) {
                if (selected == null)
                    selected = GetInitialSelected();
                selected.OnClicked();
            }
        }
        base.InstantiateItems();
    }

    protected virtual UnitItem GetInitialSelected() {
        return _items[0];
    }

    protected override void OnItemClick(UnitItem item) {
        if (item != null && selectable)
            _lastSelected = item.Data;
        base.OnItemClick(item);
    }
}