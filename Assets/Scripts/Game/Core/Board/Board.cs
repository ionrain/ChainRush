using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public enum BoardEventType { SetupCells, Ready, SetupItems, UsedHint, SelectedCells }

public struct BoardEvent {
    public BoardEventType Type { get; private set; }
    public Board Board { get; private set; }

    static BoardEvent e;
    public static void Trigger(BoardEventType type, Board board) {
        e.Type = type;
        e.Board = board;
        MMEventManager.TriggerEvent(e);
    }
}

public class Board : SerializedMonoBehaviour, MMEventListener<LevelLoadEvent>, MMEventListener<InputEvent> {
    [Header("Board")]
    [SerializeField] SpriteRenderer background;
    [SerializeField] SpriteRenderer border;
    [SerializeField] SpriteRenderer shadow;
    [SerializeField] float backgroundOffset = 0.2f;
    [SerializeField] Vector2 borderOffset;
    [SerializeField] Vector2 shadowOffset;
    
    [Header("Cells")]
    [SerializeField] Transform cellsRoot;
    [SerializeField] Cell cellPrefab;
    [SerializeField] float cellSize = 1.28f;
    [SerializeField] float cellHighlightInitialDelay = 1f;
    [SerializeField] float cellAnimationDelay = 0.02f;
    
    [Header("Cell Items")]
    [SerializeField] Transform itemsRoot;
    [SerializeField] Dictionary<CellItemType, CellItem> itemPrefabs = new();

    [Header("Events")]
    [SerializeField] UnityEvent OnBoardShow;
    [SerializeField] UnityEvent OnBoardHide;

    public float HalfCellSize { get; private set; } = 0.64f;
    public List<Cell> Cells => _cells;
    public int CellsCount => _cells.Count;

    Dictionary<Vector2Int, Cell> _cellsMap = new();
    List<Cell> _cells = new List<Cell>();
    Vector2 _boardCenter = Vector2.zero;
    Vector2Int _boardSize = Vector2Int.zero;
    bool _visible;    
    LevelData _data;
    List<Vector2Int> _selectedCells = new();
    bool _isSelecting;

