using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using Sirenix.OdinInspector;
using System;

public enum Attribute { Power, Defense, Health, Speed, SkillSpeed, Luck, MagnetPower, Regeneration, InvulnerabilityTime, 
                        InvulnerabilityInterval, ResourceMultiplier }

public struct ElementalAttribute : IEquatable<ElementalAttribute> {
    public Element Element;
    public Attribute Attribute;

    public ElementalAttribute(Element element, Attribute attribute) {
        Element = element;
        Attribute = attribute;
    }

    public bool Suitable(ElementalAttribute other) {
        return Attribute == other.Attribute && (Element == Element.Any || Element == other.Element);
    }

    public override bool Equals(object obj) {
        return obj is ElementalAttribute attribute && Equals(attribute);
    }

    public bool Equals(ElementalAttribute other) {
        return Element == other.Element && Attribute == other.Attribute;
    }

    public override int GetHashCode() {
        return HashCode.Combine(Element, Attribute);
    }

    public static bool operator ==(ElementalAttribute x, ElementalAttribute y) {
        return x.Element == y.Element && x.Attribute == y.Attribute;
    }

    public static bool operator !=(ElementalAttribute x, ElementalAttribute y) {
        return !(x == y);
    }

    public override string ToString() {
        return string.Format("{0} {1}", Element, Attribute);
    }
}

[Serializable]
public struct AttributeAction : IEquatable<AttributeAction> {
    public Attribute Attribute;
    public MathAction Action;
    public MathValueType Type;

    public AttributeAction(Attribute attribute, MathAction action, MathValueType valueType) {
        Attribute = attribute;
        Action = action;
        Type = valueType;
    }

    public override bool Equals(object obj) {
        return obj is AttributeAction attribute && Equals(attribute);
    }

    public bool Equals(AttributeAction other) {
        return Attribute == other.Attribute && Action == other.Action && Type == other.Type;
    }

    public override int GetHashCode() {
        return HashCode.Combine(Attribute, Action, Type);
    }

    public static bool operator ==(AttributeAction x, AttributeAction y) {
        return x.Attribute == y.Attribute && x.Action == y.Action && x.Type == y.Type;
    }

    public static bool operator !=(AttributeAction x, AttributeAction y) {
        return !(x == y);
    }
}

[Serializable]
public class AttributeData {
    public Sprite icon;
    public Sprite pictogram;
    public LocalizedString title;

    public string Title => title != null && !title.IsEmpty ? title.GetLocalizedString() : string.Empty;
}

[CreateAssetMenu(fileName = "New AttributesData", menuName = "Game/AttributesData", order = 11)]
public class AttributesData : SerializedScriptableObject {
    public Dictionary<Attribute, AttributeData> data = new Dictionary<Attribute, AttributeData>();
    
    public string GetDisplayPattern(Attribute attribute) => attribute == Attribute.Power || attribute == Attribute.Defense ? "{0} {1}" : "{1}";
    public AttributeData GetData(Attribute attribute) =>  data.ContainsKey(attribute) ? data[attribute] : null;
}
