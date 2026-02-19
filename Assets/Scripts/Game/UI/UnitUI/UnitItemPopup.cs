using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

public class UnitItemPopup : ItemPopup {
    [Header("Unit Item Popup")]
    [SerializeField] protected LocalizedString equipLoc;
    [SerializeField] protected LocalizedString unequipLoc;
    [SerializeField] protected Button equipButton;
    [SerializeField] protected TextMeshProUGUI equipLabel;

    protected override void SetupUI() {
        base.SetupUI();
        SetupEquipLabel();
    }

    void SetupEquipLabel() {
        bool equipped = data.owner == ItemOwner.Unit;
        if (equipLabel != null)
            equipLabel.text = equipped ? unequipLoc.GetLocalizedString() : equipLoc.GetLocalizedString();
    }

    public void Equip() {
        ItemEvent.Trigger(EventStage.Start, data.owner == ItemOwner.Unit ? ItemEventType.Unequip : ItemEventType.Equip, data);
        SetVisibility(false);
    }
}
