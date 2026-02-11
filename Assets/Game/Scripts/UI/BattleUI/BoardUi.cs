using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.UI;

public enum BoardUiEventType { SetupCells, SetupItems, Ready }
public enum BoardRefreshMode { Auto, SemiAuto, Manual }
public enum CellSelectPatternType { None = 0, SelectOne = 1, Line = 2, Corner = 4, Box = 8, Zigzag = 16 }

public struct BoardUiEvent {
    public BoardUiEventType Type { get; private set; }
    public BoardUi Board { get; private set; }

    static BoardUiEvent e;
    public static void Trigger(BoardUiEventType type, BoardUi board) {
        e.Type = type;
        e.Board = board;
        MMEventManager.TriggerEvent(e);
    }
}

public class CellSelectPattern {
    public CellSelectPatternType Type { get; private set; }
    public List<Vector2Int> Cells { get; private set; }

    public CellSelectPattern(CellSelectPatternType type) {
        Type = type;
        Cells = new List<Vector2Int>();
    }

    public void Add(Vector2Int pos) => Cells.Add(pos);
    public int Count => Cells.Count;
}

public class BoardUi : SerializedMonoBehaviour, MMEventListener<LevelLoadEvent>, MMEventListener<LevelProgressEvent> {
    [SerializeField] RectTransform panelRoot;
    [SerializeField] float panelHeightPercent = 50;

    public float PanelHeightPercent => panelHeightPercent;
    [SerializeField] GridLayoutGroup cellContainer;
    [SerializeField] CellUi cellPrefab;
    [SerializeField] Dictionary<CellItemType, Color> cellColors = new();

    [Header("Item Data")]
    [SerializeField] ResourcesData resourcesData;
    [SerializeField] AllUnitsData unitsData;
    [SerializeField] BuffsData buffsData;
    [SerializeField] AttributesData attributesData;
    [SerializeField] BoostersData boostersData;

    [Header("Refresh Settings")]
    [SerializeField] BoardRefreshMode refreshMode = BoardRefreshMode.Auto;
    [SerializeField] bool pauseTimerOnSelection;
    [SerializeField] float minIntervalBetweenRefreshes = 1f;
    [SerializeField] float refreshDuration = 1f;
    [SerializeField] Progressbar refreshProgressbar;
    [SerializeField] TextMeshProUGUI refreshLabel;
    [SerializeField] LocalizedString refreshLabelFormat;
    [SerializeField] List<CellItemType> guaranteedOnFirstRefresh = new();

    [Header("Events")]
    [SerializeField] UnityEvent OnBoardRefresh;
    [SerializeField] UnityEvent OnBoardSelectStart;
    [SerializeField] UnityEvent OnBoardSelectEnd;
    [SerializeField] UnityEvent<float> OnPanelTopChanged;

    LevelData _data;
    Vector2Int _boardSize = Vector2Int.zero;
    Dictionary<Vector2Int, CellUi> _cellsMap = new();
    List<Vector2Int> _selectedCells = new();
    bool _isSelecting;
    float _refreshTimer;
    float _refreshInterval;
    string _refreshLabelFormat = "{0}";

    // Per-type cooldown tracking
    Dictionary<CellItemType, float> _typeCooldowns = new();
    Dictionary<CellItemType, float> _typeCooldownIntervals = new();
    bool _isRefreshPaused;
    bool _isFirstRefresh;

