using System;
using System.Collections.Generic;
using System.Linq;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.Skills;
using Core.World;
using MoreMountains.TopDownEngine;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyMovementForces()
        {
            var installer = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(installer, "assets");
            var prefab = LoadRequired<GameObject>("Assets/Game/Prefabs/Units/Unit.prefab");
            var body = prefab.GetComponent<Rigidbody2D>();
            var health = prefab.GetComponent<Health>();
            var unit = prefab.GetComponent<Unit>();
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic || health == null
                || health.ImmuneToKnockback || health.ImmuneToKnockbackIfZeroDamage || health.KnockbackForceMultiplier != 1)
                throw new InvalidOperationException("The source unit force response has changed.");
            bool mergeBody = new SerializedObject(unit).FindProperty("changeColliderSettings").boolValue;
            foreach (string name in new[] { "Water", "Cola" })
            {
                var source = LoadRequired<UnitData>("Assets/Game/Resources/Units/" + name + "Data.asset");
                for (int form = 0; form < source.MergeStatesCount; form++)
                {
                    string hostName = name + "Unit" + (form == 0 ? "" : (form + 1).ToString());
                    var host = LoadRequired<CapabilityHostData>(SharedRoot + "/Units/" + name + "/" + hostName + ".asset");
                    var movement = ChainRushBoardPlannerAuthoring.WriteContentAsset<MovementData>(
                        SharedRoot + "/Movement/" + hostName + "Movement.asset", LoadRequired<MovementData>(MovementPath),
                        host.Id + ".movement", definitions);
                    var response = new MovementForceResponseData();
                    SetField(response, "mass", mergeBody ? source.GetMergeData(form).mass : body.mass);
                    SetField(response, "stepDuration", Time.fixedDeltaTime);
                    // TopDownController2D.ApplyImpact uses these explicit constants in the source game.
                    SetField(response, "decayPerSecond", 5f);
                    SetField(response, "minimumForce", 200f);
                    if (!response.IsValid) throw new InvalidOperationException("Invalid source force response: " + hostName);
                    SetField(movement, "forceResponse", response);
                    var seeds = host.WalletEntries.Single(entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath)).Seed;
                    seeds.RemoveAll(seed => seed.Asset is MovementData);
                    seeds.Add(new SeedEntry(movement, 1, EconomyFormType.Stack));
                    EditorUtility.SetDirty(movement); EditorUtility.SetDirty(host);
                }
            }
            foreach (string name in new[] { "BugBrownSmall", "BugBrownMedium", "BugGreenSmall", "BugGreenMedium", "BugPurpleSmall", "BugPurpleMedium" })
            {
                var source = LoadRequired<GameObject>("Assets/Game/Prefabs/Enemies/" + name + ".prefab");
                var sourceBody = source.GetComponent<Rigidbody2D>();
                var sourceHealth = source.GetComponent<Health>();
                if (sourceBody == null || sourceBody.bodyType != RigidbodyType2D.Dynamic || sourceHealth == null
                    || sourceHealth.ImmuneToKnockback || sourceHealth.ImmuneToKnockbackIfZeroDamage
                    || sourceHealth.KnockbackForceMultiplier != 1)
                    throw new InvalidOperationException("The source enemy force response requires explicit authoring: " + name);
                var host = LoadRequired<CapabilityHostData>(EconomyRoot + "/" + name + ".asset");
                var movement = ChainRushBoardPlannerAuthoring.WriteContentAsset<MovementData>(
                    SharedRoot + "/Movement/" + name + "Movement.asset", LoadRequired<MovementData>(MovementPath),
                    host.Id + ".movement", definitions);
                var response = new MovementForceResponseData();
                SetField(response, "mass", sourceBody.mass);
                SetField(response, "stepDuration", Time.fixedDeltaTime);
                SetField(response, "decayPerSecond", 5f);
                SetField(response, "minimumForce", 200f);
                SetField(movement, "forceResponse", response);
                var seeds = host.WalletEntries.Single(entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath)).Seed;
                seeds.RemoveAll(seed => seed.Asset is MovementData);
                seeds.Add(new SeedEntry(movement, 1, EconomyFormType.Stack));
                EditorUtility.SetDirty(movement); EditorUtility.SetDirty(host);
            }
            var adapters = LoadRequired<SkillEffectAdapterCatalogData>(SkillsCatalogPath);
            if (!adapters.Adapters.Any(value => value is SkillCarrierForceEffectAdapterData))
                adapters.Adapters.Add(new SkillCarrierForceEffectAdapterData());
            EditorUtility.SetDirty(adapters); EditorUtility.SetDirty(installer);
        }

        static void WriteCarrierForce(Core.Skills.SkillData hit, DamageOnTouch source)
        {
            if (source.DamageCausedKnockbackType == DamageOnTouch.KnockbackStyles.NoKnockback) return;
            if (source.DamageCausedKnockbackType != DamageOnTouch.KnockbackStyles.AddForce
                || source.DamageCausedKnockbackDirection != DamageOnTouch.KnockbackDirections.BasedOnScriptDirection)
                throw new InvalidOperationException("The enemy weapon requires another explicit force direction: " + source.name);
            var force = new SkillCarrierForceEffectData();
            ConfigureEffect(force, EffectRecipient.Target, RoundContent(source.DamageCausedKnockbackForce.magnitude * 1000));
            SetField(force, "directionType", SkillForceDirectionType.CarrierTravel);
            SetField(force, "protectionTag", LoadRequired<Core.Taxonomy.TaxonomyTermData>(DamageProtectionPath));
            hit.Effects.Add(force);
        }
    }
}
