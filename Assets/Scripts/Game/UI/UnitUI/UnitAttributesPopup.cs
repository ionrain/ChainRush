using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitAttributesPopup : Popup<Dictionary<ElementalAttribute, float>> {
    [Header("Hero Attributes Popup")]
    [SerializeField] ElementsData elements;
    [SerializeField] AttributesData attributes;
    [SerializeField] Dictionary<ElementalAttribute, IconTextItem> labels = new Dictionary<ElementalAttribute, IconTextItem>();
    [SerializeField] Color zeroColor = Color.white;
    [SerializeField] Color nonZeroColor = Color.white;

    public override bool Setup(Dictionary<ElementalAttribute, float> data) {
        if (data != null && elements != null && attributes != null) {
            foreach (var pair in data)
                if (labels.ContainsKey(pair.Key)) {
                    IconTextItem label = labels[pair.Key];
                    if (label != null) {
                        label.SetText(pair.Value.ToShortString());
                        
                        if (pair.Key.Element != Element.Any) {
                            ElementData elementData = elements.GetData(pair.Key.Element);
                            if (elementData != null) {
                                label.SetIcon(elementData.icon);
                                label.SetIconColor(elementData.color);
                            }
                        } else {
                            AttributeData attributeData = attributes.GetData(pair.Key.Attribute);
                            if (attributeData != null)
                                label.SetIcon(attributeData.pictogram);
                        }

                        label.SetTextColor(pair.Value > 0 ? nonZeroColor : zeroColor);
                    }
                }
                return base.Setup(data);
        }
        return false;
    }
}
