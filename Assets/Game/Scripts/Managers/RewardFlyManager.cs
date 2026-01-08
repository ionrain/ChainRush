using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

public struct RewardFlyEvent {
    public EventStage Stage { get; private set; }
    public Reward Reward { get; private set; }
    public Vector2 Position { get; private set; }

    static RewardFlyEvent e;
    public static void Trigger(EventStage stage, Reward reward, Vector2 position) {
        e.Stage = stage;
        e.Reward = reward;
        e.Position = position;
        MMEventManager.TriggerEvent(e);
    }
}

[System.Serializable]
public class RewardFlyData {
    public MMF_Player target;
    public GameObject prefab;
}

public class RewardFlyManager : SerializedMonoBehaviour, MMEventListener<RewardFlyEvent> {
    [SerializeField] Dictionary<ResourceType, RewardFlyData> resources = new();
    [SerializeField] RewardFlyData unitCard;
    [SerializeField] float duration = 1f;
    [SerializeField] float initialDelay = 0.5f;
    [SerializeField] float flyDelay = 0.5f;
    [SerializeField] float spawnDelay = 0.2f;
    [SerializeField] int maxCount = 10;
    [SerializeField] Vector2 positionOffset = Vector2.zero;
    [SerializeField] Vector2 positionRange = Vector2.one;
    [SerializeField] Vector2Int rotationRange = Vector2Int.zero;
    [SerializeField] Vector2 scaleRange = Vector2.one;

    public void Fly(RewardFlyData data, int count, Vector2 position) {
        if (data.prefab != null && data.target != null)
            StartCoroutine(CreateItems(data.prefab, Mathf.Min(count, maxCount), position, data.target));
    }

    IEnumerator CreateItems(GameObject prefab, int count, Vector2 initialPosition, MMF_Player target) {
        yield return new WaitForSecondsRealtime(initialDelay);
        for (int i = 0; i < count; i++) {
            Vector2 position = initialPosition + positionOffset + new Vector2(Random.Range(-positionRange.x, positionRange.x), Random.Range(-positionRange.y, positionRange.y));
            var go = Instantiate(prefab, position, Quaternion.AngleAxis(Random.Range(rotationRange.x, rotationRange.y), Vector3.forward), transform);
            go.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);
            StartCoroutine(FlyToPosition(go, target));
            yield return new WaitForSecondsRealtime(spawnDelay);
        }
    }

    IEnumerator FlyToPosition(GameObject gameObject, MMF_Player target) {
        yield return new WaitForSecondsRealtime(flyDelay);
        gameObject.transform.DOMove(target.transform.position, duration).SetEase(Ease.InOutQuad).OnComplete(() => {
                target.PlayFeedbacks();
                gameObject.SetActive(false);
                Destroy(gameObject);
            });
    }

    public void OnMMEvent(RewardFlyEvent e) {
        if (e.Stage == EventStage.Start) {
            RewardFlyData data = null;
            if (e.Reward is ResourceReward resourceReward && resources.ContainsKey(resourceReward.Resource))
                data = resources[resourceReward.Resource];
            else if (e.Reward is UnitCardReward && unitCard != null)
                data = unitCard;
            if (data != null)
                Fly(data, e.Reward.Amount, e.Position);
        }
    }

    void OnEnable() {
        this.MMEventStartListening<RewardFlyEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<RewardFlyEvent>();
    }
}
