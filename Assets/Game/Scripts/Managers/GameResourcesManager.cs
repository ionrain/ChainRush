using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public enum ResourceSource { Idle, Gameplay, Reward, Quest, Chest, Shop, Exchange, Offer, Inventory, Cheats, Ads }
public enum ResourceTarget { LevelStart, LevelUp, Revive, Inventory, Skill, Citadel, Chest, ShopRefresh, Exchange }
public enum ResourceType { SoftCurrency, HardCurrency, Energy, UnitCard, Bolt, BattlePassTicket }

public struct EarnResourcesEvent {
    public EventStage Stage { get; private set; }
    public ResourceSource Source { get; private set; }
    public string ItemId { get; private set; }
    public Dictionary<ResourceType, int> Resources { get; private set; }

    static EarnResourcesEvent e;
    public static void Trigger(EventStage stage, ResourceSource source, Dictionary<ResourceType, int> resources, string itemId) {
        e.Stage = stage;
        e.Resources = resources;
        e.Source = source;
        e.ItemId = itemId;
        MMEventManager.TriggerEvent(e);
    }
}

public struct SpendResourcesEvent {
    public EventStage Stage { get; private set; }
    public ResourceTarget Target { get; private set; }
    public string ItemId { get; private set; }
    public Dictionary<ResourceType, int> Resources { get; private set; }

    static SpendResourcesEvent e;
    public static void Trigger(EventStage stage, ResourceTarget target, Dictionary<ResourceType, int> resources, string itemId) {
        e.Stage = stage;
        e.Resources = resources;
        e.Target = target;
        e.ItemId = itemId;
        MMEventManager.TriggerEvent(e);
    }
}

public struct BalanceResourcesEvent {
    public EventStage Stage { get; private set; }
    public Dictionary<ResourceType, ResourceBalanceData> Balance { get; private set; }

    static BalanceResourcesEvent e;
    public static void Trigger(EventStage stage, Dictionary<ResourceType, ResourceBalanceData> balance) {
        e.Stage = stage;
        e.Balance = balance;
        MMEventManager.TriggerEvent(e);
    }

    public static void Trigger(EventStage stage, List<ResourceType> balance) {
        e.Stage = stage;
        e.Balance = new Dictionary<ResourceType, ResourceBalanceData>();
        balance.ForEach(t => e.Balance[t] = new ResourceBalanceData());
        MMEventManager.TriggerEvent(e);
    }
}

public struct EarnResourceEvent {
    public EventStage Stage { get; private set; }
    public ResourceType Resource { get; private set; }
    public ResourceSource Source { get; private set; }
    public string ItemId { get; private set; }
    public int Value { get; private set; }

    static EarnResourceEvent e;
    public static void Trigger(EventStage stage, ResourceType resourceType, ResourceSource source, string itemId, int value) {
        e.Stage = stage;
        e.Resource = resourceType;
        e.Source = source;
        e.Value = value;
        e.ItemId = itemId;
        MMEventManager.TriggerEvent(e);
    }
}

public struct SpendResourceEvent {
    public EventStage Stage { get; private set; }
    public ResourceType Resource { get; private set; }
    public ResourceTarget Target { get; private set; }
    public string ItemId { get; private set; }
    public int Value { get; private set; }

    static SpendResourceEvent e;
    public static void Trigger(EventStage stage, ResourceType resourceType, ResourceTarget target, string itemId, int value) {
        e.Stage = stage;
        e.Resource = resourceType;
        e.Target = target;
        e.ItemId = itemId;
        e.Value = value;
        MMEventManager.TriggerEvent(e);
    }
}

public class GameResourcesManager : MMSingleton<GameResourcesManager>, MMEventListener<EarnResourceEvent>, MMEventListener<SpendResourceEvent>,
    MMEventListener<EarnResourcesEvent>, MMEventListener<SpendResourcesEvent>, MMEventListener<BalanceResourcesEvent> {
    [SerializeField] ResourcesData data;

    public void OnMMEvent(EarnResourcesEvent e) {
        if (e.Stage == EventStage.Start) {
            data.AddResources(e.Resources);
            EarnResourcesEvent.Trigger(EventStage.End, e.Source, e.Resources, e.ItemId);
            BalanceResourcesEvent.Trigger(EventStage.End, data.GetFullBalance());
        }
    }

    public void OnMMEvent(SpendResourcesEvent e) {
        if (e.Stage == EventStage.Start) {
            data.SpendResources(e.Resources);
            SpendResourcesEvent.Trigger(EventStage.End, e.Target, e.Resources, e.ItemId);
            BalanceResourcesEvent.Trigger(EventStage.End, data.GetFullBalance());
        }
    }

    public void OnMMEvent(BalanceResourcesEvent e) {
        if (e.Stage == EventStage.Start)
            BalanceResourcesEvent.Trigger(EventStage.End, data.GetBalance(new List<ResourceType>(e.Balance.Keys)));
    }

    public void OnMMEvent(EarnResourceEvent e) {
        if (e.Source != ResourceSource.Gameplay && e.Stage == EventStage.Start) {
            data.AddResource(e.Resource, e.Value);
            EarnResourceEvent.Trigger(EventStage.End, e.Resource, e.Source, e.ItemId, e.Value);
            BalanceResourcesEvent.Trigger(EventStage.End, data.GetFullBalance());
        }
    }

    public void OnMMEvent(SpendResourceEvent e) {
        if (e.Stage == EventStage.Start) {
            bool success = data.SpendResource(e.Resource, e.Value);
            if (success) {
                SpendResourceEvent.Trigger(EventStage.End, e.Resource, e.Target, e.ItemId, e.Value);
                BalanceResourcesEvent.Trigger(EventStage.End, data.GetFullBalance());
            }
        }
    }

    void OnEnable() {
        if (data != null) {
            this.MMEventStartListening<EarnResourceEvent>();
            this.MMEventStartListening<SpendResourceEvent>();
            this.MMEventStartListening<EarnResourcesEvent>();
            this.MMEventStartListening<SpendResourcesEvent>();
            this.MMEventStartListening<BalanceResourcesEvent>();
        } else
            Debug.Log("GameResourcesManager OnEnable: PlayerData is NULL");
    }

    void OnDisable() {
        this.MMEventStopListening<EarnResourceEvent>();
        this.MMEventStopListening<SpendResourceEvent>();
        this.MMEventStopListening<EarnResourcesEvent>();
        this.MMEventStopListening<SpendResourcesEvent>();
        this.MMEventStopListening<BalanceResourcesEvent>();
    }
}
