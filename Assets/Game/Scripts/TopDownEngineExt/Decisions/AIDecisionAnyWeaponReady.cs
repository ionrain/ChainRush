using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public class AIDecisionAnyWeaponReady : AIDecision {
    [SerializeField] WeaponManager weaponManager;

    public override bool Decide() {
        if (_brain != null && _brain.Target != null && weaponManager != null)
            return weaponManager.IsAnyWeaponReady(Vector2.Distance(_brain.transform.position, _brain.Target.position));
        return false;
    }
}
