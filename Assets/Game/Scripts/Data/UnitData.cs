using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Sirenix.OdinInspector;
using Spine.Unity;
using MoreMountains.Tools;
using System;
using UnityEngine.Events;

[Serializable]
public class UnitDataEvent : UnityEvent<UnitData> { }

[Flags]
public enum UnitType { None = 0, Normal = 1, Hero = 2 }
public enum UnitState { Locked, ReadyToBeUnlocked, Available, Selected }
public enum UnitSpeciality { Warrior, Assassin, Tank, Range, Mage, Support, Any }
public enum UnitEventType { ChangeState, LevelUp, CardBalanceChange }
public enum UnitMergeState { First, Second, Third, Forth, Fifth }

public class MergeStateData {
    public SkeletonDataAsset spineData;
    public Sprite icon;
    public LocalizedString description;
    public float healthBarOffset = 1f;
    public float mass = 1f;
    public Vector2 colliderSize = Vector2.one;
    public Vector2 colliderOffset = Vector2.zero;
    public Dictionary<Attribute, float> attributeMultipliers = new();

    public string Description => description != null && !description.IsEmpty ? description.GetLocalizedString() : string.Empty;

    public float GetAttributeMultiplier(Attribute attribute) {
        return attributeMultipliers != null ? attributeMultipliers.GetValueOrDefault(attribute, 1) : 1;
    }
}

public struct UnitEvent {
    public EventStage Stage { get; private set; }
    public UnitEventType Type { get; private set; }
    public UnitData Data { get; private set; }

    static UnitEvent e;
    public static void Trigger(EventStage stage, UnitEventType eventType, UnitData data) {
        e.Stage = stage;
        e.Type = eventType;
        e.Data = data;
        MMEventManager.TriggerEvent(e);
    }
}

public class AttributeUpgradeData {
    protected const int _deltaIncreaseInterval = 10;
    protected const float _deltaIncreaseSpeed = 0.5f;

    [SerializeField] protected float baseValue = 100;
    [SerializeField] protected float minDelta = 10;
    [SerializeField] protected float delta = 50;

    public virtual float GetValue(int level) {
        if (minDelta == 0 && delta == 0)
            return baseValue;

        int multiplier = Mathf.FloorToInt((float)level / _deltaIncreaseInterval);
        int number = level - multiplier * _deltaIncreaseInterval + 1;
        return Mathf.RoundToInt(baseValue + level * minDelta + 
               delta * multiplier * (number + _deltaIncreaseInterval * _deltaIncreaseSpeed * (multiplier - 1)));
    }
}

[Serializable]
public class UnitSkill {
    public SkillData data;
    public bool defaultSkill;
    public bool main;
    public int requiredLevel;

    public string Name => data.name;
    public bool Aquired => data != null ? data.Aquired : false;
    public bool CanBeAquired => data != null ? data.CanBeAquired : false;
    
    public int Cost => data != null ? (int)data.skillType : 0;

    public void ResetLevel() {
        if (data != null) {
            if (data.Aquired)
                data.Initialize();
        }
    }

    public void Reset() {
        data.Aquired = defaultSkill;
        if (data != null)
            data.Pair = null;
    }

    public void SetAquired(bool value) {
        if (data != null)
            data.Aquired = value;
    }
}

[Serializable]
public class InventoryItemData {
    public ItemType item;
    public string name;

    public InventoryItemData (ItemType itemType, string itemName) {
        item = itemType;
        name = itemName;
    }
}

[Serializable]
public class UnitSkillData {
    public string skill;
    public bool aquired;

    public UnitSkillData(string skillData, bool skillAquired) {
        skill = skillData;
        aquired = skillAquired;
    }
}

[Serializable]
public class UnitStateData {
    public string unit;
    public UnitState state;
    public int level;
    public int cards;
    public List<InventoryItemData> inventory = new List<InventoryItemData>();
    public List<UnitSkillData> skills = new List<UnitSkillData>();
}

