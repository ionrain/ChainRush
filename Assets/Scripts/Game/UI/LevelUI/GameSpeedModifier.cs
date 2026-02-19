using UnityEngine;
using UnityEngine.Events;

public class GameSpeedModifier : MonoBehaviour {
    public static string TurnOnPrefsKey => "GameSpeedModifierTurnOn";

    [SerializeField] float speed = 2f;

    [Header("Events")]
    [SerializeField] UnityEvent OnTurnOn;
    [SerializeField] UnityEvent OnTurnOff;
    [SerializeField] UnityEvent<float> OnChange;

    bool _activated = false;
    bool _started = false;

    void Awake() {
        if (PlayerPrefs.GetInt(TurnOnPrefsKey, 1) == 1) {
            _activated = true;
            OnTurnOn?.Invoke();
        }
    }

    public static void ClearPlayerPrefs() {
        PlayerPrefs.DeleteKey(TurnOnPrefsKey);
    }

    public void Toggle() {
        if (_activated)
            OnTurnOff?.Invoke();
        else
            OnTurnOn?.Invoke();

        if (_started)
            SetSpeed(_activated ? 1f : speed);

        _activated = !_activated;
        PlayerPrefs.SetInt(TurnOnPrefsKey, _activated ? 1 : 0);
        PlayerPrefs.Save();
    }

    void SetSpeed(float value) {
        Time.timeScale = value;
        OnChange?.Invoke(value);
    }

    public void StartModification() {
        _started = true;
        if (_activated)
            SetSpeed(speed);
    }

    public void FinishModification() {
        _started = false;
        if (_activated)
            SetSpeed(1);
    }
}
