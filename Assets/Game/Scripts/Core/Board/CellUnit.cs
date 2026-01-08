using System.Collections.Generic;
using UnityEngine;

public class CellUnit : CellItem {
    [SerializeField] AllUnitsData units;

    UnitData _data;

    public override void Setup(Vector2Int position, object param) {
        base.Setup(position, param);
        if (units != null) {
            List<UnitData> selected = units.Get(UnitListType.Selected);
            if (selected.Count == 1) {
                _data = selected[Random.Range(0, selected.Count)];
                name = string.Format("CellUnit_{0}_{1}x{2}", _data.name, position.x, position.y);
                if (image != null)
                    image.sprite = _data.Icon;
            }
        }
    }

    public override void SetVisible(bool value) {
        if (value)
            PartyUnitEvent.Trigger(_data != null ? EventStage.Start : EventStage.Process, PartyUnitEventType.Create, _data);
        base.SetVisible(value);
    }
}
