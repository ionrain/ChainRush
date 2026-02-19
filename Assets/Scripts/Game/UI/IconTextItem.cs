using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IconTextItem : MonoBehaviour {
    [SerializeField] Image icon;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] bool canChangeColor;

    public string Text => label.text;

    public void Setup(Sprite sprite, string text) {
        SetIcon(sprite);
        SetText(text);
    }

    public void SetText(string text) {
        if (label != null && text.Length > 0)
            label.SetText(text);
    }

    public void SetTextColor(Color color) {
        if (label != null && canChangeColor)
            label.color = color;
    }

    public void SetIconColor(Color color) {
        if (icon != null && canChangeColor)
            icon.color = color;
    }

    public void SetIcon(Sprite sprite) {
        if (icon != null && sprite != null)
            icon.sprite = sprite;
    }
}
