using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine;
using System.Linq;
using System;

public class ResourceData {
    public LocalizedString title;
    public Sprite icon;
    public Sprite smallIcon;
    public int capacity;
    public int productionBase;
    public int productionRate;
    public float productionMultiplier;

    public int Value { get; private set; }
    public string Title => !title.IsEmpty ? title.GetLocalizedString() : string.Empty;

    public ResourceBalanceData GetBalance() => new ResourceBalanceData(Value, capacity);

    public int GetProduction(int level, float timeMultiplier = 1) {
        int result = Mathf.RoundToInt((productionBase + productionRate * productionMultiplier * level) * timeMultiplier);
        return capacity > 0 ? Mathf.Min(result, capacity - Value) : result;
    }

    public void AddValue(int value, bool force = true) {
        Value = force ? Value + value : Mathf.Min(Value + value, capacity > 0 ? capacity : int.MaxValue);
    }

    public bool SubtractValue(int value) {
        if (Value >= value) {
            Value -= value;
            return true;
        }
        return false;
    }

    public void Reset() {
        Value = 0;
    }
}

[Serializable]
public struct ResourceBalanceData {
    public int Value;
    public int Capacity;

    public ResourceBalanceData(int value, int capacity) {
        Value = value;
        Capacity = capacity;
    }

    public void SetCapacity(int value) {
        Capacity = value;
    }

    public void SetValue(int value) {
        Value = value;
    }
}

[CreateAssetMenu(fileName = "New ResourceData", menuName = "Game/ResourceData", order = 10)]
public class ResourcesData : GameSettings {
    public Dictionary<ResourceType, ResourceData> resources = new Dictionary<ResourceType, ResourceData>();
    public Dictionary<ResourceType, int> defaultResources = new Dictionary<ResourceType, int>();
    public int maxProductionDuration = 60*60*24;
    public int productionInterval = 60*60;

    public TimeSpan ProductionTimeSpan { get {
            var value = DateTime.Now - DateTime.FromBinary(_productionStart);
            return value.TotalSeconds < maxProductionDuration ? value : TimeSpan.FromSeconds(maxProductionDuration);
        }
    }

    long _productionStart;

    public Dictionary<ResourceType, int> GetIdleResources(int level) {
        float timeMultiplier =(float)ProductionTimeSpan.TotalSeconds / productionInterval;
        Dictionary<ResourceType, int> result = new();
        foreach (var pair in resources) {
            ResourceData data = pair.Value;
            if (data != null) {
                int production = data.GetProduction(level, timeMultiplier);
                if (production > 0)
                    result[pair.Key] = production;
            }
        }
        return result;
    }

    public void AddResources(Dictionary<ResourceType, int> values) {
        foreach (var pair in values)
            AddResource(pair.Key, pair.Value);
    }

    public void AddResource(ResourceType resource, int value) {
        ResourceData data = Get(resource);
        if (data != null)
            data.AddValue(value);
    }

    public bool SpendResource(ResourceType resource, int value) {
        ResourceData data = Get(resource);
        if (data != null)
            return data.SubtractValue(value);
        return false;
    }

    public void SpendResources(Dictionary<ResourceType, int> values) {
        foreach (var pair in values)
            SpendResource(pair.Key, pair.Value);
    }

    public ResourceBalanceData GetResourceBalance(ResourceType resource) {
        ResourceData data = Get(resource);
        if (data != null)
            return data.GetBalance();
        return new ResourceBalanceData();
    }

    public Dictionary<ResourceType, ResourceBalanceData> GetBalance(List<ResourceType> list) {
        Dictionary<ResourceType, ResourceBalanceData> result = new Dictionary<ResourceType, ResourceBalanceData>();
        foreach (ResourceType resource in list)
            result[resource] = GetResourceBalance(resource);
        return result;
    }

    public Dictionary<ResourceType, ResourceBalanceData> GetFullBalance() {
        return GetBalance(resources.Keys.ToList());
    }

    public void ResetProduction() {
        _productionStart = DateTime.Now.ToBinary();
    }

    public void SetProductionStart(int minuts) {
        _productionStart = (DateTime.Now - TimeSpan.FromMinutes(minuts)).ToBinary();
    }

    public override void Reset() {
        ResetProduction();
        if (resources != null && defaultResources != null) {
            foreach (var pair in resources) {
                ResourceData data = pair.Value;
                if (data != null) {
                    data.Reset();
                    if (defaultResources.ContainsKey(pair.Key))
                        data.AddValue(defaultResources[pair.Key]);
                }
            }
        } else
            Debug.LogError("ResourcesData Get: Resources lis or Default Resources list is NULL");
    }

    public override void Load(GameData gameData) {
        _productionStart = gameData.productionStart;
        foreach (ResourceAmount resourceAmount in gameData.Resources) {
            ResourceData data = Get(resourceAmount.resource);
            if (data != null) {
                data.Reset();
                data.AddValue(resourceAmount.amount);
            }
        }
    }

    public override void Save(GameData gameData) {
        gameData.productionStart = _productionStart;
        foreach (var pair in resources) {
            ResourceData data = pair.Value;
            if (data != null)
                gameData.AddResourceAmount(new ResourceAmount(pair.Key, data.Value));
        }
    }

    public ResourceData Get(ResourceType resource) {
        ResourceData result = null;
        if (resources != null && resources.Count > 0) {
            result = resources.GetValueOrDefault(resource);
            if (result == null)
                Debug.LogErrorFormat("ResourcesData Get: ResourceData is NULL for {0}", resource);
        } else
            Debug.LogError("ResourcesData Get: Resources list is NULL or Empty");
        return result;
    }
}
