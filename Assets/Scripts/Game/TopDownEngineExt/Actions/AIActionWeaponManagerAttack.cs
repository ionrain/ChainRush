using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public class AIActionWeaponManagerAttack : AIAction {
    [SerializeField] WeaponManager weaponManager;

    public override void OnEnterState() {
        base.OnEnterState();
        if (weaponManager != null && _brain != null && _brain.Target != null)
            weaponManager.Attack(_brain.Target);
    }

    public override void PerformAction() { }

    public override void OnExitState() {
        if (weaponManager != null)
            weaponManager.Interrupt();
        base.OnExitState();
    }
}
