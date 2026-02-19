using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Flags]
public enum TriggerMode { Script = 0, OnEvent = 1, OnEnable = 2, OnStart = 4, OnAwake = 8 }

public class Triggerable : MonoBehaviour {
    [SerializeField] protected UnityEvent OnTrigger;
    [SerializeField] protected TriggerMode triggerMode;
    [SerializeField] protected bool once;

    protected bool _triggered;
    protected bool _canBeTriggered = true;

    protected virtual void OnEnable() {
        if (triggerMode.HasFlag(TriggerMode.OnEnable))
            Trigger();
    }

    protected virtual void Awake() {
        if (triggerMode.HasFlag(TriggerMode.OnAwake))
            Trigger();
    }

    protected virtual void Start() {
        if (triggerMode.HasFlag(TriggerMode.OnStart))
            Trigger();
    }

    public virtual void Trigger() {
        if (gameObject.activeInHierarchy && _canBeTriggered && (!once || !_triggered)) {
            _triggered = true;
            OnInvoke();
        }
    }

    protected virtual void OnInvoke() {
        OnTrigger?.Invoke();
    }
}
