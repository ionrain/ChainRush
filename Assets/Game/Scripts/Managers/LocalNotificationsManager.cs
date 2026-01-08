using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Notifications;
using UnityEngine;
using UnityEngine.Localization;

public class LocalNotificationsManager : MonoBehaviour {
    [SerializeField] LocalizedString title;
    [SerializeField] LocalizedString text;
    bool _canSend;

    void Awake() {
        var args = NotificationCenterArgs.Default;
        args.AndroidChannelId = "default";
        args.AndroidChannelName = "Notifications";
        args.AndroidChannelDescription = "Main notifications";
        NotificationCenter.Initialize(args);
    }

    IEnumerator Start() {
        var request = NotificationCenter.RequestPermission();
        if (request.Status == NotificationsPermissionStatus.RequestPending)
            yield return request;
        _canSend = request.Status == NotificationsPermissionStatus.Granted && title != null && !title.IsEmpty && text != null && !text.IsEmpty;
        NotificationCenter.CancelAllScheduledNotifications();
    }

    void OnApplicationQuit() {
        if (_canSend) {
            var notification = new Notification() { Title = title.GetLocalizedString(), Text = text.GetLocalizedString() };
            NotificationCenter.ScheduleNotification(notification, new NotificationIntervalSchedule(TimeSpan.FromHours(24)));
        }
    }
}
