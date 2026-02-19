using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class LoadingProgressLabel : MonoBehaviour {
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] LocalizedString pattern;

    string _pattern = string.Empty;

    void Awake() {
        if (pattern != null && !pattern.IsEmpty)
            _pattern = pattern.GetLocalizedString();
    }

    public void SetProgress(float progress) {
        if (label != null && _pattern.Length > 0)
            label.SetText(string.Format(_pattern, (int)(progress * 100)));
    }
}
