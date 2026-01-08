using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class UnityBoardEvent : UnityEvent<Board> { }

public class BoardEventTrigger : EventTrigger<BoardEventType>, MMEventListener<BoardEvent>  {
    [Header("Board Event Trigger")]
    [SerializeField] UnityBoardEvent OnBoardEvent;

    Board _board;
    
    public void OnMMEvent(BoardEvent e) {
        if (e.Type == eventType) {
            _board = e.Board;
            Trigger();
        }
    }

    protected override void OnInvoke() {
        OnBoardEvent?.Invoke(_board);
        base.OnInvoke();
    }

    protected override void OnEnable() {
        if (triggerMode.HasFlag(TriggerMode.OnEvent))
            this.MMEventStartListening<BoardEvent>();
        base.OnEnable();
    }

    void OnDisable() {
        this.MMEventStopListening<BoardEvent>();
    }
}
