using MoreMountains.Tools;
using UnityEngine;

public class TabUnlockCondition : MonoBehaviour, MMEventListener<GameSettingsEvent> {
    public delegate void TabUnlockConditionEvent(bool result);
    public event TabUnlockConditionEvent OnCheck;

    [SerializeField] protected TriggerMode triggerMode;

    protected virtual string PlayerPrefsId => gameObject.name;

    protected virtual bool WasShown => PlayerPrefs.HasKey(PlayerPrefsId);

    protected virtual void Start() {
        if (triggerMode.HasFlag(TriggerMode.OnStart))
            Check();
    }

    public virtual void Check() {
        
    }

    public virtual void InvokeOnCheck(bool result) {
        if (result && PlayerPrefsId.Length > 0) {
            PlayerPrefs.SetInt(PlayerPrefsId, 1);
            PlayerPrefs.Save();
        }
        OnCheck?.Invoke(result);
    }

    protected virtual void ClearPlayerPrefs() {
        if (PlayerPrefsId.Length > 0 && PlayerPrefs.HasKey(PlayerPrefsId))
            PlayerPrefs.DeleteKey(PlayerPrefsId);
    }

    public virtual void Disable() {
        //gameObject.SetActive(false);
    }

    public void OnMMEvent(GameSettingsEvent e) {
        if (e.Action == GameSettingsAction.Reset)
            ClearPlayerPrefs();
    }

    protected virtual void OnEnable() {
        this.MMEventStartListening<GameSettingsEvent>();
    }

    protected virtual void OnDisable() {
        this.MMEventStopListening<GameSettingsEvent>();
    }
}
