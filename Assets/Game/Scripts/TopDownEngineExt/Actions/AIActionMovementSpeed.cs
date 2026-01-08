using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;

public class AIActionMovementSpeed : AIAction {
    [SerializeField] CharacterMovement movementAbility;
    [SerializeField] float speed;

    public override void PerformAction() {
        if (movementAbility != null)
            movementAbility.MovementSpeed = speed;
    }
}
