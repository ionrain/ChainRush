using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;

public enum CellItemType { None = 0, Unit = 1, Buff = 2, Booster = 4, Loot = 8 }

public class CellItem : SerializedMonoBehaviour {
    [SerializeField] protected SpriteRenderer image;
    [SerializeField] protected MMF_Player activateFeedback;
    [SerializeField] protected MMF_Player highlightFeedback;
    [SerializeField] protected MMF_Player dehighlightFeedback;

    bool _highlighted;

    public virtual void Setup(Vector2Int position, object param) {
    }

    public virtual void Highlight(bool value) {
        if (_highlighted != value) {
            _highlighted = value;
            if (value && highlightFeedback != null) {
                gameObject.SetActive(true);
                highlightFeedback.PlayFeedbacks();
            } else if (!value && dehighlightFeedback != null)
                dehighlightFeedback.PlayFeedbacks();
        }
    }

    public virtual void SetVisible(bool value) {
        if (value) {
            gameObject.SetActive(true);
            activateFeedback?.PlayFeedbacks();
        } else if (!_highlighted)
            gameObject.SetActive(false);
    }
}
