using UnityEngine;
using UnityEngine.Events;

public class LocationIcon : MonoBehaviour {
    [SerializeField] UnityEvent OnLocked;
    [SerializeField] UnityEvent OnUnlocked;
    [SerializeField] UnityEvent OnUnlock;

    public void SetLocked(bool value) {
        if (value)
            OnLocked?.Invoke();
        else
            OnUnlocked?.Invoke();
    }

    public void Unlock() {
        OnUnlock?.Invoke();
    }
}
