using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;

public class LootManager : SerializedMonoBehaviour, MMEventListener<EarnResourceEvent>, MMEventListener<ItemEvent>, MMEventListener<SkillLevelUpEvent>,
                           MMEventListener<LevelLoadEvent> {
    Dictionary<ResourceType, float> _resources = new Dictionary<ResourceType, float>();
    List<ItemData> _inventory = new List<ItemData>();

    float _collectSkillMultiplier = 1;
    Dictionary<ResourceType, float> _levelMultipliers = new();

    public void Setup() {
        _resources.Clear();
        _inventory.Clear();
    }

    public void MultiplyResources(float multiplier) {
        var keys = new List<ResourceType>(_resources.Keys);
        keys.ForEach(t => _resources[t] *= multiplier);
    }

    public void OnMMEvent(EarnResourceEvent e) {
        if (e.Source == ResourceSource.Gameplay && e.Stage == EventStage.Process) {
            float amount = _resources.ContainsKey(e.Resource) ? _resources[e.Resource] : 0;
            _resources[e.Resource] = amount + e.Value * _collectSkillMultiplier * _levelMultipliers.GetValueOrDefault(e.Resource, 1);
        }
    }

    public void OnMMEvent(ItemEvent e) {
        if (e.Stage == EventStage.Start && e.Type == ItemEventType.Pick && !_inventory.Contains(e.Value))
            _inventory.Add(e.Value);
    }

    public void TransferLoot() {
        RewardEvent.Trigger(EventStage.Start, RewardEventType.Transfer, new RewardItem(RewardItemType.Loot, GetRewards(), RewardState.Ready));
    }

    public List<Reward> GetRewards() {
        List<Reward> result = new List<Reward>();
        foreach (var resource in _resources)
                result.Add(new ResourceReward(resource.Key, Mathf.RoundToInt(resource.Value)));
        _inventory.ForEach(t => { if (t.Transferable) result.Add(new InventoryReward(t)); });
        return result;
    }

    public void OnMMEvent(SkillLevelUpEvent e) {
        if (e.Skill != null && e.Skill.Data != null && e.Skill.Data.attribute == Attribute.ResourceMultiplier) {
            SkillLevel current = e.Skill.CurrentLevel;
            if (current != null)
                _collectSkillMultiplier = current.GetParameterValue(SkillParameterType.Amount, 1);
            else
                Debug.LogErrorFormat("EnemyManager SkillEvent: current SkillLevel is NULL for {0}", e.Skill.name);
        }
    }

    public void OnMMEvent(LevelLoadEvent e) {
        if (e.Stage == EventStage.Start && e.Data != null && e.Data.collectMultipliers != null)
            _levelMultipliers = new Dictionary<ResourceType, float>(e.Data.collectMultipliers);
    }

    void OnEnable() {
        this.MMEventStartListening<EarnResourceEvent>();
        this.MMEventStartListening<ItemEvent>();
        this.MMEventStartListening<SkillLevelUpEvent>();
        this.MMEventStartListening<LevelLoadEvent>();
    }

    void OnDisable() {
        this.MMEventStopListening<EarnResourceEvent>();
        this.MMEventStopListening<ItemEvent>();
        this.MMEventStopListening<SkillLevelUpEvent>();
        this.MMEventStopListening<LevelLoadEvent>();
    }
}
