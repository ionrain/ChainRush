using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using Unity.Cinemachine;
using UnityEngine;

public class CinemachineCameraManager :SerializedMonoBehaviour,
    MMEventListener<UnitActionEvent>,
    MMEventListener<BoardUiEvent>,
    MMEventListener<LevelLoadEvent>
{
    [SerializeField] CinemachineCamera cinemachine;
    [SerializeField] BoxCollider2D confinerBox;
    [SerializeField] Dictionary<Transform, float> offsetTargets = new();

    float _goalDistance;

    public void OnMMEvent(UnitActionEvent e) {
        if (e.Type == UnitActionType.Spawn && e.Unit != null && cinemachine != null)
            cinemachine.Follow = e.Unit.transform;
    }

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null && e.Data.Goal != null)
            _goalDistance = e.Data.Goal.GoalType == LevelGoalType.Distance ? e.Data.Goal.GoalAmount : 0;
    }

    public void OnMMEvent(BoardUiEvent e) {
        if (e.Type == BoardUiEventType.Ready && e.Board != null) {
            if (confinerBox == null) return;
            float yOffset = UpdateConfinerSize(e.Board.PanelHeightPercent);

            offsetTargets.ForEach(t => t.Key.position = new Vector3(t.Key.position.x, yOffset + t.Value, t.Key.position.z));
        }
    }

    float UpdateConfinerSize(float panelHeightPercent) {
        Camera cam = Camera.main;
        if (confinerBox == null || cam == null) return 0;

        float width, visibleHeight;

        if (cam.orthographic) {
            visibleHeight = 2f * cam.orthographicSize;
            width = visibleHeight * cam.aspect;
        } else {
            float distance = Mathf.Abs(cam.transform.position.z - confinerBox.transform.position.z);
            float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            visibleHeight = 2f * distance * Mathf.Tan(halfFovRad);
            width = visibleHeight * cam.aspect;
        }

        if (_goalDistance > 0)
            width += _goalDistance;

        float panelFraction = panelHeightPercent / 100f;
        float yOffset = visibleHeight * 0.5f * panelFraction;

        confinerBox.size = new Vector2(width + 0.1f, visibleHeight + 1f);
        confinerBox.offset = Vector2.zero;

        Vector3 pos = confinerBox.transform.position;
        pos.x = _goalDistance * 0.5f;
        pos.y = yOffset;
        confinerBox.transform.position = pos;
        return yOffset;
    }

    void OnEnable() {
        this.MMEventStartListening<UnitActionEvent>();
        this.MMEventStartListening<BoardUiEvent>();
        this.MMEventStartListening<LevelLoadEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<UnitActionEvent>();
        this.MMEventStopListening<BoardUiEvent>();
        this.MMEventStopListening<LevelLoadEvent>();
    }
}
