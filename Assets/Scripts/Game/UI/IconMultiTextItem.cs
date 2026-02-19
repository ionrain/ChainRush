using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IconMultiTextItem : SerializedMonoBehaviour {
    [SerializeField] protected Image icon;
    [SerializeField] protected List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
    [SerializeField] protected Dictionary<TextMeshProUGUI, GameObject> delimiters = new Dictionary<TextMeshProUGUI, GameObject>();
    [SerializeField] protected bool canChangeColor;

    public virtual void Setup(Sprite sprite, List<string> texts) {
        SetIcon(sprite);
        SetTexts(texts);
    }

    public virtual void SetTexts(List<string> texts) {
        if (texts != null) {
            int count = Mathf.Min(labels.Count, texts.Count);
            for (int i = 0; i < count; i++)
                SetText(i, texts[i]);
        } else
            Debug.LogFormat("IconMultiTextItem SetTexts: Texts List is NULL for {0}", gameObject.name);
    }

    public virtual void SetText(int index, string text) {
        TextMeshProUGUI label = labels[index];
        if (label != null)
            label.text = text;
        if (delimiters != null && delimiters.ContainsKey(label))
            delimiters[label]?.SetActive(text.Length > 0);
    }

    public virtual void SetTextColors(List<Color> colors) {
        if (colors != null && canChangeColor) {
            int count = Mathf.Min(labels.Count, colors.Count);
            for (int i = 0; i < count; i++)
                SetTextColor(i, colors[i]);
        } else
            Debug.LogFormat("IconMultiTextItem SetTextColors: Colors List is NULL for {0}", gameObject.name);
    }

    public virtual void SetTextColor(int index, Color color) {
        TextMeshProUGUI label = labels[index];
        if (label != null)
            label.color = color;
    }

    public virtual void SetIconColor(Color color) {
        if (icon != null && canChangeColor)
            icon.color = color;
    }

    public virtual void SetIcon(Sprite sprite) {
        if (icon != null && sprite != null)
            icon.sprite = sprite;
    }
}