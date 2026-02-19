using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Sirenix.OdinInspector;
using MoreMountains.Tools;

public enum ItemEventType { Generate, Get, Buy, Sell, Equip, Unequip, Pick }

public struct ItemEvent {
    public EventStage Stage;
    public ItemEventType Type;
    public ItemData Value;

    static ItemEvent e;
    public static void Trigger(EventStage stage, ItemEventType eventType, ItemData value) {
        e.Type = eventType;
        e.Stage = stage;
        e.Value = value;
        MMEventManager.TriggerEvent(e);
    }
}

public enum ItemType { Weapon, Armor, Shield, Ring, Footware, Headdress, All }
public enum ItemOwner { None, Unit, Inventory, Shop, Chest, Quest, Reward }

[System.Serializable]
public struct ItemAttribute {
    public Attribute attribute;
    public Element element;
    public MathAction action;
    public float value;
}

[System.Serializable]
public class ItemStateData {
    public string name;
    public string generatedFrom;
    public ItemType itemType;
    public Grade grade;
    public ItemOwner owner;
    public int level;
    public ResourceAmount shopPrice;
    public List<ItemAttribute> attributes;
}

[CreateAssetMenu(fileName = "New ItemData", menuName = "Game/ItemData", order = 3)]
public class ItemData : SerializedScriptableObject, IListItemData {
    public static int MaxLevel => 99;

    [SerializeField] bool defaultItem;
    public LocalizedString title;
    public LocalizedString description;
    public Sprite icon;
    public ItemType itemType;
    public Grade grade;
    public UnitClass unitClass;
    public ItemOwner owner;
    public int level;
    public int maxLevel;
    public ResourceAmount shopPrice;
    public List<ItemAttribute> attributes;
    public string trigger;

    public bool Transferable { get; set; } = true;
    public string Title { get { return title.GetLocalizedString(); } }
    public string Description { get { return description.GetLocalizedString(); } }
    public Sprite Icon => icon;
    public bool Generated => GeneratedFrom.Length > 0;
    public string GeneratedFrom { get; set; } = string.Empty;

    public void Reset() {
        SetOwner(defaultItem ? ItemOwner.Inventory : ItemOwner.None);
    }

    public void GenerateName() {
        name = string.Format("{0}-{1}-{2}-Lvl{3}-{4}", itemType.ToString(), grade.ToString(), Title.Replace(" ", "-"), level, Random.Range(1, 1000000));
    }

    public void SetOwner(ItemOwner itemOwner) {
        owner = itemOwner;
    }

    public ItemStateData GetStateData() {
        ItemStateData result = new ItemStateData();

        result.name = name;
        result.generatedFrom = GeneratedFrom;
        result.grade = grade;
        result.itemType = itemType;
        result.owner = owner;
        result.level = level;
        result.shopPrice = shopPrice;
        result.attributes = attributes;

        return result;
    }

    public void SetStateData(ItemStateData stateData, ItemData templateItem) {
        level = stateData.level;
        owner = stateData.owner;

        if (templateItem != null) {
            name = stateData.name;
            title = templateItem.title;
            itemType = stateData.itemType;
            description = templateItem.description;
            GeneratedFrom = stateData.generatedFrom;
            icon = templateItem.icon;
            itemType = templateItem.itemType;
            unitClass = templateItem.unitClass;
            grade = stateData.grade;
            shopPrice = stateData.shopPrice;
            attributes = stateData.attributes;
        }
    }
}
