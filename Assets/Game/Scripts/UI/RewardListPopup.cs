using System.Collections.Generic;
using UnityEngine;

public class RewardListPopup : Popup<List<Reward>> {
    [Header("Reward List Popup")]
    [SerializeField] RewardsData rewards;
    [SerializeField] IconTextItem itemPrefab;

    protected override void Awake() {
        base.Awake();
        if (positionContainer != null)
            positionContainer.gameObject.SetActive(false);
    }

    public override bool Setup(List<Reward> value) {
        if (rewards != null && value != null && itemPrefab != null && positionContainer != null) {
            foreach (Transform child in positionContainer)
                Destroy(child.gameObject);

            foreach (Reward reward in value) {
                if (reward != null) {
                    IconTextItem item = Instantiate(itemPrefab, positionContainer);
                    item.Setup(rewards.GetIcon(reward), reward.Amount.ToShortString());
                }
            }
            return base.Setup(value);
        }
        return false;
    }

    public override void SetVisibility(bool visible) {
        if (positionContainer != null)
            positionContainer.gameObject.SetActive(visible);
        base.SetVisibility(visible);
    }
}
