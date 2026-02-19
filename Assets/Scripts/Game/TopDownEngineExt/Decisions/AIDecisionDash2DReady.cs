using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class AIDecisionDash2DReady : AIDecision {
    [SerializeField] CharacterDash2D dashAbility;
    [SerializeField] float minDistance = 3;
    [SerializeField] float maxDistance = 6;
    public override bool Decide() {
        if (_brain != null && _brain.Target != null && dashAbility != null && dashAbility.AbilityPermitted && dashAbility.Cooldown.Ready()) {
            float distance = Vector2.Distance(transform.position, _brain._lastKnownTargetPosition);
            return distance >= minDistance && distance <= maxDistance;
        }
        return false;
    }
}
