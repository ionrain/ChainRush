using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

public class LevelRouletteListItemData : IListItemData {
    public Sprite icon;
    public Color backgroundColor;
    public Color borderColor;
    public bool selected;

    public string Title => string.Empty;
    public string Description => string.Empty;

    public Sprite Icon => icon;

    public LevelRouletteListItemData(Sprite icon, bool selected, Color backgroundColor, Color borderColor) {
        this.icon = icon;
        this.selected = selected;
        this.backgroundColor = backgroundColor;
        this.borderColor = borderColor;
    }
}

public class LevelRouletteListItem : ListItem<LevelRouletteListItem, LevelRouletteListItemData> {
    [SerializeField] MMF_Player showFeedback;
    [SerializeField] MMF_Player blinkFeedback;
    [SerializeField] MMF_Player selectedFeedback;
    [SerializeField] MMF_Player fadeFeedback;

    public override void Setup(LevelRouletteListItemData data) {
        base.Setup(data);

        if (_data != null) {
            _selected = data.selected;

            if (icon != null)
                icon.sprite = _data.Icon;
            
            if (background != null) {
                background.transform.localScale = Vector3.zero;
                if (_data.backgroundColor != Color.clear)
                    background.color = _data.backgroundColor;
            }

            Color color = _data.borderColor;

            if (border != null && color != Color.clear)
                border.color = color;
            
            /*if (selected != null && color != Color.clear) {
                float a = selected.color.a;
                selected.color = new Color(color.r, color.g, color.b, a);
            }*/
        }
    }

    public override void Show() {
        showFeedback?.PlayFeedbacks();
    }

    public void Blink() {
        if (blinkFeedback != null) {
            if (blinkFeedback.IsPlaying)
                blinkFeedback.StopFeedbacks();
            blinkFeedback.PlayFeedbacks();
        }
    }

    public void Selected() {
        if (blinkFeedback != null && blinkFeedback.IsPlaying)
            blinkFeedback.StopFeedbacks();
        selectedFeedback?.PlayFeedbacks();
    }

    public void Fade() {
        fadeFeedback?.PlayFeedbacks();
    }
}
