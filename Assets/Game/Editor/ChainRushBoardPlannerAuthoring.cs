using System;
using System.Collections.Generic;
using System.Reflection;
using ChainRush.Board;
using Core;
using Core.Activities;
using Core.Activities.Selection;
using Core.CapabilityHosts;
using Core.Economy;
using Core.Economy.Authoring;
using Core.GameRuntime;
using Core.GameRuntime.Installers;
using Core.Objectives;
using Core.Orchestration;
using Core.Production;
using Core.Production.Authoring;
using Core.Taxonomy;
using Core.World;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using EntityId = Core.Entities.EntityId;
using FrameworkResourceData = Core.Economy.Modules.ResourceEconomyModule.ResourceData;

namespace ChainRush.Editor
{
    public static class ChainRushBoardPlannerAuthoring
    {
        const string AutobattleRoot = "Assets/Game/Activities/Autobattle";
        const string BoardRoot = "Assets/Game/Activities/Board";
        const string SharedRoot = "Assets/Game/Activities/Shared";
        const string PopulationRoot = BoardRoot + "/Population";
        const string PlannerRoot = PopulationRoot + "/Planner";
        const string SpaceRoot = BoardRoot + "/Space";
        const string ShapesRoot = SpaceRoot + "/Shapes";
        const string ShapeRulesRoot = ShapesRoot + "/Rules";
        const string AgentsRoot = BoardRoot + "/Agents";
        const string ObjectivesRoot = BoardRoot + "/Objectives";
        const string OrchestrationRoot = BoardRoot + "/Orchestration";
        const string OrchestrationModulesRoot = OrchestrationRoot + "/Modules";
        const string OrchestrationTaxonomyRoot = OrchestrationRoot + "/Taxonomy";
        const string SharedUnitsRoot = SharedRoot + "/Units";
        const string SharedWaterRoot = SharedUnitsRoot + "/Water";

        const string PlannerPath = PlannerRoot + "/BoardPlanner.asset";
        const string BoardActivityPath = BoardRoot + "/Definition/BoardActivity.asset";
        const string BoardHostPath = BoardRoot + "/Economy/BoardHost.asset";
        const string BoardWalletPath = BoardRoot + "/Economy/BoardWallet.asset";
        const string BoardWalletTagPath = BoardRoot + "/Economy/BoardWalletTag.asset";
        const string WaterPath = BoardRoot + "/Economy/WaterBoardBase.asset";
        const string WaterTagPath = BoardRoot + "/Taxonomy/WaterBoardItem.asset";
        const string BoardCellTagPath = BoardRoot + "/Taxonomy/BoardCellTag.asset";
        const string BoardItemFamilyPath = BoardRoot + "/Taxonomy/BoardItemFamily.asset";
        const string MergeSelectionTypePath = BoardRoot + "/Taxonomy/BoardMergeSelection.asset";
        const string MergeSelectedTagPath = BoardRoot + "/Taxonomy/BoardMergeSelected.asset";
        const string MergeRecipe4Path = BoardRoot + "/Production/BoardMergeRecipe4.asset";
        const string MergeRecipe3Path = BoardRoot + "/Production/BoardMergeRecipe3.asset";
        const string MergeRecipe2Path = BoardRoot + "/Production/BoardMergeRecipe2.asset";
        const string MergeRecipe1Path = BoardRoot + "/Production/BoardMergeRecipe1.asset";
        const string MergeProductionPath = BoardRoot + "/Production/BoardProduction.asset";
        const string MergeCatalogPath = BoardRoot + "/Production/BoardProductionCatalog.asset";
        const string BoardUIPrefabPath = BoardRoot + "/UI/BoardUI.prefab";
        const string WaterProjectionPrefabPath =
            BoardRoot + "/Projection/WaterBoardBase.prefab";
        const string SharedWalletPath = SharedRoot + "/Economy/ActivityWallet.asset";
        const string SharedWalletTagPath = SharedRoot + "/Economy/ActivityWalletTag.asset";
        const string ExperiencePath = SharedRoot + "/Economy/Experience.asset";
        const string ExperienceToTurnTokenRecipePath =
            AutobattleRoot + "/Production/ExperienceToTurnTokenRecipe.asset";

        const string BoardPlaneShapePath = ShapesRoot + "/BoardPlane.asset";
        const string SingleShapePath = ShapesRoot + "/Single.asset";
        const string LineShapePath = ShapesRoot + "/Line.asset";
        const string CornerShapePath = ShapesRoot + "/Corner.asset";
        const string BoxShapePath = ShapesRoot + "/Box.asset";
        const string ZigzagShapePath = ShapesRoot + "/Zigzag.asset";
        const string SingleRulePath = ShapeRulesRoot + "/SingleRule.asset";
        const string LineRulePath = ShapeRulesRoot + "/LineRule.asset";
        const string CornerRulePath = ShapeRulesRoot + "/CornerRule.asset";
        const string ZigzagRulePath = ShapeRulesRoot + "/ZigzagRule.asset";

        const string TurnTokenPath = SharedRoot + "/Economy/BoardTurnToken.asset";
        const string WaterUnitPath = SharedWaterRoot + "/WaterUnit.asset";
        const string PopulationProducerPath = BoardRoot + "/Economy/BoardPopulationProducer.asset";
        const string RefreshRecipePath = BoardRoot + "/Production/BoardRefreshRecipe.asset";
        const string WaterRecipePath = BoardRoot + "/Production/WaterBoardBaseRecipe.asset";
        const string PopulationProductionPath = BoardRoot + "/Production/BoardPopulationProduction.asset";
        const string PopulationCatalogPath = BoardRoot + "/Production/BoardPopulationCatalog.asset";
        const string PopulationAgentPath = AgentsRoot + "/BoardPopulationAgent.asset";
        const string SelectionAgentPath = AgentsRoot + "/BoardSelectionAgent.asset";
        const string PopulationObjectivePath = ObjectivesRoot + "/BoardPopulationObjective.asset";
        const string SelectionObjectivePath = ObjectivesRoot + "/BoardSelectionObjective.asset";
        const string MergeObjectivePath = ObjectivesRoot + "/BoardMergeObjective.asset";
        const string OperatorFamilyPath = OrchestrationTaxonomyRoot + "/BoardOperatorFamily.asset";
        const string PopulationAgentOperatorPath = OrchestrationTaxonomyRoot + "/BoardPopulationAgentOperator.asset";
        const string ProductionYieldOperatorPath = OrchestrationTaxonomyRoot + "/BoardProductionYieldOperator.asset";
        const string ProductionAvailableOperatorPath = OrchestrationTaxonomyRoot + "/BoardProductionAvailableOperator.asset";
        const string MaterializedProductionOperatorPath = OrchestrationTaxonomyRoot + "/BoardMaterializedProductionOperator.asset";
        const string SelectionAgentOperatorPath = OrchestrationTaxonomyRoot + "/BoardSelectionAgentOperator.asset";
        const string ProductionInputOperatorPath = OrchestrationTaxonomyRoot + "/BoardProductionInputOperator.asset";
        const string EconomyStateModulePath = OrchestrationModulesRoot + "/BoardEconomyState.asset";
        const string ProductionStateModulePath = OrchestrationModulesRoot + "/BoardProductionState.asset";
        const string ProjectionStateModulePath = OrchestrationModulesRoot + "/BoardProjectionState.asset";
        const string BrainPath = OrchestrationRoot + "/BoardBrain.asset";
        const string OrchestrationPath = OrchestrationRoot + "/BoardOrchestration.asset";

        const string RuntimeProfilePath = "Assets/Game/Runtime/Host/ChainRushGameRuntimeProfile.asset";
        const string EconomyDefinitionsInstallerPath = "Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset";
        const string EconomyRuntimeInstallerPath = "Assets/Game/Runtime/Installers/ChainRushEconomyRuntimeInstaller.asset";
        const string TaxonomyInstallerPath = "Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset";
        const string FoundationInstallerPath = "Assets/Game/Runtime/Installers/ChainRushGameplayFoundationInstaller.asset";
        const string ProductionInstallerPath = "Assets/Game/Runtime/Installers/ChainRushProductionRuntimeInstaller.asset";
        const string ProjectionInstallerPath = "Assets/Game/Runtime/Installers/ChainRushProjectionRuntimeInstaller.asset";

        const string UpgradedAssetPath = BoardRoot + "/Economy/WaterBoardUpgraded.asset";
        const string UpgradedPrefabPath = BoardRoot + "/Projection/WaterBoardUpgraded.prefab";

        static readonly string[] VerticalSliceCreatedPaths =
        {
            TurnTokenPath,
            WaterUnitPath,
            PopulationProducerPath,
            RefreshRecipePath,
            WaterRecipePath,
            PopulationProductionPath,
            PopulationCatalogPath,
            PopulationAgentPath,
            PopulationObjectivePath,
            OperatorFamilyPath,
            PopulationAgentOperatorPath,
            ProductionYieldOperatorPath,
            ProductionAvailableOperatorPath,
            MaterializedProductionOperatorPath,
            EconomyStateModulePath,
            ProductionStateModulePath,
            ProjectionStateModulePath,
            BrainPath,
            OrchestrationPath,
        };

        static readonly string[] SpatialShapeCreatedPaths =
        {
            BoardPlaneShapePath,
            SingleShapePath,
            LineShapePath,
            CornerShapePath,
            BoxShapePath,
            ZigzagShapePath,
            SingleRulePath,
            LineRulePath,
            CornerRulePath,
            ZigzagRulePath,
        };

