using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class CitadelPanelItem : SerializedMonoBehaviour {
    [SerializeField] Vector2Int position;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] GameObject upgradeMark;
    [SerializeField] Button button;

    [Header("Events")]
    [SerializeField] UnityEvent OnActivate;
    [SerializeField] UnityEvent OnDeactivate;
    [SerializeField] UnityEvent OnDeselect;
    [SerializeField] UnityEvent OnSelect;
    [SerializeField] UnityEvent OnUpgrade;

    public Vector2Int Position => position;

    public void Setup(UnityAction action) {
        if (button != null && action != null)
            button.onClick.AddListener(action);
    }

    public void SetSelected(bool value) {
        if (button != null)
            button.enabled = !value;

        if (value)
            OnSelect?.Invoke();
        else
            OnDeselect?.Invoke();
    }

    public void SetActive(bool value) {
        if (value)
            OnActivate?.Invoke();
        else
            OnDeactivate?.Invoke();
    }

    public void Upgrade() {
        OnUpgrade?.Invoke();
    }

    public void SetAttributeAmount(int value) {
        label?.SetText(value.ToString());
    }

    public void SetUpgradeMark(bool value) {
        if (upgradeMark != null)
            upgradeMark.SetActive(value);
    }
}
