using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SafeAreaFiller : MonoBehaviour {
    RectTransform _rectTransform;

    void Awake() {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Refresh(RectOffset rect) {
        if (_rectTransform != null && rect != null)
            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rect.top);
    }
}
