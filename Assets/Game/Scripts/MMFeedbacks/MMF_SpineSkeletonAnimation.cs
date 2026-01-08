using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// This feedback will play the associated particles system on play, and stop it on stop
/// </summary>
[AddComponentMenu("")]
[FeedbackHelp("This feedback will simply play the specified Spine Animation.")]
[FeedbackPath("Spine/Skeleton Animation")]
public class MMF_SpineSkeletonAnimation : MMF_Feedback {
    public enum Modes { Set, Add }

    /// a static bool used to disable all feedbacks of this type at once
    public static bool FeedbackTypeAuthorized = true;
    public override float FeedbackDuration { get { return ApplyTimeMultiplier(GetDuration()); } set {  } }
    public override bool HasAutomatedTargetAcquisition => true;
    public override string GetLabel() => SpineAnimator != null ? string.Format("{0} - {1}", Label, Name) : Label;
    protected override void AutomateTargetAcquisition() => SpineAnimator = FindAutomatedTarget<SkeletonAnimation>();
    
    #if UNITY_EDITOR
    /// sets the inspector color for this feedback
    public override Color FeedbackColor { get { return MMFeedbacksInspectorColors.AnimationColor; } }
    public override bool EvaluateRequiresSetup() { return SpineAnimator == null; }
    public override string RequiredTargetText { get { return SpineAnimator != null ? SpineAnimator.name : "";  } }
    public override string RequiresSetupText { get { return "This feedback requires that a SpineAnimator be set to be able to work properly. You can set one below."; } }
    #endif

    [MMFInspectorGroup("Bound Spine Animation", true, 41, true)]
    public SkeletonAnimation SpineAnimator;

    public Modes Mode = Modes.Set;

    [SpineAnimation(dataField = "SpineAnimator")]
    public string Name;

    public int Track = 0;

    public bool Loop = false;

    [MMCondition("Loop", true, true)]
    public bool AddEmpty = false;

    [MMCondition("Loop", true, true)]
    public float EmptyDelay = 0;

    [MMFEnumCondition("Mode", (int)Modes.Add)]
    public float Delay = 0;

    public bool ClearState = false;
    public float StopMixDuration = 0;


	protected override void CustomInitialization(MMF_Player owner) {
		base.CustomInitialization(owner);
        if (ClearState)
            SpineAnimator?.ClearState();
	}

    protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1) {
        if (!Active || !FeedbackTypeAuthorized)
            return;
        if (Mode == Modes.Set)
            SpineAnimator.state.SetAnimation(Track, Name, Loop); 
        else 
            SpineAnimator.state.AddAnimation(Track, Name, Loop, Delay);

        if (!Loop && AddEmpty)
            SpineAnimator.state.AddEmptyAnimation(Track, StopMixDuration, EmptyDelay);
    }

    protected override void CustomStopFeedback(Vector3 position, float feedbacksIntensity = 1.0f) {
        SpineAnimator.state.SetEmptyAnimation(Track, StopMixDuration);
    }

	protected override void CustomReset() {
        base.CustomReset();

        if (InCooldown)
            return;

        if (ClearState)
            SpineAnimator?.ClearState();
    }

    protected virtual float GetDuration() {
        if (SpineAnimator != null && Name.Length > 0) {
            var animation = SpineAnimator.Skeleton.Data.FindAnimation(Name);
            if (animation != null)
                return animation.Duration;
        }
        return 0;
    }
}
