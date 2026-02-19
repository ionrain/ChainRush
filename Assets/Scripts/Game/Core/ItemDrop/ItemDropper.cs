using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public struct ItemDropRequestEvent {
    public int ChannelId { get; private set; }
    public int RequesterId { get; private set; }
    public Vector2 Position { get; private set; }

    static ItemDropRequestEvent e;
    public static void Trigger(int channelId, int requesterId, Vector2 position) {
        e.ChannelId = channelId;
        e.RequesterId = requesterId;
        e.Position = position;
        MMEventManager.TriggerEvent(e);
    }
}

public struct ItemDropEvent {
    public GameObject Item { get; private set; }

    static ItemDropEvent e;

    public static void Trigger(GameObject item) {
        e.Item = item;
        MMEventManager.TriggerEvent(e);
    }
}

public class DropData {
    public float emptyProbability;
    public Dictionary<GameObject, float> options = new Dictionary<GameObject, float>();


    public float GetProbability(GameObject prefab) {
        return options.GetValueOrDefault(prefab, 0) * (1 - emptyProbability);
    }

    public void NormalizeProbabilities() {
        float totalProbability = 0;
        foreach (var option in options)
            totalProbability += option.Value;

        List<GameObject> keys = new List<GameObject>(options.Keys);
        foreach (var key in keys)
            options[key] /= totalProbability;
    }

    public GameObject GetRandom() {
        if (Random.value >= emptyProbability) {
            float probability = Random.value;
            float sum = 0;
            float newSum = 0;
            foreach (var option in options) {
                newSum = sum + option.Value;
                if (probability > sum && probability <= newSum)
                    return option.Key;
                else
                    sum = newSum;
            }
        }
        return null;
    }
}

public class ItemDropper : SimplePoolUser, MMEventListener<ItemDropRequestEvent> {
    [Header("Item Dropper")]
    [SerializeField] protected int channelId = 0;
    [SerializeField] protected Dictionary<int, DropData> dropList = new Dictionary<int, DropData>();

    [Header("Exclusions")]
    [SerializeField] protected List<GameObject> dontDropOnStageComplete = new List<GameObject>();
    [SerializeField] protected List<GameObject> dontDropOnLevelEnd = new List<GameObject>();

    public int ChannelId => channelId;

    protected virtual void Awake() {
        foreach (var pair in dropList)
            pair.Value.NormalizeProbabilities();
        SetupPools();
    }

    protected virtual void OnEnable() {
        this.MMEventStartListening<ItemDropRequestEvent>();
    }

    public virtual void OnMMEvent(ItemDropRequestEvent e) {
        if (channelId == 0 || e.ChannelId == channelId) {
            if (dropList.ContainsKey(e.RequesterId)) {
                DropData data = dropList[e.RequesterId];
                if (data != null)
                    StartCoroutine(Drop(data, e.Position));
            }
        }
    }

    protected virtual KeyValuePair<GameObject, int> GetDropPrefab(DropData data) {
        return data != null ? new KeyValuePair<GameObject, int>(data.GetRandom(), 0) : new KeyValuePair<GameObject, int>();
    }

    protected virtual IEnumerator Drop(DropData data, Vector2 position) {
        yield return null;
        KeyValuePair<GameObject, int> prefab = GetDropPrefab(data);
        if (prefab.Key != null) {
            GameObject item = Spawn(prefab.Key, position, Quaternion.identity);
            ItemDropEvent.Trigger(item);
        }
    }

    protected virtual void OnDisable() {
        this.MMEventStopListening<ItemDropRequestEvent>();
    }
}
