using UnityEngine;

public class ScreenBounds : MonoBehaviour {
    [SerializeField] BoxCollider2D top;
    [SerializeField] BoxCollider2D left;
    [SerializeField] BoxCollider2D right;
    [SerializeField] BoxCollider2D bottom;
    [SerializeField] float size = 2;
    [SerializeField] float muiltiplier = 1;
    [SerializeField] float offset;
    [SerializeField] bool once;

    Camera _camera;
    Vector2 _screenSize;

    void Awake() {
        _camera = Camera.main;
        UpdateBounds();
    }

    void FixedUpdate() {
        if (!once)
            UpdateBounds();
    }

    void UpdateBounds() {
        Vector2 screenSize = _camera.GetScreenSize() * muiltiplier;
        if (_screenSize != screenSize) {
            _screenSize = screenSize;

            Vector2 topOffset = new Vector2(0, _screenSize.y * 0.5f + size * 0.5f + offset);
            Vector2 topSize = new Vector2(_screenSize.x + 2 * size, size);
            Vector2 rightOffset = new Vector2(_screenSize.x * 0.5f + size * 0.5f + offset, 0);
            Vector2 rightSize = new Vector2(size, _screenSize.y +  2 * size);

            if (top != null) {
                top.offset = topOffset;
                top.size = topSize;
            }

            if (top != null) {
                bottom.offset = -topOffset;
                bottom.size = topSize;
            }

            if (right != null) {
                right.offset = rightOffset;
                right.size = rightSize;
            }

            if (left != null) {
                left.offset = - rightOffset;
                left.size = rightSize;
            }
        }
    }
}
