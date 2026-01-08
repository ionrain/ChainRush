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

public class Board : SerializedMonoBehaviour, MMEventListener<LevelStageStateEvent>, MMEventListener<PartyBoostEvent>, 
                     MMEventListener<LevelLoadEvent>, MMEventListener<InputEvent> {
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
    LevelStage _data;
    bool _useRandomCellOpener = true;
    bool _firstCell = true;
    bool _firstStage = true;
    int _unitsCount = 0;
    LevelStageDifficulty _difficulty;
    Dictionary<PartyBoosterType, int> _boosterCounts = new();
    Dictionary<TrapType, int> _trapCounts = new();
    int AllTrapsCount => _trapCounts.Sum(t => t.Value);
    int _difficultyCount;
    bool _showTraps;
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

    public void ToggleShowTraps() {
        ShowTraps(!_showTraps);
    }

    public void SetCheckMarks(bool value) {
        checkMarks = value;
    }
    
    public void OnMMEvent(LevelLoadEvent e) {
        _difficultyCount = System.Enum.GetValues(typeof(LevelStageDifficulty)).Length;
        cellSize = e.Data.cellSize;
        if (e.Stage == EventStage.Start && e.Data != null)
            _useRandomCellOpener = e.Data.startWithRandomOpener;
    }

    public Vector2 GetCellWorldPosition(Vector2Int position) {
        return cellsRoot != null ? (Vector2)position * cellSize + (Vector2)cellsRoot.position - _boardCenter : Vector2.zero;
    }

    public void ShowTraps(bool value) {
        if (_showTraps != value) {
            _showTraps = value;
            _cells.ForEach(t => { if (t.Type == CellType.Trap && t.Item != null) t.Item.Highlight(_showTraps); });
        }
    }

    public void OnMMEvent(LevelStageStateEvent e) {
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
    }

    void TryHideBoard() {
        if (_visible) {
            _visible = false;
            OnBoardHide?.Invoke();
        }
    }

    public void OnMMEvent(PartyBoostEvent e) {
        if (e.Booster == PartyBoosterType.RandomCellOpener && e.EventStage == EventStage.Start)
            StartCoroutine(BoosterCellOpen(_cells, cellOpenBoostStartDelay));
    }

    IEnumerator BoosterCellOpen(List<Cell> source, float delay = 0) {
        bool canUseRandomCellOpener = _useRandomCellOpener;
        if (_useRandomCellOpener)
            _useRandomCellOpener = false;
        yield return new WaitForSeconds(delay);
        List<Cell> cells = new();
        cells = source.FindAll(t => t.Type != CellType.Trap && t.Number == 0);
        if (cells.Count == 0)
            cells = source.FindAll(t => t.Type != CellType.Trap);
        if (cells.Count > 0) {
            Cell cell = cells[Random.Range(0, cells.Count)];
            
            if (_firstStage) {
                _firstStage = false;
                if (canUseRandomCellOpener && _data.unitsCount - _unitsCount > 0)
                    CreateCellItemsWithType(new List<Cell>{ cell }, CellType.Unit, 1);
            }

            if (cellOpenBoostFeedback != null) {
                cellOpenBoostFeedback.transform.position = cell.transform.position;
                cellOpenBoostFeedback.PlayFeedbacks();
            }
            yield return new WaitForSeconds(cellOpenBoostDelay);
            OnCellOpened(cell);
        }
        PartyBoostEvent.Trigger(EventStage.End, PartyBoosterType.RandomCellOpener, PartyBoosterSource.None, Vector2.zero);
        
        if (canUseRandomCellOpener) 
            BoardReady();

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
    
    public void Setup(LevelStageStateData data) {
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
    }

    public void SetupOpenCells() {        
        List<Cell> trapCells = _cells.FindAll(t => t.Type == CellType.Trap);
        Dictionary<Cell, List<Cell>> trapsNeighbours = new();
        trapCells.ForEach(t => {
            List<Cell> neighbours = _cells.FindAll(n => n.IsNear(t));;
            trapsNeighbours[t] = neighbours;
        });

        List<Cell> emptyCells = _cells.FindAll(t => t.Type == CellType.Empty);
        int emptyCount = emptyCells.Count;
        for (int i = 0; i < emptyCount; i++) {
            Cell cell = emptyCells[Random.Range(0, emptyCells.Count)];

            bool suitable = true;            
            foreach (var pair in trapsNeighbours) {
                if (pair.Value.Contains(cell) && pair.Value.Count < 2) {
                    suitable = false;
                    break;
                }
            }

            if (!suitable) {
                emptyCells.Remove(cell);
                continue;
            }

            cell.SetType(CellType.Open);
            _openShare += _openRate;

            foreach (var pair in trapsNeighbours)
                if (pair.Value.Contains(cell))
                    pair.Value.Remove(cell);                    
            emptyCells.Remove(cell);
            _cells.Remove(cell);

            if (_openShare >= _data.openCellShare)
                break;            
        }
    }

    IEnumerator BoardReadyAction(float delay) {
        yield return new WaitForSeconds(delay);
        
        if (_useRandomCellOpener)
            StartCoroutine(BoosterCellOpen(_cells.FindAll(t => t.Type == CellType.Empty)));
        else
            BoardReady();
    }

    void CreateCellItems(List<Cell> allCells, List<Cell> trapExceptions) {
        if (_data != null && allCells.Count > 0) {
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
        }
    }

    void CreateTraps(List<Cell> freeCells) {
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
    }

    void UpdateTrapNumbers() {
        List<Cell> trapCells = _cells.FindAll(t => t.Type == CellType.Trap);
        _cells.ForEach(t => SetTrapNumbers(t, trapCells));
        _cells.RemoveAll(t => t.Type == CellType.Open);
    }

    void CreateCellItemsWithType(List<Cell> freeCells, CellType cellType, int count, object param = null) {
        CellItem prefab = itemPrefabs.GetValueOrDefault(cellType);
        if (prefab != null && count > 0) {
            for (int i = 0; i < count; i++) {
                if (freeCells.Count > 0) {
                    Cell cell = freeCells[Random.Range(0, freeCells.Count)];
                    CreateCellItem(cell, cellType, prefab, param);
                    freeCells.Remove(cell);
                    if (cellType == CellType.Trap)
                        ApplyDifficulty(freeCells);
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
        else if (cellType == CellType.Trap) {
            TrapType trapType = (TrapType)param;
            _trapCounts[trapType] = _trapCounts.GetValueOrDefault(trapType) + 1;
            item.Highlight(_showTraps);
        } else if (cellType == CellType.Booster) {
            PartyBoosterType booster = (PartyBoosterType)param;
            _boosterCounts[booster] = _boosterCounts.GetValueOrDefault(booster) + 1;            
        }
    }

    void CreatePredefinedCellItem(Cell cell) {
        if (cell != null) {
            CellType cellType = cell.Type;
            object param = null;
            CellItem prefab = itemPrefabs.GetValueOrDefault(cell.Type);
            if (prefab != null) {
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
            }
        }
    }

    void SetTrapNumbers(Cell cell, List<Cell> trapCells) {
        int result = 0;
        trapCells.ForEach(t => { if (t.IsNear(cell)) result++; });
        cell.SetNumber(result);
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
                    if (cellType != CellType.Block)
                            _cells.Add(cell);
                }
            }
            _allCells = new List<Cell>(_cells);
            if (predefinedCells.ContainsValue(CellType.Trap))
                UpdateTrapNumbers();
        } else
            Debug.LogError("Board CreateGrid: grid size isn't suitable or predefinedCells is NULL");
    }

    void Flood(Cell cell, float delay = 0) {
        delay += cellAnimationDelay;
        cell.SetVisible(false, delay);
        _cells.Remove(cell);

        if (cell.Type != CellType.Trap && cell.Number == 0) {
            List<Cell> checklist = _cells.FindAll(t => t.IsValidForFlood(cell, !autoOpenChestCells));
            checklist.ForEach(t => Flood(t, delay));
        }
    }

    void CheckTraps() {
        List<Cell> trapCells = _cells.FindAll(t => t.Type == CellType.Trap);
        List<List<Cell>> disputable = new ();

        foreach (Cell trapCell in trapCells) {
            List<Cell> neighbours = _cells.FindAll(t => t.IsNear(trapCell));
            if (neighbours.Count == 0) {
                trapCell.Reveal();
                _cells.Remove(trapCell);
            } else {
                List<Cell> trapNeighbours = neighbours.FindAll(t => t.Type == CellType.Trap);
                trapNeighbours.Add(trapCell);
                disputable.Add(trapNeighbours);
            }
        }

        foreach (List<Cell> list in disputable) {
            bool canBeTransparent = true;
            foreach (Cell item in list) {
                List<Cell> all = _cells.FindAll(t => t.IsNear(item));
                List<Cell> selected = all.FindAll(t => t.Type == CellType.Trap);
                if (selected.Count < all.Count) {
                    canBeTransparent = false;
                    break;
                }
            }

            if (canBeTransparent)
                list.ForEach(t => {
                    t.Reveal();
                    _cells.Remove(t);
                });
        }
    }

    int CheckCellMarks(Cell targetCell, bool checkAround, bool full, bool iterations) {
        int found = 0;
        List<Cell> cells = _allCells.FindAll(t => (!checkAround || t.IsNear(targetCell)) && t.Type != CellType.Trap);

        int count = 1;
        while (count > 0) {
            count = 0;

            foreach (Cell cell in cells) {
                List<Cell> neighbours = _allCells.FindAll(t => t.IsNear(cell));
                if (!cell.Visible) {
                    if (cell.Number == 0) {
                        foreach (var neighbour in neighbours)
                            if (neighbour.SetupMark(CellMarkType.Free)) {
                                found++;
                                if (full)
                                    count++;
                                else
                                    return found;
                            }
                    } else {
                        List<Cell> traps = neighbours.FindAll(t => (!t.Visible && t.Type == CellType.Trap) || t.MarkState == CellMarkType.Trap);
                        if (traps.Count == cell.Number) {
                            traps.ForEach(t => neighbours.Remove(t));
                            foreach (var neighbour in neighbours)
                                if (neighbour.SetupMark(CellMarkType.Free)) {
                                    found++;
                                    if (full)
                                        count++;
                                    else
                                        return found;
                                }
                        }
                    }
                }
            }

            foreach (Cell cell in cells) {
                List<Cell> neighbours = _allCells.FindAll(t => t.IsNear(cell));
                if (!cell.Visible && cell.Number > 0) {
                    List<Cell> potentialTraps = neighbours.FindAll(t => (t.Visible && t.MarkState != CellMarkType.Free) || (!t.Visible && t.Type == CellType.Trap));
                    if (potentialTraps.Count == cell.Number)
                        foreach (var trap in potentialTraps)
                            if (trap.SetupMark(CellMarkType.Trap)) {
                                found++;
                                if (fullSearch)
                                    count++;
                                else
                                    return found;
                            }
                }
            }

            if (!iterations)
                count = 0;
        }
        return found;
    }

    Cell CreateCell(int i, int j, float x, float y, CellType cellType) {
        Vector2Int position = new Vector2Int(i, j);
        bool isBlock = cellType == CellType.Block;
        Cell prefab = isBlock ? blockPrefab : cellPrefab;
    
        Cell cell = Instantiate(prefab, new Vector3(x, y, 0),Quaternion.identity,  cellsRoot);
        cell.Setup(cellType, position, cellSize);
        cell.gameObject.name = string.Format("{0}_{1}x{2}", prefab.name, i, j);
        
        if (!isBlock) {
            if (cellType != CellType.Empty)
                CreatePredefinedCellItem(cell);
            cell.OnOpen += OnCellOpened;
            cell.OnSelect += OnCellSelected;
            cell.OnDeselect += OnCellDeselected;
        }

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

        if (_firstCell) {
            _firstCell = false;
            List<Cell> freeCells = new List<Cell>(_cells);
            List<Cell> exceptions = freeCells.FindAll(t => t.IsNear(cell));
            freeCells.Remove(cell);
            CreateCellItems(freeCells, exceptions);
        }
        Flood(cell);
        if (cell.Type != CellType.Trap)
            CheckTraps();
        
        if (checkMarks)
            CheckCellMarks(cell, checkAroundOpened, fullSearch, iterate);          
    }

    void OnCellSelected(Cell cell) {
        OnCellDeselected(null);

        _selectedCell = cell;
        List<Cell> neighbours = _cells.FindAll(t => t.IsNear(cell));
        neighbours.ForEach(t => t.Highlight(true));
        if (checkMarks && CheckCellMarks(cell, true, true, true) > 0)
            BoardEvent.Trigger(BoardEventType.UsedHint, this);
    }

    public void OnMMEvent(InputEvent e) {
        if (e.Type == InputEventType.Tap)
            _allCells.ForEach(t => t.CheckTap(e.Position));
    }    

    void OnEnable() {
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<LevelStageStateEvent>();
        this.MMEventStartListening<PartyBoostEvent>();
        this.MMEventStartListening<InputEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<LevelStageStateEvent>();
        this.MMEventStopListening<PartyBoostEvent>();
        this.MMEventStopListening<InputEvent>();
    }


}
