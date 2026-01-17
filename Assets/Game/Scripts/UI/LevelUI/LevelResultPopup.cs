using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelResultPopup : RewardPopup, MMEventListener<LevelResultEvent>, MMEventListener<BalanceResourcesEvent> {
    [Header("Level Reward Popup")]
    [SerializeField] protected LevelResult showCondition = LevelResult.None;
    [SerializeField] protected AllUnitsData units;
    [SerializeField] protected Button replayButton;
    [SerializeField] protected ConfirmationPopup confirmationPopup;
    [SerializeField] protected SceneLoader sceneLoader;   

    bool _useEnergy = true;

    public void UseEnergy(bool value) {
        _useEnergy = value;
    }

    void RequestEnergyBalance() {
        if (_useEnergy)
            BalanceResourcesEvent.Trigger(EventStage.Start, new List<ResourceType> { ResourceType.Energy });
    }

    protected void LoadScene(SceneName sceneName, LevelActionType action) {
        if (confirmationPopup != null && sceneLoader != null)
            if (confirmationPopup.Setup(() => LoadSceneAction(sceneName, action)))
                confirmationPopup.SetVisibility(true);
    }

    protected void LoadSceneAction(SceneName sceneName, LevelActionType action) {
        SetTimescale(1f);
        SetVisibility(false);
        LevelActionEvent.Trigger(EventStage.Start, action);
        if (action == LevelActionType.Reload && _useEnergy)
            SpendResourceEvent.Trigger(EventStage.Start, ResourceType.Energy, ResourceTarget.LevelStart, "LevelReplay", units.energyPrice);
        sceneLoader.LoadScene(sceneName);
    }

    public virtual void Home(bool showConfirmation = true) {
        if (showConfirmation)
            LoadScene(SceneName.Main, LevelActionType.Quit);
        else
            LoadSceneAction(SceneName.Main, LevelActionType.Quit);
    }

    public virtual void Restart() {
        LoadScene(SceneName.Level, LevelActionType.Reload);
    }

    public virtual void OnMMEvent(LevelResultEvent e) {
        if (showCondition == e.Result && Setup(e.Data)) {
            RequestEnergyBalance();
            SetVisibility(true);
        }
    }

    public void OnMMEvent(BalanceResourcesEvent e) {
        if (e.Stage == EventStage.End && e.Balance.ContainsKey(ResourceType.Energy) && units != null && replayButton != null && _useEnergy)
            replayButton.interactable = e.Balance.GetValueOrDefault(ResourceType.Energy).Value >= units.energyPrice;
    }  

    protected override void OnEnable() {
        this.MMEventStartListening<LevelResultEvent>();
        this.MMEventStartListening<BalanceResourcesEvent>();
    }

    protected override void OnDisable() {
        this.MMEventStopListening<LevelResultEvent>();
        this.MMEventStopListening<BalanceResourcesEvent>();
    }

}
