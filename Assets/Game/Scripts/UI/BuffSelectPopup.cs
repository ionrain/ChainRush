using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.Utilities;
using UnityEngine;

public struct BuffSelectEvent {
    public EventStage EventStage { get; private set; }
    public List<BuffData> Data { get; private set; }
    public int Iteration { get; private set; }
    static BuffSelectEvent e;
    public static void Trigger(EventStage eventStage, List<BuffData> data, int iteration = 0) {
        e.EventStage = eventStage;
        e.Data = data;
        e.Iteration = iteration;
        MMEventManager.TriggerEvent(e);
    }
}

public class BuffSelectPopup : Popup<List<BuffData>>, MMEventListener<BuffSelectEvent> {
    [SerializeField] Dictionary<int, List<GameObject>> iterationObjects = new();

    [Header("Items")]
    [SerializeField] BuffSelectItem prefab;
    [SerializeField] Transform itemsRoot;

    int _iteration = 0;
    List<BuffSelectItem> _items = new(); 

    public override bool Setup(List<BuffData> value) {
        if (base.Setup(value) && SpawnItems()) {
            iterationObjects.ForEach(t => {
                if (t.Value != null) {
                    bool isCurrent = t.Key <= _iteration;
                    t.Value.ForEach(p => {
                        if (p != null)
                            p.SetActive(isCurrent);
                        else
                            Debug.LogErrorFormat("BuffSelectPopup Setup: Object is NULL for iteration {0}", t.Key);
                    });
                } else
                    Debug.LogErrorFormat("BuffSelectPopup Setup: Objects List is NULL for iteration {0}", t.Key);
            });
            return true;
        }
        return false;
    }

    bool SpawnItems() {
        if (data != null && itemsRoot != null && prefab != null) {
            _items.Clear();
            itemsRoot.DestroyChildren();
            foreach (BuffData buffData in data) {
                BuffSelectItem item = Instantiate(prefab, itemsRoot);
                item.OnClick += OnItemClicked;
                item.Setup(buffData);
                // item.SetTakeAction(() => {
                //     TakeItem(item);
                //     SetVisibility(false);
                // });
                _items.Add(item);
            }
            if (_items.Count > 0)
                _items[0].SetSelected(true);
            return true;
        }
        return false;
    }

    void TakeItem(BuffSelectItem item) {
        if (item != null && item.Data != null)
            BuffSelectEvent.Trigger(EventStage.End, new List<BuffData> { item.Data });
    }

    public void TakeAll() {
        _items.ForEach(t => TakeItem(t));
        SetVisibility(false);
    }

    public void Reroll() {
        SpawnItems();
    }

    void OnItemClicked(BuffSelectItem item) {
        TakeItem(item);
        SetVisibility(false);
        // _items.ForEach(t => {
        //     if (t != item)
        //         t.SetSelected(false);
        // });
    }

    public void OnMMEvent(BuffSelectEvent e) {
        if (e.EventStage == EventStage.Start) {
            _iteration = e.Iteration;
            if (Setup(e.Data))
                SetVisibility(true);
        }
    }

    void OnEnable() {
        this.MMEventStartListening<BuffSelectEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<BuffSelectEvent>();
    }
}
