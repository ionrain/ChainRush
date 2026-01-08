using UnityEngine;

public class RectTransformPositionSetter : MonoBehaviour {
    RectTransform _rectTransform;

    void Awake() {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void SetY(int y) {
        if (_rectTransform)
            _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, -y);
    }
}
