using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// This feedback will play the associated particles system on play, and stop it on stop
/// </summary>
[AddComponentMenu("")]
[FeedbackHelp("This feedback will simply play the specified Spine Animation.")]
[FeedbackPath("Spine/State Animation")]
public class MMF_SpineStateAnimation : MMF_Feedback {
    /// a static bool used to disable all feedbacks of this type at once
    public static bool FeedbackTypeAuthorized = true;
    public override float FeedbackDuration { get { return ApplyTimeMultiplier(GetDuration()); } set {  } }
    public override bool HasAutomatedTargetAcquisition => true;
    public override string GetLabel() => string.Format("{0} - {1}", Label, State);
    protected override void AutomateTargetAcquisition() => SpineModel = FindAutomatedTarget<SpineSkeletonModel>();
    
    #if UNITY_EDITOR
    /// sets the inspector color for this feedback
    public override Color FeedbackColor { get { return MMFeedbacksInspectorColors.AnimationColor; } }
    public override bool EvaluateRequiresSetup() { return SpineModel == null; }
    public override string RequiredTargetText { get { return SpineModel != null ? SpineModel.name : "";  } }
    public override string RequiresSetupText { get { return "This feedback requires that a SpineAnimator be set to be able to work properly. You can set one below."; } }
    #endif

    [MMFInspectorGroup("Bound Spine Animation", true, 41, true)]
    public SpineSkeletonModel SpineModel;
    public AnimationState State;

    protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1) {
        if (!Active || !FeedbackTypeAuthorized)
            return;
        SpineModel?.PlayAnimation(State);
    }

    protected virtual float GetDuration() {
        if (SpineModel != null && State != AnimationState.None) 
            return SpineModel.GetDuration(State, string.Empty).y;
        return 0;
    }
}
