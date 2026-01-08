using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "New ItemTemplateData", menuName = "Game/ItemTemplateData", order = 8)]
public class ItemTemplateData : SerializedScriptableObject {
    public List<ItemData> items = new List<ItemData>();
    public Dictionary<Grade, List<List<ItemAttribute>>> patterns = new Dictionary<Grade, List<List<ItemAttribute>>>();
    public float levelPriceMultiplier = 1;

    public ItemData GetByName(string searchName) {
        return items != null ? items.Find(t => t.name.Equals(searchName)) : null;
    }
}
