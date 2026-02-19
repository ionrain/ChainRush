using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class MeleeAttackSkill : AttackSkill {
    public override bool Setup(GameObject owner, SkillData skillData, Dictionary<Element, float> multipliers, float skillSpeed, bool activate) {
        if (!base.Setup(owner, skillData, multipliers, skillSpeed, activate))
            return false;

        MeleeWeapon meleeWeapon = weapon as MeleeWeapon;
        if (meleeWeapon == null || meleeWeapon.ExistingDamageArea == null) {
            Debug.LogErrorFormat("MeleeWeaponSkill Setup: MeleeWeapon or ExistingDamageArea is NULL for {0}", Data.Title);
            return false;
        }

        DamageOnTouch area = meleeWeapon.ExistingDamageArea;
        area.gameObject.name = gameObject.name;
        
        CircleCollider2D collider = area.GetComponent<CircleCollider2D>();
        if (collider != null)
            collider.radius = Data.GetParameterValue(SkillParameterType.Radius, _level, collider.radius);
        else
            Debug.LogFormat("MeleeWeaponSkill Setup: Collider is NULL for {0}", Data.Title);

        float damage = Data.GetParameterValue(SkillParameterType.Amount, _level, 0); 
        area.DamageCausedKnockbackForce.x = Data.GetParameterValue(SkillParameterType.Knockback, _level, area.DamageCausedKnockbackForce.x);

        foreach (var pair in _multipliers) {
            ElementData elementData = elements.GetData(pair.Key);
            if (elementData != null)
                area.TypedDamages.ManageEntry(elementData.damageType, damage * pair.Value);
        }

        if (area.TypedDamages.Count > 0) {
            var typedDamage = area.TypedDamages[0];
            float speedMultiplier = Data.GetParameterValue(SkillParameterType.SpeedMultiplier, _level, 0);
            float speedChangeDuration = Data.GetParameterValue(SkillParameterType.SpeedChangeDuration, _level, 0);
            float forcedStateDuration = Data.GetParameterValue(SkillParameterType.ForcedStateDuration, _level, 0);
            CharacterStates.CharacterConditions forcedState = Data.forcedState;

            if (speedChangeDuration > 0) {
                typedDamage.ApplyMovementMultiplier = true;
                typedDamage.MovementMultiplier = speedMultiplier;
                typedDamage.MovementMultiplierDuration = speedChangeDuration;
            }

            if (forcedState != CharacterStates.CharacterConditions.Normal && forcedStateDuration > 0) {
                typedDamage.ForceCharacterCondition = true;
                typedDamage.ForcedCondition = forcedState;
                typedDamage.ForcedConditionDuration = forcedStateDuration;
            }
        }

        return true;
    }
}
