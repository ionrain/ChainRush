using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New AllItemsData", menuName = "Game/AllItemsData", order = 17)]
public class AllItemsData : GameSettings {
    public List<ItemData> items = new List<ItemData>();
    public ItemGenerationData generationData;

    public List<ItemData> GetByOwner(ItemOwner owner) {
        List<ItemData> result = new List<ItemData>();
        result.AddRange(items.FindAll(t => t.owner == owner));
        return result;
    }

    public ItemData GetByName(string name) {
        return items.Find(t => t != null && t.name.Equals(name));
    }

    public override void Reset() {
        items.RemoveAll(t => t == null || t.Generated);
        items.ForEach(t => t.Reset());
    }

    public void ClearBlanks() {
        items.RemoveAll(t => t == null);
    }

    public override void Load(GameData gameData) {
        foreach (ItemStateData stateData in gameData.items) {
            ItemData item = null;
            ItemData template = null;
            bool generated = stateData.generatedFrom.Length > 0;
            
            if (!generated) {
                item = GetByName(stateData.name);
            } else if (generationData != null) {
                template = generationData.GetByTypeAndName(stateData.itemType, stateData.generatedFrom);
                if (template != null) {
                    item = ScriptableObject.CreateInstance<ItemData>();
                    items.Add(item);
                }
            }
            
            if (item != null)
                item.SetStateData(stateData, template);
        }
        ClearBlanks();
    }

    public override void Save(GameData gameData) {
        foreach (ItemData item in items) {
            if (!item.Generated || item.owner != ItemOwner.Shop)
                gameData.items.Add(item.GetStateData());
        }
    }
}
