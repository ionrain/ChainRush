using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class EnemyProjectileWeapon : ProjectileWeapon {
    protected float _powerMultiplier;

    public void Setup(float powerMultiplier) {
        _powerMultiplier = powerMultiplier;
    }

    public override GameObject SpawnProjectile(Vector3 spawnPosition, int projectileIndex, int totalProjectiles, bool triggerObjectActivation = true) {
        GameObject result = base.SpawnProjectile(spawnPosition, projectileIndex, totalProjectiles, triggerObjectActivation);
        DamageOnTouchController damageController = result.GetComponent<DamageOnTouchController>();
        if (damageController != null)
            damageController.Setup(_powerMultiplier);
        return result;
    }
}
