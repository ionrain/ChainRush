using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

public class StagesProgressItem : MonoBehaviour {
    [SerializeField] MMF_Player activateFeedback;
    [SerializeField] MMF_Player deactivateFeedback;
    [SerializeField] Image icon;
    [SerializeField] Sprite normalSprite;
    [SerializeField] Sprite battleSprite;

    public void Setup(LevelStage stage) {
        if (stage != null) {
            if (icon != null)
                icon.sprite = stage.enemyData != null ? battleSprite : normalSprite;
        }
    }

    public void Activate() {
        if (activateFeedback != null)
            activateFeedback.PlayFeedbacks();
    }

    public void Deactivate() {
        if (deactivateFeedback != null)
            deactivateFeedback.PlayFeedbacks();
    }
}