    void Reset() {
        _cells.Clear();
        foreach (Transform t in itemsRoot)
            Destroy(t.gameObject);
        foreach (Transform t in cellsRoot)
            Destroy(t.gameObject);
    }
    
    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null) {
            _data = e.Data;
            _boardSize = _data.boardSize;
            //cellSize = _data.cellSize;
            Setup();
            _visible = true;
            OnBoardShow?.Invoke();            
        }
    }

    public Vector2 GetCellWorldPosition(Vector2Int position) {
        return cellsRoot != null ? (Vector2)position * cellSize + (Vector2)cellsRoot.position - _boardCenter : Vector2.zero;
    }

    void TryHideBoard() {
        if (_visible) {
            _visible = false;
            OnBoardHide?.Invoke();
        }
    }

    void BoardReady() {
        BoardEvent.Trigger(BoardEventType.Ready, this);
    }

    public void DeactivateAllCellsExcept(Vector2Int position) {
        _cells.ForEach(t => t.SetActive(t.Position == position) );
    }

    public void DeactivateAllCells() {
        _cells.ForEach(t => t.SetActive(false));
    }

    public void ActivateAllCells() {
        _cells.ForEach(t => t.SetActive(true));
    }

    public void HighlightCell(Vector2Int position, bool value, bool force = false) {
        Cell cell = _cells.Find(t => t.Position == position);
        if (cell != null)
            cell.Highlight(value, force);
    }    
    
   public void Setup() {
        HalfCellSize = 0.5f * cellSize;
        _boardCenter = (_boardSize - Vector2.one) * HalfCellSize;

        for (int i = 0; i < _boardSize.x; i++)
            for (int j = 0; j < _boardSize.y; j++)
                _cellsMap[new Vector2Int(i, j)] = null;

        if (background != null) {
            background.size = (Vector2)_boardSize * cellSize + new Vector2(backgroundOffset, backgroundOffset);
            if (shadow != null)
                shadow.size = background.size + shadowOffset;
            if (border != null)
                border.size = background.size + borderOffset;
        }

        CreateGrid();
        CreateCellItems();

        _cells.ForEach(t => t.SetVisible(true, (t.Position.x + t.Position.y) * cellAnimationDelay + cellHighlightInitialDelay));

        BoardEvent.Trigger(BoardEventType.SetupCells, this);
        StartCoroutine(BoardReadyAction((_boardSize.x + _boardSize.y) * cellAnimationDelay));
    }

    IEnumerator BoardReadyAction(float delay) {
        yield return new WaitForSeconds(delay);
        BoardReady();
    }

    void CreateCellItems() {
        /*if (_data != null && allCells.Count > 0) {
            List<Cell> freeCells = allCells.FindAll(t => t.Type == CellType.Empty);
            trapExceptions.ForEach(t => freeCells.Remove(t));
            if (_data.traps != null)
                CreateTraps(freeCells);
            CreateCellItemsWithType(freeCells, CellType.Unit, _data.unitsCount - _unitsCount);
            if (_data.boosters != null)
                foreach (var pair in _data.boosters)
                    CreateCellItemsWithType(freeCells, CellType.Booster, pair.Value - _boosterCounts.GetValueOrDefault(pair.Key), pair.Key);
            UpdateTrapNumbers();
            BoardEvent.Trigger(BoardEventType.SetupItems, this);
        }*/
    }

    /*void ApplyDifficulty(List<Cell> freeCells) { 
        if (_difficulty > LevelStageDifficulty.None) {
            List<Cell> cellsToRemove = new List<Cell>();
            int intDifficulty = (int)_difficulty;
            foreach (Cell cell in _cells) {
                if (cell.Type == CellType.Trap) {
                    List<Cell> neighbours = _cells.FindAll(t => t.IsNear(cell));
                    List<Cell> traps = neighbours.FindAll(t => t.Type == CellType.Trap);
                    traps.Add(cell);
                    int trapsCount = traps.Count - 1;
                    if (_difficulty == LevelStageDifficulty.Easy) {
                        foreach (Cell neighbour in neighbours) {
                            List<Cell> secondNeghbours = freeCells.FindAll(t => t.IsNear(neighbour));
                            secondNeghbours.ForEach(t => { if (!cellsToRemove.Contains(t)) cellsToRemove.Add(t); });
                            if (!cellsToRemove.Contains(neighbour))
                                cellsToRemove.Add(neighbour);
                        }
                    } else if (trapsCount >= intDifficulty - 2) {
                        foreach (Cell trap in traps) {
                            List<Cell> freeSlots = freeCells.FindAll(t => t.IsNear(trap));
                            freeSlots.ForEach(t => { if (!cellsToRemove.Contains(t)) cellsToRemove.Add(t); });
                        }
                    }
                }
            }
            cellsToRemove.ForEach(t => { if (freeCells.Contains(t)) freeCells.Remove(t);} );
        }
    }*/

    void CreateCellItemsWithType(List<Cell> freeCells, CellItemType cellItemType, int count, object param = null) {
        CellItem prefab = itemPrefabs.GetValueOrDefault(cellItemType);
        if (prefab != null && count > 0) {
            for (int i = 0; i < count; i++) {
                if (freeCells.Count > 0) {
                    Cell cell = freeCells[Random.Range(0, freeCells.Count)];
                    CreateCellItem(cell, prefab, param);
                    freeCells.Remove(cell);
                }
            }
        }
    }

    void CreateCellItem(Cell cell, CellItem prefab, object param) {
        CellItem item = Instantiate(prefab, cell.transform.position, Quaternion.identity, itemsRoot);
        item.Setup(cell.Position,param);
        cell.SetItem(item);
        cell.name = string.Format("{0}-{1}", cell.name, item.GetType().Name);
        item.transform.localScale *= cellSize;
    }

    void CreateGrid() {
        if (cellPrefab != null && cellsRoot != null && _boardSize.x > 0 && _boardSize.y > 0) {
            Vector2 offset = (Vector2)cellsRoot.position - _boardCenter;
            for (int i = 0; i < _boardSize.x; i++) {
                float x = i * cellSize + offset.x;
                for (int j = 0; j < _boardSize.y; j++) {
                    float y = j * cellSize + offset.y;
                    CreateCell(i, j, x, y);
                }
            }
        } else
            Debug.LogError("Board CreateGrid: grid size isn't suitable or cellPrefab or cellsRoot is NULL");
    }

    void CreateCell(int i, int j, float x, float y) {
        Vector2Int position = new Vector2Int(i, j);

        Cell cell = Instantiate(cellPrefab, new Vector3(x, y, 0),Quaternion.identity,  cellsRoot);
        cell.Setup(position, cellSize);
        cell.gameObject.name = string.Format("{0}_{1}x{2}", cellPrefab.name, i, j);

        _cellsMap[position] = cell;
    }

    Cell GetCellAtWorldPosition(Vector2 worldPos) {
        Vector2 localPos = worldPos - (Vector2)cellsRoot.position + _boardCenter;
        int i = Mathf.FloorToInt(localPos.x / cellSize);
        int j = Mathf.FloorToInt(localPos.y / cellSize);
        Vector2Int gridPos = new Vector2Int(i, j);
        return _cellsMap.GetValueOrDefault(gridPos);
    }

    bool IsAdjacent(Vector2Int a, Vector2Int b) {
        return a != b && Mathf.Abs(a.x - b.x) <= 1 && Mathf.Abs(a.y - b.y) <= 1;
    }

    bool HasSameContent(Cell a, Cell b) {
        if (a == null || b == null) return false;
        if (a.Item == null || b.Item == null) return a.Item == b.Item;
        return a.Item.GetType() == b.Item.GetType();
    }

    void StartSelection(Cell cell) {
        if (cell == null || cell.Item == null || !cell.Visible) return;
        _selectedCells.Add(cell.Position);
        cell.Highlight(true);
    }

    bool TryAddToSelection(Cell cell) {
        if (cell == null || _selectedCells.Count == 0) return false;

        Vector2Int lastPos = _selectedCells[^1];

        if (_selectedCells.Contains(cell.Position) || !IsAdjacent(lastPos, cell.Position))
            return false;

        Cell firstCell = _cellsMap.GetValueOrDefault(_selectedCells[0]);
        if (!HasSameContent(firstCell, cell))
            return false;

        _selectedCells.Add(cell.Position);
        cell.Highlight(true);
        return true;
    }

    void EndSelection() {
        if (_selectedCells.Count > 0)
            BoardEvent.Trigger(BoardEventType.SelectedCells, this);
    }

    public void ClearSelection() {
        foreach (var pos in _selectedCells) {
            Cell cell = _cellsMap.GetValueOrDefault(pos);
            if (cell != null)
                cell.Highlight(false);
        }
        _selectedCells.Clear();
    }

    public void OnMMEvent(InputEvent e) {
        switch (e.Type) {
            case InputEventType.Press:
                Cell pressedCell = GetCellAtWorldPosition(e.Position);
                if (pressedCell != null && pressedCell.Item != null) {
                    _isSelecting = true;
                    StartSelection(pressedCell);
                }
                break;

            case InputEventType.Move:
                if (_isSelecting && _selectedCells.Count > 0) {
                    Cell moveCell = GetCellAtWorldPosition(e.Position);
                    if (moveCell != null && !TryAddToSelection(moveCell)) {
                        if (moveCell.Position != _selectedCells[^1]) {
                            _isSelecting = false;
                            EndSelection();
                        }
                    }
                }
                break;

            case InputEventType.Release:
                if (_isSelecting) {
                    _isSelecting = false;
                    EndSelection();
                }
                break;

            case InputEventType.Tap:
                Cell tappedCell = GetCellAtWorldPosition(e.Position);
                if (tappedCell != null && tappedCell.Item != null) {
                    StartSelection(tappedCell);
                    EndSelection();
                }
                break;
        }
    }

    void OnEnable() {
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<InputEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<InputEvent>();
    }
}
