using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public enum BoardEventType { SetupCells, Ready, SetupItems, UsedHint }

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
    [SerializeField] Transform canvas;
    [SerializeField] Vector2 canvasOffset;
    [SerializeField] SpriteRenderer background;
    [SerializeField] SpriteRenderer border;
    [SerializeField] SpriteRenderer shadow;
    [SerializeField] float backgroundOffset = 0.2f;
    [SerializeField] Vector2 borderOffset;
    [SerializeField] Vector2 shadowOffset;
    [SerializeField] Transform gridDimRoot;
    [SerializeField] SpriteRenderer gridDimPrefab;
    
    [Header("Cells")]
    [SerializeField] Transform cellsRoot;
    [SerializeField] Cell cellPrefab;
    [SerializeField] Cell blockPrefab;
    [SerializeField] float cellSize = 1.28f;
    [SerializeField] float cellHighlightInitialDelay = 1f;
    [SerializeField] float cellAnimationDelay = 0.02f;
    [SerializeField] float cellOpenBoostStartDelay = 0.4f;
    [SerializeField] float cellOpenBoostDelay = 1f;
    [SerializeField] MMF_Player cellOpenBoostFeedback;
    [SerializeField] bool autoOpenChestCells;

    [Header("Helper")]
    [SerializeField] bool checkMarks = true;
    [SerializeField] bool checkAroundOpened;
    [SerializeField] bool fullSearch;
    [SerializeField] bool iterate;
    
    [Header("Cell Items")]
    [SerializeField] Transform itemsRoot;
    [SerializeField] Dictionary<CellType, CellItem> itemPrefabs = new Dictionary<CellType, CellItem>();

    [Header("Events")]
    [SerializeField] UnityEvent OnBoardShow;
    [SerializeField] UnityEvent OnBoardHide;

    public float HalfCellSize { get; private set; } = 0.64f;
    public List<Cell> Cells => _cells;
    public List<Cell> AllCells => _allCells;
    public int CellsCount => _cells.Count;

    List<Cell> _cells = new List<Cell>();
    List<Cell> _allCells = new List<Cell>();
    Vector2 _boardCenter = Vector2.zero;
    Vector2Int _boardSize = Vector2Int.zero;
    int _unitsCount = 0;
    Dictionary<PartyBoosterType, int> _boosterCounts = new();
    Dictionary<TrapType, int> _trapCounts = new();
    Cell _selectedCell;
    bool _visible;    
    float _openRate;
    float _openShare;

    void Reset() {
        _cells.Clear();
        _unitsCount = 0;
        _boosterCounts.Clear();
        _trapCounts.Clear();
        _openRate = 0;
        _openShare = 0;
        foreach (Transform t in itemsRoot)
            Destroy(t.gameObject);
        foreach (Transform t in cellsRoot)
            Destroy(t.gameObject);
        foreach (Transform t in gridDimRoot)
            Destroy(t.gameObject);
    }

    public void SetCheckMarks(bool value) {
        checkMarks = value;
    }
    
    public void OnMMEvent(LevelLoadEvent e) {
        /*_difficultyCount = System.Enum.GetValues(typeof(LevelStageDifficulty)).Length;
        cellSize = e.Data.cellSize;
        if (e.Stage == EventStage.Start && e.Data != null)
            _useRandomCellOpener = e.Data.startWithRandomOpener;*/
    }

    public Vector2 GetCellWorldPosition(Vector2Int position) {
        return cellsRoot != null ? (Vector2)position * cellSize + (Vector2)cellsRoot.position - _boardCenter : Vector2.zero;
    }

    /*public void OnMMEvent(LevelStageStateEvent e) {
        if (e.EventStage == EventStage.End && e.State == LevelStageState.Start && e.IsValid) {
            _firstStage = e.Data.StageIndex == 0;          
            Setup(e.Data);
            _visible = true;
            OnBoardShow?.Invoke();
        } else if (e.State == LevelStageState.ClearBonus) {
            if (e.EventStage == EventStage.Start) {
                DeactivateAllCells();
            } else if (e.EventStage == EventStage.End) {
                TryHideBoard();
                var bonus = e.Data.Stage.ClearBonus;
                if (bonus != null && bonus is BoosterStageClearBonus boosterBonus && boosterBonus.Booster == PartyBoosterType.RandomCellOpener)
                    _useRandomCellOpener = true;
            }
        } else if (e.State == LevelStageState.Battle && e.EventStage == EventStage.Start)
            TryHideBoard();
    }*/

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
        _allCells.ForEach(t => t.SetActive(t.Position == position) );
    }

    public void DeactivateAllCells() {
        _allCells.ForEach(t => t.SetActive(false));
    }

    public void ActivateAllCells() {
        _allCells.ForEach(t => t.SetActive(true));
    }

    public void HighlightCell(Vector2Int position, bool value, bool force = false) {
        Cell cell = _allCells.Find(t => t.Position == position);
        if (cell != null)
            cell.Highlight(value, force);
    }    
    
   /* public void Setup(LevelStageStateData data) {
        if (data != null && cellPrefab != null && blockPrefab != null && cellsRoot != null && itemsRoot != null && gridDimRoot != null) {
            Reset();
            
            _data = data.Stage;
            _openRate = 1f / (_data.CellsCount - _data.BoostersCount - _data.TrapsCount - _data.unitsCount -_data.BlocksCount);
            _firstCell = _data.openCellShare == 0;
            HalfCellSize = 0.5f * cellSize;
            _boardSize = _data.boardSize;
            _boardCenter = (_boardSize - Vector2.one) * HalfCellSize;
            _difficulty = data.Stage.difficulty;
            if (background != null) {
                background.size = (Vector2)_boardSize * cellSize + new Vector2(backgroundOffset, backgroundOffset);
                if (shadow != null)
                    shadow.size = background.size + shadowOffset;
                if (border != null)
                    border.size = background.size + borderOffset;
            }

            if (canvas != null)
                canvas.localPosition = new Vector3(canvasOffset.x, _boardSize.y * cellSize * 0.5f + canvasOffset.y, 0);

            CreateGrid(_data.predefinedCells);

            if (!_firstCell) {
                CreateCellItems(_cells, new());
                SetupOpenCells();
            }

            _cells.ForEach(t => t.SetVisible(true, (t.Position.x + t.Position.y) * cellAnimationDelay + cellHighlightInitialDelay));

            BoardEvent.Trigger(BoardEventType.SetupCells, this);
            StartCoroutine(BoardReadyAction((_boardSize.x + _boardSize.y) * cellAnimationDelay + data.Delay));
        }
    }*/

    IEnumerator BoardReadyAction(float delay) {
        yield return new WaitForSeconds(delay);
        BoardReady();
    }

    void CreateCellItems(List<Cell> allCells, List<Cell> trapExceptions) {
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

    /*void CreateTraps(List<Cell> freeCells) {
        List<Cell> trapCells = new List<Cell>(freeCells);
        foreach (var pair in _data.traps)
            CreateCellItemsWithType(trapCells, CellType.Trap, pair.Value - _trapCounts.GetValueOrDefault(pair.Key), pair.Key);
        freeCells.RemoveAll(t => t.Type == CellType.Trap);
        if (AllTrapsCount < _data.TrapsCount) {
            if (_difficulty != LevelStageDifficulty.None) {
                int difficulty = (int)_difficulty;
                _difficulty = difficulty < _difficultyCount - 1 ? (LevelStageDifficulty)(difficulty + 1) : LevelStageDifficulty.None;
                CreateTraps(freeCells);
            } else
                Debug.LogErrorFormat("Board CreateTraps: Cannot create all the traps ({0} left). Possibly there's not enough free cells.", _data.TrapsCount - AllTrapsCount);
        }
    }

    void ApplyDifficulty(List<Cell> freeCells) { 
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

    void CreateCellItemsWithType(List<Cell> freeCells, CellType cellType, int count, object param = null) {
        CellItem prefab = itemPrefabs.GetValueOrDefault(cellType);
        if (prefab != null && count > 0) {
            for (int i = 0; i < count; i++) {
                if (freeCells.Count > 0) {
                    Cell cell = freeCells[Random.Range(0, freeCells.Count)];
                    CreateCellItem(cell, cellType, prefab, param);
                    freeCells.Remove(cell);
                    /*if (cellType == CellType.Trap)
                        ApplyDifficulty(freeCells);*/
                }
            }
        }
    }

    void CreateCellItem(Cell cell, CellType cellType, CellItem prefab, object param) {
        CellItem item = Instantiate(prefab, cell.transform.position, Quaternion.identity, itemsRoot);
        item.Setup(cell.Position,param);
        cell.SetItem(cellType, item);
        cell.name = string.Format("{0}-{1}", cell.name, cellType);
        item.transform.localScale *= cellSize;
        item.SetVisible(false);

        if (cellType == CellType.Unit)
            _unitsCount++;
        else if (cellType == CellType.Booster) {
            PartyBoosterType booster = (PartyBoosterType)param;
            _boosterCounts[booster] = _boosterCounts.GetValueOrDefault(booster) + 1;            
        }
    }

    void CreatePredefinedCellItem(Cell cell) {
        if (cell != null) {
            CellType cellType = cell.Type;
            object param = null;
            CellItem prefab = itemPrefabs.GetValueOrDefault(cell.Type);
           /* if (prefab != null) {
                if (cellType == CellType.Trap && _data.TrapsCount > 0) {
                    foreach (var pair in _data.traps) {
                        if (_trapCounts.GetValueOrDefault(pair.Key) < pair.Value) {
                            prefab = itemPrefabs.GetValueOrDefault(CellType.Trap);
                            param = pair.Key;
                            break;
                        }
                    }
                } else if (cellType == CellType.Booster && _data.BoostersCount > 0) {
                    foreach (var pair in _data.boosters) {
                        if (_boosterCounts.GetValueOrDefault(pair.Key) < pair.Value) {
                            param = pair.Key;
                            break;
                        }
                    }
                }
                CreateCellItem(cell, cell.Type, prefab, param);
            }*/
        }
    }

    void CreateGrid(Dictionary<Vector2Int, CellType> predefinedCells) {
        Vector2 offset = (Vector2)cellsRoot.position - _boardCenter;
        if (predefinedCells != null && _boardSize.x > 0 && _boardSize.y > 0) {
            for (int i = 0; i < _boardSize.x; i++) {
                float x = i * cellSize + offset.x;
                for (int j = 0; j < _boardSize.y; j++) {
                    CellType cellType = predefinedCells.GetValueOrDefault(new Vector2Int(i, j), CellType.Empty);
                    float y = j * cellSize + offset.y;
                    Cell cell = CreateCell(i, j, x, y, cellType);
                    if (cellType == CellType.Open)
                        _openShare += _openRate;
                }
            }
            _allCells = new List<Cell>(_cells);
        } else
            Debug.LogError("Board CreateGrid: grid size isn't suitable or predefinedCells is NULL");
    }

    Cell CreateCell(int i, int j, float x, float y, CellType cellType) {
        Vector2Int position = new Vector2Int(i, j);
    
        Cell cell = Instantiate(cellPrefab, new Vector3(x, y, 0),Quaternion.identity,  cellsRoot);
        cell.Setup(cellType, position, cellSize);
        cell.gameObject.name = string.Format("{0}_{1}x{2}", cellPrefab.name, i, j);
        
        if (cellType != CellType.Empty)
            CreatePredefinedCellItem(cell);
        cell.OnOpen += OnCellOpened;
        cell.OnSelect += OnCellSelected;
        cell.OnDeselect += OnCellDeselected;

        if (gridDimRoot != null && gridDimPrefab != null/* && (i + j) % 2 != 1*/) {
            SpriteRenderer dimmer = Instantiate(gridDimPrefab, cell.transform.position, Quaternion.identity, gridDimRoot);
            dimmer.size = new Vector2(cellSize, cellSize);
        }

        return cell;
    }

    void OnCellDeselected(Cell cell) {
        if (_selectedCell == cell || cell == null) {
                
            if (_selectedCell != null)
                _selectedCell.SetSelected(false, false);
            _selectedCell = null;

            _cells.ForEach(t => t.Highlight(false));
        }
    }

    void OnCellOpened(Cell cell) {
        OnCellDeselected(null);
        List<Cell> freeCells = new List<Cell>(_cells);
        List<Cell> exceptions = freeCells.FindAll(t => t.IsNear(cell));
        freeCells.Remove(cell);
        CreateCellItems(freeCells, exceptions);
    }

    void OnCellSelected(Cell cell) {
        OnCellDeselected(null);

        _selectedCell = cell;
        List<Cell> neighbours = _cells.FindAll(t => t.IsNear(cell));
        neighbours.ForEach(t => t.Highlight(true));
    }

    public void OnMMEvent(InputEvent e) {
        if (e.Type == InputEventType.Tap || e.Type == InputEventType.Move)
            _allCells.ForEach(t => t.CheckTap(e.Position));
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
