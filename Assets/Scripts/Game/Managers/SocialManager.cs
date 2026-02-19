using UnityEngine;
/*#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif*/
using MoreMountains.Tools;

public enum SocialEventType { ShowLeaderboard }

public struct SocialEvent {
    public SocialEventType Type { get; private set; }

    static SocialEvent e;
    public static void Trigger(SocialEventType type) {
        e.Type = type;
        MMEventManager.TriggerEvent(e);
    }
}

public class SocialManager : MonoBehaviour/*, MMEventListener<LevelResultEvent>, MMEventListener<SocialEvent>*/ {
    string _leaderboardId = "CgkI77fc49wKEAIQAA";
    bool _authenticated = false;

    /*public void Start() {
        //PlayGamesPlatform.DebugLogEnabled = true;
        #if UNITY_ANDROID
        PlayGamesPlatform.Instance.Authenticate(ProcessAuthentication);
        #endif
    }

    #if UNITY_ANDROID
    *void ProcessAuthentication(SignInStatus result) {
        _authenticated = result == SignInStatus.Success;
        ConsoleEvent.Trigger(string.Format("SocialManager ProcessAuthentication: result = {0}", result));
    }
    #endif

    void ShowLeaderboard() {
        #if UNITY_ANDROID
        if (_authenticated)
            PlayGamesPlatform.Instance.ShowLeaderboardUI(_leaderboardId);
        else
            ConsoleEvent.Trigger("SocialManager ShowLeaderboard: not authenticated");
        #endif
    }

    void PublishScore(int score) {
        #if UNITY_ANDROID
        if (_authenticated) 
            PlayGamesPlatform.Instance.ReportScore(score, _leaderboardId, "", (bool success) => {   
                ConsoleEvent.Trigger(string.Format("SocialManager PublishScore: result = {0}", success));
            });
        else
            ConsoleEvent.Trigger("SocialManager PublishScore: not authenticated");             
        #endif
    }

    public void OnMMEvent(LevelResultEvent e) {
        if (e.Data != null && (e.Result == LevelResult.Success || e.Result == LevelResult.Failure))
            PublishScore(e.Data.Score.TotalScore);
    }

    public void OnMMEvent(SocialEvent e) {
        if (e.Type == SocialEventType.ShowLeaderboard)
            ShowLeaderboard();
    }

    void OnEnable() {
        this.MMEventStartListening<LevelResultEvent>();
        this.MMEventStartListening<SocialEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelResultEvent>();
        this.MMEventStopListening<SocialEvent>();
    }*/
}