    // Types unavailable for this level (cached at start)
    HashSet<CellItemType> _unavailableTypes = new();

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null) {
            _data = e.Data;
            _boardSize = _data.boardSize;
            _isRefreshPaused = false;

            refreshProgressbar?.Setup();
            _refreshLabelFormat = refreshLabelFormat?.GetLocalizedString();

            ResizePanel();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);

            CreateCells();
            InitializeTypeCooldowns();
            InitializeUnavailableTypes();
            UpdateRefreshIntervals(0);
            _refreshTimer = _refreshInterval;
            _isFirstRefresh = true;
            RefreshBoard();
            BoardUiEvent.Trigger(BoardUiEventType.Ready, this);
        }
    }

    public void OnMMEvent(LevelProgressEvent e) {
        UpdateRefreshIntervals(e.Progress);
    }

    void Update() {
        if (_data?.difficulty == null) return;

        float dt = Time.unscaledDeltaTime;
        UpdateCooldowns(dt);

        if (!_isRefreshPaused && refreshMode != BoardRefreshMode.Manual)
            UpdateRefreshTimer(dt);
    }

    void UpdateCooldowns(float dt) {
        foreach (var type in new List<CellItemType>(_typeCooldowns.Keys))
            _typeCooldowns[type] += dt;
    }

    void UpdateRefreshTimer(float dt) {
        float oldValue = _refreshTimer;
        _refreshTimer -= dt;

        refreshProgressbar?.SetValue(_refreshTimer);
        if ((int)oldValue > (int)_refreshTimer && refreshLabel != null)
            refreshLabel.SetText(string.Format(_refreshLabelFormat, Mathf.CeilToInt(_refreshTimer)));

        if (_refreshTimer <= 0) {
            if (_isSelecting && _selectedCells.Count > 0) {
                _isSelecting = false;
                EndSelection();
            }

            if (!_isRefreshPaused)
                TriggerRefresh();
        }
    }

    public void TriggerRefresh() {
        RefreshBoard();
        _refreshTimer = _refreshInterval;
        _isRefreshPaused = false;
    }

    void InitializeTypeCooldowns() {
        _typeCooldowns.Clear();
        _typeCooldownIntervals.Clear();

        if (_data?.difficulty?.refreshCooldowns == null) return;

        foreach (var kvp in _data.difficulty.refreshCooldowns) {
            // Start with cooldown ready (can be used immediately)
            _typeCooldowns[kvp.Key] = kvp.Value.Evaluate(0f);
            _typeCooldownIntervals[kvp.Key] = kvp.Value.Evaluate(0f);
        }
    }

    void InitializeUnavailableTypes() {
        _unavailableTypes.Clear();

        List<UnitData> selectedHeroes = unitsData != null ? unitsData.Get(UnitType.Hero, UnitListType.Selected) : null;
        bool heroAvailable = false;
        if (selectedHeroes != null) {
            foreach (UnitData hero in selectedHeroes) {
                List<UnitSkill> skills = hero.GetSkills(SkillListType.Aquired);
                if (skills != null && skills.Count > 1) {
                    heroAvailable = true;
                    break;
                }
            }
        }
        if (!heroAvailable) _unavailableTypes.Add(CellItemType.HeroSkill);
    }

    void UpdateRefreshIntervals(float progress) {
        if (_data?.difficulty == null) return;

        _refreshInterval = _data.difficulty.refreshInterval.Evaluate(progress);
        refreshProgressbar?.SetTotal(_refreshInterval);

        // Update per-type cooldown intervals
        foreach (var kvp in _data.difficulty.refreshCooldowns) {
            _typeCooldownIntervals[kvp.Key] = kvp.Value.Evaluate(progress);
        }
    }

    List<CellItemType> GetCooldownReadyTypes() {
        List<CellItemType> ready = new();
        if (_data?.difficulty == null) return ready;

        foreach (var kvp in _typeCooldowns) {
            CellItemType type = kvp.Key;
            float cooldown = kvp.Value;
            float interval = _typeCooldownIntervals.GetValueOrDefault(type, 0f);

            if (cooldown >= interval) {
                ready.Add(type);
            }
        }
        return ready;
    }

    void RefreshBoard() {
        if (_data?.difficulty == null) return;

        ApplyPatternsToBoard();
        _cellsMap.ForEach(t => { if (t.Value != null) t.Value.SetActive(true); });
        BoardUiEvent.Trigger(BoardUiEventType.SetupItems, this);
        OnBoardRefresh?.Invoke();
    }
        
    void ResizePanel() {
        if (panelRoot == null) return;

        RectTransform parent = panelRoot.parent as RectTransform;
        if (parent == null) return;

        float parentHeight = parent.rect.height;
        float targetHeight = parentHeight * (panelHeightPercent / 100f);

        panelRoot.sizeDelta = new Vector2(panelRoot.sizeDelta.x, targetHeight);

        Canvas.ForceUpdateCanvases();
        Vector3[] corners = new Vector3[4];
        panelRoot.GetWorldCorners(corners);
        OnPanelTopChanged?.Invoke(corners[1].y);
    }

    void CreateCells() {
        _cellsMap.Clear();
        if (cellContainer == null || cellPrefab == null) return;

        RectTransform containerRect = cellContainer.GetComponent<RectTransform>();
        containerRect.DestroyChildren();
        RectOffset padding = cellContainer.padding;
        Vector2 spacing = cellContainer.spacing;

        float availableWidth = containerRect.rect.width - padding.left - padding.right;
        float availableHeight = containerRect.rect.height - padding.top - padding.bottom;

        float totalSpacingX = (_boardSize.x - 1) * spacing.x;
        float totalSpacingY = (_boardSize.y - 1) * spacing.y;

        float cellWidth = (availableWidth - totalSpacingX) / _boardSize.x;
        float cellHeight = (availableHeight - totalSpacingY) / _boardSize.y;

        float cellSize = Mathf.Min(cellWidth, cellHeight);
        cellContainer.cellSize = new Vector2(cellSize, cellSize);

        int totalCells = _boardSize.x * _boardSize.y;
        for (int i = 0; i < totalCells; i++) {
            int x = i % _boardSize.x;
            int y = i / _boardSize.x;
            Vector2Int pos = new Vector2Int(x, y);

            CellUi cell = Instantiate(cellPrefab, cellContainer.transform);
            cell.Setup(pos, cellSize);
            cell.OnCellPointerDownEvent += OnCellPointerDown;
            cell.OnCellPointerEnterEvent += OnCellPointerEnter;
            cell.OnCellPointerUpEvent += OnCellPointerUp;
            _cellsMap[pos] = cell;
        }
        BoardUiEvent.Trigger(BoardUiEventType.SetupCells, this);
    }

    CellUiItem CreateItemForType(CellItemType type) {
        Sprite icon = null;
        string id = type.ToString();

        switch (type) {
            case CellItemType.SoftCurrency:
                ResourceData softData = resourcesData != null ? resourcesData.Get(ResourceType.SoftCurrency) : null;
                if (softData != null) {
                    icon = softData.icon;
                    id = ResourceType.SoftCurrency.ToString();
                }
                break;

            case CellItemType.Unit:
                List<UnitData> selectedUnits = unitsData != null ? unitsData.Get(UnitType.Normal, UnitListType.Selected) : null;
                if (selectedUnits != null && selectedUnits.Count > 0) {
                    UnitData unit = selectedUnits[Random.Range(0, selectedUnits.Count)];
                    icon = unit.Icon;
                    id = unit.name;
                }
                break;

            case CellItemType.HeroSkill:
                List<UnitData> selectedHeroes = unitsData != null ? unitsData.Get(UnitType.Hero, UnitListType.Selected) : null;
                if (selectedHeroes != null && selectedHeroes.Count > 0) {
                    UnitData hero = selectedHeroes[Random.Range(0, selectedHeroes.Count)];
                    List<UnitSkill> heroSkills = hero.GetSkills(SkillListType.Aquired);
                    if (heroSkills != null && heroSkills.Count > 1) {
                        UnitSkill skill = heroSkills[Random.Range(1, heroSkills.Count)];
                        if (skill.data != null) {
                            icon = skill.data.icon;
                            id = skill.Name;
                        }
                    }
                }
                break;

            case CellItemType.Buff:
                BuffGradesData buffData = buffsData != null ? buffsData.GetRandom() : null;
                if (buffData != null && attributesData != null) {
                    AttributeData attrData = attributesData.GetData(buffData.attribute);
                    if (attrData != null) {
                        icon = attrData.icon;
                        id = buffData.attribute.ToString();
                    }
                }
                break;

            case CellItemType.Booster:
                BoosterData boosterData = boostersData != null ? boostersData.GetRandom() : null;
                if (boosterData != null) {
                    icon = boosterData.Icon;
                    id = boosterData.Type.ToString();
                }
                break;
        }

        return new CellUiItem(type, icon, id);
    }

    #region Pattern Generation

    List<CellSelectPattern> GeneratePatterns() {
        List<CellSelectPattern> result = new List<CellSelectPattern>();
        if (_data?.difficulty?.cellPatterns == null || _cellsMap.Count == 0)
            return result;

        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        List<Vector2Int> availableCells = new List<Vector2Int>(_cellsMap.Keys);

        ShuffleList(availableCells);

        List<(CellSelectPatternType type, int maxSize)> patternTypes = new();
        foreach (var kvp in _data.difficulty.cellPatterns) {
            if (kvp.Key != CellSelectPatternType.None && kvp.Value > 0)
                patternTypes.Add((kvp.Key, kvp.Value));
        }

        if (patternTypes.Count == 0)
            return result;

        int cellIndex = 0;

        while (cellIndex < availableCells.Count) {
            Vector2Int start = availableCells[cellIndex];

            if (occupied.Contains(start)) {
                cellIndex++;
                continue;
            }

            var (patternType, maxSize) = patternTypes[Random.Range(0, patternTypes.Count)];

            CellSelectPattern pattern = patternType switch {
                CellSelectPatternType.SelectOne => GenerateSelectOne(start),
                CellSelectPatternType.Line => GenerateLine(start, maxSize, occupied),
                CellSelectPatternType.Corner => GenerateCorner(start, maxSize, occupied),
                CellSelectPatternType.Box => GenerateBox(start, maxSize, occupied),
                CellSelectPatternType.Zigzag => GenerateZigzag(start, maxSize, occupied),
                _ => null
            };

            if (pattern != null && pattern.Count > 0) {
                foreach (var pos in pattern.Cells)
                    occupied.Add(pos);
                result.Add(pattern);
            }

            cellIndex++;
        }

        return result;
    }

    CellSelectPattern GenerateSelectOne(Vector2Int start) {
        CellSelectPattern pattern = new CellSelectPattern(CellSelectPatternType.SelectOne);
        pattern.Add(start);
        return pattern;
    }

    CellSelectPattern GenerateLine(Vector2Int start, int maxLength, HashSet<Vector2Int> occupied) {
        CellSelectPattern pattern = new CellSelectPattern(CellSelectPatternType.Line);
        pattern.Add(start);

        Vector2Int[] directions = {
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1)
        };

        Vector2Int dir = directions[Random.Range(0, directions.Length)];
        Vector2Int current = start;

        for (int i = 1; i < maxLength; i++) {
            Vector2Int next = current + dir;
            if (!IsValidPosition(next) || occupied.Contains(next))
                break;
            pattern.Add(next);
            current = next;
        }
        return pattern;
    }

    CellSelectPattern GenerateCorner(Vector2Int start, int maxLength, HashSet<Vector2Int> occupied) {
        CellSelectPattern pattern = new CellSelectPattern(CellSelectPatternType.Corner);
        pattern.Add(start);

        Vector2Int[] firstDirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        Vector2Int dir1 = firstDirs[Random.Range(0, firstDirs.Length)];

        Vector2Int dir2 = (dir1.x != 0)
            ? new Vector2Int(0, Random.value > 0.5f ? 1 : -1)
            : new Vector2Int(Random.value > 0.5f ? 1 : -1, 0);

        Vector2Int current = start;
        int firstLegLength = Random.Range(1, Mathf.Max(2, maxLength / 2));

        for (int i = 0; i < firstLegLength && pattern.Count < maxLength; i++) {
            Vector2Int next = current + dir1;
            if (!IsValidPosition(next) || occupied.Contains(next))
                break;
            pattern.Add(next);
            current = next;
        }

        for (int i = 0; pattern.Count < maxLength; i++) {
            Vector2Int next = current + dir2;
            if (!IsValidPosition(next) || occupied.Contains(next))
                break;
            pattern.Add(next);
            current = next;
        }
        return pattern;
    }

    CellSelectPattern GenerateBox(Vector2Int start, int maxLength, HashSet<Vector2Int> occupied) {
        CellSelectPattern pattern = new CellSelectPattern(CellSelectPatternType.Box);

        int width = Random.Range(2, Mathf.Min(4, maxLength));
        int height = Mathf.Max(1, maxLength / width);

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                Vector2Int pos = start + new Vector2Int(x, y);
                if (!IsValidPosition(pos) || occupied.Contains(pos))
                    continue;
                if (pattern.Count >= maxLength)
                    break;
                pattern.Add(pos);
            }
        }
        return pattern;
    }

    CellSelectPattern GenerateZigzag(Vector2Int start, int maxLength, HashSet<Vector2Int> occupied) {
        CellSelectPattern pattern = new CellSelectPattern(CellSelectPatternType.Zigzag);
        pattern.Add(start);

        Vector2Int current = start;
        Vector2Int mainDir = Random.value > 0.5f ? Vector2Int.right : Vector2Int.down;
        Vector2Int altDir = (mainDir == Vector2Int.right)
            ? (Random.value > 0.5f ? Vector2Int.up : Vector2Int.down)
            : (Random.value > 0.5f ? Vector2Int.left : Vector2Int.right);

        bool useAlt = false;

        for (int i = 1; i < maxLength; i++) {
            Vector2Int dir = useAlt ? altDir : mainDir;
            Vector2Int next = current + dir;

            if (!IsValidPosition(next) || occupied.Contains(next))
                break;

            pattern.Add(next);
            current = next;
            useAlt = !useAlt;
        }
        return pattern;
    }

    void ApplyPatternsToBoard() {
        List<CellSelectPattern> patterns = GeneratePatterns();
        if (patterns.Count == 0) return;

        ShuffleList(patterns);

        int totalCells = 0;
        foreach (var p in patterns)
            totalCells += p.Count;

        HashSet<int> assignedIndices = new();
        HashSet<CellItemType> usedTypes = new();

        // 1. Распределяем гарантированные доли из alwaysAvailableOnRefresh (по количеству ячеек)
        if (_data?.difficulty?.alwaysAvailableOnRefresh != null) {
            foreach (var kvp in _data.difficulty.alwaysAvailableOnRefresh) {
                CellItemType type = kvp.Key;
                if (_unavailableTypes.Contains(type)) continue;

                int targetCellCount = Mathf.RoundToInt(totalCells * kvp.Value);
                int currentCellCount = 0;

                for (int i = 0; i < patterns.Count && currentCellCount < targetCellCount; i++) {
                    if (assignedIndices.Contains(i)) continue;

                    ApplyItemToPattern(patterns[i], type);
                    usedTypes.Add(type);
                    assignedIndices.Add(i);
                    currentCellCount += patterns[i].Count;
                }
            }
        }

        // 1.5 First refresh: guarantee one pattern per type from guaranteedOnFirstRefresh
        if (_isFirstRefresh) {
            foreach (var type in guaranteedOnFirstRefresh) {
                if (_unavailableTypes.Contains(type)) continue;
                if (usedTypes.Contains(type)) continue;
                if (!_typeCooldowns.ContainsKey(type)) continue;

                for (int i = 0; i < patterns.Count; i++) {
                    if (assignedIndices.Contains(i)) continue;
                    ApplyItemToPattern(patterns[i], type);
                    usedTypes.Add(type);
                    assignedIndices.Add(i);
                    break;
                }
            }
            _isFirstRefresh = false;
        }

        // 2. Оставшиеся паттерны заполняем типами из refreshCooldowns
        List<CellItemType> cooldownReady = GetCooldownReadyTypes();

        List<CellItemType> fallbackTypes = new();
        if (_data?.difficulty?.alwaysAvailableOnRefresh != null) {
            fallbackTypes.AddRange(_data.difficulty.alwaysAvailableOnRefresh.Keys);
        }

        List<CellItemType> fillTypes = (cooldownReady.Count > 0 ? cooldownReady : fallbackTypes)
            .FindAll(t => !_unavailableTypes.Contains(t));

        if (fillTypes.Count > 0) {
            for (int i = 0; i < patterns.Count; i++) {
                if (assignedIndices.Contains(i)) continue;
                CellItemType type = fillTypes[Random.Range(0, fillTypes.Count)];
                ApplyItemToPattern(patterns[i], type);
                usedTypes.Add(type);
            }
        }

        // 3. Сбрасываем кулдауны использованных типов из refreshCooldowns
        foreach (var type in usedTypes) {
            if (_typeCooldowns.ContainsKey(type))
                _typeCooldowns[type] = 0f;
        }
    }

    void ApplyItemToPattern(CellSelectPattern pattern, CellItemType type) {
        CellUiItem item = CreateItemForType(type);

        foreach (var pos in pattern.Cells) {
            CellUi cell = _cellsMap.GetValueOrDefault(pos);
            if (cell != null) {
                cell.SetItem(item);
                if (cellColors.TryGetValue(type, out Color color))
                    cell.SetColor(color);
            }
        }
    }

    bool IsValidPosition(Vector2Int pos) {
        return pos.x >= 0 && pos.x < _boardSize.x && pos.y >= 0 && pos.y < _boardSize.y;
    }

    void ShuffleList<T>(List<T> list) {
        for (int i = list.Count - 1; i > 0; i--) {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion

    #region Cell Selection

    bool IsAdjacent(Vector2Int a, Vector2Int b) {
        return a != b && Mathf.Abs(a.x - b.x) <= 1 && Mathf.Abs(a.y - b.y) <= 1;
    }

    bool HasSameContent(CellUi a, CellUi b) {
        if (a == null || b == null) return false;
        return a.Item.Type == b.Item.Type && a.Item.Type != CellItemType.None && a.Item.Icon == b.Item.Icon;
    }

    void StartSelection(CellUi cell) {
        if (cell == null || cell.Item == null || cell.Item.Type == CellItemType.None) return;
        _selectedCells.Add(cell.Position);
        cell.Highlight(true);
        CellUiItemSelectEvent.Trigger(EventStage.Process, cell.Item, _selectedCells.Count);
    }

    bool TryAddToSelection(CellUi cell) {
        if (cell == null || _selectedCells.Count == 0) return false;

        Vector2Int lastPos = _selectedCells[^1];
        if (_selectedCells.Contains(cell.Position) || !IsAdjacent(lastPos, cell.Position))
            return false;

        CellUi firstCell = _cellsMap.GetValueOrDefault(_selectedCells[0]);
        if (!HasSameContent(firstCell, cell))
            return false;

        _selectedCells.Add(cell.Position);
        cell.Highlight(true);
        CellUiItemSelectEvent.Trigger(EventStage.Process, cell.Item, _selectedCells.Count);

        return true;
    }

    void EndSelection() {
        if (_selectedCells.Count > 0) {
            CellUi cell = _cellsMap.GetValueOrDefault(_selectedCells[0]);
            if (cell != null)
                CellUiItemSelectEvent.Trigger(EventStage.End, cell.Item, _selectedCells.Count);
            ClearSelection();

            _cellsMap.ForEach(t => { if (t.Value != null) t.Value.SetActive(false); });

            if (refreshMode == BoardRefreshMode.SemiAuto || refreshMode == BoardRefreshMode.Manual) {
                _isRefreshPaused = true;
            } else {
                _isRefreshPaused = false;
                _refreshTimer = minIntervalBetweenRefreshes;
                refreshProgressbar?.SetValue(_refreshTimer);
                refreshLabel?.SetText(string.Format(_refreshLabelFormat, Mathf.CeilToInt(_refreshTimer)));
            }
            OnBoardSelectEnd?.Invoke();
        }
    }

    public void ClearSelection() {
        _selectedCells.Clear();
    }

    public void OnCellPointerDown(CellUi cell) {
        if (cell != null && cell.Item != null && cell.Item.Type != CellItemType.None) {
            ClearSelection();
            _isSelecting = true;
            OnBoardSelectStart?.Invoke();
            if (pauseTimerOnSelection)
                _isRefreshPaused = true;
            StartSelection(cell);
        }
    }

    public void OnCellPointerEnter(CellUi cell) {
        if (_isSelecting && _selectedCells.Count > 0 && cell != null) {
            if (!TryAddToSelection(cell)) {
                if (cell.Position != _selectedCells[^1]) {
                    _isSelecting = false;
                    EndSelection();
                }
            }
        }
    }

    public void OnCellPointerUp(CellUi cell) {
        _cellsMap.ForEach(t => { if (t.Value != null) t.Value.Highlight(false); });
        if (_isSelecting) {
            _isSelecting = false;
            EndSelection();
        }
    }

    #endregion

    void OnEnable() {
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<LevelProgressEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<LevelProgressEvent>();
    }
}
