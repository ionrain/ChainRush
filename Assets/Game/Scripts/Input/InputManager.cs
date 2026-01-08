using System.Collections;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

[System.Flags]
public enum InputEventType { None = 0, Press = 1, Release = 2, Tap = 4, Move = 8, All = Press | Release | Tap | Move }

public struct InputEvent {
    public InputEventType Type { get; private set; }
    public Vector2 Position {get; private set; }

    static InputEvent e;
    public static void Trigger(InputEventType eventType, Vector2 position) {
        e.Type = eventType;
        e.Position = position;
        MMEventManager.TriggerEvent(e);
    }
}

public class InputManager : MonoBehaviour {
    [SerializeField] InputEventType eventTypes = InputEventType.All;

    Controls _controls;
    Camera _mainCamera;
    float _cameraZ;
    bool _enabled;

    void Awake() {
        _controls = new Controls();
        _mainCamera = Camera.main;
        _cameraZ = _mainCamera.transform.position.z;
    }

    Vector2 GetPosition() {
        if (_controls != null)
            return ConvertScreenToWorld(_controls.Touch.Position.ReadValue<Vector2>());
        return Vector2.zero;
    }

    Vector2 ConvertScreenToWorld(Vector2 screen) {
        return _mainCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cameraZ));
    }

    void OnEnable() {
        EnableInput();
    }

    void EnableInput() {
        if (_controls != null && !_enabled) {
            _enabled = true;
            _controls.Enable();
            EnhancedTouchSupport.Enable();
            _controls.Touch.Press.started += TouchPressed;
            _controls.Touch.Release.performed += TouchReleased;
            _controls.Touch.Tap.performed += TouchTapped;
            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerMove += TouchMoved;
        } else
            Debug.LogWarning("InputManager OnEnable: controls is NULL");
    }

    void DisableInput() {
        if (_controls != null && _enabled) {
            _enabled = false;
            _controls.Touch.Press.started -= TouchPressed;
            _controls.Touch.Release.started -= TouchReleased;
            _controls.Touch.Tap.performed -= TouchTapped;
            UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerMove -= TouchMoved;
            _controls.Disable();
            EnhancedTouchSupport.Disable();
        } else
            Debug.LogWarning("InputManager OnDisable: controls is NULL");
    }

    void TouchPressed(InputAction.CallbackContext context) {
        SendEvent(InputEventType.Press, GetPosition());
    }

    void TouchReleased(InputAction.CallbackContext context) {
        SendEvent(InputEventType.Release, GetPosition());
    }

    void TouchTapped(InputAction.CallbackContext context) {
        SendEvent(InputEventType.Tap, GetPosition());
    }

    void TouchMoved(Finger finger) {
        SendEvent(InputEventType.Move, ConvertScreenToWorld(finger.screenPosition));
    }

    void SendEvent(InputEventType eventType, Vector2 position) {
        if (eventTypes.HasFlag(eventType))
            InputEvent.Trigger(eventType, position);
    }

    void OnDisable() {
        DisableInput();
    }
}
