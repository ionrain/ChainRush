using System;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;

public class LevelProgressPanel : MonoBehaviour, MMEventListener<LevelLoadEvent>, MMEventListener<LevelProgressEvent>{
    [SerializeField] Progressbar progressbar;
    [SerializeField] TextMeshProUGUI timeLabel;

    float _duration;

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null && progressbar != null) {
            _duration = e.Data.Goal.GoalAmount;
            //progressbar.Setup();
            //progressbar.SetTotal(_duration);
            //progressbar?.SetValue(0f);
        }
    }

    public void OnMMEvent(LevelProgressEvent e) {
        progressbar?.SetValue(e.Progress);
        TimeSpan time = TimeSpan.FromSeconds(e.Time);
        timeLabel?.SetText(time.ToString(@"m\:ss"));
    }

    void OnEnable() {
        this.MMEventStartListening<LevelLoadEvent>();
        this.MMEventStartListening<LevelProgressEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelLoadEvent>();
        this.MMEventStopListening<LevelProgressEvent>();
    }
}
