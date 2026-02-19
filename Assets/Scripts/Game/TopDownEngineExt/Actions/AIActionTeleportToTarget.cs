using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class AIActionTeleportToTarget : AIAction {
    [SerializeField] Character character;
    [SerializeField] float radius = 3;

    public override void PerformAction() {
        if (_brain.Target != null && character != null) {
            Vector3 randomPos = Random.onUnitSphere;
            randomPos.x *= radius;
            randomPos.y *= radius;
            randomPos.z = 0;
            character.transform.position = _brain.Target.transform.position + randomPos;
        }
     }
}