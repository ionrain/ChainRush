using System;
using UnityEngine;

public class UnitDataAction : MonoBehaviour {
    [SerializeField] bool sendEvent;
    [SerializeField] EventStage stage;
    [SerializeField] UnitEventType eventType;
    [SerializeField] bool changeState;
    [SerializeField] UnitState state;

    public void OnUnitDataEvent(UnitData data) {
        if (data != null) {
            if (changeState)
                data.SetState(state);
            if (sendEvent)
                UnitEvent.Trigger(stage, eventType, data);
        }
    }
}