[CreateAssetMenu(fileName = "New UnitData", menuName = "Game/UnitData", order = 2)]
public class UnitData : SerializedScriptableObject, IListItemData {
    const int MAX_LEVEL = 100;

    public bool defaultUnit;
    public UnitType type;
    public LocalizedString title;
    public LocalizedString shortName;
    public LocalizedString description;
    public Sprite icon;
    public Grade grade;
    public Element element;
    public UnitSpeciality speciality;
    public Dictionary<Attribute, AttributeUpgradeData> attributes = new Dictionary<Attribute, AttributeUpgradeData>();
    public List<UnitSkill> skills = new List<UnitSkill>();
    public List<MergeStateData> mergeStates = new();

    [Header("Spine Animation")]
    public Dictionary<AnimationState, SpineAnimationData> animations = new Dictionary<AnimationState, SpineAnimationData>();
    public MoreMountains.TopDownEngine.Weapon.WeaponStates attackState = MoreMountains.TopDownEngine.Weapon.WeaponStates.WeaponUse;
    public ParticleSystem deathParticleFX;

    public string Title => title != null && !title.IsEmpty ? title.GetLocalizedString() : string.Empty;
    public string ShortName => shortName != null && !shortName.IsEmpty ? shortName.GetLocalizedString() : string.Empty;
    public string Description => description != null && !description.IsEmpty ? description.GetLocalizedString() : string.Empty;
    public int Level { get; private set;}
    public Dictionary<ItemType, ItemData> Inventory => _inventory;
    public Sprite Icon => icon;
    public bool IsMelee => speciality < UnitSpeciality.Range;
    public bool Upgradable => Level + 1 < MAX_LEVEL;
    public bool LevelValid => Level >= 0;
    public int CardBalance { get; private set; }
    public bool Unlocked => State != UnitState.Locked && State != UnitState.ReadyToBeUnlocked;
    public UnitState State { get; private set; }
    public int MergeStatesCount => mergeStates != null ? mergeStates.Count : 0;

    Dictionary<ItemType, ItemData> _inventory = new Dictionary<ItemType, ItemData>();

    public int GetUpgradeSoftPrice() {
        return Mathf.RoundToInt((1000 + 100 * Mathf.Pow(Level, 2 + Level * 0.01f)) / 100) * 100;
    }
    
    public int GetUpgradeCardPrice() {
        return (Level + 1) * 5 * (1 + Mathf.FloorToInt((float)Level / 5));
    }

    public MergeStateData GetMergeData(int index) {
        return mergeStates != null && index >= 0 && index < mergeStates.Count ? mergeStates[index] : null;
    }

    public ItemData AddInventoryItem(ItemData data) {
        ItemData removed = null;
        if (data != null) {
            ItemType itemType = data.itemType;
            removed = RemoveInventoryItem(itemType);
            data.owner = ItemOwner.Unit;
            _inventory[itemType] = data;
        }
        return removed;
    }

    public bool AddCards(int value) {
        CardBalance += value;
        return true;
    }

    public bool SpendCards() {
        int value = GetUpgradeCardPrice();
        if (CardBalance >= value) {
            CardBalance -= value;
            return true;
        }
        return false;
    }

    public bool CanBeUpgraded(int soft) {
        return Unlocked && Upgradable && GetUpgradeSoftPrice() <= soft && GetUpgradeCardPrice() <= CardBalance;
    }

    public ItemData RemoveInventoryItem(ItemType itemType) {
        if (_inventory.ContainsKey(itemType)) {
            ItemData existing = _inventory[itemType];
            if (existing != null) {
                existing.owner = ItemOwner.Inventory;
                _inventory.Remove(itemType);
                return existing;
            }
        }
        return null;
    }

    public void Upgrade() {
        Level++;
    }

    public void ResetSkillLevels() {
        skills.ForEach(t => { if (t.data != null) t.ResetLevel(); });
    }

