using MoreMountains.Tools;
using UnityEngine;

public class UnitItem : ListItem<UnitItem, UnitData>, MMEventListener<UnitEvent> {
    [Header("UnitItem")]
    [SerializeField] Progressbar cardProgressbar;
    [SerializeField] Color lockedColor = Color.white;
    [SerializeField] GameObject lockedIcon;
    [SerializeField] GameObject readyIcon;
    [SerializeField] GameObject notification;
    [SerializeField] GameObject cardIcon;

    public override void Setup(UnitData data) {
        base.Setup(data);
        if (_data != null) {
            if (label != null)
                label.SetText((_data.Level + 1).ToString());
            if (icon != null)
                icon.sprite = _data.Icon;
            if (cardProgressbar != null) {
                cardProgressbar.Setup();
                UpdateCards();
            }
                
            UpdateLockedUI();
        } else
            Debug.LogFormat("HeroItem Setup: HeroData is NULL for {0}", gameObject.name);
    }

    public void UpdateCards() {
        if (cardProgressbar != null) {
            cardProgressbar.SetTotal(_data.GetUpgradeCardPrice());
            cardProgressbar.SetValue(_data.CardBalance);
        }
    }

    public void CheckNotification(int softBalance) {
        if (_data != null) {
            SetNotification(_data.CanBeUpgraded(softBalance));
        } else
            Debug.LogFormat("HeroItem CheckNotification: HeroData is NULL for {0}", gameObject.name); 
    }

    public void SetNotification(bool enabled) {
        if (notification != null)
            notification.SetActive(enabled);
    }

    void UpdateLockedUI() {
        if (icon != null)
            icon.color = _data.Unlocked ? Color.white : lockedColor;
        if (lockedIcon != null)
            lockedIcon.SetActive(_data.State == UnitState.Locked);
        if (readyIcon != null)
            readyIcon.SetActive(_data.State == UnitState.ReadyToBeUnlocked);
        if (cardIcon != null)
            cardIcon.SetActive(_data.State >= UnitState.Available);
        if (cardProgressbar != null)
            cardProgressbar.gameObject.SetActive(_data.State >= UnitState.Available);
    }

    public void OnMMEvent(UnitEvent e) {
        if (_data != null && e.Data == _data) {
            if (e.Type == UnitEventType.ChangeState) {
                UpdateLockedUI();
                if (notification != null && notification.activeSelf && _data.State == UnitState.Available)
                    SetNotification(false);
            } else if (e.Stage == EventStage.End && (e.Type == UnitEventType.CardBalanceChange || e.Type == UnitEventType.LevelUp))
                UpdateCards();
        }
    }

    protected override void OnEnable() {
        this.MMEventStartListening<UnitEvent>();
        base.OnEnable();
    }

    protected override void OnDisable() {
        this.MMEventStopListening<UnitEvent>();
        base.OnDisable();
    }
}
