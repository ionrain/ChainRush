using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class TabSelectButton : MonoBehaviour {
    public delegate void TabSelectButtonEvent(TabSelectButton button);
    public event TabSelectButtonEvent OnTabSelectButtonPress;

    [Header("General")]
    [SerializeField] bool defaultTab;
    [SerializeField] UnityEvent selectEvent;
    [SerializeField] UnityEvent deselectEvent;

    [Header("Bindings")]
    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] RectTransform icon;
    [SerializeField] TextMeshProUGUI label;

    [Header("Normal State")]
    [SerializeField] float normalScale = 0.7f;
    [SerializeField] float normalWidth = 200;
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color normalTextColor = Color.white;

    [Header("Active State")]
    [SerializeField] float selectedScale = 1;
    [SerializeField] float selectedWidth = 270;
    [SerializeField] Color selectedColor = Color.white;
    [SerializeField] Color selectedTextColor = Color.white;

    public bool Active => button != null ? !button.interactable : false;

    void Start() {
        if (defaultTab)
            OnPress();
        else
            Deselect(true);
    }

    void OnEnable() {
        if (button != null)
            button.onClick.AddListener(OnPress);
    }

    void OnDisable() {
        if (button != null)
            button.onClick.RemoveListener(OnPress);
    }

    public void Deselect(bool force = false) {
        if (button != null && (!button.interactable || force)) {
            deselectEvent?.Invoke();
            button.interactable = true;
            UpdateView(normalScale, normalWidth, normalColor, normalTextColor);
        }
    }

    void UpdateView(float scale, float width, Color color, Color textColor) {
        if (background != null) {
            if (width > 0)
                background.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            background.color = color;
        }
        if (icon != null)
            icon.DOScale(new Vector3(scale, scale, scale), 0.1f);
        if (label != null)
            label.color = textColor;
    }

    public void InvokeSelectEvent() {
        selectEvent?.Invoke();
    }

    public void OnPress() {
        selectEvent?.Invoke();
        if (button != null)
            button.interactable = false;
        UpdateView(selectedScale, selectedWidth, selectedColor, selectedTextColor);
        OnTabSelectButtonPress?.Invoke(this);
    }
}
