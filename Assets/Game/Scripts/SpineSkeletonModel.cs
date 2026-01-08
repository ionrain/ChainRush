using System.Collections.Generic;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Events;

public enum AnimationState { None, Idle, Walk, Dead, Attack, Hit, Dash, Crouch, Jump, Land, Air, Fall, Stun, Block, 
                            Upgrade, Ability1, Ability2, Ability3, AttackPrepare, DashPrepare, Revive, Appear }

[System.Serializable]
public class SpineAnimationData {
    public string name;
    public int track = 0;
    public bool loop = false;
    public AnimationState addAfter = AnimationState.None;
    public List<AnimationState> dontInterruptStates = new List<AnimationState>();
}

public class SpineSkeletonModel : SerializedMonoBehaviour {
    [SerializeField] SkeletonAnimation spine;
    [SerializeField] bool debug;

    [Header("Animations")]
    [SerializeField] Dictionary<AnimationState, SpineAnimationData> animations = new Dictionary<AnimationState, SpineAnimationData>();
    [Header("Animation events")]
    [SerializeField] Dictionary<string, UnityEvent> events = new Dictionary<string, UnityEvent>();

    AnimationState _state;

    void Awake() {
        Setup();
    }

    public void Setup(SkeletonDataAsset asset, string skin, Dictionary<AnimationState, SpineAnimationData> spineAnimations) {
        if (spine != null) {
            spine.skeletonDataAsset = asset;
            spine.ClearState();
            if (skin.Length > 0)
                spine.initialSkinName = skin;
            animations = spineAnimations;
            spine.Initialize(true);
            _state = AnimationState.None;
            Setup(); 
        }
    }

    protected void Setup() {
        if (spine != null && spine.state != null) {
            spine.state.Event -= HandleAnimationEvent;
            spine.state.Event += HandleAnimationEvent;
        }
    }


    public bool IsAnimationActive(AnimationState state) {
        SpineAnimationData data = animations.GetValueOrDefault(state, null);
        if (data != null) {
            var tracks = spine.AnimationState.Tracks.Items;
            if (tracks.Length > data.track && tracks[data.track] != null) {
                var track = tracks[data.track];
                return !track.IsEmptyAnimation && track.Animation.Name.Equals(data.name) && track.IsComplete;
            }
        }
        return false;
    }

    public void PlayAnimation(int state) {
        PlayAnimation((AnimationState)state);
    }

    public Vector2 GetDuration(AnimationState state, string eventName) {
        Vector2 result = Vector2.zero;
        SpineAnimationData data = animations.GetValueOrDefault(state, null);
        if (spine != null && data != null) {
            var animation = spine.Skeleton.Data.FindAnimation(data.name);
            if (animation != null) {
                float total = animation.Duration;
                result = new Vector2(total, total);
                if (eventName.Length > 0) {
                    foreach (var timeline in animation.Timelines) {
                        Spine.EventTimeline eventTimeline = timeline as Spine.EventTimeline;
                        if (eventTimeline != null) {
                            foreach (var timelineEvent in eventTimeline.Events) {
                                if (timelineEvent.Data.Name.Equals(eventName)) {
                                    result.x = timelineEvent.Time;
                                    break;
                                }
                            }
                        }
                    }
                }
            } else
                Debug.LogErrorFormat("SpineSkeletonModel GetDuration: Spine animation not found for {0}", state);
        }
        return result;
    }

    public void PlayAnimation(AnimationState state, bool add = false) {
        if (spine != null && spine.state != null && animations.ContainsKey(state)) {
            if (debug)
                Debug.LogFormat("SpineSkeletonModel PlayAnimation: Changing state from {0} to {1} for {2}", _state, state, spine.skeletonDataAsset.name);
            
            SpineAnimationData data = animations[state];
            if (data != null) {
                bool playing = false;
                
                SpineAnimationData lastData = animations.GetValueOrDefault(_state, null);
                if (lastData != null && lastData.track == data.track) {
                    var tracks = spine.AnimationState.Tracks.Items;
                    if (tracks.Length > data.track && tracks[data.track] != null) {
                        var track = tracks[data.track];
                        playing = !track.IsEmptyAnimation && !track.IsComplete;
                    }
                }

                if ((data.dontInterruptStates.Contains(_state) && playing) || add)
                    spine.state.AddAnimation(data.track, data.name, data.loop, 0);
                else
                    spine.state.SetAnimation(data.track, data.name, data.loop);
                     

                if (data.addAfter != AnimationState.None)
                    PlayAnimation(data.addAfter, true);

                _state = state;
            }
        }
    }

    protected virtual void HandleAnimationEvent(Spine.TrackEntry entry, Spine.Event e) { 
        if (entry != null && e != null && e.Data != null && events.ContainsKey(e.Data.Name))
            events[e.Data.Name]?.Invoke();
    }
}
