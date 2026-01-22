using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UnitStateAction : MonoBehaviour {
    [SerializeField] AllUnitsData units;
    [SerializeField] UnitState state;

    [Header("Effect for other units")]
    [SerializeField] bool affectOthers;
    [SerializeField] UnitState otherState;
    [SerializeField] List<UnitState> excludeStates = new();

    [Header("Events")]
    [SerializeField] UnityEvent OnSuccess;
    [SerializeField] UnityEvent OnFail;

    public void Trigger(UnitData data) {
        if (data != null && units != null) {
            bool result = units.TrySetState(data, state);

            if (affectOthers) {
                List<UnitData> allUnits = units.Get(data.type, UnitListType.All);
                foreach (UnitData unit in allUnits) {
                    if (unit != data && !excludeStates.Contains(unit.State))
                        units.TrySetState(unit, otherState);
                }
            }

            if (result)
                OnSuccess?.Invoke();
            else
                OnFail?.Invoke();
        }
    }
}