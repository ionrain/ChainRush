using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

public class ItemPopup : Popup<ItemData> {
    [Header("Item Popup")]
    [SerializeField] protected GradesData grades;
    [SerializeField] protected ElementsData elements;
    [SerializeField] protected AttributesData attributes;

    [Header("General UI")]
    [SerializeField] protected LocalizedString titlePattern;
    [SerializeField] protected Image iconBack;
    [SerializeField] protected Image iconBorder;
    [SerializeField] protected Image icon;
    [SerializeField] protected TextMeshProUGUI titleLabel;
    [SerializeField] protected TextMeshProUGUI descriptionLabel;
    [SerializeField] protected Transform attributesRoot;
    [SerializeField] protected IconMultiTextItem attributeItem;

    protected string _attributeValuePattern = "+{0}";

    public override bool Setup(ItemData value) {
        if (data != null) {
            data = value;
            SetupUI();
            return true;
        }
        return false;
    }

    protected virtual void SetupUI() {
        if (grades != null) {
            GradeData gradeData = grades.GetData(data.grade);
            if (gradeData != null) {
                if (titleLabel != null )
                    titleLabel.text = string.Format(titlePattern.GetLocalizedString(), gradeData.Title, data.Title, data.level + 1);

                if (iconBack != null && iconBorder != null) {
                    iconBack.color = gradeData.backColor;
                    iconBorder.color = gradeData.borderColor;
                }
            }
        }
        if (descriptionLabel != null)
            descriptionLabel.text = data.Description;
        if (icon != null)
            icon.sprite = data.icon;
        if (attributeItem != null && attributesRoot != null && elements != null && attributes != null) {
            foreach (Transform child in attributesRoot)
                Destroy(child.gameObject);

            foreach (ItemAttribute itemAttribute in data.attributes) {
                IconMultiTextItem item = Instantiate(attributeItem, attributesRoot);
                if (item != null) {
                    AttributeData attributeData = attributes.GetData(itemAttribute.attribute);
                    ElementData elementData = elements.GetData(itemAttribute.element);
                    if (attributeData != null && elementData != null) {
                        string title = string.Format(attributes.GetDisplayPattern(itemAttribute.attribute), elementData.Title, attributeData.Title);
                        string value = string.Format(_attributeValuePattern, itemAttribute.value);
                        item.Setup(attributeData.pictogram, new List<string>() { title, value });
                        item.SetIconColor(elementData.color);
                    }
                }
            }
        }
    }


}
