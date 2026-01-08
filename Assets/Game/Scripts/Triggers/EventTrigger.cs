using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventTrigger<T> : Triggerable
    where T: System.Enum {
    [Header("Event Trigger")]
    [SerializeField] protected T eventType;
}
