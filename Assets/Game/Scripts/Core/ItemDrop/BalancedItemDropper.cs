using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public struct DropBalanceSetupEvent {
    public int ChannelId { get; private set; }
    public Dictionary<int, int> RequestersCount { get; private set; }
    public List<DropBalanceData> BalanceData { get; private set; }

    static DropBalanceSetupEvent e;
    public static void Trigger(int channelId, Dictionary<int, int> requestersCount, List<DropBalanceData> balanceData) {
        e.ChannelId = channelId;
        e.RequestersCount = requestersCount;
        e.BalanceData = balanceData;
        MMEventManager.TriggerEvent(e);
    }
}

public class DropBalanceData {
    public GameObject prefab;
    public int totalAmount = 1;
    public int minimumAverageAmount = 1;
    [Range(0, 1)]
    public float intervalBetweenDrops = 0.1f;
    public AnimationCurve probability = AnimationCurve.Linear(0, 0, 1, 1);
    public int poolSize = 20;
}

public class DropBalanceItem {
    public DropBalanceData data;

    public int AverageAmount { get; set; }

    float _nextDrop;
    int _currentAmount;

    public DropBalanceItem(DropBalanceData balanceData) {
        data = balanceData;
    }

    public void SetAverageAmount(float possibleDropsCount) {
        if (possibleDropsCount > 0)
            AverageAmount = Mathf.Max(data.minimumAverageAmount, (int)(data.totalAmount / possibleDropsCount));
    }

    public float GetProbability() {
        float result = 0;
        if (data != null) {
            float duration = 0;
            while (duration < 1) {
                result += data.probability.Evaluate(duration);
                duration += 0.1f;
            }
        }
        return result;
    }

    public float GetProbability(float progress) {
        if (data != null && (_currentAmount < data.totalAmount || data.totalAmount == 0) && _nextDrop <= progress)
            return data.probability.Evaluate(progress);
        return 0;
    }

    public void Update(float progress) {
        if (data != null) {
            _nextDrop = progress + data.intervalBetweenDrops;
            _currentAmount += AverageAmount; 
        }
    }
}

public class BalancedItemDropper : ItemDropper, MMEventListener<DropBalanceSetupEvent>, MMEventListener<LevelStageProgressEvent> {
    List<DropBalanceItem> _balanceItems = new List<DropBalanceItem>();
    float _progress = 0;

    public void OnMMEvent(DropBalanceSetupEvent e) {
        if (e.ChannelId == channelId && e.BalanceData != null && e.RequestersCount != null) {
            _balanceItems.Clear();
            _progress = 0;
            bool poolsInitialized = poolerObjects.Count > 0;
            foreach (DropBalanceData data in e.BalanceData) {
                if (!poolsInitialized)
                    poolerObjects.Add(data.prefab, data.poolSize);
                _balanceItems.Add(new DropBalanceItem(data));
            }
            CalculateDropAmounts(e.RequestersCount);
            if (!poolsInitialized)
                SetupPools();
        }
    }

    void CalculateDropAmounts(Dictionary<int, int> requestersCount) {
        foreach (DropBalanceItem dropBalanceItem in _balanceItems) {
            float balanceProbability = dropBalanceItem.GetProbability();
            float dropDataDrops = 0;
            float balanceDrops = 0;
            foreach (var pair in dropList)
                if (requestersCount.ContainsKey(pair.Key)) {
                    dropDataDrops += requestersCount[pair.Key] * pair.Value.GetProbability(dropBalanceItem.data.prefab);
                    balanceDrops += requestersCount[pair.Key] * balanceProbability;
                }

            dropBalanceItem.SetAverageAmount(Mathf.Min(balanceDrops, dropDataDrops));
        }
    }

    protected override KeyValuePair<GameObject, int> GetDropPrefab(DropData data) {
        if (Random.value >= data.emptyProbability) {
            Dictionary<DropBalanceItem, float> probabilities = new Dictionary<DropBalanceItem, float>();
            float totalProbability = 0;
            foreach (DropBalanceItem item in _balanceItems) {
                float combinedProbability = item.GetProbability(_progress) * data.GetProbability(item.data.prefab);
                if (combinedProbability > 0) {
                    probabilities.Add(item, combinedProbability);
                    totalProbability += combinedProbability;
                }
            }

            Dictionary<DropBalanceItem, float> normalized = new Dictionary<DropBalanceItem, float>();
            foreach (var pair in probabilities)
                normalized[pair.Key] = probabilities[pair.Key] / totalProbability;

            DropBalanceItem resultItem = null;
            float random = Random.value;
            float sum = 0;
            foreach (var probability in normalized) {
                float newSum = sum + probability.Value;
                if (random > sum && random <= newSum) {
                    resultItem = probability.Key;
                    break;
                } else
                    sum = newSum;
            }

            if (resultItem != null) {
                resultItem.Update(_progress);
                return new KeyValuePair<GameObject, int>(resultItem.data.prefab, resultItem.AverageAmount);
            }
        }

        return new KeyValuePair<GameObject, int>();
    }

    public void OnMMEvent(LevelStageProgressEvent e) {
        _progress = e.LevelProgress;
    }

    protected override void OnEnable() {
        this.MMEventStartListening<DropBalanceSetupEvent>();
        this.MMEventStartListening<LevelStageProgressEvent>();
        base.OnEnable();
    }

    protected override void OnDisable() {
        this.MMEventStopListening<DropBalanceSetupEvent>();
        this.MMEventStopListening<LevelStageProgressEvent>();
        base.OnDisable();
    }
}
