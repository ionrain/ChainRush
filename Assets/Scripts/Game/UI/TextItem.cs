using UnityEngine;
using TMPro;

public class TextItem : MonoBehaviour {
    [SerializeField] TextMeshProUGUI label;

    public void Setup(string text) {
        if (label != null)
            label.text = text;
    }
}
