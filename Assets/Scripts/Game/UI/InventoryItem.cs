using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.Localization;

public class InventoryItem : ListItem<InventoryItem, ItemData> {
    [SerializeField] GradesData grades;
    [SerializeField] LocalizedString itemLevelPattern;

    public override void Setup(ItemData data) {
        base.Setup(data);
        if (_data != null) {
            if (icon != null)
                icon.sprite = _data.icon;
            if (background != null && border != null && grades != null) {
                GradeData grade = grades.GetData(data.grade);
                if (grade != null) {
                    background.color = grade.backColor;
                    border.color = grade.borderColor;
                }
            }
            if (label != null && !itemLevelPattern.IsEmpty)
                label.text = string.Format(itemLevelPattern.GetLocalizedString(), data.level + 1);
        }
    }
}
