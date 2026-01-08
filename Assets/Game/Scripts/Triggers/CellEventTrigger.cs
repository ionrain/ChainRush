using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class UnityCellEvent : UnityEvent<Cell> {}
[System.Serializable]
public class UnityVector2IntEvent : UnityEvent<Vector2Int> {}

public class CellEventTrigger : EventTrigger<CellEventType>, MMEventListener<CellEvent> {
    [Header("Cell Event Trigger")]
    [SerializeField] bool checkPosition;
    [MMCondition("checkPosition", true)]
    [SerializeField] Vector2Int position;

    [Header("Events")]
    [SerializeField] UnityCellEvent OnCellEvent;
    [SerializeField] UnityVector2IntEvent OnCellPositionEvent;

    Cell _cell;

    public void OnMMEvent(CellEvent e) {
        if (e.Type == eventType && (!checkPosition || (e.Cell != null && e.Cell.Position == position))) {
            _cell = e.Cell;
            Trigger();
        }
    }

    protected override void OnInvoke() {
        OnCellEvent?.Invoke(_cell);
        OnCellPositionEvent?.Invoke(_cell.Position);
        base.OnInvoke();
    }

    protected override void OnEnable() {
        if (triggerMode.HasFlag(TriggerMode.OnEvent))
            this.MMEventStartListening<CellEvent>();
        base.OnEnable();
    }

    void OnDisable() {
        this.MMEventStopListening<CellEvent>();
    }
}
