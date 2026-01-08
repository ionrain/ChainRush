using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

public class AIDecisionAllWeaponsIdle : AIDecision {
    [SerializeField] WeaponManager weaponManager;
    public override bool Decide() {
        if (weaponManager != null)
            return weaponManager.AreAllWeaponsFinished;
        return false;
    }
}
