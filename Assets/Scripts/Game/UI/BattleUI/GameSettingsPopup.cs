using MoreMountains.TopDownEngine;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.UI;

public class GameSettingsPopup : Popup<int> {
    [SerializeField] UnityEvent OnUnityEditor;
    [SerializeField] Switch joystickSwitch;
    [SerializeField] Switch targetedAdsSwitch;
    [SerializeField] Slider musicSlider;
    [SerializeField] Slider soundSlider;
    [SerializeField] ConfirmationPopup confirmationPopup;
    [SerializeField] SceneLoader loader;
    [SerializeField] LocalizedString versionPattern;
    [SerializeField] TextMeshProUGUI versionLabel;

    void ShowConfirmation(UnityAction action) {
        if (confirmationPopup != null && confirmationPopup.Setup(action))
            confirmationPopup.SetVisibility(true);
    }

    public void TurnOffTutorial() {
        if (loader != null)
            ShowConfirmation(() => { 
                GameSettingsEvent.Trigger(EventStage.Start, GameSettingsAction.TurnOffTutorial);
                loader.LoadScene();
            });
    }

    public void ResetSettings() {
        if (loader != null)
            ShowConfirmation(() => { 
                GameSettingsEvent.Trigger(EventStage.Start, GameSettingsAction.Reset);
                loader.LoadScene();
            });
    }

    public void ToggleVibration(bool value) {
        VibrationSettingsEvent.Trigger(value);
    }

    public void AllowTargetedAds(bool value) {
        //PlayerPrefs.SetInt(GameSettingsManager.TargetedAdsParamName, value ? 1 : 0);
    }

    Switch.SwitchStates GetSwitchState(string paramName, int defaultValue = 0) {
        return PlayerPrefs.GetInt(paramName, defaultValue) == 1 ? Switch.SwitchStates.On : Switch.SwitchStates.Off;
    }

    public override void SetVisibility(bool visible) {
        if (visible) {
            #if UNITY_EDITOR || CHEATS
                OnUnityEditor?.Invoke();
            #endif

            if (joystickSwitch != null)
                joystickSwitch.InitialState = GetSwitchState(GameManager.VibrationParamName, 1);

            //if (targetedAdsSwitch != null)
            //    targetedAdsSwitch.InitialState = GetSwitchState(GameSettingsManager.TargetedAdsParamName);

            if (musicSlider != null)
                musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(GameManager.MusicVolumeParamName));

            if (soundSlider != null)
                soundSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(GameManager.SoundVolumeParamName));

            if (versionLabel != null && versionPattern != null && !versionPattern.IsEmpty)
                versionLabel.text = string.Format(versionPattern.GetLocalizedString(), Application.version);
        } else {
            VolumeSettingsEvent.Trigger(VolumeActionType.Save);
        }
        base.SetVisibility(visible);
    }

    public void MusicVolumeChange(float volume) {
        VolumeSettingsEvent.Trigger(VolumeActionType.UpdateMusic, volume);
    }

    public void SoundVolumeChange(float volume) {
        VolumeSettingsEvent.Trigger(VolumeActionType.UpdateSound, volume);
    }

    public void OpenPrivacyPolicy() {
        Application.OpenURL("http://morboo.com/privacy-policy/");
    }

    public void OpenTermsOfUse() {
        Application.OpenURL("http://morboo.com/terms-of-use/");
    }
}
