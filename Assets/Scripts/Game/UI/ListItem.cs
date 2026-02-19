using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public interface IListItemData {
    public string Title { get; }
    public string Description { get; }
    public Sprite Icon { get; }
}

public interface IListItem {
    public bool Select { get; set; }    
    public bool SetSelected(bool selected);
    public void Show();
}

public abstract class ListItem<T> : MonoBehaviour, IListItem {
    public delegate void ItemSelectEvent(T item);
    public event ItemSelectEvent OnSelect;
    public virtual bool Select { get { return false; } set { } }

    [Header("Events")]
    [SerializeField] UnityEvent OnSelected;
    [SerializeField] UnityEvent OnDeselected;

    [Header("UI")]
    [SerializeField] protected Image icon;
    [SerializeField] protected TextMeshProUGUI label;
    

    protected Button _button;

    protected virtual void Awake() {
        _button = GetComponent<Button>();
    }

    protected virtual void OnEnable() {
        if (_button != null)
            _button.onClick.AddListener(OnClicked);
    }

    protected virtual void OnDisable() {
        if (_button != null)
            _button.onClick.RemoveListener(OnClicked);
    }

    public virtual void OnClicked() {
        OnSelect?.Invoke((T)(object)this);
    }

    public virtual bool SetSelected(bool selected) => false;

    public virtual void Show() { }
}

public abstract class ListItem<T, P> : MonoBehaviour, IListItem
    where T : MonoBehaviour
    where P : IListItemData {
    public delegate void ListItemEvent(T item);
    public event ListItemEvent OnClick;

    [Header("Events")]
    [SerializeField] UnityEvent OnSelected;
    [SerializeField] UnityEvent OnDeselected;

    [Header("UI")]
    [SerializeField] protected Image icon;
    [SerializeField] protected Image background;
    [SerializeField] protected Image border;
    [SerializeField] protected Image selected;    
    [SerializeField] protected TextMeshProUGUI label;
    [SerializeField] protected bool select = true;
    public bool Select { get { return select; } set { select = value; } }

    protected Button _button;
    protected P _data;
    protected bool _selected;

    public P Data => _data;

    protected virtual void Awake() {
        _button = GetComponent<Button>();
    }

    public virtual void Setup(P data) {
        _selected = false;
        _data = data;
    }

    protected virtual void OnEnable() {
        if (_button != null)
            _button.onClick.AddListener(OnClicked);
    }

    protected virtual void OnDisable() {
        if (_button != null)
            _button.onClick.RemoveListener(OnClicked);
    }

    public virtual void OnClicked() {
        if (!select || SetSelected(true))
            OnClick?.Invoke((T)(object)this);
    }

    public virtual bool SetSelected(bool value) {
        if (_selected != value) {
            _selected = value;
            if (value)
                OnSelected?.Invoke();
            else
                OnDeselected?.Invoke();
            return true;
        }
        return false;
    }

    public virtual void Show() { }
}