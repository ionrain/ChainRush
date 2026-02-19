using System.Collections;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public enum CellEventType { Open, Reveal, Tap }

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

public class Cell : SerializedMonoBehaviour {
    [SerializeField] Transform scaler;
    [SerializeField] float inset = 0.1f;

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
    public CellItem Item { get; private set; }
    public bool Visible { get; private set; }
    public bool Active { get; private set; }
    public bool Higlighted { get; private set; }
    public bool Selected { get; private set; }

    public void Setup(Vector2Int position, float size) { 
        Position = position;
        Active = true;

        if (scaler != null)
            scaler.localScale *= size;
    }

    public void SetItem(CellItem item) {
        Item = item;     
    }

    public void SetSelected(bool value, bool spawnEvents) {
        if (Selected != value) {
            Selected = value;

            if (Selected)
                OnSelectOn?.Invoke();
            else
                OnSelectOff?.Invoke();
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

    public void SetActive(bool value) {
        if (Visible && Active != value) {
            Active = value;

            if (Visible) {
                if (value)
                    OnActivate?.Invoke();
                else
                    OnDeactivate?.Invoke();
            }
        }
    }
}
