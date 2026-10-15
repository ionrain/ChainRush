#if UNITY_EDITOR
using Core.Events;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    [RequireComponent(typeof(Camera)), DefaultExecutionOrder(9000)]
    public sealed class PerformanceBenchCameraController : MonoBehaviour, IEventListener<PerformanceBenchCameraSettingsEvent>
    {
        Camera _camera;
        void Awake() => _camera = GetComponent<Camera>();
        void OnEnable() { if (Application.isPlaying) EventBus.Register<PerformanceBenchCameraSettingsEvent>(this); }
        void OnDisable() { if (Application.isPlaying) EventBus.Unregister<PerformanceBenchCameraSettingsEvent>(this); }
        public void OnEvent(PerformanceBenchCameraSettingsEvent e)
        {
            // Keep both patrol endpoints and the visible room inside portrait Game Views.
            _camera.orthographicSize = Mathf.Max(21, 22 / _camera.aspect) / e.Density;
            transform.SetPositionAndRotation(new Vector3(0, 0, -50), Quaternion.identity);
        }
        void Update()
        {
            float depth = -transform.position.z;
            var minimum = _camera.ViewportToWorldPoint(new Vector3(0, 0, depth));
            var maximum = _camera.ViewportToWorldPoint(new Vector3(1, 1, depth));
            EventBus.Trigger(new PerformanceBenchViewEvent(Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y)));
        }
    }
}
#endif
