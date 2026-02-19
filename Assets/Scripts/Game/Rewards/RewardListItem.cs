using TMPro;
using UnityEngine;

public class RewardListItemData : IListItemData {
    public int Amount { get; private set; }
    public int Index { get; private set; }

    public RewardListItemData(int index, Sprite icon, int amount, string description) {
        Index = index;
        Icon = icon;
        Amount = amount;
        Description = description;
    }

    public string Title => Amount.ToString();
    public string Description { get; private set; }
    public Sprite Icon { get; private set; }
}

public class RewardListItem : ListItem<RewardListItem, RewardListItemData> {
    [SerializeField] TextMeshProUGUI descriptionLabel;

    public override void Setup(RewardListItemData data) {
        base.Setup(data);
        if (_data != null) {
            if (label != null) {
                if (_data.Amount > 0)
                    label.SetText(_data.Title);
                else
                    label.gameObject.SetActive(false);
            }

            descriptionLabel?.SetText(_data.Description);

            if (icon != null)
                icon.sprite = _data.Icon;
        } else
            Debug.LogFormat("RewardListItem Setup: RewardListItemData is NULL for {0}", gameObject.name);
    }
}
