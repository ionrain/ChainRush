using System.Collections;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;

public class StageMetricsPanel : MonoBehaviour, MMEventListener<CellEvent>, MMEventListener<LevelStageStateEvent> {
    [Header("Labels")]
    [SerializeField] TextMeshProUGUI trapsLabel;
    [SerializeField] TextMeshProUGUI unitsLabel;
    [SerializeField] TextMeshProUGUI boostersLabel;

    [Header("Bubble Labels")]
    [SerializeField] TextMeshProUGUI bubbleTrapsLabel;
    [SerializeField] TextMeshProUGUI bubbleUnitsLabel;
    [SerializeField] TextMeshProUGUI bubbleBoostersLabel;

    [Header("Feedbacks")]
    [SerializeField] MMF_Player trapsFeedback;
    [SerializeField] MMF_Player unitsFeedback;
    [SerializeField] MMF_Player boostersFeedback;

    int _trapsCount;
    int _unitsCount;
    int _boostersCount;
    int _trapsDelta;
    int _unitsDelta;
    int _boostersDelta;

    public void Setup(LevelStageStateData data) {
        FillMetrics(data.Stage);
    }

    public void OnMMEvent(LevelStageStateEvent e) {
        if (e.EventStage == EventStage.Start && e.State == LevelStageState.Start)
            Setup(e.Data);
    }

    void FillMetrics(LevelStage stage) {
        _trapsCount = SetText(trapsLabel, stage.TrapsCount);
        _unitsCount = SetText(unitsLabel, stage.unitsCount);
        _boostersCount = SetText(boostersLabel, stage.BoostersCount);
    }

    int SetText(TextMeshProUGUI label, int value) {
        if (label != null)
            label.SetText(value.ToString());
        return value;
    }

    public void OnMMEvent(CellEvent e) {
        if (e.Type != CellEventType.Tap) {
            if (e.Cell.Type == CellType.Trap) {
                _trapsCount = SetText(trapsLabel, _trapsCount - 1);
                _trapsDelta--;
                if (_trapsDelta >= -1)
                    StartCoroutine(ShowTrapsFeedback());
            } else if (e.Type == CellEventType.Open) {
                if (e.Cell.Type == CellType.Unit) {
                    _unitsCount = SetText(unitsLabel, _unitsCount - 1);
                    _unitsDelta--;
                    if (_unitsDelta >= -1)
                        StartCoroutine(ShowUnitsFeedback());
                } else if (e.Cell.Type == CellType.Booster) {
                    _boostersCount = SetText(boostersLabel, _boostersCount - 1);
                    _boostersDelta--;
                    if (_boostersDelta >= -1)
                        StartCoroutine(ShowBoostersFeedback());
                }
            }
        }
    }

    IEnumerator ShowTrapsFeedback() {
        yield return null;
        if (trapsFeedback != null) {
            SetText(bubbleTrapsLabel, _trapsDelta);
            _trapsDelta = 0;
            trapsFeedback.PlayFeedbacks();
        }
    }

    IEnumerator ShowUnitsFeedback() {
        yield return null;
        if (unitsFeedback != null) {
            SetText(bubbleUnitsLabel, _unitsDelta);
            _unitsDelta = 0;
            unitsFeedback.PlayFeedbacks();
        }
    }    

    IEnumerator ShowBoostersFeedback() {
        yield return null;
        if (boostersFeedback != null) {
            SetText(bubbleBoostersLabel, _boostersDelta);
            _boostersDelta = 0;
            boostersFeedback.PlayFeedbacks();
        }
    }

    void OnEnable() {
        this.MMEventStartListening<LevelStageStateEvent>();
        this.MMEventStartListening<CellEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<LevelStageStateEvent>();
        this.MMEventStopListening<CellEvent>();
    }
}