        sealed class BoardSpatialShapes
        {
            public SpatialShapeData BoardPlane;
            public SpatialShapeData Single;
            public SpatialShapeData Line;
            public SpatialShapeData Corner;
            public SpatialShapeData Box;
            public SpatialShapeData Zigzag;

            public List<SpatialShapeData> All => new List<SpatialShapeData>
            {
                BoardPlane,
                Single,
                Line,
                Corner,
                Box,
                Zigzag,
            };
        }

        [MenuItem("ChainRush/Activities/Autobattle/Create Experience To Turn Token Recipe")]
        public static void CreateExperienceToTurnTokenRecipe()
        {
            EnsureAssetDoesNotExist(ExperiencePath);
            EnsureAssetDoesNotExist(ExperienceToTurnTokenRecipePath);

            FrameworkResourceData turnToken =
                LoadRequired<FrameworkResourceData>(TurnTokenPath);
            TaxonomyTermData sharedWalletTag =
                LoadRequired<TaxonomyTermData>(SharedWalletTagPath);
            EconomyDefinitionsInstallerData economyInstaller =
                LoadRequired<EconomyDefinitionsInstallerData>(EconomyDefinitionsInstallerPath);
            var originalAssets = new List<EconomyAssetData>(
                GetField<List<EconomyAssetData>>(economyInstaller, "assets"));
            var createdPaths = new List<string>(2);

            EnsureFolder(AutobattleRoot + "/Production");
            try
            {
                FrameworkResourceData experience = CreateEconomyAsset<FrameworkResourceData>(
                    ExperiencePath,
                    "Experience",
                    "chainrush.resource.experience",
                    EconomyOperation.Require | EconomyOperation.Issue | EconomyOperation.Consume,
                    createdPaths);
                ProductionRecipeData recipe = CreateEconomyAsset<ProductionRecipeData>(
                    ExperienceToTurnTokenRecipePath,
                    "ExperienceToTurnTokenRecipe",
                    "chainrush.production.autobattle.experience-to-turn-token.recipe",
                    EconomyOperation.Require | EconomyOperation.Issue,
                    createdPaths);

                ConfigureExperienceToTurnTokenRecipe(
                    recipe,
                    experience,
                    turnToken,
                    sharedWalletTag);

                var assets = new List<EconomyAssetData>(originalAssets);
                AddUnique(assets, experience, recipe);
                SetField(economyInstaller, "assets", assets);
                EditorUtility.SetDirty(economyInstaller);

                AssetDatabase.SaveAssets();
                Selection.activeObject = recipe;
                EditorGUIUtility.PingObject(recipe);
                Debug.Log(
                    "[ChainRush] Created Experience and ExperienceToTurnTokenRecipe authoring assets.");
            }
            catch
            {
                SetField(economyInstaller, "assets", originalAssets);
                EditorUtility.SetDirty(economyInstaller);
                for (int i = createdPaths.Count - 1; i >= 0; i--)
                    AssetDatabase.DeleteAsset(createdPaths[i]);
                AssetDatabase.SaveAssets();
                throw;
            }
        }

        [MenuItem("ChainRush/Activities/Board/Create Spatial Shape Assets")]
        public static void CreateSpatialShapeAssets()
        {
            EnsureSpatialShapeTargetsDoNotExist();
            EnsureFolder(ShapeRulesRoot);

            var createdPaths = new List<string>(SpatialShapeCreatedPaths.Length);
            try
            {
                BoardSpatialShapes shapes = CreateBoardSpatialShapes(createdPaths);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = shapes.BoardPlane;
                EditorGUIUtility.PingObject(shapes.BoardPlane);
                Debug.Log("[ChainRush] Created Board spatial shape assets.");
            }
            catch
            {
                DeleteCreatedAssets(createdPaths);
                throw;
            }
        }

        [MenuItem("ChainRush/Activities/Board/Create Population Planner Assets")]
        public static void CreatePopulationPlannerAssets()
        {
            EnsureAssetDoesNotExist(PlannerPath);
            CapabilityHostData water = LoadRequired<CapabilityHostData>(WaterPath);
            BoardSpatialShapes shapes = LoadBoardSpatialShapes();

            EnsureFolder(PlannerRoot);
            try
            {
                ProgressivePlannerData planner = ScriptableObject.CreateInstance<ProgressivePlannerData>();
                planner.name = "BoardPlanner";
                ConfigurePlanner(planner, water, shapes.Line, shapes.Single);
                AssetDatabase.CreateAsset(planner, PlannerPath);

                AssetDatabase.SaveAssets();
                Selection.activeObject = planner;
                EditorGUIUtility.PingObject(planner);
                Debug.Log($"[ChainRush] Created Board population planner assets under '{PopulationRoot}'.");
            }
            catch
            {
                AssetDatabase.DeleteAsset(PlannerPath);
                AssetDatabase.SaveAssets();
                throw;
            }
        }

        [MenuItem("ChainRush/Activities/Board/Complete Vertical Slice Wiring")]
        public static void CompleteVerticalSliceWiring()
        {
            FrameworkResourceData turnToken = LoadRequired<FrameworkResourceData>(TurnTokenPath);
            CapabilityHostData waterUnit = LoadRequired<CapabilityHostData>(WaterUnitPath);
            CapabilityHostData populationProducer = LoadRequired<CapabilityHostData>(PopulationProducerPath);
            CapabilityHostData boardHost = LoadRequired<CapabilityHostData>(BoardHostPath);
            AgentDefinitionData populationAgent =
                LoadRequired<AgentDefinitionData>(PopulationAgentPath);
            ProductionRecipeData refreshRecipe = LoadRequired<ProductionRecipeData>(RefreshRecipePath);
            ProductionRecipeData waterRecipe = LoadRequired<ProductionRecipeData>(WaterRecipePath);
            ProductionData populationProduction = LoadRequired<ProductionData>(PopulationProductionPath);
            EconomyWalletData boardWallet = LoadRequired<EconomyWalletData>(BoardWalletPath);
            TaxonomyTermData boardWalletTag = LoadRequired<TaxonomyTermData>(BoardWalletTagPath);
            TaxonomyTermData sharedWalletTag = LoadRequired<TaxonomyTermData>(SharedWalletTagPath);
            CapabilityHostData water = LoadRequired<CapabilityHostData>(WaterPath);
            TaxonomyFamilyData operatorFamily = LoadRequired<TaxonomyFamilyData>(OperatorFamilyPath);
            TaxonomyTermData populationAgentOperator =
                LoadRequired<TaxonomyTermData>(PopulationAgentOperatorPath);
            AgentDefinitionData selectionAgent =
                LoadRequired<AgentDefinitionData>(SelectionAgentPath);
            TaxonomyTermData selectionAgentOperator =
                LoadRequired<TaxonomyTermData>(SelectionAgentOperatorPath);
            TaxonomyTermData productionInputOperator =
                LoadRequired<TaxonomyTermData>(ProductionInputOperatorPath);
            TaxonomyTermData productionYieldOperator =
                LoadRequired<TaxonomyTermData>(ProductionYieldOperatorPath);
            TaxonomyTermData productionAvailableOperator =
                LoadRequired<TaxonomyTermData>(ProductionAvailableOperatorPath);
            ProductionStateOrchestrationModuleData productionState =
                LoadRequired<ProductionStateOrchestrationModuleData>(ProductionStateModulePath);
            ProjectionStateOrchestrationModuleData projectionState =
                LoadRequired<ProjectionStateOrchestrationModuleData>(ProjectionStateModulePath);
            ActivityOrchestrationConfigData orchestration =
                LoadRequired<ActivityOrchestrationConfigData>(OrchestrationPath);
            OrchestratorAIBrainData brain = LoadRequired<OrchestratorAIBrainData>(BrainPath);

            EnsureIncompleteVerticalSliceState(
                turnToken,
                waterUnit,
                populationProducer,
                refreshRecipe,
                waterRecipe,
                operatorFamily,
                populationAgentOperator,
                productionYieldOperator,
                productionAvailableOperator);

            TaxonomyTermData materializedProductionOperator =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(
                    MaterializedProductionOperatorPath);
            if (materializedProductionOperator == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(MaterializedProductionOperatorPath) != null)
                {
                    throw new InvalidOperationException(
                        $"Asset at '{MaterializedProductionOperatorPath}' is not a TaxonomyTermData.");
                }

                materializedProductionOperator = ScriptableObject.CreateInstance<TaxonomyTermData>();
                materializedProductionOperator.name = "BoardMaterializedProductionOperator";
                AssetDatabase.CreateAsset(
                    materializedProductionOperator,
                    MaterializedProductionOperatorPath);
            }

            EconomyStateOrchestrationModuleData economyState =
                AssetDatabase.LoadAssetAtPath<EconomyStateOrchestrationModuleData>(
                    EconomyStateModulePath);
            if (economyState == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(EconomyStateModulePath) != null)
                {
                    throw new InvalidOperationException(
                        $"Asset at '{EconomyStateModulePath}' is not an EconomyStateOrchestrationModuleData.");
                }

