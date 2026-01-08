using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Sirenix.OdinInspector;
using MoreMountains.TopDownEngine;

public enum Element { Any, Physical, Fire, Water, Earth, Air, Dark, Light, Poison, Chaos }

[System.Serializable]
public class ElementData {
    public LocalizedString title;
    public Sprite icon;
    public Color color = Color.white;
    public DamageType damageType;

    public string Title => title != null && !title.IsEmpty ? title.GetLocalizedString() : string.Empty;
}

[CreateAssetMenu(fileName = "New ElementsData", menuName = "Game/ElementsData", order = 12)]
public class ElementsData : SerializedScriptableObject {
    public Dictionary<Element, ElementData> data;

    public ElementData GetData(Element element) => data.ContainsKey(element) ? data[element] : null;
}
