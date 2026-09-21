using System;
using System.Collections.Generic;
using System.Linq;
using ChainRush.Gameplay;
using Core;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Projection;
using Core.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FrameworkAttributeData = Core.Attributes.AttributeData;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyHeroSkills()
        {
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(economy, "assets");
            var installer = LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var skills = GetField<List<FrameworkSkillData>>(installer, "skills");
            var source = LoadRequired<global::SkillData>("Assets/Game/Resources/Skills/SkillLightningBolt.asset");
            var sourceAttack = source.prefab as DistantAttackSkill
                ?? throw new InvalidOperationException("LightningBolt requires its source distant attack.");
            var projectileObject = LoadRequired<GameObject>("Assets/Game/Prefabs/Skills/LightningBoltProjectile.prefab");
            var projectile = projectileObject.GetComponent<MoreMountains.TopDownEngine.Projectile>();
            var collider = projectileObject.GetComponent<CircleCollider2D>();
            var touch = projectileObject.GetComponent<MoreMountains.TopDownEngine.DamageOnTouch>();
            if (projectile == null || collider == null || touch == null || projectile.Speed != 0)
                throw new InvalidOperationException("LightningBolt requires its stationary circular source projectile.");
            var carrier = WriteParticleCarrierVisual(projectileObject, "LightningBoltProjectile");
            var targets = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushVisibleEnemiesQueryData>(
                "Assets/Game/Runtime/Run/VisibleEnemiesQuery.asset", null);
            SetField(targets, "health", LoadRequired<HostValueData>(HealthPath));
            EditorUtility.SetDirty(targets);
            var attributes = new Dictionary<ElementalAttribute, FrameworkAttributeData>();
            foreach (Element element in Enum.GetValues(typeof(Element)))
            {
                if (element == Element.Any) continue;
                foreach (var attribute in new[] { global::Attribute.Power, global::Attribute.Defense })
                    attributes.Add(new ElementalAttribute(element, attribute),
                        LoadRequired<FrameworkAttributeData>(RunAttributesRoot + "/" + attribute + element + ".asset"));
            }
            double step = GetField<float>(LoadRequired<ActivityData>(ActivityPath).Schedule, "tickDelta");
            var hero = LoadRequired<CapabilityHostData>(PerfumePath);
            var seeds = hero.WalletEntries.Single(entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath)).Seed;
            var levels = new List<FrameworkSkillData>();
            for (int level = 0; level < source.LevelsCount; level++)
            {
                string name = "LightningBolt" + (level + 1);
                var skill = WriteContentSkill(SkillsRoot + "/" + name + ".asset", "chainrush.skill.lightning-bolt." + (level + 1), definitions, skills);
                SetField(skill, "targetCount", new IntRange(1, 256));
                SetField(skill, "actionCount", checked((int)RoundContent(source.GetParameterValue(
                    SkillParameterType.Count, level, sourceAttack.Weapon.BurstLength))));
                SetField(skill, "actionInterval", ContentDuration(source.GetParameterValue(
                    SkillParameterType.IntervalBetween, level, sourceAttack.Weapon.BurstTimeBetweenShots), step));
                SetField(skill, "reloadTime", ContentDuration(source.GetParameterValue(
                    SkillParameterType.Duration, level, sourceAttack.Weapon.TimeBetweenUses), step));
                var hit = WriteContentSkill(SkillsRoot + "/" + name + "Hit.asset", skill.Id + ".hit", definitions, skills);
                hit.Effects.Add(ContentDamage(1));
                var spawn = new SkillSpawnCarrierEffectData();
                SetField(spawn, "carrier", carrier);
                SetField(spawn, "carriedSkill", hit);
                SetField(spawn, "count", checked((int)RoundContent(source.GetParameterValue(SkillParameterType.Projectiles, level, 1))));
                SetField(spawn, "targetSelectionType", SkillCarrierTargetSelectionType.RandomEntity);
                SetField(spawn, "targetQuery", targets);
                SetField(spawn.SpawnResolver, "source", SkillCarrierSpawnSourceType.Target);
                float radius = source.GetParameterValue(SkillParameterType.Size, level, collider.radius);
                var area = new SkillCarrierAreaContactData();
                SetField(area, "radius", checked((int)RoundContent(radius * 1000)));
                SetField(area, "repeatInterval", checked((int)ContentDuration(touch.InvincibilityDuration, step)));
                SetField(area, "maxCandidates", 256);
                SetField(spawn, "areaContact", area);
                spawn.Parameters.Add(CarrierScalar(SkillCarrierScalarParameterType.Lives, 1));
                spawn.Parameters.Add(CarrierScalar(SkillCarrierScalarParameterType.Lifetime,
                    ContentDuration(source.GetParameterValue(SkillParameterType.Lifetime, level, projectile.LifeTime), step)));
                skill.Effects.Add(spawn);
                ConfigureElementalAttack(skill, hit, RoundContent(source.GetParameterValue(SkillParameterType.Amount, level, 0)
                    * CombatValueScale), attributes);
                SetDamageProtection(hit, touch.InvincibilityDuration, step);
                seeds.RemoveAll(seed => seed.Asset == skill);
                seeds.Add(new SeedEntry(skill, 1, EconomyFormType.Stack));
                levels.Add(skill);
                EditorUtility.SetDirty(skill);
                EditorUtility.SetDirty(hit);
            }
            var feature = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushHeroSkillFeatureData>(
                "Assets/Game/Runtime/Run/HeroSkillFeature.asset", null);
            SetField(feature, "board", LoadRequired<ActivityData>(BoardActivityPath));
            SetField(feature, "hero", hero);
            SetField(feature, "cell", LoadRequired<CapabilityHostData>(BoardRoot + "/Economy/LightningBoltBoardBase.asset"));
            SetField(feature, "health", LoadRequired<HostValueData>(HealthPath));
            SetField(feature, "targets", targets);
            SetField(feature, "sourceSkillId", source.name);
            SetField(feature, "levels", levels);
            var attributesFeature = LoadRequired<ChainRushRunAttributesFeatureData>("Assets/Game/Runtime/Run/RunAttributesFeature.asset");
            var character = GetField<List<ChainRushRunCharacterBinding>>(attributesFeature, "characters")
                .Single(binding => binding.Definition == hero);
            character.Skills.RemoveAll(binding => binding.ContentId == source.name);
            foreach (var level in levels)
                character.Skills.Add(new ChainRushRunSkillBinding { ContentId = source.name, Definition = level });
            EditorUtility.SetDirty(attributesFeature);
            foreach (string path in new[] { ActivityPath, DistanceActivityPath })
            {
                var activity = LoadRequired<ActivityData>(path);
                AddUnique(activity.Teams[0].Features, feature);
                EditorUtility.SetDirty(activity);
            }
            var scene = EditorSceneManager.OpenScene(IntegrationScenePath, OpenSceneMode.Single);
            var camera = GameObject.Find("AutobattleCamera")?.GetComponent<Camera>();
            if (camera == null) throw new InvalidOperationException("The integration combat camera is missing.");
            var adapter = camera.GetComponent<ChainRushCombatViewAdapter>() ?? camera.gameObject.AddComponent<ChainRushCombatViewAdapter>();
            SetField(adapter, "combatCamera", camera);
            EditorUtility.SetDirty(adapter);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(hero);
            EditorUtility.SetDirty(economy);
            EditorUtility.SetDirty(installer);
        }

        static ProjectionDefinitionData WriteParticleCarrierVisual(GameObject source, string name)
        {
            var definition = ChainRushBoardPlannerAuthoring.WriteContentAsset<ProjectionDefinitionData>(ProjectionRoot + "/" + name + ".asset", null);
            var root = new GameObject(name);
            try
            {
                ConfigureProjectionBinding(root, "chainrush.carrier." + name.ToLowerInvariant());
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(90, 0, 0);
                foreach (var component in visual.GetComponentsInChildren<Component>(true).Reverse())
                    if (component != null && !(component is Transform) && !(component is ParticleSystem)
                        && !(component is ParticleSystemRenderer) && !(component is SpriteRenderer))
                        UnityEngine.Object.DestroyImmediate(component);
                if (visual.GetComponentsInChildren<ParticleSystem>(true).Length == 0)
                    throw new InvalidOperationException(name + " has no particle projection.");
                string path = ProjectionRoot + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                ConfigureAddressable(path, AddressablesGroup);
                SetField(definition, "projectionPrefabReference", new ProjectionPrefabReference(AssetDatabase.AssetPathToGUID(path)));
                SetField(definition.ProjectionPool, "maxCapacity", 128);
                EditorUtility.SetDirty(definition);
                return definition;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
