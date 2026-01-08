using MoreMountains.Feedbacks;
using UnityEngine;

public class BoosterPanelItem : IconTextItem {
    [SerializeField] MMF_Player feedback;

    public void PlayFeedbacks() {
        feedback?.PlayFeedbacks();
    }
}
