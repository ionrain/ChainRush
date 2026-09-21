using System;
using System.Collections.Generic;
using System.Linq;
using ChainRush.Gameplay;
using Core;
using Core.Activities;
using Core.Activities.Analytics;
using Core.CapabilityHosts;
using Core.Economy;
using Core.Economy.Authoring;
using Core.GameRuntime.Installers;
using Core.Knowledge;
using Core.Objectives;
using Core.Orchestration;
using Core.Players;
using Core.Production;
using Core.Production.Authoring;
using Core.Taxonomy;
using Core.World;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        const long LevelProgressResolution = 1000000;

        internal static void ApplyLevelPopulation()
        {
            var economy = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(economy, "assets");
            var levels = new List<LevelData>
            {
                LoadRequired<LevelData>("Assets/Game/Resources/Levels/Location001/Loc001Lvl01Data.asset"),
                LoadRequired<LevelData>("Assets/Game/Resources/Levels/Location001/Loc001Lvl02Data.asset")
            };
            var enemies = WritePopulationEnemyDefinitions(levels, definitions);
            WriteEnemyPopulationRecipes(enemies, definitions);
            var matrix = ChainRushBoardPlannerAuthoring.WriteContentAsset<DiplomacyKnowledgeMatrixData>(
                AutobattleRoot + "/Knowledge/EnemyKnowledgeMatrix.asset", null);
            var knowledge = ChainRushBoardPlannerAuthoring.WriteContentAsset<ActivityKnowledgeConfigData>(
                AutobattleRoot + "/Knowledge/EnemyKnowledge.asset", null);
            SetField(knowledge, "diplomacyMatrix", matrix);
            var analytics = ChainRushBoardPlannerAuthoring.WriteContentAsset<ActivityAnalyticsConfigData>(
                AutobattleRoot + "/Knowledge/EnemyAnalytics.asset", null);
            SetField(analytics, "reviewInterval", 1);
            SetField(analytics, "maxPerspectivesPerReview", 1);
            SetField(analytics, "workBudget", 256);
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                var activity = LoadRequired<ActivityData>(i == 0 ? ActivityPath : DistanceActivityPath);
                WriteLevelPopulation(level, activity, enemies, knowledge, analytics);
            }
            WriteEnemyPopulationRegion();
            EditorUtility.SetDirty(economy);
            AssetDatabase.SaveAssets();
        }

        static Dictionary<GameObject, CapabilityHostData> WritePopulationEnemyDefinitions(List<LevelData> levels,
            List<EconomyAssetData> definitions)
        {
            var result = new Dictionary<GameObject, CapabilityHostData>();
            var template = LoadRequired<CapabilityHostData>(EnemyPath);
            foreach (var source in levels.SelectMany(level => level.enemyData.enemyProportions.Values)
                .SelectMany(shares => shares.Keys).Distinct().OrderBy(prefab => prefab.name, StringComparer.Ordinal))
            {
                string path = AutobattleRoot + "/Economy/" + source.name + ".asset";
                string id = source.name == "BugBrownSmall" ? template.Id : "chainrush.autobattle.enemy." + source.name.ToLowerInvariant();
                var enemy = ChainRushBoardPlannerAuthoring.WriteContentAsset(path, template, id, definitions);
                var projection = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
                try
                {
                    projection.name = source.name;
                    var visual = projection.GetComponentInChildren<SkeletonAnimation>(true);
                    var sourceVisual = source.GetComponentInChildren<SkeletonAnimation>(true);
                    if (visual == null || sourceVisual == null)
                        throw new InvalidOperationException(source.name + " requires its source skeleton visual.");
                    visual.skeletonDataAsset = sourceVisual.skeletonDataAsset;
                    string projectionPath = ProjectionRoot + "/" + source.name + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(projection, projectionPath);
                    SetProjectionPoolKey(projectionPath, id);
                    ConfigureAddressable(projectionPath, AddressablesGroup);
                    SetProjection(enemy, projectionPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(projection); }
                result.Add(source, enemy);
            }
            return result;
        }

        static void WriteEnemyPopulationRecipes(Dictionary<GameObject, CapabilityHostData> enemies, List<EconomyAssetData> definitions)
        {
            var catalog = LoadRequired<ProductionCatalogData>(EnemyCatalogPath);
            var duration = catalog.Entries[0];
            var entries = new List<ProductionCatalogEntryData>();
            var template = LoadRequired<ProductionRecipeData>(EnemyWaveRecipePath);
            foreach (var pair in enemies)
            {
                var recipe = ChainRushBoardPlannerAuthoring.WriteContentAsset(
                    pair.Key.name == "BugBrownSmall" ? EnemyWaveRecipePath : ProductionRoot + "/" + pair.Key.name + "Recipe.asset",
                    template, "chainrush.production.autobattle." + pair.Key.name.ToLowerInvariant(), definitions);
                recipe.Inputs.Clear();
                recipe.Outputs.Clear();
                recipe.Outputs.Add(new ProductionOutputData(pair.Value, EconomyFormType.Token,
                    new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(SharedWalletTagPath) }, new LongFlatProgressionData(1)));
                SetField(recipe, "limitMode", ProductionLimitMode.None);
                SetField(recipe, "capValue", 0L);
                var entry = new ProductionCatalogEntryData();
                SetStructField(ref entry, "recipe", recipe);
                SetStructField(ref entry, "workDuration", duration.WorkDuration);
                SetStructField(ref entry, "recoveryDuration", duration.RecoveryDuration);
                SetStructField(ref entry, "reservationPolicy", duration.ReservationPolicy);
                entries.Add(entry);
                EditorUtility.SetDirty(recipe);
            }
            SetField(catalog, "entries", entries);
            EditorUtility.SetDirty(catalog);
        }

        static void WriteLevelPopulation(LevelData source, ActivityData activity,
            Dictionary<GameObject, CapabilityHostData> enemies, ActivityKnowledgeConfigData knowledge,
            ActivityAnalyticsConfigData analytics)
        {
            var progress = LoadRequired<EconomyAssetData>(SharedRoot + "/Economy/LevelProgress.asset");
            var player = LoadRequired<PlayerData>("Assets/Game/Runtime/Players/ChainRushLocalPlayer.asset");
            var wallet = LoadRequired<TaxonomyTermData>(SharedWalletTagPath);
            var enemyTag = LoadRequired<TaxonomyTermData>(EnemyUnitRolePath);
            var enemySource = source.enemyData;
            var definition = ChainRushBoardPlannerAuthoring.WriteContentAsset(
                AgentsRoot + "/" + source.Id + "PopulationAgent.asset", LoadRequired<AgentDefinitionData>(EnemyWaveAgentPath));
            SetField(definition, "agentId", "chainrush.autobattle.population." + source.Id.ToLowerInvariant());
            SetField(definition, "matchConditions", new List<ObjectiveCondition>
            { new ObjectiveConditionMaterializedEntity(default, null, EconomyFormType.Token, new[] { enemyTag }, null, 1,
                CompareOperation.GreaterOrEqual, default) });
            var spawner = LoadRequired<CapabilityHostData>(EnemySpawnerPath);
            SetField(definition, "executorSelectionCriteria", new List<EntityCriterionEntryData>
            { Required(CreateCapabilityHostCriterion(spawner)), Required(CreateOwnerCriterion()) });
            SetField(definition, "targetSelectionCriteria", new List<EntityCriterionEntryData>
            { Required(CreateCapabilityHostCriterion(spawner)), Required(CreateOwnerCriterion()) });
            var beforeEnd = new AgentEconomyApplicabilityConditionData();
            SetField(beforeEnd.Owner, "bindingType", EconomyOperationOwnerBindingType.AuthoredOwner);
            SetField(beforeEnd.Owner, "owner", new EconomyOwnerRef(player));
            SetField(beforeEnd, "selection", new EconomyEntrySelectionData(progress, EconomyFormType.Stack,
                new List<TaxonomyTermData> { wallet }, null, null, null, null));
            SetField(beforeEnd, "compareOperation", CompareOperation.Less);
            SetField(beforeEnd, "amount", LevelProgressResolution);
            var interval = new AgentIntervalApplicabilityConditionData();
            SetField(interval, "interval", 1);
            SetField(definition, "applicabilityConditions", new List<AgentApplicabilityConditionData> { beforeEnd, interval });
            var population = new PopulationAgentData();
            SetField(population, "completionPolicy", PopulationCompletionPolicyType.AllowPartialVolume);
            SetField(population, "volume", new LongFlatProgressionData(enemySource.maxSimulteneousCount));
            SetField(population, "workBudget", 256);
            SetField(population, "distribution", new SpatialPopulationDistributionAlgorithmData());
            SetField(population, "shapeWalletTags", new List<TaxonomyTermData> { wallet });
            SetField(population.Progress, "resource", progress);
            var binding = new EconomyOperationOwnerBindingData();
            SetField(binding, "bindingType", EconomyOperationOwnerBindingType.AuthoredOwner);
            SetField(binding, "owner", new EconomyOwnerRef(player));
            SetField(population.Progress, "owner", binding);
            SetField(population.Progress, "walletTags", new List<TaxonomyTermData> { wallet });
            SetField(population.Progress, "scaleType", PopulationProgressScaleType.Relative);
            SetField(population.Progress, "relativeBase", LevelProgressResolution);
            var shape = LoadRequired<SpatialShapeData>(SpawnAreaShapePath);
            var compositions = enemySource.enemyProportions.OrderBy(pair => pair.Key).ToList();
            var releases = new List<PopulationReleaseData>();
            for (int i = 0; i < compositions.Count; i++)
                releases.Add(new PopulationReleaseData(new ProgressInterval(compositions[i].Key,
                    i + 1 < compositions.Count ? compositions[i + 1].Key : 1, true),
                    new SpaceRegionQueryData(SpaceRegionScopeType.ActivityRoot,
                        new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(EnemySpawnPath) }, null, null, null),
                    new List<PopulationShapeRuleData>
                    { new PopulationShapeRuleData(shape, new List<SpatialShapeUsageData>
                        { new SpatialShapeUsageData(SpatialShapeFillType.Inside, Vector3Int.zero, Vector3Int.one,
                            Vector3Int.zero, new Vector3Int(1000, 1, 1000), Vector3Int.zero) }, 1,
                        new IntRange(0, enemySource.maxSimulteneousCount)) },
                    compositions[i].Value.Select(pair => new PopulationContentRuleData(
                        new PopulationAssetContentSourceData(enemies[pair.Key]), pair.Value)).ToList()));
            SetField(population, "releases", releases);
            SetField(definition, "agent", population);
            var objective = ChainRushBoardPlannerAuthoring.WriteContentAsset<ObjectiveTemplateData>(
                ObjectivesRoot + "/" + source.Id + "ReplenishmentObjective.asset", null);
            ObjectiveConditionMaterializedEntity Count(CompareOperation operation) => new ObjectiveConditionMaterializedEntity(
                default, null, EconomyFormType.Token, new[] { enemyTag }, null, 0, operation, default,
                WriteCountThreshold(enemySource, progress, player, wallet));
            SetField(objective, "root", new ObjectiveNode("replenish-" + source.Id, null,
                new List<ObjectiveCondition> { Count(CompareOperation.Less) },
                new List<ObjectiveCondition> { Count(CompareOperation.GreaterOrEqual) }));
            SetField(objective, "completionPolicyType", ObjectiveCompletionPolicyType.ResetOnConditions);
            SetField(objective, "resetConditions", new List<ObjectiveCondition> { Count(CompareOperation.Less) });
            var brain = ChainRushBoardPlannerAuthoring.WriteContentAsset(
                OrchestrationRoot + "/" + source.Id + "EnemyBrain.asset", LoadRequired<OrchestratorAIBrainData>(EnemyBrainPath));
            foreach (var op in brain.Operators.OfType<AgentDecompOpData>()) SetField(op, "agentDefinition", definition);
            var orchestration = ChainRushBoardPlannerAuthoring.WriteContentAsset(
                OrchestrationRoot + "/" + source.Id + "EnemyOrchestration.asset", LoadRequired<ActivityOrchestrationConfigData>(EnemyOrchestrationPath));
            SetField(orchestration, "orchestratorBrain", brain);
            var regionFeature = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushReplenishmentRegionFeatureData>(
                "Assets/Game/Runtime/Run/" + source.Id + "ReplenishmentRegion.asset", null);
            SetField(regionFeature, "regionId", "chainrush.autobattle.replenishment");
            SetField(regionFeature, "center", new Vector3(22.4f, 0, 6.2f));
            SetField(regionFeature, "size", new Vector3(10, 0, 20));
            SetField(regionFeature, "tags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(EnemySpawnPath) });
            SetField(regionFeature, "distance", source.Goal.GoalType == LevelGoalType.Distance ? (float)source.Goal.GoalAmount : 0f);
            SetField(regionFeature, "progress", progress);
            var regionProgressOwner = new EconomyOperationOwnerBindingData();
            SetField(regionProgressOwner, "bindingType", EconomyOperationOwnerBindingType.AuthoredOwner);
            SetField(regionProgressOwner, "owner", new EconomyOwnerRef(player));
            SetField(regionFeature, "progressOwner", regionProgressOwner);
            SetField(regionFeature, "walletTags", new List<TaxonomyTermData> { wallet });
            SetField(regionFeature, "progressResolution", LevelProgressResolution);
            var fieldFeature = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushEnemyFieldFeatureData>(
                "Assets/Game/Runtime/Run/" + source.Id + "EnemyField.asset", null);
            SetField(fieldFeature, "progress", progress);
            var fieldProgressOwner = new EconomyOperationOwnerBindingData();
            SetField(fieldProgressOwner, "bindingType", EconomyOperationOwnerBindingType.AuthoredOwner);
            SetField(fieldProgressOwner, "owner", new EconomyOwnerRef(player));
            SetField(fieldFeature, "progressOwner", fieldProgressOwner);
            SetField(fieldFeature, "walletTags", new List<TaxonomyTermData> { wallet });
            SetField(fieldFeature, "progressResolution", LevelProgressResolution);
            SetField(fieldFeature, "enemyTags", new List<TaxonomyTermData> { enemyTag });
            var team = activity.Teams[1];
            SetStructField(ref team, "objectives", new List<ActivityTeamObjectiveData> { CreateTeamObjective(objective) });
            SetStructField(ref team, "features", new List<ActivityFeatureData> { regionFeature, knowledge, analytics, orchestration, fieldFeature });
            for (int i = 0; i < team.Wallets.Count; i++)
            {
                var teamWallet = team.Wallets[i];
                var seeds = new List<ActivityWalletSeedEntryData>(teamWallet.Seed);
                seeds.RemoveAll(seed => seed.Seed.Asset == shape);
                seeds.Add(new ActivityWalletSeedEntryData(new SeedEntry(shape, 1, EconomyFormType.Stack), ActivitySeedMaterializationType.None, null));
                SetStructField(ref teamWallet, "seed", seeds);
                team.Wallets[i] = teamWallet;
            }
            activity.Teams[1] = team;
            EditorUtility.SetDirty(activity);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(objective);
            EditorUtility.SetDirty(brain);
            EditorUtility.SetDirty(orchestration);
            EditorUtility.SetDirty(regionFeature);
            EditorUtility.SetDirty(fieldFeature);
        }

        static ObjectiveLongTargetProgressionData WriteCountThreshold(EnemyGenerationData source,
            EconomyAssetData progress, PlayerData player, TaxonomyTermData wallet)
        {
            var threshold = new ObjectiveLongTargetProgressionData();
            SetField(threshold, "resource", progress);
            var owner = new EconomyOperationOwnerBindingData();
            SetField(owner, "bindingType", EconomyOperationOwnerBindingType.AuthoredOwner);
            SetField(owner, "owner", new EconomyOwnerRef(player));
            SetField(threshold, "owner", owner);
            SetField(threshold, "walletTags", new List<TaxonomyTermData> { wallet });
            SetField(threshold, "scaleType", ObjectiveProgressScaleType.Absolute);
            var intervals = new List<LongProgressionIntervalData>();
            long start = 0;
            long Count(long argument) => Math.Min(source.maxSimulteneousCount,
                (long)(source.enemyCountCurve.Evaluate((float)argument / LevelProgressResolution) * source.maxFillCount));
            long previous = Count(0);
            for (long value = 1; value <= LevelProgressResolution; value++)
            {
                long count = Count(value);
                if (count < 0) throw new InvalidOperationException("Enemy count curve must be nonnegative.");
                if (count == previous) continue;
                intervals.Add(new LongProgressionIntervalData(new ProgressInterval(start, value, true), new LongFlatProgressionData(previous)));
                start = value;
                previous = count;
            }
            intervals.Add(new LongProgressionIntervalData(new ProgressInterval(start, 0, false), new LongFlatProgressionData(previous)));
            SetField(threshold, "progression", new LongDependentProgressionData(intervals));
            return threshold;
        }

        static void WriteEnemyPopulationRegion()
        {
            var root = PrefabUtility.LoadPrefabContents(SpacePrefabPath);
            try
            {
                var region = root.GetComponent<SpaceRegionController>();
                if (region != null) UnityEngine.Object.DestroyImmediate(region);
                var floor = root.transform.Find("Floor");
                if (floor == null) throw new InvalidOperationException("Autobattle requires its authored navigation floor.");
                floor.localPosition = new Vector3(80, floor.localPosition.y, 5);
                floor.localScale = new Vector3(200, floor.localScale.y, 40);
                PrefabUtility.SaveAsPrefabAsset(root, SpacePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
