using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


public class BuffSelectItem : ListItem<BuffSelectItem, BuffData> {
    [SerializeField] TextMeshProUGUI oldValueLabel;
    [SerializeField] TextMeshProUGUI valueLabel;
    [SerializeField] Button takeButton;
    [SerializeField] UnityEvent OnShow;
    [SerializeField] UnityEvent OnSetup;

    public void SetTakeAction(UnityAction action) {
        if (takeButton != null && action != null)
            takeButton.onClick.AddListener(action);
    }

    public override void Setup(BuffData data) {
        base.Setup(data);
        if (_data != null && icon != null && background != null && label != null && valueLabel != null && oldValueLabel != null
            && border != null && selected != null) {
            Color c = data.borderColor;
            selected.color = new Color(c.r, c.b, c.g, selected.color.a);
            border.color = c;
            label.SetText(_data.Title);
            valueLabel.SetText(_data.Description);
            oldValueLabel.SetText(_data.OldDescription);
            icon.sprite = _data.Icon;
            background.color = _data.backgroundColor;
            OnSetup?.Invoke();
        }
    }

    public override void Show() {
        OnShow?.Invoke();
    }
}