                economyState = ScriptableObject.CreateInstance<EconomyStateOrchestrationModuleData>();
                economyState.name = "BoardEconomyState";
                AssetDatabase.CreateAsset(economyState, EconomyStateModulePath);
            }
            ConfigureOrchestrationModules(
                orchestration,
                economyState,
                productionState,
                projectionState);

            ConfigureEconomyAsset(
                turnToken,
                "chainrush.resource.board-turn-token",
                EconomyOperation.Require | EconomyOperation.Issue | EconomyOperation.Consume);
            ConfigureEconomyAsset(
                waterUnit,
                "chainrush.unit.water",
                EconomyOperation.Require
                | EconomyOperation.Issue
                | EconomyOperation.Consume
                | EconomyOperation.Transfer
                | EconomyOperation.Destroy);
            SetField(waterUnit, "capabilities", new List<CapabilityEntry>(0));
            SetField(waterUnit, "walletEntries", new List<WalletEntry>(0));
            EditorUtility.SetDirty(waterUnit);

            ConfigureEconomyAsset(
                populationProducer,
                "chainrush.board.population-producer",
                EconomyOperation.Require
                | EconomyOperation.Issue
                | EconomyOperation.Consume
                | EconomyOperation.Transfer
                | EconomyOperation.Destroy);
            SetField(
                populationProducer,
                "capabilities",
                new List<CapabilityEntry>
                {
                    CreateCapabilityEntry(CapabilityHostType.ProductionOwner),
                });
            SetField(
                populationProducer,
                "walletEntries",
                new List<WalletEntry>
                {
                    new WalletEntry(
                        boardWallet,
                        new List<SeedEntry>
                        {
                            new SeedEntry(populationProduction, 1L, EconomyFormType.Stack),
                        }),
                });
            EditorUtility.SetDirty(populationProducer);

            ConfigureEconomyAsset(
                refreshRecipe,
                "chainrush.production.board.refresh.recipe",
                EconomyOperation.Require | EconomyOperation.Issue);
            ConfigureRefreshRecipe(refreshRecipe, turnToken, sharedWalletTag);
            ConfigureEconomyAsset(
                waterRecipe,
                "chainrush.production.board.water-base.recipe",
                EconomyOperation.Require | EconomyOperation.Issue);
            ConfigureWaterRecipe(waterRecipe, water, boardWalletTag);

            ConfigureTaxonomyFamily(
                operatorFamily,
                "chainrush.orchestration.board.operator",
                "ChainRush Board Operator");
            ConfigureTaxonomyTerm(
                populationAgentOperator,
                "chainrush.orchestration.board.agent.population",
                "Board Population Agent",
                operatorFamily,
                0);
            ConfigureTaxonomyTerm(
                productionYieldOperator,
                "chainrush.orchestration.board.production-yield",
                "Board Production Yield",
                operatorFamily,
                2);
            ConfigureTaxonomyTerm(
                productionAvailableOperator,
                "chainrush.orchestration.board.production-available",
                "Board Production Available",
                operatorFamily,
                3);
            ConfigureTaxonomyTerm(
                materializedProductionOperator,
                "chainrush.orchestration.board.production-materialized",
                "Board Materialized Production",
                operatorFamily,
                4);
            ConfigurePopulationAgentExecutor(populationAgent, boardHost);
            ConfigureSelectionBrain(
                brain,
                populationAgent,
                selectionAgent,
                populationAgentOperator,
                selectionAgentOperator,
                productionInputOperator,
                productionYieldOperator,
                productionAvailableOperator,
                materializedProductionOperator);

            TaxonomyRuntimeInstallerData taxonomyInstaller =
                LoadRequired<TaxonomyRuntimeInstallerData>(TaxonomyInstallerPath);
            var taxonomyTerms = new List<TaxonomyTermData>(
                GetField<TaxonomyTermData[]>(taxonomyInstaller, "terms")
                ?? new TaxonomyTermData[0]);
            AddUnique(taxonomyTerms, materializedProductionOperator);
            SetField(taxonomyInstaller, "terms", taxonomyTerms.ToArray());
            EditorUtility.SetDirty(taxonomyInstaller);

