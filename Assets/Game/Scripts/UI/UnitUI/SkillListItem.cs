using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MoreMountains.Feedbacks;

public class SkillListItem : MonoBehaviour {
    [SerializeField] Image icon;
    [SerializeField] RectTransform textRect;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI descriptionLabel;
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI priceLabel;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Color outBalanceColor = Color.red;
    [SerializeField] MMF_Player buyFeedback;

    UnitSkill _skill;
    Color _priceColor;
    bool _active;

    public SkillData Data => _skill != null ? _skill.data : null;

    public void Setup(UnitSkill skill, bool active) {
        _skill = skill;
        if (_skill != null) {
            _active = active && _skill.CanBeAquired;
            SkillData data = _skill.data;
            bool collapseInfo = !_active || _skill.Aquired;

            if (icon != null)
                icon.sprite = data.icon;
            if (titleLabel != null)
                titleLabel.text = data.Title;
            if (descriptionLabel != null)
                descriptionLabel.text = data.Description;
            if (priceLabel != null) {
                _priceColor = priceLabel.color;
                priceLabel.text = skill.Cost.ToString();
            }
            if (button != null)
                button.gameObject.SetActive(!collapseInfo);
            if (canvasGroup != null) {
                canvasGroup.interactable = _active;
                canvasGroup.alpha = _active ? 1 : 0.3f;
            }
            if (textRect != null && collapseInfo)
                textRect.offsetMax = new Vector2(0, textRect.offsetMax.y);

        }
    }

    public void OnClicked() {
        _skill.SetAquired(true);
        //SpendResourceEvent.Trigger(EventStage.Start, ResourceType.UnitSkillPoint, ResourceTarget.Skill, _skill.data.name, _skill.Cost);
        SkillAquireEvent.Trigger(EventStage.End, _skill.data);
    }

    public void PlayBuyFeedback() {
        buyFeedback?.PlayFeedbacks();
    }

    public void SetupPriceColor(int balance) {
        if (_active && _skill != null && priceLabel != null && button != null) {
            bool available = _skill.Cost <= balance;
            priceLabel.color = available ? _priceColor : outBalanceColor;
            button.interactable = available;
        }
    }
}
