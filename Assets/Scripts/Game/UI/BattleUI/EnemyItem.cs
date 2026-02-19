using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyItem : ListItem<EnemyItem, EnemyData> {
    public override void Setup(EnemyData data) {
        base.Setup(data);
        if (_data != null) {
            if (icon != null)
                icon.sprite = _data.icon;
        } else
            Debug.LogFormat("EnemyItem Setup: EnemyData is NULL for {0}", gameObject.name);
    }
}
