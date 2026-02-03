using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class SpineTextItem : MonoBehaviour {
    [SerializeField] SkeletonGraphic spine;
    [SerializeField] string animationName;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] bool canChangeColor;

    [Header("Events")]
    public UnityEvent OnTextEmpty;

    public string Text => label.text;

    public void Setup(SkeletonDataAsset spineAsset, string text) {
        SetSpine(spineAsset);
        SetText(text);
    }

    public void SetText(string text) {
        if (label != null && text.Length > 0)
            label.SetText(text);
        else
            OnTextEmpty?.Invoke();
    }

    public void SetTextColor(Color color) {
        if (label != null && canChangeColor)
            label.color = color;
    }

    public void SetSpineColor(Color color) {
        if (spine != null && canChangeColor)
            spine.color = color;
    }

    public void SetSpine(SkeletonDataAsset spineAsset) {
        if (spine != null && spineAsset != null) {
            spine.skeletonDataAsset = spineAsset;
            spine.Clear();
            spine.AnimationState.ClearTracks();
            var animation = spine.Skeleton.Data.FindAnimation(animationName);
            if (animation != null)
                spine.AnimationState.SetAnimation(0, animationName, true);
        }
    }
}

