using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TabUnlocker : MonoBehaviour {
    [SerializeField] TabUnlockCondition condition;
    [SerializeField] float unlockDelay = 2;

    [Header("Bindings")]
    [SerializeField] Button button;
    [SerializeField] Image icon;
    [SerializeField] Color lockedColor;
    [SerializeField] MMFeedbacks feedback;
    [SerializeField] TabUnlockPopup popup;

    [Header("Events")]
    [SerializeField] UnityEvent OnStartUnlocking;
    [SerializeField] UnityEvent OnCompleteUnlocking;
    [SerializeField] UnityEvent OnLocked;
    [SerializeField] UnityEvent OnUnlocked;

    void Awake() {
        if (condition != null)
            condition.OnCheck += OnChecked;
        else
            Disable();
    }

    public void Disable() {
        gameObject.SetActive(false);
    }

    void OnChecked(bool result) {
        if (result)
            StartUnlocking();
        else
            SetLocked(true);
    }

    public void TryUnlock() {
        condition?.Check();
    }

    void CompleteUnlocking() {
        if (popup != null)
            popup.OnHidden.RemoveListener(CompleteUnlocking);
        if (feedback != null) {
            feedback.PlayFeedbacks();
            if (button != null)
                button.enabled = true;
        } else
            SetLocked(false);
        OnCompleteUnlocking?.Invoke();
    }

    void StartUnlocking() {
        OnStartUnlocking?.Invoke();
        if (popup != null && icon != null && popup.Setup(icon.sprite)) {
            popup.SetFlyToPoisition(transform.position);
            popup.OnHidden.AddListener(CompleteUnlocking);
            popup.SetShowDelay(unlockDelay);
            popup.SetVisibility(true);
        } else
            CompleteUnlocking();
    }

    public void SetLocked(bool locked) {
        if (button != null)
            button.enabled = !locked;
        if (icon != null)
            icon.color = locked ? lockedColor : Color.white;
        if (locked)
            OnLocked?.Invoke();
        else
            OnUnlocked.Invoke();
    }
}
