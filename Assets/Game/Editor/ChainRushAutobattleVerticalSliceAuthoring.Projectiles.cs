using System;
using Core.Skills;
using MoreMountains.TopDownEngine;
using UnityEngine;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        static void ConfigurePlayerProjectileParameters(SkillSpawnCarrierEffectData spawn, Core.Skills.SkillData hit,
            global::SkillData source, int level, DamageOnTouch touch, double step)
        {
            var projectile = touch.GetComponent<Projectile>();
            if (projectile == null) throw new InvalidOperationException("Player projectile movement is missing: " + touch.name);
            // The source Projectile.Movement converts its authored Speed with Speed / 10.
            long speed = RoundContent(source.GetParameterValue(SkillParameterType.Speed, level, projectile.Speed) / 10d * 1000 * step);
            var velocity = spawn.Parameters.Find(value => value is SkillCarrierTargetLinearVelocityParameterValueData);
            if (velocity == null) throw new InvalidOperationException("Player projectile velocity is missing: " + source.name);
            SetField(velocity, "minSpeed", speed); SetField(velocity, "maxSpeed", speed);
            hit.Effects.RemoveAll(value => value is SkillCarrierForceEffectData);
            Vector3 strength = touch.DamageCausedKnockbackForce;
            float authored = source.GetParameterValue(SkillParameterType.Knockback, level, 0);
            if (authored > 0) strength.x = authored;
            if (strength == Vector3.zero || touch.DamageCausedKnockbackType == DamageOnTouch.KnockbackStyles.NoKnockback) return;
            if (touch.DamageCausedKnockbackType != DamageOnTouch.KnockbackStyles.AddForce
                || touch.DamageCausedKnockbackDirection != DamageOnTouch.KnockbackDirections.BasedOnDirection)
                throw new InvalidOperationException("Player projectile requires its explicit displacement force: " + source.name);
            var force = new SkillCarrierForceEffectData();
            ConfigureEffect(force, EffectRecipient.Target, RoundContent(strength.magnitude * 1000));
            SetField(force, "directionType", SkillForceDirectionType.CarrierDisplacement);
            SetField(force, "protectionTag", LoadRequired<Core.Taxonomy.TaxonomyTermData>(DamageProtectionPath));
            hit.Effects.Add(force);
        }

        static void ConfigureProjectileContact(SkillSpawnCarrierEffectData spawn, GameObject source, double step)
        {
            var box = source.GetComponent<BoxCollider2D>();
            var touch = source.GetComponent<DamageOnTouch>();
            if (box == null || touch == null || box.edgeRadius != 0)
                throw new InvalidOperationException("The player projectile requires its authored rectangular contact geometry: " + source.name);
            Vector3 scale = box.transform.lossyScale;
            var contact = new SkillCarrierAreaContactData();
            SetField(contact, "shapeType", SkillCarrierContactShapeType.Box);
            SetField(contact, "boxSize", new Vector3Int(Distance(box.size.x * Mathf.Abs(scale.x)), 0,
                Distance(box.size.y * Mathf.Abs(scale.y))));
            SetField(contact, "boxOffset", new Vector3Int(Coordinate(box.offset.x * scale.x), 0, Coordinate(box.offset.y * scale.y)));
            SetField(contact, "sweepMovement", true);
            SetField(contact, "interactionTags", new System.Collections.Generic.List<Core.Taxonomy.TaxonomyTermData>
                { LoadRequired<Core.Taxonomy.TaxonomyTermData>(CombatantRolePath) });
            SetField(contact, "repeatInterval", checked((int)Math.Max(1, ContentDuration(touch.InvincibilityDuration, step))));
            SetField(spawn, "areaContact", contact);
        }
    }
}
