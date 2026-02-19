using System;
using System.Collections;
using MoreMountains.Tools;
using UnityEngine;

#if UNITY_IOS
using UnityEngine.iOS;
#endif

#if UNITY_ANDROID
using Google.Play.Review;
#endif


public class RatePopup : Popup<int>, MMEventListener<GameSettingsEvent> {
    const string rate_time_key = "RateTime";
    const string rate_complete_key = "RateComplete";

    [SerializeField] LevelData completedLevel;
    [SerializeField] int time = 259200;

    #if UNITY_ANDROID
    ReviewManager _googleReviewManager;
    #endif

    void ClearPlayerPrefs() {
        PlayerPrefs.DeleteKey(rate_time_key);
        //PlayerPrefs.DeleteKey(rate_complete_key);
    }

    void Start() {
        #if UNITY_ANDROID
        _googleReviewManager = new ReviewManager();
        #endif       

        int lastInt = PlayerPrefs.GetInt(rate_time_key, 0);
        int delta = lastInt > 0 ? (DateTime.Now - DateTime.FromBinary(lastInt)).Seconds : time;
        bool rightLevel = completedLevel != null && completedLevel.State >= LevelState.Passed;
        if (PlayerPrefs.GetInt(rate_complete_key, 0) == 0 && delta >= time && rightLevel)
            SetVisibility(true);
    }

    public void OpenNativeRatePopup() {
        #if UNITY_ANDROID
        StartCoroutine(ShowAndroidReviewPopup());
        #endif
        #if UNITY_IOS
        if (Device.RequestStoreReview())
            PlayerPrefs.SetInt(rate_complete_key, 1);
        #endif
        SetVisibility(false);
    }

    #if UNITY_ANDROID
    IEnumerator ShowAndroidReviewPopup() {
        if (PlayerPrefs.GetInt(rate_complete_key, 0) == 0) { 
            if (_googleReviewManager == null) {
                ConsoleEvent.Trigger("RatePopup ShowAndroidReviewPopup: ReviewManager is NULL");
                yield break;
            }
            var requestFlowOperation = _googleReviewManager.RequestReviewFlow();
            yield return requestFlowOperation;
            if (requestFlowOperation.Error != ReviewErrorCode.NoError) {
                ConsoleEvent.Trigger(string.Format("RatePopup ShowAndroidReviewPopup: requestFlowOperation error {0}", requestFlowOperation.Error.ToString()));
                yield break;
            }
            var launchFlowOperation = _googleReviewManager.LaunchReviewFlow(requestFlowOperation.GetResult());
            yield return launchFlowOperation;
            if (launchFlowOperation.Error != ReviewErrorCode.NoError) {
                ConsoleEvent.Trigger(string.Format("RatePopup ShowAndroidReviewPopup: launchFlowOperation error {0}", launchFlowOperation.Error.ToString()));
                yield break;
            }
        } else
            Application.OpenURL ("market://details?id=" + Application.identifier);

        PlayerPrefs.SetInt(rate_complete_key, 1);
    }
    #endif

    public override void SetVisibility(bool visible) {
        if (!visible)
            PlayerPrefs.SetInt(rate_time_key, (int)DateTime.Now.ToBinary());
        base.SetVisibility(visible);
    }

    public void OnMMEvent(GameSettingsEvent e) {
        if (e.Action == GameSettingsAction.Reset)
            ClearPlayerPrefs();
    }

    void OnEnable() {
        this.MMEventStartListening<GameSettingsEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<GameSettingsEvent>();
    }
}
