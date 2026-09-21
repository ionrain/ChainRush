using System;
using System.Collections.Generic;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using MoreMountains.TopDownEngine;
using UnityEditor;
using UnityEngine;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        static FrameworkSkillData ConfigurePlayerMeleeAttack(FrameworkSkillData attack, global::SkillData source,
            int level, string name, MeleeWeapon weapon, long damage, double step)
        {
            var touch = weapon.ExistingDamageArea;
            var circle = touch == null ? null : touch.GetComponent<CircleCollider2D>();
            if (weapon.MeleeDamageAreaMode != MeleeWeapon.MeleeDamageAreaModes.Existing || circle == null
                || circle.offset != Vector2.zero || circle.transform.position != weapon.transform.position)
                throw new InvalidOperationException("Included melee content requires its centered source circle: " + name);
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var installer = LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var hit = WriteContentSkill(SkillsRoot + "/" + name + "Hit.asset", attack.Id + ".hit",
                GetField<List<EconomyAssetData>>(economy, "assets"), GetField<List<FrameworkSkillData>>(installer, "skills"));
            hit.Effects.Add(ContentDamage(damage));
            Vector3 knockback = touch.DamageCausedKnockbackForce;
            knockback.x = source.GetParameterValue(SkillParameterType.Knockback, level, knockback.x);
            if (knockback != Vector3.zero && touch.DamageCausedKnockbackType != DamageOnTouch.KnockbackStyles.NoKnockback)
            {
                if (touch.DamageCausedKnockbackType != DamageOnTouch.KnockbackStyles.AddForce
                    || touch.DamageCausedKnockbackDirection != DamageOnTouch.KnockbackDirections.BasedOnOwnerPosition)
                    throw new InvalidOperationException("The player area requires its explicit radial force contract: " + name);
                var force = new SkillCarrierForceEffectData();
                ConfigureEffect(force, EffectRecipient.Target, RoundContent(knockback.magnitude * 1000));
                SetField(force, "directionType", SkillForceDirectionType.OwnerToTarget);
                SetField(force, "protectionTag", LoadRequired<TaxonomyTermData>(DamageProtectionPath));
                hit.Effects.Add(force);
            }
            var area = new SkillCarrierAreaContactData();
            SetField(area, "followOwner", true);
            SetField(area, "requiredOwnerValue", LoadRequired<HostValueData>(HealthPath));
            SetField(area, "minimumOwnerValue", 1L);
            SetField(area, "radius", Distance(source.GetParameterValue(SkillParameterType.Radius, level, circle.radius)
                * Mathf.Abs(circle.transform.lossyScale.x)));
            SetField(area, "repeatInterval", checked((int)Math.Max(1, ContentDuration(touch.InvincibilityDuration, step))));
            SetField(area, "targetGeometryType", WorldTargetGeometryType.Interaction);
            SetField(area, "interactionTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatantRolePath) });
            var spawn = new SkillSpawnCarrierEffectData();
            SetField(spawn, "carrier", WriteContactCarrier());
            SetField(spawn, "carriedSkill", hit);
            SetField(spawn, "areaContact", area);
            spawn.Parameters.Add(CarrierScalar(SkillCarrierScalarParameterType.Lives, 1));
            spawn.Parameters.Add(CarrierScalar(SkillCarrierScalarParameterType.Lifetime,
                Math.Max(1, ContentDuration(weapon.ActiveDuration, step))));
            SetField(attack, "effects", new List<SkillEffectData> { spawn });
            SetField(attack, "startDelay", ContentDuration(weapon.DelayBeforeUse + weapon.InitialDelay, step));
            SetField(attack, "endDelay", ContentDuration(weapon.ActiveDuration, step));
            EditorUtility.SetDirty(economy);
            EditorUtility.SetDirty(installer);
            return hit;
        }
    }
}
