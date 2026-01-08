using System.Collections;
using System.Collections.Generic;
using MoreMountains.TopDownEngine;
using UnityEngine;

public class DistantAttackSkill : AttackSkill {
    [Header("Distant Attack Skill")]
    [SerializeField] WeaponAutoAim2D aimer;
    [SerializeField] List<SkillData> richochetData;

    [SerializeField] List<DistantAttackSkill> _richochetSkills = new();

    public override bool Setup(GameObject owner, SkillData skillData, Dictionary<Element, float> multipliers, float skillSpeed, bool activate) {
        if (!base.Setup(owner, skillData, multipliers, skillSpeed, activate))
            return false;

        DistantWeapon projectileWeapon = weapon as DistantWeapon;
        if (elements == null && projectileWeapon == null) {
            Debug.LogFormat("ProjectileWeaponSkill Setup: projectileWeapon or projectileWeapon is NULL for {0}", data.Title);
            return false;
        }

        if (aimer != null)
            aimer.ScanRadius = Data.GetParameterValue(SkillParameterType.Distance, _level, aimer.ScanRadius);

        Dictionary<DamageType, float> typedDamageValues = new Dictionary<DamageType, float>();
        foreach (var pair in _multipliers) {
            if (pair.Value > 0) {
                ElementData elementData = elements.GetData(pair.Key);
                if (elementData != null)
                    typedDamageValues.Add(elementData.damageType, pair.Value);
            }
        }

        CreateRicochetSkills();
        _richochetSkills.ForEach(skill => skill.Setup(owner, skill.Data, multipliers, skillSpeed, false));

        return projectileWeapon.Setup(Data, _level, typedDamageValues);
    }

    protected virtual void CreateRicochetSkills() {
        if (richochetData != null && richochetData.Count > 0 && _richochetSkills != null && _richochetSkills.Count == 0 && Weapon != null) {
            richochetData.ForEach(data => {
                DistantAttackSkill richochetSkill = Instantiate(data.prefab, transform) as DistantAttackSkill;
                if (richochetSkill != null && richochetSkill.Weapon != null) {
                    richochetSkill.Weapon.SetOwner(Weapon.Owner, null);
                    richochetSkill.Weapon.Initialization();
                    _richochetSkills.Add(richochetSkill);
                }
            });
        }
    }
}
