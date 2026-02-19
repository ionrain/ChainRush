using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Events;

public enum ExperienceEventType { Consume, Update, Cap, Level }
public enum ExperiencePoint { None = 0, Smallest = 3, Small = 6, Medium = 20, Large = 100, Largest = 200 }

public struct ExperienceEvent {
    public ExperienceEventType EventType { get; private set; }
    public int Value { get; private set; }

    static ExperienceEvent e;
    public static void Trigger(ExperienceEventType eventType, int value) {
        e.EventType = eventType;
        e.Value = value;
        MMEventManager.TriggerEvent(e);
    }
}

public class ExperienceManager : MonoBehaviour, MMEventListener<ExperienceEvent> {
    [SerializeField] bool setupOnStart = true;
    [SerializeField] int initialCap = 10;
    [SerializeField] int perLevelCapDelta = 30;
    [SerializeField] int perLevelCapSpeed = 10;
    [SerializeField] float levelUpNotifyDelay = 0.5f;

    [Header("UI")]
    [SerializeField] Progressbar progressbar;

    [Header("Events")]
    [SerializeField] UnityEvent OnLevelUp;


    int _exp;
    int _cap;
    int _level;

    Queue<int> _pendingLevelUps = new();
    bool _waitingForAck;
    float _notifyTimer;
    bool _notifyScheduled;

    void Awake() {
        if (setupOnStart)
            Setup();
    }

    public void Setup() {
        _exp = 0;
        _level = 0;
        _cap = initialCap;

        _pendingLevelUps.Clear();
        _waitingForAck = false;
        _notifyScheduled = false;

        if (progressbar != null)
            progressbar.Setup();

        UpdateProgessbar();

        ExperienceEvent.Trigger(ExperienceEventType.Update, _exp);
        ExperienceEvent.Trigger(ExperienceEventType.Level, _level);
        ExperienceEvent.Trigger(ExperienceEventType.Cap, _cap);
    }

    void Update() {
        if (_notifyScheduled) {
            _notifyTimer -= Time.unscaledDeltaTime;
            if (_notifyTimer <= 0f) {
                _notifyScheduled = false;
                TryNotifyNextLevelUp();
            }
        }
    }

    void UpdateProgessbar() {
        if (progressbar != null) {
            progressbar.SetTotal(_cap);
            progressbar.SetValue(_exp);
        }
    }

    void TryNotifyNextLevelUp() {
        if (_waitingForAck || _pendingLevelUps.Count == 0) return;
        _waitingForAck = true;
        int level = _pendingLevelUps.Dequeue();
        ExperienceEvent.Trigger(ExperienceEventType.Level, level);
        OnLevelUp?.Invoke();
        ExperienceEvent.Trigger(ExperienceEventType.Cap, _cap);
    }

    public void AcknowledgeLevelUp() {
        _waitingForAck = false;
        if (_pendingLevelUps.Count > 0) {
            _notifyScheduled = true;
            _notifyTimer = levelUpNotifyDelay;
        }
    }

    public void OnMMEvent(ExperienceEvent e) {
        if (e.EventType == ExperienceEventType.Consume) {
            if (e.Value > 0)
                _exp += e.Value;
            else
                _exp = _cap;

            if (_exp >= _cap) {
                while (_exp >= _cap) {
                    _exp -= _cap;
                    _level++;
                    _cap += perLevelCapSpeed + perLevelCapSpeed * _level;
                    _pendingLevelUps.Enqueue(_level);
                }

                TryNotifyNextLevelUp();
            }
            ExperienceEvent.Trigger(ExperienceEventType.Update, _exp);
            UpdateProgessbar();
        }
    }

    void OnEnable() {
        this.MMEventStartListening<ExperienceEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<ExperienceEvent>();
    }
}
