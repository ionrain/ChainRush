using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;

public class AIActionSpineAnimation : AIAction {
    [SerializeField] SpineSkeletonModel spineModel;
    [SerializeField] AnimationState state;

    public override void OnEnterState() {
        base.OnEnterState();
        spineModel?.PlayAnimation(state);
    }

    public override void PerformAction() { }
}
