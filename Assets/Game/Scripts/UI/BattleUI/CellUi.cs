using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MoreMountains.Tools;
using UnityEngine.Events;

public enum CellItemType { None = 0, Unit = 1, HeroSkill = 2, Buff = 4, Booster = 8, SoftCurrency = 16 }

public class CellUiItem {
    public CellItemType Type { get; private set; }
    public Sprite Icon { get; private set; }
    public string Id { get; private set; }

    public CellUiItem(CellItemType type, Sprite icon, string id) {
        Type = type;
        Icon = icon;
        Id = id;
    }
}

public struct CellUiItemSelectEvent {
    public EventStage Stage { get; private set; }
    public CellUiItem Item { get; private set; }
    public int Count { get; private set; }

    static CellUiItemSelectEvent e;
    public static void Trigger(EventStage stage, CellUiItem item, int count) {
        e.Stage = stage;
        e.Item = item;
        e.Count = count;
        MMEventManager.TriggerEvent(e);
    }
}
    
public class CellUi : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IBeginDragHandler, IDragHandler, IEndDragHandler {
    public delegate void CellUIEvent(CellUi cell);
    public event CellUIEvent OnCellPointerDownEvent;
    public event CellUIEvent OnCellPointerUpEvent;
    public event CellUIEvent OnCellPointerEnterEvent;

    [SerializeField] Image background;
    [SerializeField] Image icon;
    [SerializeField] float iconSizePercent = 50;

    [Header("Events")]
    [SerializeField] UnityEvent OnSelect;
    [SerializeField] UnityEvent OnDeselect;
    [SerializeField] UnityEvent OnActivate;
    [SerializeField] UnityEvent OnDeactivate;    

    public Vector2Int Position { get; private set; }
    public CellUiItem Item { get; private set; }
    public bool Highlighted { get; private set; }
    public bool Active { get; private set; } = true;


    public void Setup(Vector2Int position, float cellSize) {
        Position = position;
        name = $"CellUI_{position.x}x{position.y}";

        // Adjust icon size based on iconSizePercent
        if (icon != null) {
            RectTransform iconRect = icon.rectTransform;
            if (iconRect != null) {
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;

                float size = cellSize * (iconSizePercent / 100f);
                iconRect.sizeDelta = new Vector2(size, size);
            }
        }
    }

    public void SetItem(CellUiItem item) {
        Item = item;
        icon.sprite = item != null ? item.Icon : null;
    }

    public void SetColor(Color color) {
        if (background != null)
            background.color = color;
    }

    public void Highlight(bool value) {
        if (Highlighted != value) {
            Highlighted = value;
            if (value)
                OnSelect?.Invoke();
            else
                OnDeselect?.Invoke();
        }
    }

    public void SetActive(bool value) {
        if (Active != value) {
            Active = value;
            if (value)
                OnActivate?.Invoke();
            else {
                OnDeactivate?.Invoke();
                Highlight(false);
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData) {
        if (Active) {
            Highlight(true);
            OnCellPointerDownEvent?.Invoke(this);
        }
    }

    public void OnPointerUp(PointerEventData eventData) {
        if (Active) {
            Highlight(false);
            OnCellPointerUpEvent?.Invoke(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) {
        if (Active && eventData.dragging) {
            Highlight(true);
            OnCellPointerEnterEvent?.Invoke(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData) {
        // Необходимо для активации dragging
    }

    public void OnDrag(PointerEventData eventData) {
        // Необходимо для поддержания dragging
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (Active) {
            Highlight(false);
            OnCellPointerUpEvent?.Invoke(this);
        }
    }
}
