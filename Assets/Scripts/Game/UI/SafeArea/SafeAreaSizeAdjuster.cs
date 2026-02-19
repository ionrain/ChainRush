using UnityEngine;

public class SafeAreaSizeAdjuster : MonoBehaviour {
    [SerializeField] bool horizontal = true;
    [SerializeField] bool vertical = true;

    RectTransform _rectTransform;
    float _size;
    bool _initialized;

    void Initialize() {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null) {
            Debug.LogError("SafeAreaSizeAdjuster requires a RectTransform component.");
            return;
        }
        _size = _rectTransform.sizeDelta.y;
        _initialized = true;
    }

    public void OnRefresh(RectOffset rect) {
        if (!_initialized)
            Initialize();
        
        if (_rectTransform != null && rect != null) {
            float newSize = _size + rect.top;
            _rectTransform.sizeDelta = new Vector2(horizontal ? newSize : _rectTransform.sizeDelta.x, vertical ? newSize : _rectTransform.sizeDelta.y);
        }
    }
}
