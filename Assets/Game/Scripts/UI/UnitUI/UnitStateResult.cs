using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class UnitStateResult : SerializedMonoBehaviour {
    [SerializeField] Dictionary<UnitState, UnityEvent> events = new();

    UnitData _data;

    public void Setup(UnitData data) {
        _data = data;
    }

    public void CheckState() {
        if (_data == null || events == null) return;
        UnitState state = _data.State;
        if (events.TryGetValue(state, out UnityEvent unityEvent))
            unityEvent?.Invoke();
    }
}
