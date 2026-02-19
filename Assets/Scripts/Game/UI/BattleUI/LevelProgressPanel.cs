using System;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;

public class LevelProgressPanel : MonoBehaviour, MMEventListener<LevelLoadEvent>, MMEventListener<LevelProgressEvent>{
    [SerializeField] Progressbar progressbar;
    [SerializeField] TextMeshProUGUI label;

    LevelGoalType _goalType;
    int _targetValue;

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null && e.Data.Goal != null && progressbar != null) {
            _goalType = e.Data.Goal.GoalType;
            _targetValue = e.Data.Goal.GoalAmount;
            //progressbar.Setup();
            //progressbar.SetTotal(_duration);
            //progressbar?.SetValue(0f);
        }
    }

    public void OnMMEvent(LevelProgressEvent e) {
        progressbar?.SetValue(e.Progress);
        if (_goalType == LevelGoalType.Survive) {
            TimeSpan time = TimeSpan.FromSeconds(e.Time);
            label?.SetText(time.ToString(@"m\:ss"));
        } else if (_goalType == LevelGoalType.Distance) {
            label?.SetText(string.Format("{0:F0}/{1}", e.Distance, _targetValue));
        }

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
