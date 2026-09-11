using System;
using System.Collections.Generic;
using Core;
using Core.AI;
using Core.AI.Actions;
using Core.AI.Conditions;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Objectives;
using Core.Production.Authoring;
using Core.Projection;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        const string PerfumePath = SharedRoot + "/Units/Perfume/Perfume.asset";
        const string HeroSpawnPath = TaxonomyRoot + "/HeroSpawn.asset";
        const string CombatGeometryPath = SpaceRoot + "/CombatGeometry.asset";
        const string CarrierLivesPath = HostValuesRoot + "/CarrierLives.asset";

        [MenuItem("Tools/ChainRush/Authoring/Apply Playable Board Content")]
        public static void ApplyPlayableContent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Content authoring requires Edit Mode.");
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var taxonomy = LoadRequired<TaxonomyRuntimeInstallerData>(TaxonomyInstallerPath);
            var skillsInstaller = LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var definitions = new List<EconomyAssetData>(GetField<List<EconomyAssetData>>(economy, "assets"));
            var terms = new List<TaxonomyTermData>(GetField<TaxonomyTermData[]>(taxonomy, "terms"));
            var skills = new List<FrameworkSkillData>(GetField<List<FrameworkSkillData>>(skillsInstaller, "skills"));
            var activity = LoadRequired<ActivityData>(ActivityPath);
            double step = GetField<float>(activity.Schedule, "tickDelta");
            if (!(step > 0)) throw new InvalidOperationException("Autobattle requires a positive simulation step.");

            var geometry = WriteCombatGeometry(definitions);
            var lives = WriteCarrierLives(definitions);
            var water = WriteUnitForms("Water", 4, geometry, lives, step, definitions, skills);
            var cola = WriteUnitForms("Cola", 4, geometry, lives, step, definitions, skills);
            var hero = WriteUnitForms("Perfume", 1, geometry, lives, step, definitions, skills)[0];
            var heroSpawn = ChainRushBoardPlannerAuthoring.WriteContentAsset<TaxonomyTermData>(HeroSpawnPath, null);
            SetField(heroSpawn, "id", "chainrush.autobattle.marker.hero-spawn");
            SetField(heroSpawn, "family", LoadRequired<TaxonomyFamilyData>(MarkerFamilyPath));
            AddUnique(terms, heroSpawn);
            WriteHeroSocket(heroSpawn);
            WriteDeployment(water, cola, hero, heroSpawn, definitions);
            AddCombatGeometryToEnemy(geometry);
            var enemyBrain = LoadRequired<AIBrainData>(EnemyCombatBrainPath);
            var defeatTag = LoadRequired<TaxonomyTermData>(DefeatStatePath);
            AIBrainStateData enemyDefeat = null;
            foreach (AIBrainNodeData node in enemyBrain.Nodes)
                foreach (AIBrainStateData state in node.States)
                    if (state.Tag == defeatTag)
                    {
                        if (enemyDefeat != null)
                            throw new InvalidOperationException("Enemy brain has more than one defeat state.");
                        enemyDefeat = state;
                    }
            if (enemyDefeat == null)
                throw new InvalidOperationException("Enemy brain requires a defeat state.");
            SetField(enemyDefeat, "onExitActions", new List<AIBrainExitActionData> { CreateRemovalOnFailureAction() });
            EditorUtility.SetDirty(enemyBrain);
            ChainRushBoardPlannerAuthoring.ApplyPlayableBoardContent(water, cola, definitions, terms);

            SetField(economy, "assets", definitions);
            SetField(taxonomy, "terms", terms.ToArray());
            SetField(skillsInstaller, "skills", skills);
            var runtime = LoadRequired<EconomyRuntimeInstallerData>(EconomyRuntimeInstallerPath);
            var domains = GetField<List<EconomyDomainType>>(runtime, "domains");
            if (!domains.Contains(EconomyDomainType.Interaction)) domains.Add(EconomyDomainType.Interaction);
            var adapters = LoadRequired<SkillEffectAdapterCatalogData>(SkillsCatalogPath);
            if (!adapters.Adapters.Exists(adapter => adapter is SkillSpawnCarrierEffectAdapterData))
                adapters.Adapters.Add(new SkillSpawnCarrierEffectAdapterData());
            EditorUtility.SetDirty(economy);
            EditorUtility.SetDirty(taxonomy);
            EditorUtility.SetDirty(skillsInstaller);
            EditorUtility.SetDirty(runtime);
            EditorUtility.SetDirty(adapters);
            AssetDatabase.SaveAssets();
            Debug.Log("Playable content authored: nine Board cells, eight deployable forms and stationary Perfume. Runtime code unchanged.");
        }

        static InteractionGeometryData WriteCombatGeometry(List<EconomyAssetData> definitions)
        {
            var geometry = ChainRushBoardPlannerAuthoring.WriteContentAsset<InteractionGeometryData>(
                CombatGeometryPath, null, "chainrush.interaction.combat-body", definitions);
            SetField(geometry, "allowedOperations", AllMutableOperations);
            var binding = new InteractionGeometryBindingData();
            SetField(binding, "shape", LoadRequired<SpatialShapeData>(SpawnAreaShapePath));
            SetField(binding, "usage", new SpatialShapeUsageData(SpatialShapeFillType.Inside,
                Vector3Int.zero, Vector3Int.one, Vector3Int.zero, new Vector3Int(1000, 0, 1000), Vector3Int.zero));
            SetField(geometry, "bindings", new List<InteractionGeometryBindingData> { binding });
            return geometry;
        }

        static HostValueData WriteCarrierLives(List<EconomyAssetData> definitions)
        {
            var lives = ChainRushBoardPlannerAuthoring.WriteContentAsset(CarrierLivesPath,
                LoadRequired<HostValueData>(HealthPath), "chainrush.host-value.carrier-lives", definitions);
            var installer = LoadRequired<GameplayHostValuesInstallerData>(HostValuesInstallerPath);
            AddUnique(GetField<List<HostValueData>>(installer, "values"), lives);
            var configs = GetField<List<HostValueDefinitionData>>(installer, "definitions");
            configs.RemoveAll(config => config.Value == lives);
            var definition = new HostValueDefinitionData();
            SetField(definition, "value", lives);
            SetField(definition, "initializationType", HostValueInitializationType.ProjectedSeed);
            configs.Add(definition);
            EditorUtility.SetDirty(installer);
            return lives;
        }

        static List<CapabilityHostData> WriteUnitForms(string unitName, int count, InteractionGeometryData geometry,
            HostValueData lives, double step, List<EconomyAssetData> definitions, List<FrameworkSkillData> skills)
        {
            var source = LoadRequired<UnitData>("Assets/Game/Resources/Units/" + unitName + "Data.asset");
            UnitSkill main = source.skills.Find(skill => skill.main && skill.defaultSkill);
            if (main?.data == null || source.mergeStates.Count < count || !(main.data.prefab is AttackSkill attackSource))
                throw new InvalidOperationException(unitName + " requires merge forms and an authored main attack.");
            bool hero = unitName == "Perfume";
            bool ranged = attackSource is DistantAttackSkill;
            var result = new List<CapabilityHostData>();
            var template = LoadRequired<CapabilityHostData>(WaterUnitPath);
            for (int form = 0; form < count; form++)
            {
                string name = hero ? unitName : unitName + "Unit" + (form == 0 ? "" : (form + 1).ToString());
                string id = "chainrush.unit." + unitName.ToLowerInvariant() + (form == 0 ? "" : "." + (form + 1));
                int level = main.syncWithMergeState ? form : 0;
                if (!main.data.LevelIsValid(level)) throw new InvalidOperationException(name + " has no matching skill level.");
                var unit = ChainRushBoardPlannerAuthoring.WriteContentAsset(SharedRoot + "/Units/" + unitName + "/" + name + ".asset",
                    template, id, definitions);
                SetField(unit, "icon", source.mergeStates[form].icon);
                SetField(unit, "capabilities", new List<CapabilityEntry>
                {
                    CreateCapability(CapabilityHostType.SkillOwner),
                    CreateCapability(CapabilityHostType.AIBrainOwner, LoadRequired<TaxonomyTermData>(CombatNodePath))
                });
                if (!hero) unit.Capabilities.Add(CreateCapability(CapabilityHostType.MovementOwner));
                long health = RoundContent(BaseAttribute(source, form, global::Attribute.Health));
                long damage = RoundContent(main.data.GetParameterValue(SkillParameterType.Amount, level, 0)
                    * BaseAttribute(source, form, global::Attribute.Power));
                double interval = main.data.GetParameterValue(SkillParameterType.Duration, level,
                    attackSource.Weapon.TimeBetweenUses) / BaseAttribute(source, form, global::Attribute.SkillSpeed);
                int radius = ResolveContentAttackRadius(main.data, level, attackSource);
                var attack = WriteContentSkill(SkillsRoot + "/" + name + "Attack.asset", id + ".attack", definitions, skills);
                var requirement = new SkillTargetDistanceRequirementData();
                SetField(requirement, "distance", radius);
                SetField(requirement, "compareOperation", CompareOperation.LessOrEqual);
                SetField(requirement, "targetGeometryType", WorldTargetGeometryType.Spatial);
                SetField(attack, "requirements", new List<SkillRequirementData> { requirement });
                SetField(attack, "reloadTime", ContentDuration(interval, step));
                SetField(attack, "actionCount", checked((int)RoundContent(main.data.GetParameterValue(SkillParameterType.Count, level, 1))));
                SetField(attack, "actionInterval", ContentDuration(main.data.GetParameterValue(
                    SkillParameterType.IntervalBetween, level, attackSource.Weapon.BurstTimeBetweenShots), step));
                if (ranged) WriteRangedAttack(attack, main.data, level, name, unitName, damage, lives, step, definitions, skills);
                else SetField(attack, "effects", new List<SkillEffectData> { ContentDamage(damage) });

                FrameworkSkillData approach = null;
                if (!hero)
                {
                    approach = ChainRushBoardPlannerAuthoring.WriteContentAsset(SkillsRoot + "/" + name + "Approach.asset",
                        LoadRequired<FrameworkSkillData>(ApproachSkillPath), id + ".approach", definitions);
                    SetField(approach.Effects[0], "value", RoundContent(BaseAttribute(source, form, global::Attribute.Speed) * 1000 * step));
                    AddUnique(skills, approach);
                }
                var brain = WriteContentBrain(name, attack, approach, radius, definitions);
                var seed = new List<SeedEntry>
                {
                    new SeedEntry(brain, 1, EconomyFormType.Stack, new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatNodePath) }),
                    new SeedEntry(attack, 1, EconomyFormType.Stack),
                    new SeedEntry(LoadRequired<HostValueData>(HealthPath), health, EconomyFormType.Stack),
                    new SeedEntry(geometry, 1, EconomyFormType.Stack)
                };
                if (!hero)
                {
                    seed.Add(new SeedEntry(approach, 1, EconomyFormType.Stack));
                    seed.Add(new SeedEntry(LoadRequired<MovementData>(MovementPath), 1, EconomyFormType.Stack));
                }
                SetField(unit, "walletEntries", new List<WalletEntry> { new WalletEntry(LoadRequired<EconomyWalletData>(UnitWalletPath), seed) });
                WriteUnitVisual(unit, source.mergeStates[form], name);
                result.Add(unit);
                Debug.Log($"{name}: health={health}, damage={damage}, range={radius}, reload={attack.ReloadTime}, sourceSkill={main.data.name}[{level}]");
            }
            return result;
        }

        static double BaseAttribute(UnitData unit, int form, global::Attribute attribute)
        {
            if (!unit.attributes.TryGetValue(attribute, out var value) || value == null)
                throw new InvalidOperationException(unit.name + " is missing " + attribute);
            return value.GetValue(0) * unit.mergeStates[form].GetAttributeMultiplier(attribute);
        }

        static long RoundContent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new InvalidOperationException("Content values must be finite and nonnegative.");
            return checked((long)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        static long ContentDuration(double seconds, double step)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new InvalidOperationException("Content durations must be finite and nonnegative.");
            return checked((long)Math.Ceiling(seconds / step));
        }

        static int ResolveContentAttackRadius(global::SkillData skill, int level, AttackSkill prefab)
        {
            float radius;
            if (prefab is MeleeAttackSkill)
            {
                var collider = prefab.GetComponentInChildren<CircleCollider2D>(true);
                if (collider == null) throw new InvalidOperationException(skill.name + " has no melee circle.");
                radius = skill.GetParameterValue(SkillParameterType.Radius, level, collider.radius);
            }
            else
            {
                var distant = new SerializedObject(prefab);
                var aimer = new SerializedObject(distant.FindProperty("aimer").objectReferenceValue);
                radius = skill.GetParameterValue(SkillParameterType.Distance, level, aimer.FindProperty("ScanRadius").floatValue);
            }
            return checked((int)RoundContent(radius * 1000d));
        }

        static FrameworkSkillData WriteContentSkill(string path, string id, List<EconomyAssetData> definitions,
            List<FrameworkSkillData> skills)
        {
            var skill = ChainRushBoardPlannerAuthoring.WriteContentAsset(path, LoadRequired<FrameworkSkillData>(AttackSkillPath), id, definitions);
            SetField(skill, "startDelay", 0L);
            SetField(skill, "endDelay", 0L);
            SetField(skill, "reloadTime", 0L);
            SetField(skill, "actionCount", 1);
            SetField(skill, "actionInterval", 0L);
            SetField(skill, "requirements", new List<SkillRequirementData>());
            SetField(skill, "effects", new List<SkillEffectData>());
            AddUnique(skills, skill);
            return skill;
        }

        static SkillHostValueEffectData ContentDamage(long damage)
        {
            var effect = new SkillHostValueEffectData();
            ConfigureEffect(effect, EffectRecipient.Target, -damage);
            SetField(effect, "hostValue", LoadRequired<HostValueData>(HealthPath));
            return effect;
        }

        static void WriteRangedAttack(FrameworkSkillData attack, global::SkillData source, int level, string name,
            string unitName, long damage, HostValueData lives, double step,
            List<EconomyAssetData> definitions, List<FrameworkSkillData> skills)
        {
            var hit = WriteContentSkill(SkillsRoot + "/" + name + "Hit.asset", attack.Id + ".hit", definitions, skills);
            var consumeLife = new SkillHostValueEffectData();
            ConfigureEffect(consumeLife, EffectRecipient.Carrier, -1);
            SetField(consumeLife, "hostValue", lives);
            SetField(hit, "effects", new List<SkillEffectData> { ContentDamage(damage), consumeLife });
            string projectileName = unitName == "Cola" ? "DaggerProjectile" : "TurretKettleProjectile";
            var original = LoadRequired<GameObject>("Assets/Game/Prefabs/Skills/" + projectileName + ".prefab");
            var projectile = original.GetComponent<MoreMountains.TopDownEngine.Projectile>();
            if (projectile == null) throw new InvalidOperationException(projectileName + " has no projectile authoring.");
            double speed = source.GetParameterValue(SkillParameterType.Speed, level, projectile.Speed);
            double lifetime = source.GetParameterValue(SkillParameterType.Lifetime, level, projectile.LifeTime);
            var carrier = WriteCarrierVisual(original, projectileName);
            var velocity = new SkillCarrierTargetLinearVelocityParameterValueData();
            long speedValue = RoundContent(speed * 1000 * step);
            SetField(velocity, "minSpeed", speedValue);
            SetField(velocity, "maxSpeed", speedValue);
            var spawn = new SkillSpawnCarrierEffectData();
            SetField(spawn, "carrier", carrier);
            SetField(spawn, "carriedSkill", hit);
            SetField(spawn, "parameters", new List<SkillCarrierParameterValueData>
            {
                CarrierScalar(SkillCarrierScalarParameterType.Lives, 1, lives),
                CarrierScalar(SkillCarrierScalarParameterType.Lifetime, ContentDuration(lifetime, step)), velocity
            });
            SetField(attack, "effects", new List<SkillEffectData> { spawn });
        }

        static SkillCarrierScalarParameterValueData CarrierScalar(SkillCarrierScalarParameterType type, long value, HostValueData hostValue = null)
        {
            var parameter = new SkillCarrierScalarParameterValueData();
            SetField(parameter, "parameterType", type);
            SetField(parameter, "minValue", value);
            SetField(parameter, "maxValue", value);
            SetField(parameter, "carrierHostValue", hostValue);
            return parameter;
        }

        static AIBrainData WriteContentBrain(string name, FrameworkSkillData attack, FrameworkSkillData approach,
            int radius, List<EconomyAssetData> definitions)
        {
            var brain = ChainRushBoardPlannerAuthoring.WriteContentAsset(AIRoot + "/" + name + "Brain.asset",
                LoadRequired<AIBrainData>(AlliedCombatBrainPath), "chainrush.ai." + name.ToLowerInvariant(), definitions);
            var key = LoadRequired<TaxonomyTermData>(CombatTargetPath);
            var first = LoadRequired<TaxonomyTermData>(CombatStateAPath);
            var second = LoadRequired<TaxonomyTermData>(CombatStateBPath);
            foreach (var state in brain.Nodes[0].States)
            {
                if (state.Tag != first && state.Tag != second) continue;
                var query = new SelectEntityTargetByQueryAIBrainActionData();
                SetField(query, "targetKey", key);
                SetField(query, "searchRadius", approach == null ? radius : 50000);
                SetField(query, "maxCandidates", 256);
                SetField(query, "targetGeometryType", WorldTargetGeometryType.Spatial);
                SetField(query, "compatibleSkill", attack);
                SetField(query, "requiredTargetTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatantRolePath) });
                SetField(query, "blockedTargetStates", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(DefeatStatePath) });
                var actions = new List<AIBrainActionData>();
                if (state.Tag == first || approach == null) actions.Add(query);
                actions.Add(CreateUseSkillAction(approach != null && state.Tag == first ? approach : attack,
                    key, approach != null && state.Tag == first ? SkillCompletionPolicyType.OnExecutionComplete : SkillCompletionPolicyType.OnActivation));
                SetField(state, "onTickActions", actions);
                if (approach != null && state.Tag == first)
                {
                    var stop = new InterruptSkillAIBrainExitActionData();
                    SetField(stop, "skill", approach);
                    SetField(state, "onExitActions", new List<AIBrainExitActionData> { stop });
                }
            }
            if (approach != null)
            {
                var condition = new TargetDistanceAIBrainConditionData();
                SetField(condition, "targetKey", key);
                SetField(condition, "targetGeometryType", WorldTargetGeometryType.Spatial);
                SetField(condition, "distance", radius);
                SetField(condition, "compareOperation", CompareOperation.LessOrEqual);
                brain.Transitions.Insert(1, CreateBrainTransition(first, LoadRequired<TaxonomyTermData>(CombatNodePath), second, condition));
            }
            return brain;
        }

        static void WriteUnitVisual(CapabilityHostData unit, MergeStateData form, string name)
        {
            if (form.spineData == null) throw new InvalidOperationException(name + " has no authored skeleton.");
            var root = new GameObject(name);
            try
            {
                ConfigureProjectionBinding(root, unit.Id);
                var visual = SkeletonAnimation.NewSkeletonAnimationGameObject(form.spineData);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localRotation = Quaternion.Euler(90, 0, 0);
                visual.transform.localScale = Vector3.one * 0.8f;
                string path = ProjectionRoot + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                ConfigureAddressable(path, AddressablesGroup);
                SetProjection(unit, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static ProjectionDefinitionData WriteCarrierVisual(GameObject source, string name)
        {
            var definition = ChainRushBoardPlannerAuthoring.WriteContentAsset<ProjectionDefinitionData>(ProjectionRoot + "/" + name + ".asset", null);
            var root = new GameObject(name);
            try
            {
                ConfigureProjectionBinding(root, "chainrush.carrier." + name.ToLowerInvariant());
                var sprites = source.GetComponentsInChildren<SpriteRenderer>(true);
                if (sprites.Length == 0) throw new InvalidOperationException(name + " has no sprite visual.");
                foreach (var sprite in sprites)
                {
                    if (sprite.sprite == null) continue;
                    var child = new GameObject(sprite.name);
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = Quaternion.Euler(90, 0, 0) * source.transform.InverseTransformPoint(sprite.transform.position);
                    child.transform.localRotation = Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(source.transform.rotation) * sprite.transform.rotation;
                    child.transform.localScale = sprite.transform.lossyScale;
                    var renderer = child.AddComponent<SpriteRenderer>();
                    EditorUtility.CopySerialized(sprite, renderer);
                }
                string path = ProjectionRoot + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                ConfigureAddressable(path, AddressablesGroup);
                SetField(definition, "projectionPrefabReference", new ProjectionPrefabReference(AssetDatabase.AssetPathToGUID(path)));
                SetField(definition.ProjectionPool, "maxCapacity", 128);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return definition;
        }

        static void WriteHeroSocket(TaxonomyTermData tag)
        {
            var root = PrefabUtility.LoadPrefabContents(SpacePrefabPath);
            try
            {
                var old = root.transform.Find("HeroSpawn");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                CreateMarkerSocket(root.transform, "HeroSpawn", new Vector3Int(-10000, 0, 0), new List<TaxonomyTermData> { tag });
                PrefabUtility.SaveAsPrefabAsset(root, SpacePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void WriteDeployment(List<CapabilityHostData> water, List<CapabilityHostData> cola, CapabilityHostData hero,
            TaxonomyTermData heroSpawn, List<EconomyAssetData> definitions)
        {
            var activity = LoadRequired<ActivityData>(ActivityPath);
            var team = activity.Teams[0];
            var forms = new List<CapabilityHostData>(water);
            forms.AddRange(cola);
            var catalog = LoadRequired<ProductionCatalogData>(PlayerCatalogPath);
            var entries = GetField<List<ProductionCatalogEntryData>>(catalog, "entries");
            var entryTemplate = entries.Find(entry => entry.Recipe == LoadRequired<ProductionRecipeData>(DeploymentRecipePath));
            var objectives = new List<ActivityTeamObjectiveData>();
            var newEntries = new List<ProductionCatalogEntryData>();
            var walletTags = new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(SharedWalletTagPath) };
            foreach (var unit in forms)
            {
                var recipe = ChainRushBoardPlannerAuthoring.WriteContentAsset(ProductionRoot + "/" + unit.name + "DeploymentRecipe.asset",
                    LoadRequired<ProductionRecipeData>(DeploymentRecipePath), "chainrush.production.deploy." + unit.name.ToLowerInvariant(), definitions);
                recipe.Inputs.Clear();
                recipe.Inputs.Add(new ProductionInputData(EconomyOperation.Consume, unit, EconomyFormType.Stack, walletTags, null, new LongFlatProgressionData(1)));
                recipe.Outputs.Clear();
                recipe.Outputs.Add(new ProductionOutputData(unit, EconomyFormType.Token, walletTags, new LongFlatProgressionData(1)));
                var entry = new ProductionCatalogEntryData();
                SetStructField(ref entry, "recipe", recipe);
                SetStructField(ref entry, "workDuration", entryTemplate.WorkDuration);
                SetStructField(ref entry, "recoveryDuration", entryTemplate.RecoveryDuration);
                SetStructField(ref entry, "reservationPolicy", entryTemplate.ReservationPolicy);
                newEntries.Add(entry);
                string path = unit == water[0] ? PlayerDeploymentObjectivePath : ObjectivesRoot + "/" + unit.name + "DeploymentObjective.asset";
                var objective = ChainRushBoardPlannerAuthoring.WriteContentAsset(path, LoadRequired<ObjectiveTemplateData>(PlayerDeploymentObjectivePath));
                SetField(objective, "root", new ObjectiveNode("deploy-" + unit.name.ToLowerInvariant(), null,
                    new List<ObjectiveCondition> { new ObjectiveConditionEconomyMetric(walletTags, EconomyFormType.Stack, unit, 1, CompareOperation.GreaterOrEqual, null, null) },
                    new List<ObjectiveCondition> { new ObjectiveConditionEconomyMetric(walletTags, EconomyFormType.Stack, unit, 0, CompareOperation.Equal, null, null) }));
                SetField(objective, "completionPolicyType", ObjectiveCompletionPolicyType.Reset);
                objectives.Add(CreateTeamObjective(objective));
            }
            newEntries.AddRange(entries.FindAll(entry => entry.Recipe == LoadRequired<ProductionRecipeData>(ExperienceRecipePath)));
            SetField(catalog, "entries", newEntries);
            EditorUtility.SetDirty(catalog);
            objectives.Add(CreateTeamObjective(LoadRequired<ObjectiveTemplateData>(TurnTokenObjectivePath)));
            SetStructField(ref team, "objectives", objectives);
            for (int index = 0; index < team.Wallets.Count; index++)
            {
                var wallet = team.Wallets[index];
                var seeds = new List<ActivityWalletSeedEntryData>(wallet.Seed);
                seeds.RemoveAll(entry => entry.Seed.Asset == hero || forms.Contains(entry.Seed.Asset as CapabilityHostData));
                if (wallet.Wallet == LoadRequired<EconomyWalletData>(SharedWalletPath))
                    seeds.Add(new ActivityWalletSeedEntryData(new SeedEntry(hero, 1, EconomyFormType.Token), ActivitySeedMaterializationType.Spatial,
                        new List<TaxonomyTermData>(), new List<TaxonomyTermData> { heroSpawn }));
                SetStructField(ref wallet, "seed", seeds);
                team.Wallets[index] = wallet;
            }
            activity.Teams[0] = team;
            EditorUtility.SetDirty(activity);
        }

        static void AddCombatGeometryToEnemy(InteractionGeometryData geometry)
        {
            var enemy = LoadRequired<CapabilityHostData>(EnemyPath);
            var wallet = enemy.WalletEntries.Find(entry => entry.Wallet == LoadRequired<EconomyWalletData>(UnitWalletPath));
            if (!wallet.Seed.Exists(entry => entry.Asset == geometry)) wallet.Seed.Add(new SeedEntry(geometry, 1, EconomyFormType.Stack));
            EditorUtility.SetDirty(enemy);
        }
    }
}
