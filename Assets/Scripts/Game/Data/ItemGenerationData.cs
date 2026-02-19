using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class AttributeGenerationData {
    public float levelDelta = 1;
    public float spread = 0.25f;
}

[CreateAssetMenu(fileName = "New ItemGenerationData", menuName = "Game/ItemGenerationData", order = 4)]
public class ItemGenerationData : SerializedScriptableObject {
    [Header("Values")]
    public Dictionary<ItemType, List<ItemTemplateData>> templatesData = new Dictionary<ItemType, List<ItemTemplateData>>();
    public Dictionary<Attribute, float> attributeDeltas = new Dictionary<Attribute, float>();
    public Dictionary<Attribute, int> roundDigits = new Dictionary<Attribute, int>();
    public Vector2Int levelSpread;
    public float attributeSpread = 0.25f;

    [Header("Shop Price")]
    public Dictionary<Grade, float> gradeMultipliers = new Dictionary<Grade, float>();

    public ItemData GetByTypeAndName(ItemType itemType, string itemName) {
        if (templatesData != null) {
            List<ItemTemplateData> templates = templatesData[itemType];
            foreach (ItemTemplateData template in templates) {
                ItemData data = template.GetByName(itemName);
                if (data != null)
                    return data;
            }
        }
        return null;
    }
}
