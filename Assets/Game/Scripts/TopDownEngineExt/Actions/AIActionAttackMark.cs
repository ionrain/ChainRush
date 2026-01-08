using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

public class AIActionAttackMark : AIAction {
    [SerializeField] AttackMark attackMark;

    public override void OnEnterState() {
        if (_brain != null)
            attackMark.Activate(_brain.Target);
        base.OnEnterState();
    }

    public override void PerformAction() { }
}