            EnsureCompletedVerticalSliceState(
                turnToken,
                waterUnit,
                populationProducer,
                refreshRecipe,
                waterRecipe,
                operatorFamily,
                populationAgentOperator,
                productionYieldOperator,
                productionAvailableOperator,
                materializedProductionOperator);
            EnsureOrchestrationModules(
                orchestration,
                economyState,
                productionState,
                projectionState);
            EnsurePopulationAgentExecutor(populationAgent, boardHost);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = populationProducer;
            EditorGUIUtility.PingObject(populationProducer);
            Debug.Log("[ChainRush] Board vertical slice incomplete assets were completed without changing GUIDs.");
        }

        [MenuItem("ChainRush/Activities/Board/Apply Materialization Endpoint Wiring")]
        public static void ApplyMaterializationEndpointWiring()
        {
            OrchestratorAIBrainData brain = LoadRequired<OrchestratorAIBrainData>(BrainPath);
            TaxonomyTermData operatorId =
                LoadRequired<TaxonomyTermData>(MaterializedProductionOperatorPath);
            ReplaceMaterializationOperator(brain, operatorId);

            OrchestrationDecisionGraphData graph = brain.DecisionGraph;
            if (graph == null)
                throw new InvalidOperationException("Board brain has no decision graph.");

            OrchestrationDecisionData materializationDecision = null;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i] is OrchestrationDecisionData decision
                    && string.Equals(
                        decision.DecisionId,
                        "board-materialized-production",
                        StringComparison.Ordinal))
                {
                    if (materializationDecision != null)
                        throw new InvalidOperationException("Board brain has duplicate materialization decisions.");
                    materializationDecision = decision;
                }
            }
            if (materializationDecision == null)
                throw new InvalidOperationException("Board brain has no materialization decision.");

            ScopeDecisionConditionData scope = null;
            for (int i = 0; i < materializationDecision.Conditions.Count; i++)
            {
                if (!(materializationDecision.Conditions[i] is ScopeDecisionConditionData candidate))
                    continue;
                if (scope != null)
                    throw new InvalidOperationException("Board materialization decision has duplicate scope criteria.");
                scope = candidate;
            }
            if (scope == null)
                throw new InvalidOperationException("Board materialization decision has no scope criterion.");

            SetField(scope, "scopeType", OrchestrationDecompositionScopeType.GlobalObjective);
            EditorUtility.SetDirty(brain);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new List<string> { BrainPath });
            AssetDatabase.Refresh();
            Debug.Log("[ChainRush] Board materialization endpoint wiring was applied.");
        }

        static void ReplaceMaterializationOperator(
            OrchestratorAIBrainData brain,
            TaxonomyTermData operatorId)
        {
            List<OrchestrationDecompOpData> operators = brain.Operators;
            int replacementIndex = -1;
            for (int i = 0; i < operators.Count; i++)
            {
                if (operators[i] is MaterializedEntityProductionDecompOpData existing)
                {
                    if (replacementIndex >= 0)
                        throw new InvalidOperationException("Board brain has duplicate materialization operators.");
                    SetField(existing, "operatorId", operatorId);
                    replacementIndex = i;
                    continue;
                }
                if (operators[i] != null)
                    continue;
                if (replacementIndex >= 0)
                    throw new InvalidOperationException("Board brain has multiple unresolved operators.");
                replacementIndex = i;
            }

            if (replacementIndex < 0)
                throw new InvalidOperationException("Board brain has no materialization operator slot.");
            if (operators[replacementIndex] == null)
            {
                var replacement = new MaterializedEntityProductionDecompOpData();
                SetField(replacement, "operatorId", operatorId);
                operators[replacementIndex] = replacement;
            }

            SetField(brain, "operators", operators);
        }

        static void ConfigurePlanner(
            ProgressivePlannerData planner,
            CapabilityHostData water,
            SpatialShapeData line,
            SpatialShapeData single)
        {
            var serialized = new SerializedObject(planner);
            SerializedProperty patterns = serialized.FindProperty("patternRules");
            patterns.arraySize = 2;

            ConfigurePattern(
                patterns.GetArrayElementAtIndex(0),
                line,
                3L,
                1L,
                1L);
            ConfigurePattern(
                patterns.GetArrayElementAtIndex(1),
                single,
                1L,
                1L,
                0L);

            SerializedProperty contents = serialized.FindProperty("contentRules");
            contents.arraySize = 1;
            SerializedProperty content = contents.GetArrayElementAtIndex(0);
            content.FindPropertyRelative("asset").objectReferenceValue = water;
            content.FindPropertyRelative("weight").managedReferenceValue =
                new LongLinearProgressionData(1L, 0L);
            content.FindPropertyRelative("minimumPatternCount").managedReferenceValue =
                new LongLinearProgressionData(0L, 0L);
            content.FindPropertyRelative("guaranteedCellShare").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigurePattern(
            SerializedProperty property,
            SpatialShapeData shape,
            long size,
            long weight,
            long minimumCount)
        {
            property.FindPropertyRelative("shape").objectReferenceValue = shape;
            property.FindPropertyRelative("size").managedReferenceValue =
                new LongLinearProgressionData(size, 0L);
            property.FindPropertyRelative("weight").managedReferenceValue =
                new LongLinearProgressionData(weight, 0L);
            property.FindPropertyRelative("minimumCount").managedReferenceValue =
                new LongLinearProgressionData(minimumCount, 0L);
        }

        static void ConfigureRefreshRecipe(
            ProductionRecipeData recipe,
            EconomyAssetData turnToken,
            TaxonomyTermData sharedWalletTag)
        {
            recipe.Inputs.Clear();
            recipe.Inputs.Add(new ProductionInputData(
                EconomyOperation.Consume,
                turnToken,
                EconomyFormType.Stack,
                new List<TaxonomyTermData> { sharedWalletTag },
                null,
                new LongFlatProgressionData(1L)));
            recipe.Outputs.Clear();
            EditorUtility.SetDirty(recipe);
        }

        static void ConfigureExperienceToTurnTokenRecipe(
            ProductionRecipeData recipe,
            EconomyAssetData experience,
            EconomyAssetData turnToken,
            TaxonomyTermData sharedWalletTag)
        {
            recipe.Inputs.Clear();
            recipe.Inputs.Add(new ProductionInputData(
                EconomyOperation.Consume,
                experience,
                EconomyFormType.Stack,
                new List<TaxonomyTermData> { sharedWalletTag },
                null,
                new LongStepProgressionData(6L, 2L, 1L)));
            recipe.Outputs.Clear();
            recipe.Outputs.Add(new ProductionOutputData(
                turnToken,
                EconomyFormType.Stack,
                new List<TaxonomyTermData> { sharedWalletTag },
                new LongFlatProgressionData(1L)));
            EditorUtility.SetDirty(recipe);
        }

        static void ConfigureWaterRecipe(
            ProductionRecipeData recipe,
            CapabilityHostData water,
            TaxonomyTermData boardWalletTag)
        {
            recipe.Inputs.Clear();
            recipe.Outputs.Clear();
            recipe.Outputs.Add(new ProductionOutputData(
                water,
                EconomyFormType.Token,
                new List<TaxonomyTermData> { boardWalletTag },
                new LongFlatProgressionData(1L)));
            EditorUtility.SetDirty(recipe);
        }

        static ProductionRecipeData CreateMergeRecipe(
            string path,
            string name,
            string id,
            long selectedAmount,
            CapabilityHostData water,
            CapabilityHostData waterUnit,
            TaxonomyTermData boardWalletTag,
            TaxonomyTermData sharedWalletTag,
            TaxonomyTermData selectedTag,
            List<string> createdPaths)
        {
            ProductionRecipeData recipe = CreateEconomyAsset<ProductionRecipeData>(
                path,
                name,
                id,
                EconomyOperation.Require | EconomyOperation.Issue,
                createdPaths);
            recipe.Inputs.Clear();
            recipe.Inputs.Add(new ProductionInputData(
                EconomyOperation.Consume,
                water,
                EconomyFormType.Token,
                new List<TaxonomyTermData> { boardWalletTag },
                new List<TaxonomyTermData> { selectedTag },
                new LongFlatProgressionData(selectedAmount)));
            recipe.Outputs.Clear();
            recipe.Outputs.Add(new ProductionOutputData(
                waterUnit,
                EconomyFormType.Stack,
                new List<TaxonomyTermData> { sharedWalletTag },
                new LongFlatProgressionData(1L)));
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        static ObjectiveTemplateData CreateSelectionObjective(
            TaxonomyTermData requestType,
            List<string> createdPaths)
        {
            var root = new ObjectiveNode(
                "board-selection",
                null,
                new List<ObjectiveCondition>
                {
                    new ObjectiveConditionSelectionRequest(
                        requestType,
                        1L,
                        CompareOperation.GreaterOrEqual),
                },
                new List<ObjectiveCondition>
                {
                    new ObjectiveConditionSelectionRequest(
                        requestType,
                        0L,
                        CompareOperation.Equal),
                },
                new List<ObjectiveCondition>(0));
            ObjectiveTemplateData objective = ScriptableObject.CreateInstance<ObjectiveTemplateData>();
            objective.name = "BoardSelectionObjective";
            SetField(objective, "root", root);
            SetField(objective, "completionPolicyType", ObjectiveCompletionPolicyType.Reset);
            AssetDatabase.CreateAsset(objective, SelectionObjectivePath);
            createdPaths.Add(SelectionObjectivePath);
            return objective;
        }

        static ObjectiveTemplateData CreateMergeObjective(
            CapabilityHostData water,
            TaxonomyTermData boardWalletTag,
            TaxonomyTermData selectedTag,
            List<string> createdPaths)
        {
            var root = new ObjectiveNode(
                "board-merge",
                null,
                new List<ObjectiveCondition>
                {
                    CreateSelectedEconomyCondition(
                        water,
                        boardWalletTag,
                        selectedTag,
                        1L,
                        CompareOperation.GreaterOrEqual),
                },
                new List<ObjectiveCondition>
                {
                    CreateSelectedEconomyCondition(
                        water,
                        boardWalletTag,
                        selectedTag,
                        0L,
                        CompareOperation.Equal),
                },
                new List<ObjectiveCondition>(0));
            ObjectiveTemplateData objective = ScriptableObject.CreateInstance<ObjectiveTemplateData>();
            objective.name = "BoardMergeObjective";
            SetField(objective, "root", root);
            SetField(objective, "completionPolicyType", ObjectiveCompletionPolicyType.Reset);
            AssetDatabase.CreateAsset(objective, MergeObjectivePath);
            createdPaths.Add(MergeObjectivePath);
            return objective;
        }

        static ObjectiveConditionEconomyMetric CreateSelectedEconomyCondition(
            CapabilityHostData water,
            TaxonomyTermData boardWalletTag,
            TaxonomyTermData selectedTag,
            long targetValue,
            CompareOperation compareOperation)
        {
            return new ObjectiveConditionEconomyMetric(
                new List<TaxonomyTermData> { boardWalletTag },
                EconomyFormType.Token,
                water,
                targetValue,
                compareOperation,
                null,
                new List<TaxonomyTermData> { selectedTag });
        }

        static AgentDefinitionData CreateSelectionAgent(
            CapabilityHostData boardHost,
            TaxonomyTermData waterTag,
            TaxonomyTermData requestType,
            TaxonomyTermData selectedTag,
            List<string> createdPaths)
        {
            var agentData = new SelectionAgentData();
            SetField(
                agentData,
                "resultTags",
                new List<TaxonomyTermData> { selectedTag });

            AgentDefinitionData definition = CreateAgentDefinition(
                SelectionAgentPath,
                "BoardSelectionAgent",
                "board-selection",
                110,
                new List<ObjectiveCondition>
                {
                    new ObjectiveConditionSelectionRequest(
                        requestType,
                        0L,
                        CompareOperation.Equal),
                },
                new List<EntityCriterionEntryData>
                {
                    Required(CreateCapabilityHostCriterion(boardHost, null)),
                    Required(CreateOwnerCriterion()),
                },
                new List<EntityCriterionEntryData>
                {
                    Required(CreateCapabilityHostCriterion(
                        null,
                        null,
                        new List<TaxonomyTermData> { waterTag })),
                    Required(CreateOwnerCriterion()),
                    Required(CreateAssetCountCriterion()),
                    Required(CreateSegmentLengthCriterion(1000, 1000)),
                },
                agentData,
                createdPaths);
            SetField(
                definition,
                "stopPolicyType",
                AgentStopPolicyType.None);
            return definition;
        }

        static void ConfigureSelectionBoardHost(
            CapabilityHostData boardHost,
            EconomyWalletData boardWallet,
            ProductionData mergeProduction)
        {
            SetField(
                boardHost,
                "capabilities",
                new List<CapabilityEntry>
                {
                    CreateCapabilityEntry(CapabilityHostType.SelectionOwner),
                    CreateCapabilityEntry(CapabilityHostType.ProductionOwner),
                });
            SetField(
                boardHost,
                "walletEntries",
                new List<WalletEntry>
                {
                    new WalletEntry(
                        boardWallet,
                        new List<SeedEntry>
                        {
                            new SeedEntry(mergeProduction, 1L, EconomyFormType.Stack),
                        }),
                });
            EditorUtility.SetDirty(boardHost);
        }

        static void ConfigureCatalog(
            ProductionCatalogData catalog,
            params ProductionRecipeData[] recipes)
        {
            catalog.Entries.Clear();
            for (int i = 0; recipes != null && i < recipes.Length; i++)
            {
                ProductionCatalogEntryData entry = default;
                SetStructField(ref entry, "recipe", recipes[i]);
                SetStructField(ref entry, "workDuration", 1);
                SetStructField(ref entry, "recoveryDuration", 0);
                SetStructField(ref entry, "reservationPolicy", ProductionReservationPolicy.OnEnqueue);
                catalog.Entries.Add(entry);
            }
            EditorUtility.SetDirty(catalog);
        }

        static void ConfigureProduction(
            ProductionData production,
            ProductionCatalogData catalog,
            TaxonomyTermData materializationProviderType)
        {
            production.SupportedCatalogs.Clear();
            production.SupportedCatalogs.Add(catalog);
            SetField(production, "maxQueuedOrders", 1);
            SetField(production, "maxParallelPipelines", 1);
            SetField(production, "limitReachedPolicy", ProductionLimitReachedPolicy.DisableProduction);
            SetField(production, "startPolicy", ProductionStartPolicyType.Explicit);
            SetField(production, "materializationProviderType", materializationProviderType);
            EditorUtility.SetDirty(production);
        }

        static void ConfigureBoardWater(
            CapabilityHostData water,
            TaxonomyTermData waterTag)
        {
            water.Tags.Clear();
            water.Tags.Add(waterTag);
            EditorUtility.SetDirty(water);
        }

        static void ConfigureAddressable(string assetPath, string groupName)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException("Addressable Asset Settings are not configured.");

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                throw new InvalidOperationException(string.Concat(
                    "Addressables group is missing: ",
                    groupName,
                    "."));
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException(string.Concat("Addressable asset is missing: ", assetPath, "."));

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = assetPath;
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
        }

        static ObjectiveTemplateData CreatePopulationObjective(
            EconomyAssetData turnToken,
            TaxonomyTermData sharedWalletTag,
            CapabilityHostData water,
            TaxonomyTermData boardWalletTag,
            TaxonomyTermData selectedTag,
            TaxonomyTermData boardCellTag,
            List<string> createdPaths)
        {
            var turnTokenActivation = new ObjectiveConditionEconomyMetric(
                new List<TaxonomyTermData> { sharedWalletTag },
                EconomyFormType.Stack,
                turnToken,
                1L,
                CompareOperation.GreaterOrEqual,
                null,
                null);
            ObjectiveConditionEconomyMetric mergeCompleteActivation =
                CreateSelectedEconomyCondition(
                    water,
                    boardWalletTag,
                    selectedTag,
                    0L,
                    CompareOperation.Equal);
            var success = new ObjectiveConditionMaterializedMarkerCoverage(
                water,
                EconomyFormType.Token,
                null,
                null,
                new List<TaxonomyTermData> { boardCellTag },
                0L,
                CompareOperation.Equal);
            var root = new ObjectiveNode(
                "chainrush-board-population",
                null,
                new List<ObjectiveCondition>
                {
                    turnTokenActivation,
                    mergeCompleteActivation,
                },
                new List<ObjectiveCondition> { success },
                new List<ObjectiveCondition>(0));
            ObjectiveTemplateData objective = ScriptableObject.CreateInstance<ObjectiveTemplateData>();
            objective.name = "BoardPopulationObjective";
            SetField(objective, "root", root);
            SetField(objective, "completionPolicyType", ObjectiveCompletionPolicyType.Reset);
            AssetDatabase.CreateAsset(objective, PopulationObjectivePath);
            createdPaths.Add(PopulationObjectivePath);
            return objective;
        }

        static AgentDefinitionData CreatePopulationAgent(
            ProgressivePlannerData planner,
            ProductionRecipeData refreshRecipe,
            CapabilityHostData executorHost,
            TaxonomyTermData boardCellTag,
            TaxonomyTermData boardWalletTag,
            List<string> createdPaths)
        {
            var agentData = new PopulationAgentData();
            SetField(agentData, "planner", planner);
            SetField(agentData, "completionRecipe", refreshRecipe);
            SetField(agentData, "shapeWalletTags", new List<TaxonomyTermData> { boardWalletTag });

            var match = new ObjectiveConditionMaterializedMarkerCoverage(
                null,
                EconomyFormType.Token,
                null,
                null,
                new List<TaxonomyTermData> { boardCellTag },
                0L,
                CompareOperation.Equal);
            AgentDefinitionData definition = CreateAgentDefinition(
                PopulationAgentPath,
                "BoardPopulationAgent",
                "chainrush-board-population",
                100,
                new List<ObjectiveCondition> { match },
                new List<EntityCriterionEntryData>
                {
                    Required(CreateCapabilityHostCriterion(executorHost, null)),
                    Required(CreateOwnerCriterion()),
                },
                new List<EntityCriterionEntryData>
                {
                    Required(CreateMarkerCriterion(boardCellTag)),
                },
                agentData,
                createdPaths);
            SetField(
                definition,
                "stopPolicyType",
                AgentStopPolicyType.None);
            return definition;
        }

        static void ConfigurePopulationAgentExecutor(
            AgentDefinitionData populationAgent,
            CapabilityHostData executorHost)
        {
            SetField(
                populationAgent,
                "executorSelectionCriteria",
                new List<EntityCriterionEntryData>
                {
                    Required(CreateCapabilityHostCriterion(executorHost, null)),
                    Required(CreateOwnerCriterion()),
                });
            EditorUtility.SetDirty(populationAgent);
        }

        static void EnsurePopulationAgentExecutor(
            AgentDefinitionData populationAgent,
            CapabilityHostData executorHost)
        {
            List<EntityCriterionEntryData> criteria =
                populationAgent.ExecutorSelectionCriteria;
            if (criteria.Count != 2
                || !(criteria[0].Criterion is CapabilityHostCriterionData capabilityHost)
                || capabilityHost.Definition != executorHost
                || !(criteria[1].Criterion is OwnerCriterionData))
            {
                throw new InvalidOperationException(
                    "Board Population Agent must use the Board host as its executor.");
            }
        }

        static AgentDefinitionData CreateAgentDefinition(
            string path,
            string name,
            string id,
            int priority,
            List<ObjectiveCondition> matchConditions,
            List<EntityCriterionEntryData> executorCriteria,
            List<EntityCriterionEntryData> targetCriteria,
            AgentData agent,
            List<string> createdPaths)
        {
            AgentDefinitionData definition = ScriptableObject.CreateInstance<AgentDefinitionData>();
            definition.name = name;
            SetField(definition, "agentId", id);
            SetField(definition, "basePriority", priority);
            SetField(definition, "updateInterval", 1);
            SetField(definition, "matchConditions", matchConditions);
            SetField(definition, "executorSelectionCriteria", executorCriteria);
            SetField(definition, "targetSelectionCriteria", targetCriteria);
            SetField(definition, "controlType", AgentControlType.Endpoint);
            SetField(definition, "agent", agent);
            SetField(definition, "stopPolicyType", AgentStopPolicyType.None);
            SetField(definition, "executorBusyPolicyType", AgentExecutorBusyPolicyType.Wait);
            SetField(definition, "executorReservationPolicyType", ExecutorReservationPolicyType.PerWork);
            AssetDatabase.CreateAsset(definition, path);
            createdPaths.Add(path);
            return definition;
        }

        static CapabilityHostCriterionData CreateCapabilityHostCriterion(
            CapabilityHostBaseData definition,
            List<CapabilityHostType> capabilities,
            List<TaxonomyTermData> requiredAssetTags = null)
        {
            var criterion = new CapabilityHostCriterionData();
            SetField(criterion, "definition", definition);
            SetField(
                criterion,
                "requiredAssetTags",
                requiredAssetTags ?? new List<TaxonomyTermData>(0));
            SetField(
                criterion,
                "requiredCapabilityTypes",
                capabilities ?? new List<CapabilityHostType>(0));
            return criterion;
        }

        static OwnerCriterionData CreateOwnerCriterion()
        {
            var criterion = new OwnerCriterionData();
            SetField(criterion, "ownerSelectionType", AgentOwnerSelectionType.ParticipantOwner);
            return criterion;
        }

        static AssetCountCriterionData CreateAssetCountCriterion()
        {
            var criterion = new AssetCountCriterionData();
            SetField(criterion, "compareOperation", CompareOperation.Equal);
            SetField(criterion, "targetValue", 1);
            return criterion;
        }

        static SegmentLengthCriterionData CreateSegmentLengthCriterion(
            int minimumDistance,
            int maximumDistance)
        {
            var criterion = new SegmentLengthCriterionData();
            SetField(criterion, "minimumDistance", minimumDistance);
            SetField(criterion, "maximumDistance", maximumDistance);
            return criterion;
        }

        static MarkerCriterionData CreateMarkerCriterion(TaxonomyTermData markerTag)
        {
            var criterion = new MarkerCriterionData();
            SetField(criterion, "requiredTags", new List<TaxonomyTermData> { markerTag });
            SetField(criterion, "excludedTags", new List<TaxonomyTermData>(0));
            SetField(criterion, "providerType", markerTag);
            SetField(criterion, "scopeType", MarkerScopeType.ActivityRoot);
            return criterion;
        }

        static EntityCriterionEntryData Required(EntityCriterionData criterion)
        {
            return new EntityCriterionEntryData(CriterionRequirementType.Required, criterion);
        }

        static void ConfigureSelectionBrain(
            OrchestratorAIBrainData brain,
            AgentDefinitionData populationAgent,
            AgentDefinitionData selectionAgent,
            TaxonomyTermData populationAgentOperator,
            TaxonomyTermData selectionAgentOperator,
            TaxonomyTermData productionInputOperator,
            TaxonomyTermData productionYieldOperator,
            TaxonomyTermData productionAvailableOperator,
            TaxonomyTermData materializedProductionOperator)
        {
            var populationOperation = new AgentDecompOpData();
            SetField(populationOperation, "operatorId", populationAgentOperator);
            SetField(populationOperation, "agentDefinition", populationAgent);
            var selectionOperation = new AgentDecompOpData();
            SetField(selectionOperation, "operatorId", selectionAgentOperator);
            SetField(selectionOperation, "agentDefinition", selectionAgent);
            var productionInputOperation = new ProductionInputConsumptionDecompOpData();
            SetField(productionInputOperation, "operatorId", productionInputOperator);
            var yieldOperation = new ProductionYieldDecompOpData();
            SetField(yieldOperation, "operatorId", productionYieldOperator);
            var availableOperation = new ProductionAvailableDecompOpData();
            SetField(availableOperation, "operatorId", productionAvailableOperator);
            var materializedProductionOperation = new MaterializedEntityProductionDecompOpData();
            SetField(materializedProductionOperation, "operatorId", materializedProductionOperator);

            var graph = new OrchestrationDecisionGraphData();
            SetField(
                graph,
                "nodes",
                new List<OrchestrationDecisionNodeData>
                {
                    CreateDecision(
                        "board-population-agent",
                        OrchestrationFactType.MaterializationMarkerAvailable,
                        populationAgentOperator,
                        true,
                        OrchestrationDecompositionScopeType.GlobalObjective),
                    CreateDecision(
                        "board-selection-agent",
                        OrchestrationFactType.SelectionRequest,
                        selectionAgentOperator,
                        true,
                        OrchestrationDecompositionScopeType.GlobalObjective),
                    CreateDecision(
                        "board-production-input",
                        OrchestrationFactType.EconomyAmount,
                        productionInputOperator,
                        false,
                        OrchestrationDecompositionScopeType.GlobalObjective),
                    CreateDecision(
                        "board-materialized-production",
                        OrchestrationFactType.MaterializedEntity,
                        materializedProductionOperator,
                        false,
                        OrchestrationDecompositionScopeType.GlobalObjective),
                    CreateDecision(
                        "board-production-yield",
                        OrchestrationFactType.ProductionYield,
                        productionYieldOperator,
                        false),
                    CreateDecision(
                        "board-production-available",
                        OrchestrationFactType.ProductionAvailable,
                        productionAvailableOperator,
                        false),
                });

            SetField(
                brain,
                "operators",
                new List<OrchestrationDecompOpData>
                {
                    populationOperation,
                    selectionOperation,
                    productionInputOperation,
                    yieldOperation,
                    availableOperation,
                    materializedProductionOperation,
                });
            SetField(brain, "decisionGraph", graph);
            EditorUtility.SetDirty(brain);
        }

        static OrchestrationDecisionData CreateDecision(
            string id,
            OrchestrationFactType factType,
            TaxonomyTermData operatorId,
            bool matchAgent,
            OrchestrationDecompositionScopeType? scopeType = null)
        {
            var factCondition = new FactTypeDecisionConditionData();
            SetField(factCondition, "factType", factType);
            var conditions = new List<OrchestrationDecisionConditionData> { factCondition };
            if (matchAgent)
                conditions.Add(new AgentMatchDecisionConditionData());
            if (scopeType.HasValue)
            {
                var scopeCondition = new ScopeDecisionConditionData();
                SetField(scopeCondition, "scopeType", scopeType.Value);
                conditions.Add(scopeCondition);
            }

            var decision = new OrchestrationDecisionData();
            SetField(decision, "decisionId", id);
            SetField(decision, "conditions", conditions);
            SetField(decision, "operatorId", operatorId);
            return decision;
        }

        static ActivityOrchestrationConfigData CreateOrchestration(
            OrchestratorAIBrainData brain,
            EconomyStateOrchestrationModuleData economyState,
            ProductionStateOrchestrationModuleData productionState,
            ProjectionStateOrchestrationModuleData projectionState,
            List<string> createdPaths)
        {
            ActivityOrchestrationConfigData orchestration =
                ScriptableObject.CreateInstance<ActivityOrchestrationConfigData>();
            orchestration.name = "BoardOrchestration";
            SetField(orchestration, "orchestratorBrain", brain);
            ConfigureOrchestrationModules(
                orchestration,
                economyState,
                productionState,
                projectionState);
            SetField(orchestration, "debugName", "ChainRush Board");
            AssetDatabase.CreateAsset(orchestration, OrchestrationPath);
            createdPaths.Add(OrchestrationPath);
            return orchestration;
        }

        static void ConfigureBoardActivity(
            ActivityData activity,
            EconomyWalletData sharedWallet,
            EconomyWalletData boardWallet,
            EconomyAssetData turnToken,
            CapabilityHostData boardHost,
            CapabilityHostData populationProducer,
            ObjectiveTemplateData objective,
            ActivityOrchestrationConfigData orchestration,
            TaxonomyTermData boardCellTag,
            BoardSpatialShapes shapes)
        {
            ActivityTeamWalletData sharedWalletData = default;
            SetStructField(ref sharedWalletData, "wallet", sharedWallet);
            SetStructField(
                ref sharedWalletData,
                "seed",
                new List<ActivityWalletSeedEntryData>
                {
                    new ActivityWalletSeedEntryData(
                        new SeedEntry(turnToken, 1L, EconomyFormType.Stack),
                        ActivitySeedMaterializationType.None,
                        new List<TaxonomyTermData>(0)),
                });

            ActivityTeamWalletData boardWalletData = default;
            SetStructField(ref boardWalletData, "wallet", boardWallet);
            var boardSeed = new List<ActivityWalletSeedEntryData>
            {
                new ActivityWalletSeedEntryData(
                    new SeedEntry(boardHost, 1L, EconomyFormType.Token),
                    ActivitySeedMaterializationType.NonSpatial,
                    new List<TaxonomyTermData>(0)),
                new ActivityWalletSeedEntryData(
                    new SeedEntry(populationProducer, 1L, EconomyFormType.Token),
                    ActivitySeedMaterializationType.NonSpatial,
                    new List<TaxonomyTermData>(0)),
            };
            List<SpatialShapeData> availableShapes = shapes.All;
            for (int i = 0; i < availableShapes.Count; i++)
            {
                boardSeed.Add(new ActivityWalletSeedEntryData(
                    new SeedEntry(availableShapes[i], 1L, EconomyFormType.Stack),
                    ActivitySeedMaterializationType.None,
                    new List<TaxonomyTermData>(0)));
            }
            SetStructField(
                ref boardWalletData,
                "seed",
                boardSeed);

            ActivityTeamObjectiveData teamObjective = default;
            SetStructField(ref teamObjective, "template", objective);
            SetStructField(ref teamObjective, "successScoreDelta", 0);
            SetStructField(ref teamObjective, "failScoreDelta", 0);

            ActivityTeamData team = activity.Teams[0];
            SetStructField(
                ref team,
                "objectives",
                new List<ActivityTeamObjectiveData> { teamObjective });
            SetStructField(
                ref team,
                "wallets",
                new List<ActivityTeamWalletData> { sharedWalletData, boardWalletData });
            SetStructField(
                ref team,
                "features",
                new List<ActivityFeatureData> { orchestration });
            activity.Teams[0] = team;

            if (activity.Space == null)
                throw new InvalidOperationException("BoardActivity requires an Activity space.");
            SetField(
                activity.Space,
                "markerProviders",
                new List<SpatialMarkerProviderData>
                {
                    CreateBoardShapeProvider(shapes.BoardPlane, boardCellTag),
                });
            EditorUtility.SetDirty(activity);
        }

        static void ConfigureBoardObjectives(
            ActivityData activity,
            ObjectiveTemplateData populationObjective,
            ObjectiveTemplateData selectionObjective,
            ObjectiveTemplateData mergeObjective)
        {
            if (activity == null || activity.Teams.Count != 1)
                throw new InvalidOperationException("Board Activity must contain exactly one team.");

            ActivityTeamData team = activity.Teams[0];
            SetStructField(
                ref team,
                "objectives",
                new List<ActivityTeamObjectiveData>
                {
                    CreateTeamObjective(populationObjective),
                    CreateTeamObjective(selectionObjective),
                    CreateTeamObjective(mergeObjective),
                });
            activity.Teams[0] = team;
            EditorUtility.SetDirty(activity);
        }

        static ActivityTeamObjectiveData CreateTeamObjective(ObjectiveTemplateData objective)
        {
            ActivityTeamObjectiveData teamObjective = default;
            SetStructField(ref teamObjective, "template", objective);
            SetStructField(ref teamObjective, "successScoreDelta", 0);
            SetStructField(ref teamObjective, "failScoreDelta", 0);
            return teamObjective;
        }

        static void ConfigureOrchestrationModules(
            ActivityOrchestrationConfigData orchestration,
            EconomyStateOrchestrationModuleData economyState,
            ProductionStateOrchestrationModuleData productionState,
            ProjectionStateOrchestrationModuleData projectionState)
        {
            SetField(
                orchestration,
                "modules",
                new List<OrchestrationDomainModuleData>
                {
                    economyState,
                    productionState,
                    projectionState,
                });
            EditorUtility.SetDirty(orchestration);
        }

        static void EnsureOrchestrationModules(
            ActivityOrchestrationConfigData orchestration,
            EconomyStateOrchestrationModuleData economyState,
            ProductionStateOrchestrationModuleData productionState,
            ProjectionStateOrchestrationModuleData projectionState)
        {
            List<OrchestrationDomainModuleData> modules = orchestration.Modules;
            if (modules.Count != 3
                || modules[0] != economyState
                || modules[1] != productionState
                || modules[2] != projectionState)
            {
                throw new InvalidOperationException(
                    "Board orchestration must contain Economy, Production, and Projection state modules in authored order.");
            }
        }

        static void ConfigureBoardUIPrefab(
            CapabilityHostData boardHost,
            TaxonomyTermData selectionRequestType)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BoardUIPrefabPath);
            try
            {
                BoardUIController controller = root.GetComponent<BoardUIController>();
                if (controller == null)
                    throw new InvalidOperationException("BoardUI prefab has no BoardUIController.");

                var serialized = new SerializedObject(controller);
                serialized.FindProperty("boardHostDefinition").objectReferenceValue = boardHost;
                SerializedProperty requestType = serialized.FindProperty("selectionRequestType");
                if (requestType == null)
                {
                    throw new InvalidOperationException(
                        "BoardUIController selectionRequestType field is not imported yet.");
                }
                requestType.objectReferenceValue = selectionRequestType;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BoardUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static BoardSpatialShapes CreateBoardSpatialShapes(List<string> createdPaths)
        {
            SpatialShapeRuleData singleRule = CreateSpatialShapeRule(
                SingleRulePath,
                "SingleRule",
                new List<SpatialShapeRuleData.ContinuationPathData>(0),
                createdPaths);
            SpatialShapeRuleData lineRule = CreateSpatialShapeRule(
                LineRulePath,
                "LineRule",
                new List<SpatialShapeRuleData.ContinuationPathData>
                {
                    new SpatialShapeRuleData.ContinuationPathData(
                        Vector3Int.zero,
                        new List<Vector3Int> { Vector3Int.right }),
                },
                createdPaths);
            SpatialShapeRuleData cornerRule = CreateSpatialShapeRule(
                CornerRulePath,
                "CornerRule",
                new List<SpatialShapeRuleData.ContinuationPathData>
                {
                    new SpatialShapeRuleData.ContinuationPathData(
                        Vector3Int.zero,
                        new List<Vector3Int> { Vector3Int.right }),
                    new SpatialShapeRuleData.ContinuationPathData(
                        Vector3Int.zero,
                        new List<Vector3Int> { Vector3Int.forward }),
                },
                createdPaths);
            SpatialShapeRuleData zigzagRule = CreateSpatialShapeRule(
                ZigzagRulePath,
                "ZigzagRule",
                new List<SpatialShapeRuleData.ContinuationPathData>
                {
                    new SpatialShapeRuleData.ContinuationPathData(
                        Vector3Int.zero,
                        new List<Vector3Int>
                        {
                            Vector3Int.right,
                            Vector3Int.forward,
                            Vector3Int.right,
                            Vector3Int.back,
                        }),
                },
                createdPaths);

            return new BoardSpatialShapes
            {
                BoardPlane = CreateSpatialShape(
                    BoardPlaneShapePath,
                    "BoardPlane",
                    "chainrush.spatial.shape.board-plane",
                    SpatialShapeType.Box,
                    null,
                    createdPaths),
                Single = CreateSpatialShape(
                    SingleShapePath,
                    "Single",
                    "chainrush.spatial.shape.single",
                    SpatialShapeType.Custom,
                    singleRule,
                    createdPaths),
                Line = CreateSpatialShape(
                    LineShapePath,
                    "Line",
                    "chainrush.spatial.shape.line",
                    SpatialShapeType.Custom,
                    lineRule,
                    createdPaths),
                Corner = CreateSpatialShape(
                    CornerShapePath,
                    "Corner",
                    "chainrush.spatial.shape.corner",
                    SpatialShapeType.Custom,
                    cornerRule,
                    createdPaths),
                Box = CreateSpatialShape(
                    BoxShapePath,
                    "Box",
                    "chainrush.spatial.shape.box",
                    SpatialShapeType.Box,
                    null,
                    createdPaths),
                Zigzag = CreateSpatialShape(
                    ZigzagShapePath,
                    "Zigzag",
                    "chainrush.spatial.shape.zigzag",
                    SpatialShapeType.Custom,
                    zigzagRule,
                    createdPaths),
            };
        }

        static SpatialShapeRuleData CreateSpatialShapeRule(
            string path,
            string name,
            List<SpatialShapeRuleData.ContinuationPathData> continuationPaths,
            List<string> createdPaths)
        {
            SpatialShapeRuleData rule = CreateAsset<SpatialShapeRuleData>(path, name, createdPaths);
            SetField(rule, "requiredCells", new List<Vector3Int> { Vector3Int.zero });
            SetField(
                rule,
                "continuationPaths",
                continuationPaths ?? new List<SpatialShapeRuleData.ContinuationPathData>(0));
            SetField(
                rule,
                "forbiddenRelations",
                new List<SpatialShapeRuleData.ForbiddenRelationData>(0));
            EditorUtility.SetDirty(rule);
            return rule;
        }

        static SpatialShapeData CreateSpatialShape(
            string path,
            string name,
            string id,
            SpatialShapeType shapeType,
            SpatialShapeRuleData customRule,
            List<string> createdPaths)
        {
            SpatialShapeData shape = CreateEconomyAsset<SpatialShapeData>(
                path,
                name,
                id,
                EconomyOperation.Require
                | EconomyOperation.Issue
                | EconomyOperation.Consume
                | EconomyOperation.Transfer
                | EconomyOperation.Reserve
                | EconomyOperation.DirectSet,
                createdPaths);
            SetField(shape, "shapeType", shapeType);
            SetField(shape, "customRule", customRule);
            EditorUtility.SetDirty(shape);
            return shape;
        }

        static BoardSpatialShapes LoadBoardSpatialShapes()
        {
            return new BoardSpatialShapes
            {
                BoardPlane = LoadRequired<SpatialShapeData>(BoardPlaneShapePath),
                Single = LoadRequired<SpatialShapeData>(SingleShapePath),
                Line = LoadRequired<SpatialShapeData>(LineShapePath),
                Corner = LoadRequired<SpatialShapeData>(CornerShapePath),
                Box = LoadRequired<SpatialShapeData>(BoxShapePath),
                Zigzag = LoadRequired<SpatialShapeData>(ZigzagShapePath),
            };
        }

        static SpatialShapeProviderData CreateBoardShapeProvider(
            SpatialShapeData boardPlane,
            TaxonomyTermData boardCellTag)
        {
            var provider = new SpatialShapeProviderData();
            SetField(provider, "providerType", boardCellTag);
            SetField(
                provider,
                "usagePolicy",
                new SpatialMarkerUsagePolicyData(
                    SpatialMarkerSelectionType.Next,
                    SpatialMarkerReusePolicyType.ExhaustBeforeReuse));
            SetField(provider, "shape", boardPlane);
            SetField(
                provider,
                "usage",
                new SpatialShapeUsageData(
                    SpatialShapeFillType.Inside,
                    Vector3Int.zero,
                    new Vector3Int(4, 1, 4),
                    Vector3Int.zero,
                    new Vector3Int(1000, 1, 1000),
                    Vector3Int.zero));
            SetField(provider, "markerTags", new List<TaxonomyTermData> { boardCellTag });
            return provider;
        }

        static CapabilityHostData CreateCapabilityHost(
            string path,
            string name,
            string id,
            List<CapabilityEntry> capabilities,
            List<WalletEntry> walletEntries,
            List<string> createdPaths)
        {
            CapabilityHostData host = CreateEconomyAsset<CapabilityHostData>(
                path,
                name,
                id,
                EconomyOperation.Require
                | EconomyOperation.Issue
                | EconomyOperation.Consume
                | EconomyOperation.Transfer
                | EconomyOperation.Destroy,
                createdPaths);
            SetField(host, "capabilities", capabilities ?? new List<CapabilityEntry>(0));
            SetField(host, "walletEntries", walletEntries ?? new List<WalletEntry>(0));
            EditorUtility.SetDirty(host);
            return host;
        }

        static CapabilityEntry CreateCapabilityEntry(CapabilityHostType capabilityType)
        {
            var entry = new CapabilityEntry();
            SetField(entry, "capabilityType", capabilityType);
            SetField(entry, "selectorTags", new List<TaxonomyTermData>(0));
            return entry;
        }

        static TaxonomyFamilyData CreateTaxonomyFamily(
            string path,
            string name,
            string id,
            string displayName,
            List<string> createdPaths)
        {
            TaxonomyFamilyData family = CreateAsset<TaxonomyFamilyData>(path, name, createdPaths);
            ConfigureTaxonomyFamily(family, id, displayName);
            return family;
        }

        static TaxonomyTermData CreateTaxonomyTerm(
            string path,
            string name,
            string id,
            string displayName,
            TaxonomyFamilyData family,
            int sortOrder,
            List<string> createdPaths)
        {
            TaxonomyTermData term = CreateAsset<TaxonomyTermData>(path, name, createdPaths);
            ConfigureTaxonomyTerm(term, id, displayName, family, sortOrder);
            return term;
        }

        static T CreateEconomyAsset<T>(
            string path,
            string name,
            string id,
            EconomyOperation allowedOperations,
            List<string> createdPaths)
            where T : EconomyAssetData
        {
            T asset = CreateAsset<T>(path, name, createdPaths);
            ConfigureEconomyAsset(asset, id, allowedOperations);
            return asset;
        }

        static void ConfigureEconomyAsset(
            EconomyAssetData asset,
            string id,
            EconomyOperation allowedOperations)
        {
            SetField(asset, "id", id);
            SetField(asset, "allowedOperations", allowedOperations);
            SetField(asset, "slotFootprint", 1);
            EditorUtility.SetDirty(asset);
        }

        static void ConfigureTaxonomyFamily(
            TaxonomyFamilyData family,
            string id,
            string displayName)
        {
            SetField(family, "id", id);
            SetField(family, "displayName", displayName);
            SetField(family, "cardinality", TaxonomyCardinality.Multiple);
            EditorUtility.SetDirty(family);
        }

        static void ConfigureTaxonomyTerm(
            TaxonomyTermData term,
            string id,
            string displayName,
            TaxonomyFamilyData family,
            int sortOrder)
        {
            SetField(term, "id", id);
            SetField(term, "displayName", displayName);
            SetField(term, "family", family);
            SetField(term, "sortOrder", sortOrder);
            EditorUtility.SetDirty(term);
        }

        static T CreateAsset<T>(
            string path,
            string name,
            List<string> createdPaths)
            where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            createdPaths.Add(path);
            return asset;
        }

        static void AddUnique<T>(List<T> destination, params T[] values)
            where T : UnityEngine.Object
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                T value = values[i];
                if (value != null && !destination.Contains(value))
                    destination.Add(value);
            }
        }

        static void EnsureVerticalSliceFolders()
        {
            EnsureFolder(AgentsRoot);
            EnsureFolder(ObjectivesRoot);
            EnsureFolder(OrchestrationRoot);
            EnsureFolder(OrchestrationModulesRoot);
            EnsureFolder(OrchestrationTaxonomyRoot);
            EnsureFolder(SharedUnitsRoot);
            EnsureFolder(SharedWaterRoot);
        }

        static void EnsureIncompleteVerticalSliceState(
            EconomyAssetData turnToken,
            CapabilityHostData waterUnit,
            CapabilityHostData populationProducer,
            ProductionRecipeData refreshRecipe,
            ProductionRecipeData waterRecipe,
            TaxonomyFamilyData operatorFamily,
            TaxonomyTermData populationAgentOperator,
            TaxonomyTermData productionYieldOperator,
            TaxonomyTermData productionAvailableOperator)
        {
            bool incomplete = HasEmptyOrExpectedId(
                    turnToken,
                    "chainrush.resource.board-turn-token")
                && HasEmptyOrExpectedId(waterUnit, "chainrush.unit.water")
                && waterUnit.Capabilities.Count == 0
                && waterUnit.WalletEntries.Count == 0
                && HasEmptyOrExpectedId(
                    populationProducer,
                    "chainrush.board.population-producer")
                && (populationProducer.Capabilities.Count == 0
                    || (populationProducer.Capabilities.Count == 1
                        && populationProducer.SupportsCapability(CapabilityHostType.ProductionOwner)))
                && populationProducer.WalletEntries.Count <= 1
                && HasEmptyOrExpectedId(
                    refreshRecipe,
                    "chainrush.production.board.refresh.recipe")
                && refreshRecipe.Inputs.Count <= 1
                && refreshRecipe.Outputs.Count == 0
                && HasEmptyOrExpectedId(
                    waterRecipe,
                    "chainrush.production.board.water-base.recipe")
                && waterRecipe.Inputs.Count == 0
                && waterRecipe.Outputs.Count <= 1
                && HasEmptyOrExpectedId(
                    operatorFamily,
                    "chainrush.orchestration.board.operator")
                && IsEmptyOrConfiguredTerm(
                    populationAgentOperator,
                    "chainrush.orchestration.board.agent.population",
                    operatorFamily,
                    0)
                && IsEmptyOrConfiguredTerm(
                    productionYieldOperator,
                    "chainrush.orchestration.board.production-yield",
                    operatorFamily,
                    2)
                && IsEmptyOrConfiguredTerm(
                    productionAvailableOperator,
                    "chainrush.orchestration.board.production-available",
                    operatorFamily,
                    3);
            if (!incomplete)
            {
                throw new InvalidOperationException(
                    "Vertical slice assets are not in the exact incomplete state produced by the failed authoring run. Refusing to overwrite them.");
            }
        }

        static void EnsureCompletedVerticalSliceState(
            EconomyAssetData turnToken,
            CapabilityHostData waterUnit,
            CapabilityHostData populationProducer,
            ProductionRecipeData refreshRecipe,
            ProductionRecipeData waterRecipe,
            TaxonomyFamilyData operatorFamily,
            TaxonomyTermData populationAgentOperator,
            TaxonomyTermData productionYieldOperator,
            TaxonomyTermData productionAvailableOperator,
            TaxonomyTermData materializedProductionOperator)
        {
            bool completed = HasId(turnToken, "chainrush.resource.board-turn-token")
                && HasId(waterUnit, "chainrush.unit.water")
                && HasId(populationProducer, "chainrush.board.population-producer")
                && populationProducer.SupportsCapability(CapabilityHostType.ProductionOwner)
                && populationProducer.WalletEntries.Count == 1
                && HasId(refreshRecipe, "chainrush.production.board.refresh.recipe")
                && refreshRecipe.Inputs.Count == 1
                && refreshRecipe.Outputs.Count == 0
                && HasId(waterRecipe, "chainrush.production.board.water-base.recipe")
                && waterRecipe.Inputs.Count == 0
                && waterRecipe.Outputs.Count == 1
                && HasId(operatorFamily, "chainrush.orchestration.board.operator")
                && HasConfiguredTerm(
                    populationAgentOperator,
                    "chainrush.orchestration.board.agent.population",
                    operatorFamily,
                    0)
                && HasConfiguredTerm(
                    productionYieldOperator,
                    "chainrush.orchestration.board.production-yield",
                    operatorFamily,
                    2)
                && HasConfiguredTerm(
                    productionAvailableOperator,
                    "chainrush.orchestration.board.production-available",
                    operatorFamily,
                    3)
                && HasConfiguredTerm(
                    materializedProductionOperator,
                    "chainrush.orchestration.board.production-materialized",
                    operatorFamily,
                    4);
            if (!completed)
            {
                throw new InvalidOperationException(
                    "Vertical slice completion did not produce the required recipes, producer, resources, and operator taxonomy.");
            }
        }

        static bool IsEmptyOrConfiguredTerm(
            TaxonomyTermData term,
            string id,
            TaxonomyFamilyData family,
            int sortOrder)
        {
            if (term == null)
                return false;

            string currentId = term.Id;
            TaxonomyFamilyData currentFamily = GetField<TaxonomyFamilyData>(term, "family");
            int currentSortOrder = GetField<int>(term, "sortOrder");
            bool empty = string.IsNullOrWhiteSpace(currentId)
                && currentFamily == null
                && currentSortOrder == 0;
            bool configured = string.Equals(currentId, id, StringComparison.Ordinal)
                && currentFamily == family
                && currentSortOrder == sortOrder;
            return empty || configured;
        }

        static bool HasConfiguredTerm(
            TaxonomyTermData term,
            string id,
            TaxonomyFamilyData family,
            int sortOrder)
        {
            return HasId(term, id)
                && GetField<TaxonomyFamilyData>(term, "family") == family
                && GetField<int>(term, "sortOrder") == sortOrder;
        }

        static bool HasId(EconomyAssetData asset, string id)
        {
            return asset != null && string.Equals(asset.Id, id, StringComparison.Ordinal);
        }

        static bool HasEmptyOrExpectedId(EconomyAssetData asset, string id)
        {
            return asset != null
                && (string.IsNullOrWhiteSpace(asset.Id)
                    || string.Equals(asset.Id, id, StringComparison.Ordinal));
        }

        static bool HasId(TaxonomyFamilyData family, string id)
        {
            return family != null && string.Equals(family.Id, id, StringComparison.Ordinal);
        }

        static bool HasEmptyOrExpectedId(TaxonomyFamilyData family, string id)
        {
            return family != null
                && (string.IsNullOrWhiteSpace(family.Id)
                    || string.Equals(family.Id, id, StringComparison.Ordinal));
        }

        static bool HasId(TaxonomyTermData term, string id)
        {
            return term != null && string.Equals(term.Id, id, StringComparison.Ordinal);
        }

        static void EnsureVerticalSliceTargetsDoNotExist()
        {
            for (int i = 0; i < VerticalSliceCreatedPaths.Length; i++)
                EnsureAssetDoesNotExist(VerticalSliceCreatedPaths[i]);
        }

        static void EnsureSpatialShapeTargetsDoNotExist()
        {
            for (int i = 0; i < SpatialShapeCreatedPaths.Length; i++)
                EnsureAssetDoesNotExist(SpatialShapeCreatedPaths[i]);
        }

        static void DeleteCreatedAssets(List<string> createdPaths)
        {
            for (int i = createdPaths.Count - 1; i >= 0; i--)
                AssetDatabase.DeleteAsset(createdPaths[i]);
            AssetDatabase.SaveAssets();
        }

        static void EnsureAssetDoesNotExist(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException($"Board authoring target already exists: '{path}'.");
        }

        static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException($"Missing required Board asset at '{path}'.");
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"Cannot create authoring folder '{path}'.");

            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException($"Unity failed to create authoring folder '{path}'.");
        }

        static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = FindField(target, fieldName);
            return (T)field.GetValue(target);
        }

        static void SetField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = FindField(target, fieldName);
            field.SetValue(target, value);
        }

        static void SetStructField<TStruct, TValue>(
            ref TStruct target,
            string fieldName,
            TValue value)
            where TStruct : struct
        {
            object boxed = target;
            SetField(boxed, fieldName, value);
            target = (TStruct)boxed;
        }

        static FieldInfo FindField(object target, string fieldName)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                    return field;
                type = type.BaseType;
            }

            throw new MissingFieldException(target.GetType().FullName, fieldName);
        }
    }
}
