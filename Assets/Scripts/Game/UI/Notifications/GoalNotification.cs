using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public class GoalNotification : GameNotification {
    [Header("Goal Notification")]
    [SerializeField] LocalizedString notificationPattern;

    public override bool Setup(string message, Sprite sprite, object param) {
        LevelGoal goal = param as LevelGoal;
        if (notificationPattern != null && goal != null)
            return base.Setup(notificationPattern.GetLocalizedString(), sprite, param);
        return false;
    }
}
