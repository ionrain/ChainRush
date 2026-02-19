using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class AIDecisionWeaponReady : AIDecision {
    [SerializeField] Weapon weapon;
    [SerializeField] float distance = 1;

    public override bool Decide() {
        return weapon != null && weapon.gameObject.activeSelf && weapon.WeaponState.CurrentState == Weapon.WeaponStates.WeaponIdle && 
                    Vector2.Distance(_brain.Target.position, _brain.transform.position) <= distance;
    }
}
