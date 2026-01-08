using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyList : ItemList<AllEnemiesData, EnemyItem> {
    protected override void InstantiateItems() {
        var list = data.GetUnlocked();
        int count = list.Count;
        if (count > 0) {
            EnemyItem selected = null;
            for (int i = 0; i < count; i++) {
                EnemyData enemyData = list[i];
                EnemyItem item = Instantiate(prefab, root);
                item.OnClick += OnItemClick;
                item.Setup(enemyData);
                _items.Add(item);
                if (selectable && selected == null)
                    selected = item;
            }

            if (selectable) {
                selected = _items[0];
                selected.OnClicked();
            }
        }
        base.InstantiateItems();
    }
}
