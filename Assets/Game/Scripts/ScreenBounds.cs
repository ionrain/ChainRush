using UnityEngine;

public class ScreenBounds : MonoBehaviour {
    [SerializeField] BoxCollider2D top;
    [SerializeField] BoxCollider2D left;
    [SerializeField] BoxCollider2D right;
    [SerializeField] BoxCollider2D bottom;
    [SerializeField] float size = 2;
    [SerializeField] float muiltiplier = 1;

    Camera _camera;
    Vector2 _screenSize;

    void Awake() {
        _camera = Camera.main;
    }

    void FixedUpdate() {
        Vector2 screenSize = _camera.GetScreenSize() * muiltiplier;
        if (_screenSize != screenSize) {
            _screenSize = screenSize;

            if (top != null && bottom != null && left != null && right != null) {
                top.offset = new Vector2(0, _screenSize.y * 0.5f + size * 0.5f);
                top.size = new Vector2(_screenSize.x + 2 * size, size);
                bottom.offset = -top.offset;
                bottom.size = top.size;

                right.offset = new Vector2(_screenSize.x * 0.5f + size * 0.5f, 0);
                right.size = new Vector2(size, _screenSize.y +  2 * size);
                left.offset = - right.offset;
                left.size = right.size;
            }
        }
    }
}
