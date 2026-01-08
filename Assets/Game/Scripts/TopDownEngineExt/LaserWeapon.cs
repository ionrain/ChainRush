using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class LaserWeapon : MeleeWeapon {
    [Header("Laser Weapon")]
    [SerializeField] List<LineRenderer> lineRenderers = new();
    [SerializeField] Transform endPoint;
    [SerializeField] BoxCollider2D damageCollider;

    public override void TurnWeaponOn() {
        base.TurnWeaponOn();
        if (Owner != null && Owner.CharacterBrain != null && Owner.CharacterBrain.Target != null && endPoint != null) {
            endPoint.position = Owner.CharacterBrain.Target.position;
            float length = endPoint.localPosition.x;
            foreach (LineRenderer line in lineRenderers) {
                int count = line.positionCount;
                float delta = length / (count - 1);
                if (count > 1) {
                    for (int i = 1; i < count; i++) {
                        Vector3 position = line.GetPosition(i);
                        line.SetPosition(i, new Vector3(delta * i, position.y, position.z));
                    }
                }
            }
            if (damageCollider != null) {
                damageCollider.size = new Vector2(length, damageCollider.size.y);
                damageCollider.offset = new Vector2(length * 0.5f, damageCollider.offset.y);
            }
        }
    }
}
