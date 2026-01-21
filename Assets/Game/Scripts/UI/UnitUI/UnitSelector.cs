using System.Collections.Generic;
using UnityEngine;

public class UnitSelector : MonoBehaviour {
    [SerializeField] AllUnitsData allUnitsData;
    [SerializeField] UnitListType listType = UnitListType.All;
    [SerializeField] UnitType unitType;
    [SerializeField] List<UnitState> priorityStates = new();
    [SerializeField] UnitDataEvent OnSelected;

    List<UnitData> _units = new();
    int _index = -1;

    public void Setup() {
        if (allUnitsData != null && priorityStates != null) {
            _units.Clear();
            _index = -1;
            _units = allUnitsData.Get(unitType, listType);
            foreach (UnitState state in priorityStates) {
                foreach (UnitData data in _units) {
                    if (data.State == state) {
                        Select(data);                        
                        return;
                    }
                }
            }
        } else
            Debug.LogError("UnitSelector Setup: allUnitsData or priorityStates is null for " + gameObject.name);
    }

    void Select(UnitData data) {
        if (data != null && _units != null) {
            _index = _units.IndexOf(data);
            OnSelected?.Invoke(data);
        }
    }

    public void Forward() {
        if (_units != null && _index >= 0) {
            if (_index < _units.Count - 1)
                _index++;
            else
                _index = 0;
            Select(_units[_index]);
        }
    }

    public void Backward() {
        if (_units != null && _index >= 0) {
            if (_index > 0)
                _index--;
            else
                _index = _units.Count - 1;
            Select(_units[_index]);
        }
    }
}