    public bool HasSkillsToBuy(int balance) {
        if (Unlocked)
            foreach (UnitSkill skill in skills)
                if (skill != null && !skill.Aquired && skill.requiredLevel <= Level && skill.Cost <= balance)
                    return true;
        return false;
    }

    public void SetState(UnitState state) {
        State = state;
    }

    public List<UnitSkill> GetSkills(SkillListType listType) {
        List<UnitSkill> result = new List<UnitSkill>();
        foreach (UnitSkill skill in skills)
            if (skill != null && skill.data != null)
                if (listType == SkillListType.All || (skill.Aquired && listType == SkillListType.Aquired))
                    result.Add(skill);
        return result;
    }

    public UnitSkill GetSkillByName(string skillName) {
        return skills.Find(t => t.Name.Equals(skillName));
    }

    public Dictionary<ElementalAttribute, float> GetAllAttributes(bool returnZero = true) {
        Dictionary<ElementalAttribute, float> result = new Dictionary<ElementalAttribute, float>();
        Array attributes = Enum.GetValues(typeof(Attribute));
        foreach (Attribute attribute in attributes) {
            Dictionary<Element, float> attributeValues = GetAttributeValues(attribute);
            foreach (var pair in attributeValues)
                if (pair.Value > 0 || returnZero)
                    result.Add(new ElementalAttribute(pair.Key, attribute), pair.Value);
        }
        return result;
    }

    public Dictionary<Element, float> GetAttributeValues(Attribute attribute) {
        bool elemental = attribute == Attribute.Power || attribute == Attribute.Defense;
        Dictionary<Element, float> result = new Dictionary<Element, float>();
        Array elements = Enum.GetValues(typeof(Element));
        if (elemental) {
            foreach (Element e in elements)
                if (e != Element.Any)
                    result[e] = GetAttributeValue(attribute, e);
        } else
            result[Element.Any] = GetAttributeValue(attribute);
        return result;
    }

    public float GetAttributeValue(Attribute attribute, Element targetElement = Element.Any) {
        float result = 0;

        if ((targetElement == Element.Any || element == targetElement) && LevelValid && 
            attributes.ContainsKey(attribute) && attributes[attribute] != null)
            result += attributes[attribute].GetValue(Level);

        foreach (var pair in _inventory) {
            if (pair.Value != null) {
                foreach (ItemAttribute itemAttribute in pair.Value.attributes) {
                    if ((targetElement == Element.Any || itemAttribute.element == targetElement) && itemAttribute.attribute == attribute) {
                        if (itemAttribute.action == MathAction.Add)
                            result += itemAttribute.value;
                        else if (itemAttribute.action == MathAction.Substract)
                            result -= itemAttribute.value;
                    }
                }
            }
        }

        return result;
    }

    public void Reset() {
        Level = 0;
        CardBalance = 0;
        State = defaultUnit ? UnitState.Selected : UnitState.Locked;
        _inventory.Clear();
        skills.ForEach(t => t.Reset());
    }

    public void SetStateData(UnitStateData stateData, AllItemsData items) {
        State = stateData.state;
        Level = stateData.level;
        CardBalance = stateData.cards;
        if (items != null) {
            foreach (InventoryItemData inventoryData in stateData.inventory) {
                ItemData data = items.GetByName(inventoryData.name);
                if (data != null)
                    _inventory[inventoryData.item] = data;
            }
        }
        foreach (UnitSkillData skillData in stateData.skills) {
            UnitSkill skill = skills.Find(t => t.Name.Equals(skillData.skill));
            if (skill != null)
                skill.SetAquired(skillData.aquired);
        }   
    }

    public UnitStateData GetStateData() {
        UnitStateData result = new UnitStateData();
        result.unit = name;
        result.state = State;
        result.level = Level;
        result.cards = CardBalance;
        foreach (var pair in _inventory)
            if (pair.Value != null)
                result.inventory.Add(new InventoryItemData(pair.Key, pair.Value.name));
        foreach (UnitSkill skill in skills)
            result.skills.Add(new UnitSkillData(skill.Name, skill.Aquired));
        return result;
    }
}