using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public enum CellType { Empty = 0, Trap = 1, Unit = 2, Booster = 4, Loot = 8, Block = 16, Open = 32 }
public enum CellEventType { Open, Reveal, Tap }
public enum CellMarkType { None, Free, Maybe, Trap }

public struct CellEvent {
    public CellEventType Type { get; private set; }
    public Cell Cell { get; private set; }

    static CellEvent e;
    public static void Trigger(CellEventType eventType, Cell cell) {
        e.Type = eventType;
        e.Cell = cell;
        MMEventManager.TriggerEvent(e);
    }
}

[System.Serializable]
public class CellNumberData {
    public Sprite sprite;
    public Color color = Color.white;
}

[RequireComponent(typeof(BoxCollider2D))]
public class Cell : SerializedMonoBehaviour {
    public delegate void CellHandler(Cell cell);
    public event CellHandler OnOpen;
    public event CellHandler OnSelect;
    public event CellHandler OnDeselect;

    [SerializeField] SpriteRenderer image;
    [SerializeField] SpriteRenderer imageAdditive;
    [SerializeField] SpriteRenderer selectedImage;
    [SerializeField] bool selectable = true;
    [SerializeField] BoxCollider2D boxCollider;
    [SerializeField] Transform scaler;
    [SerializeField] float inset = 0.1f;
    [SerializeField] List<Sprite> boxes = new List<Sprite>();
    [SerializeField] Dictionary<CellMarkType, MMF_Player> marks = new();

    [Header("Number")]
    [SerializeField] List<CellNumberData> numbers = new List<CellNumberData>();    
    [SerializeField] SpriteRenderer numberImage;
    [SerializeField] Color deactivatedNumberColor = Color.grey;
    [SerializeField] bool deactivateNumber = true;
    [SerializeField] bool showNumbers = true;

    [Header("Events")]
    [SerializeField] UnityEvent OnStartShow;
    [SerializeField] UnityEvent OnShow;
    [SerializeField] UnityEvent OnStartHide;
    [SerializeField] UnityEvent OnHide;
    [SerializeField] UnityEvent OnReveal;
    [SerializeField] UnityEvent OnActivate;
    [SerializeField] UnityEvent OnDeactivate;
    [SerializeField] UnityEvent OnHighlightOn;
    [SerializeField] UnityEvent OnHighlightOff;
    [SerializeField] UnityEvent OnSelectOn;
    [SerializeField] UnityEvent OnSelectOff;

    public Vector2Int Position { get; private set; }
    public int Number { get; private set; }
    public CellType Type { get; private set; }
    public CellItem Item { get; private set; }
    public bool Visible { get; private set; }
    public bool Active { get; private set; }
    public bool Higlighted { get; private set; }
    public bool Selected { get; private set; }
    public bool Revealed { get; private set; }
    public CellMarkType MarkState { get; private set; }  

    Color _color;

    public void Setup(CellType cellType, Vector2Int position, float size) {
        SetType(cellType);
        Position = position;
        boxCollider.size = new Vector2(size, size);
        Active = true;
        if (image != null) {
            image.sprite = boxes != null ? boxes[Random.Range(0, boxes.Count)] : null;
            if (imageAdditive != null)
                imageAdditive.sprite = image.sprite;
        }

        if (scaler != null)
            scaler.localScale *= size;
    }

    public void SetType(CellType cellType) {
        Type = cellType;
        if (image != null)
            image.gameObject.SetActive(Type != CellType.Open);        
    }

    public void SetNumber(int value) {
        Number = value;
        if (showNumbers && Number > 0) {
            int realValue = Number - 1;
            if (numberImage != null && numbers != null && realValue >= 0 && realValue < numbers.Count) {
                CellNumberData number = numbers[realValue];
                if (number != null) {
                    numberImage.sprite = number.sprite;
                    numberImage.color = number.color;
                    if (selectedImage != null) {
                        Color color = numberImage.color;
                        color.a = 0.5f;
                        selectedImage.color = color;
                    }
                    _color = number.color;
                }
            }
        }
    }

    public void SetItem(CellType cellType, CellItem item) {
        if (item != null) {
            Type = cellType;
            Item = item;
        }
    }

    public void CheckTap(Vector2 position) {
        if (boxCollider.bounds.Contains(position) && Active && !Revealed && (Visible || Number > 0)) {
            if (!Visible)
                SetSelected(!Selected, true);
            else
                OnOpen?.Invoke(this);

            CellEvent.Trigger(CellEventType.Tap, this);
        }
    }

    public void SetSelected(bool value, bool spawnEvents) {
        if (selectable && Selected != value) {
            Selected = value;

            if (Selected) {
                if (spawnEvents)
                    OnSelect?.Invoke(this);
                OnSelectOn?.Invoke();
            } else {
                if (spawnEvents)
                    OnDeselect?.Invoke(this);
                OnSelectOff?.Invoke();
            }
        }
    }

    public void SetVisible(bool value, float delay) {
        if (Visible != value) {
            Visible = value;
            if (value) {
                OnStartShow?.Invoke();
            } else {
                if (Item != null)
                    Item.Highlight(false);
                Highlight(false, true);
                OnStartHide?.Invoke();                
                CellEvent.Trigger(CellEventType.Open, this);
            }

            StartCoroutine(SetVisibleCo(value, delay));
        }
    }

    IEnumerator SetVisibleCo(bool value, float delay) {
        yield return new WaitForSeconds(delay);
        if (value)
            OnShow?.Invoke();
        else
            OnHide?.Invoke();
        if (Item != null)
            Item.SetVisible(!value);
        if (Type == CellType.Empty && !value)
            ItemDropRequestEvent.Trigger(0, 3, transform.position);            
    }

    public bool IsNear(Cell cell) {
        return this != cell && Mathf.Abs(cell.Position.x - Position.x) <= 1 && Mathf.Abs(cell.Position.y - Position.y) <= 1;
    }

    public void Highlight(bool value, bool force = false) {
        if (Higlighted != value && (Visible || force)) {
            Higlighted = value;
            if (value)
                OnHighlightOn?.Invoke();
            else
                OnHighlightOff?.Invoke();
        }
    }

    public bool IsValidForFlood(Cell cell, bool onlyEmpty) {
        return (onlyEmpty ? Type == CellType.Empty : (Type != CellType.Unit || Type == CellType.Booster)) && IsNear(cell);
    }

    public void Reveal() {
        if (!Revealed) {
            Revealed = true;
            if (Item != null) {
                Item.Highlight(false);
                Item.SetVisible(false);
            }
            Highlight(false, true);
            OnReveal?.Invoke();
            CellEvent.Trigger(CellEventType.Reveal, this);
        }
    }

    public void SetActive(bool value) {
        if ((Visible || Number > 0) && Active != value) {
            Active = value;

            if (Number > 0 && deactivateNumber)
                numberImage.color = value ? _color : deactivatedNumberColor;

            if (Visible) {
                if (value)
                    OnActivate?.Invoke();
                else
                    OnDeactivate?.Invoke();
            }
        }
    }

    public bool SetupMark(CellMarkType markType) {
        if (marks != null && Visible && MarkState != markType) {
            MarkState = markType;
            MMF_Player mark = marks.GetValueOrDefault(markType);
            if (mark != null)
                mark.PlayFeedbacks();
            return true;
        }
        return false;
    }
}
