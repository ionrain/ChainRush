using UnityEngine;
using TMPro;
using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine.UI;

public class GameNotification : MonoBehaviour {
    public delegate void GameNotificationEventHandler(GameNotification notification);
    public event GameNotificationEventHandler OnFinish;

    [SerializeField] protected string messageTemplate = "{0}";
    [SerializeField] protected TextMeshProUGUI messageText;
    [SerializeField] protected Image icon;
    [SerializeField] protected float delay = 0;
    [SerializeField] protected float duration = 2;
    [SerializeField] protected MMFeedbacks feedback;

    public GameNotificationType NotificationType { get; set; }
    public bool UseCache { get; set; }

    protected string _message = string.Empty;

    public virtual bool Setup(string message, Sprite sprite, object param) {
        if (messageText != null && message != string.Empty) {
            _message = message;
            messageText.text = string.Format(messageTemplate, _message);
        }
        if (icon != null)
            icon.sprite = sprite;
        return true;
    }

    public virtual void Play() {
        StartCoroutine(Play(delay));
    }

    protected virtual IEnumerator Play(float delay) {
        if (delay > 0)
            yield return new WaitForSecondsRealtime(delay);
        if (feedback != null)
            feedback.PlayFeedbacks();
        StartCoroutine(Stop(duration));
    }

    protected virtual IEnumerator Stop(float delay) {
        yield return new WaitForSecondsRealtime(delay);
        OnFinish?.Invoke(this);
    }
}
