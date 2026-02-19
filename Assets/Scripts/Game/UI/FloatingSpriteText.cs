using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

public class FloatingSpriteText : MonoBehaviour {
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] protected TextMeshPro label;
    [SerializeField] MMFeedbacks feedback;

    public void Setup(Sprite sprite, string text) {
        if (spriteRenderer != null)
            spriteRenderer.sprite = sprite;
        if (label != null)
            label.text = text;
    }

    public void Show() {
        if (feedback != null)
            feedback.PlayFeedbacks();
    }
}
