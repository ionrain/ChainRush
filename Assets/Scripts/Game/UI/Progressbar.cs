using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Progressbar : MonoBehaviour {
    [SerializeField] bool initializeOnAwake;
    [SerializeField] bool convertToPercent;
    [SerializeField] float value;
    [SerializeField] float total;
    [SerializeField] bool changeColor;
    [SerializeField] Gradient color;

    [Header("UI Elements")]
    [SerializeField] TextMeshProUGUI valueText;
    [SerializeField] string format = "{0}/{1}";
    [SerializeField] Image progressFill;

    float _width;

    public float Progress { get; private set; }
    public float CurrentWidth => progressFill != null ? progressFill.rectTransform.rect.width : 0;

    public void Awake() {
        if (initializeOnAwake) {
            Setup();
            //SetValue(0);
        }
    }

    public void SetMaxWidth(float value) {
        _width = value;
    }

    public void Setup() {
        if (progressFill != null) {
            if (progressFill.type != Image.Type.Filled)
                SetMaxWidth(CurrentWidth);
        }
    }

    public void SetTotal(float total, bool refresh = false) {
        this.total = total;
        if (refresh)
            SetValue(value);
    }

    public void SetValue(float newValue) {
        value = newValue;
        Progress = Mathf.Clamp01(value / total);
        if (progressFill != null) {
            if (progressFill.type == Image.Type.Filled)
                progressFill.fillAmount = Progress;
            else if (_width > 0)
                progressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Progress * _width);
            if (changeColor && color != null)
                progressFill.color = color.Evaluate(Progress);
        }
        if (valueText != null)
            valueText.text = string.Format(format, convertToPercent ? (int)(value*100) : value, convertToPercent ? (int)(total*100) : total);
    }
}
