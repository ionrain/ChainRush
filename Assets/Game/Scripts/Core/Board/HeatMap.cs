using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.Utilities;
using UnityEngine;

public enum HeatMapOperation { Add, Max }
public enum HeatMapMode { Static, Dynamic }

public class HeatMap : MonoBehaviour, MMEventListener<CellEvent>, MMEventListener<BoardEvent>, MMEventListener<LevelStageStateEvent> {
    [SerializeField] CellType target;
    [SerializeField] HeatMapMode mode = HeatMapMode.Static;
    [SerializeField] HeatMapOperation operation = HeatMapOperation.Add;
    [SerializeField] int spread = 5;

    [Header("Colorize settings")]
    [SerializeField] Gradient gradient;
    [SerializeField] Color targetColor = Color.white;
    [SerializeField] bool colorTarget = true;
    [SerializeField] SpriteRenderer itemPrefab;
    [SerializeField] Transform itemsRoot;

    Dictionary<Vector2Int, SpriteRenderer> _items = new();

    public void OnMMEvent(CellEvent e) {
        if (mode == HeatMapMode.Dynamic && e.Cell != null && (e.Type == CellEventType.Open || e.Type == CellEventType.Reveal) &&
            _items.TryGetValue(e.Cell.Position, out SpriteRenderer item))
            item.gameObject.SetActive(true);
    }

    public void OnMMEvent(BoardEvent e) {
        if (e.Type == BoardEventType.SetupItems && e.Board != null && e.Board.Cells != null && itemsRoot != null && itemPrefab != null) {
            float size = e.Board.HalfCellSize * 2;
            Vector2 cellSize = new Vector2(size, size);
            var cells = new List<Cell>(e.Board.AllCells);
            var targetCells = cells.FindAll(t => t.Type == target);
            float gradientStep = 1f / Mathf.Max(1, spread);
            Dictionary<Vector2Int, float> heatMap = new();
            targetCells.ForEach(t => cells.Remove(t));
            targetCells.ForEach(t => {
                if (colorTarget)
                    CreateItem(t.Position, t.transform.localPosition, cellSize, targetColor);
                cells.ForEach(c => {
                    Vector2Int distance = c.Position - t.Position;
                    int maxDistance = Mathf.Max(Mathf.Abs(distance.x), Mathf.Abs(distance.y));
                    if (maxDistance <= spread) {
                        float value = heatMap.GetValueOrDefault(c.Position, 0f);
                        float delta = (spread - maxDistance) * gradientStep;
                        heatMap[c.Position] = operation == HeatMapOperation.Add ? Mathf.Min(delta + value, 1) : Mathf.Max(delta, value);
                    }
                });
            });
            heatMap.ForEach(t => {
                var cell = cells.Find(c => c.Position == t.Key);
                if (cell != null && t.Value >= gradientStep)
                    CreateItem(t.Key, cell.transform.localPosition, cellSize, gradient.Evaluate(1 - t.Value));
            });
        }
    }

    void CreateItem(Vector2Int key, Vector3 position, Vector2 cellSize, Color color) {
        SpriteRenderer item = Instantiate(itemPrefab, itemsRoot);
        item.size = cellSize;
        item.color = color;
        item.transform.localPosition = position;
        if (mode == HeatMapMode.Dynamic)
            item.gameObject.SetActive(false);
        _items[key] = item;
    }

    public void OnMMEvent(LevelStageStateEvent e) {
        if (e.State == LevelStageState.Start && e.EventStage == EventStage.Start) {
            if (itemsRoot != null)
                itemsRoot.DestroyChildren();
            _items.Clear();
        }
    }

    void OnEnable() {
        this.MMEventStartListening<LevelStageStateEvent>();
        this.MMEventStartListening<CellEvent>();
        this.MMEventStartListening<BoardEvent>();
    }
    void OnDisable() {
        this.MMEventStopListening<LevelStageStateEvent>();
        this.MMEventStopListening<CellEvent>();
        this.MMEventStopListening<BoardEvent>();
    }

}
