using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.Utilities;
using UnityEngine;
using UnityEngine.Localization;

public enum GameNotificationType { Default, Information, Error, LevelGoalComplete, LevelSuccess, LevelFailure,
Boss, Progress, StageStart, StageBattle, StageComplete, StageClearBonus }

[System.Serializable]
public class GameNotificationTemplate {
    public GameNotificationType notificationType;
    public bool useCache;
    public GameObject templatePrefab;
}

public struct GameNotificationEvent {
    public EventStage Stage { get; private set; }
    public GameNotificationType Type { get; private set; }
    public string Message { get; private set; }
    public Sprite Sprite { get; private set; }
    public object Param { get; private set; }

    static GameNotificationEvent e;
    public static void Trigger(EventStage stage, GameNotificationType notificationType, string message, Sprite sprite, object param = null) {
        e.Stage = stage;
        e.Type = notificationType;
        e.Message = message;
        e.Sprite = sprite;
        e.Param = param;
        MMEventManager.TriggerEvent(e);
    }
}

public class GameNotificationManager : MonoBehaviour, MMEventListener<GameNotificationEvent>, MMEventListener<LevelGoalResultEvent>,
    MMEventListener<LevelResultEvent>/*, MMEventListener<LevelStageStateEvent> , MMEventListener<PartyUnitEvent>*/ {
    [SerializeField] LocalizedString stagePattern;
    [SerializeField] List<GameNotificationTemplate> notificationTemplates;
    List<GameNotificationTemplate> _cache = new List<GameNotificationTemplate>();

    Queue<GameNotification> notificationQueue = new Queue<GameNotification>();
    bool isShowing = false;
    string _stagePattern = string.Empty;

    void Awake() {
        if (stagePattern != null && !stagePattern.IsEmpty)
        _stagePattern = stagePattern.GetLocalizedString();
    }

    public void Clear() {
        isShowing = false;
        transform.DestroyChildren();
        notificationQueue.ForEach(t => Destroy(t.gameObject));
        notificationQueue.Clear();
    }
    
	void OnEnable () {
        foreach (var template in notificationTemplates)
            if (template.useCache) {
                GameObject prefab = Instantiate(template.templatePrefab, transform);
                prefab.SetActive(false);
                _cache.Add(new GameNotificationTemplate() {
                    notificationType = template.notificationType,
                    useCache = template.useCache,
                    templatePrefab = prefab
                });
            }
        this.MMEventStartListening<GameNotificationEvent>();
        this.MMEventStartListening<LevelGoalResultEvent>();
        this.MMEventStartListening<LevelResultEvent>();
        //this.MMEventStartListening<LevelStageStateEvent>();
        //this.MMEventStartListening<PartyUnitEvent>();
    }

    void EnqueueMessage(GameNotificationType notificationType, string message = "", Sprite sprite = null, object param = null) {
        GameObject notificationObject = null;
        var template = _cache.Find(t => t.notificationType == notificationType);
        if (template != null) {
            _cache.Remove(template);
            notificationObject = template.templatePrefab;
        } else {
            template = notificationTemplates.Find(t => t.notificationType == notificationType);
            if (template != null)
                notificationObject = Instantiate(template.templatePrefab, transform);
        }

        if (template != null && notificationObject != null) {
            GameNotification notification = notificationObject.GetComponent<GameNotification>();
            if (notification != null) {
                if (notification.Setup(message, sprite, param)) {
                    notification.UseCache = template.useCache;
                    notification.NotificationType = template.notificationType;
                    notification.OnFinish += OnNotificationFinished;
                    notificationQueue.Enqueue(notification);
                    ShowMessage();
                }
            } else
                Destroy(notificationObject);
        }
    }

    void OnNotificationFinished(GameNotification notification) {
        if (notification != null) {
            notification.gameObject.SetActive(false);
            notification.OnFinish -= OnNotificationFinished;
            if (notification.UseCache)
                _cache.Add(new GameNotificationTemplate() {
                    notificationType = notification.NotificationType,
                    useCache = notification.UseCache, templatePrefab = notification.gameObject
                });
            else
                Destroy(notification.gameObject);
        }
        isShowing = false;
        ShowMessage();
    }

    void ShowMessage() {
        if (!isShowing && notificationQueue.Count > 0) {
            isShowing = true;
            GameNotification notification = notificationQueue.Dequeue();
            notification.gameObject.SetActive(true);
            notification.Play();
        }
    }

    public void OnMMEvent(GameNotificationEvent e) {
        if (e.Stage == EventStage.Start)
            EnqueueMessage(e.Type, e.Message, e.Sprite, e.Param);
    }

    public void OnMMEvent(LevelGoalResultEvent e) {
        if (e.Goal != null && e.Goal.Achieved) {
            EnqueueMessage(GameNotificationType.LevelGoalComplete, string.Empty, null, e.Goal);
        }
    }

    public void OnMMEvent(LevelResultEvent e) {
        if (e.Result == LevelResult.Success)
            EnqueueMessage(GameNotificationType.LevelSuccess);
        else if (e.Result == LevelResult.Failure)
            EnqueueMessage(GameNotificationType.LevelFailure);
    }

    /*public void OnMMEvent(LevelStageStateEvent e) {
        if (e.EventStage == EventStage.Process) {
            if (e.State == LevelStageState.Start)
                EnqueueMessage(GameNotificationType.StageStart, string.Format(_stagePattern, e.Data.StageIndex + 1));
            else if (e.State == LevelStageState.Battle)
                EnqueueMessage(GameNotificationType.StageBattle);
            else if (e.State == LevelStageState.Complete && e.Data != null && e.IsValid && e.Data.Stage.enemyData != null)
                EnqueueMessage(GameNotificationType.StageComplete);
        }
    }*/

    /*public void OnMMEvent(PartyUnitEvent e) {
        if (e.Type == PartyUnitEventType.ClearBonus && e.EventStage == EventStage.Start && e.Unit != null)
            EnqueueMessage(GameNotificationType.StageClearBonus, string.Empty, e.Unit.Icon);
    }*/


    void OnDisable() {
        _cache.Clear();
        this.MMEventStopListening<GameNotificationEvent>();
        this.MMEventStopListening<LevelGoalResultEvent>();
        this.MMEventStopListening<LevelResultEvent>();
        //this.MMEventStopListening<LevelStageStateEvent>();
        //this.MMEventStopListening<PartyUnitEvent>();
    }
}
