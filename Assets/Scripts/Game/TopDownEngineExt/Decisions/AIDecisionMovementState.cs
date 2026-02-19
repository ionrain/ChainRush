using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class AIDecisionMovementState : AIDecision {
    [SerializeField] Character character;
    [SerializeField] CharacterStates.MovementStates movementState;

    public override bool Decide() {
        return character != null && character.MovementState.CurrentState == movementState;
    }
}
