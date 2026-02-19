using System.Collections;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;

public class TutorialBoardAction : MonoBehaviour, MMEventListener<CellEvent> {
    [SerializeField] Board board;
    [SerializeField] Triggerable tutorialHandPrefab;
    [SerializeField] Vector2Int selectedPosition;
    
    [Header("Events")]
    [SerializeField] UnityEvent OnProcess;
    [SerializeField] UnityEvent OnComplete;

    protected Triggerable _tutorialHand;

    public void SetSelectedX(int x) {
        selectedPosition.x = x;
    }

    public void SetSelectedY(int y) {
        selectedPosition.y = y;
    }

    public void OnBoardReady(Board board) {
        Perform();
    }

    public void Perform() {
         if (gameObject.activeInHierarchy && board != null) {
            OnProcess?.Invoke();
            board.DeactivateAllCellsExcept(selectedPosition);
            board.HighlightCell(selectedPosition, true, true);
            float halfCellSize = board.HalfCellSize;
            Vector2 cellPosition = board.GetCellWorldPosition(selectedPosition);
            if (tutorialHandPrefab != null)
                _tutorialHand = Instantiate(tutorialHandPrefab, cellPosition + new Vector2(halfCellSize, - halfCellSize), Quaternion.AngleAxis(45, Vector3.forward));
        }       
    }

    public void OnMMEvent(CellEvent e) {
        if (e.Type == CellEventType.Tap && e.Cell != null && e.Cell.Position == selectedPosition) {
            board.HighlightCell(selectedPosition, false, true);
            board?.ActivateAllCells();         
            _tutorialHand?.Trigger();
            OnComplete?.Invoke();  
        } 
    }

    void OnEnable() {
        this.MMEventStartListening<CellEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<CellEvent>();
    }
}
