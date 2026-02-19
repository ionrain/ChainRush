using UnityEngine;
using UnityEngine.Localization;

public class GameNotificationTrigger : EventTrigger<GameNotificationType> {
    [Header("Game Notification Trigger")]
    [SerializeField] protected LocalizedString message;
    [SerializeField] protected Sprite sprite;
    [SerializeField] protected float interval;

    protected float _nextTime;

    protected override void OnInvoke() {
        base.OnInvoke();
        string text = message != null && !message.IsEmpty ? FormatMessage() : string.Empty;
        GameNotificationEvent.Trigger(EventStage.Start, eventType, text, sprite);
    }

    public virtual string FormatMessage() {
        return message.GetLocalizedString();
    }

    public override void Trigger() {
        _canBeTriggered = Time.time >= _nextTime;
        if (_canBeTriggered)
            _nextTime = Time.time + interval;
        base.Trigger();
    }
}
