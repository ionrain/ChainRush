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
    public static partial class ChainRushBoardPlannerAuthoring
    {
        const string AutobattleRoot = "Assets/Game/Activities/Autobattle";
        const string BoardRoot = "Assets/Game/Activities/Board";
        const string SharedRoot = "Assets/Game/Activities/Shared";
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
        const string BoardActivityPath = BoardRoot + "/Definition/BoardActivity.asset";
        const string BoardHostPath = BoardRoot + "/Economy/BoardHost.asset";
        const string BoardWalletPath = BoardRoot + "/Economy/BoardWallet.asset";
        const string BoardWalletTagPath = BoardRoot + "/Economy/BoardWalletTag.asset";
        const string WaterPath = BoardRoot + "/Economy/WaterBoardBase.asset";
        const string WaterTagPath = BoardRoot + "/Taxonomy/WaterBoardItem.asset";
        const string BoardCellTagPath = BoardRoot + "/Taxonomy/BoardCellTag.asset";
        const string BoardItemFamilyPath = BoardRoot + "/Taxonomy/BoardItemFamily.asset";
        const string MergeSelectedTagPath = BoardRoot + "/Taxonomy/BoardMergeSelected.asset";
        const string MergeRecipe1Path = BoardRoot + "/Production/BoardMergeRecipe1.asset";
        const string MergeCatalogPath = BoardRoot + "/Production/BoardProductionCatalog.asset";
        const string WaterProjectionPrefabPath =
            BoardRoot + "/Projection/WaterBoardBase.prefab";
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
        const string WaterRecipePath = BoardRoot + "/Production/WaterBoardBaseRecipe.asset";
        const string PopulationProductionPath = BoardRoot + "/Production/BoardPopulationProduction.asset";
        const string PopulationCatalogPath = BoardRoot + "/Production/BoardPopulationCatalog.asset";
        const string PopulationAgentPath = AgentsRoot + "/BoardPopulationAgent.asset";
        const string SelectionAgentPath = AgentsRoot + "/BoardSelectionAgent.asset";
        const string PopulationObjectivePath = ObjectivesRoot + "/BoardPopulationObjective.asset";
        const string SelectionObjectivePath = ObjectivesRoot + "/BoardSelectionObjective.asset";
        const string MergeObjectivePath = ObjectivesRoot + "/BoardMergeObjective.asset";
        const string OperatorFamilyPath = OrchestrationTaxonomyRoot + "/BoardOperatorFamily.asset";
        const string EconomyOperationOperatorPath = OrchestrationTaxonomyRoot + "/BoardEconomyOperationOperator.asset";
        const string ClearBoardOperatorPath = OrchestrationTaxonomyRoot + "/BoardClearOperator.asset";
        const string PopulationAgentOperatorPath = OrchestrationTaxonomyRoot + "/BoardPopulationAgentOperator.asset";
        const string ProductionYieldOperatorPath = OrchestrationTaxonomyRoot + "/BoardProductionYieldOperator.asset";
        const string ProductionAvailableOperatorPath = OrchestrationTaxonomyRoot + "/BoardProductionAvailableOperator.asset";
        const string MaterializedProductionOperatorPath = OrchestrationTaxonomyRoot + "/BoardMaterializedProductionOperator.asset";
        const string EconomyStateModulePath = OrchestrationModulesRoot + "/BoardEconomyState.asset";
        const string ProductionStateModulePath = OrchestrationModulesRoot + "/BoardProductionState.asset";
        const string ProjectionStateModulePath = OrchestrationModulesRoot + "/BoardProjectionState.asset";
        const string BrainPath = OrchestrationRoot + "/BoardBrain.asset";
        const string OrchestrationPath = OrchestrationRoot + "/BoardOrchestration.asset";

        const string EconomyDefinitionsInstallerPath = "Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset";
        const string TaxonomyInstallerPath = "Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset";

        static readonly string[] VerticalSliceCreatedPaths =
        {
            TurnTokenPath,
            WaterUnitPath,
            PopulationProducerPath,
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
                new LongStepProgressionData(6L, 2L, 1L, 1d, 1d)));
            recipe.Outputs.Clear();
            recipe.Outputs.Add(new ProductionOutputData(
                turnToken,
                EconomyFormType.Stack,
                new List<TaxonomyTermData> { sharedWalletTag },
                new LongFlatProgressionData(1L)));
            EditorUtility.SetDirty(recipe);
        }

        static void ConfigureCellRecipe(
            ProductionRecipeData recipe,
            CapabilityHostData cell,
            TaxonomyTermData boardWalletTag)
        {
            recipe.Inputs.Clear();
            recipe.Outputs.Clear();
            recipe.Outputs.Add(new ProductionOutputData(
                cell,
                EconomyFormType.Token,
                new List<TaxonomyTermData> { boardWalletTag },
                new LongFlatProgressionData(1L)));
            EditorUtility.SetDirty(recipe);
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

        [MenuItem("ChainRush/Activities/Board/Configure Population Objectives")]
        public static void ConfigurePopulationObjectives()
        {
            var agent = LoadRequired<AgentDefinitionData>(PopulationAgentPath);
            var population = new PopulationAgentData();
            ConfigurePopulationSettings(population);
            SetField(population, "shapeWalletTags",
                new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(BoardWalletTagPath) });
            SetField(agent, "agent", population);
            SetField(agent, "targetSelectionCriteria", CreatePopulationProducerCriteria());
            SetField(agent, "matchConditions", new List<ObjectiveCondition>
            {
                CreatePopulationCoverage()
            });
            EditorUtility.SetDirty(agent);

            ConfigureBoardRefresh();
            ConfigurePopulationDecision();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("ChainRush/Activities/Board/Configure Clear Before Fill")]
        public static void ConfigureBoardRefresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Board refresh authoring requires Edit Mode.");
            var turn = LoadRequired<FrameworkResourceData>(TurnTokenPath);
            var wallet = LoadRequired<TaxonomyTermData>(SharedWalletTagPath);
            var itemTag = LoadRequired<TaxonomyTermData>(BoardContentTagPath);
            var objective = LoadRequired<ObjectiveTemplateData>(PopulationObjectivePath);
            ConfigurePopulationObjective(objective, turn, wallet, itemTag,
                LoadRequired<TaxonomyTermData>(BoardWalletTagPath),
                LoadRequired<TaxonomyTermData>(MergeSelectedTagPath),
                LoadRequired<TaxonomyTermData>(BoardCellTagPath));
            var term = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(EconomyOperationOperatorPath);
            if (term == null)
                term = CreateTaxonomyTerm(EconomyOperationOperatorPath, "BoardEconomyOperationOperator",
                    "chainrush.orchestration.board.economy-operation", "Board Economy Operation",
                    LoadRequired<TaxonomyFamilyData>(OperatorFamilyPath), 6, new List<string>());
            var installer = LoadRequired<TaxonomyRuntimeInstallerData>(TaxonomyInstallerPath);
            var terms = new List<TaxonomyTermData>(GetField<TaxonomyTermData[]>(installer, "terms"));
            if (!terms.Contains(term))
                terms.Add(term);
            var clearTerm = WriteContentTerm(ClearBoardOperatorPath, "chainrush.orchestration.board.clear", 7, terms,
                LoadRequired<TaxonomyFamilyData>(OperatorFamilyPath));
            SetField(installer, "terms", terms.ToArray());
            EditorUtility.SetDirty(installer);
            var brain = LoadRequired<OrchestratorAIBrainData>(BrainPath);
            ConfigureEconomyOperation(brain, term, turn, wallet);
            ConfigureBoardCleanupOperation(brain, clearTerm, itemTag,
                LoadRequired<TaxonomyTermData>(BoardWalletTagPath), LoadRequired<TaxonomyTermData>(MergeSelectedTagPath));
            var selection = LoadRequired<ObjectiveTemplateData>(SelectionObjectivePath);
            selection.Root.ActivateConditions.RemoveAll(condition => condition is ObjectiveConditionMaterializedRegionCoverage);
            selection.Root.ActivateConditions.Add(CreatePopulationCoverage());
            EditorUtility.SetDirty(selection);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("ChainRush/Activities/Board/Configure Population Regions")]
        public static void ConfigurePopulationRegions()
        {
            var agent = LoadRequired<AgentDefinitionData>(PopulationAgentPath);
            if (!(agent.Agent is PopulationAgentData population))
                throw new InvalidOperationException("Board requires its Population agent before region authoring.");
            ConfigurePopulationSettings(population);
            SetField(agent, "targetSelectionCriteria", CreatePopulationProducerCriteria());
            SetField(agent, "matchConditions", new List<ObjectiveCondition>
            {
                CreatePopulationCoverage()
            });
            EditorUtility.SetDirty(agent);
            ConfigureBoardRefresh();
            ConfigurePopulationDecision();
            AssetDatabase.SaveAssets();
        }

        static void ConfigureEconomyOperation(OrchestratorAIBrainData brain, TaxonomyTermData term,
            EconomyAssetData turn, TaxonomyTermData wallet)
        {
            var operation = new EconomyOperationDecompOpData();
            SetField(operation, "operatorId", term);
            SetField(operation, "operation", EconomyOperation.Consume);
            SetField(operation, "selection", new EconomyEntrySelectionData(turn, EconomyFormType.Stack,
                new List<TaxonomyTermData> { wallet }, null, null, null, null));
            brain.Operators.RemoveAll(item => item.OperatorId == term);
            brain.Operators.Add(operation);
            brain.DecisionGraph.Nodes.RemoveAll(item => item.DecisionId == "board-consume-turn");
            brain.DecisionGraph.Nodes.Insert(0, CreateDecision("board-consume-turn",
                OrchestrationFactType.EconomyOperation, term, false,
                OrchestrationDecompositionScopeType.GlobalObjective));
            EditorUtility.SetDirty(brain);
        }

        static void ConfigurePopulationObjective(
            ObjectiveTemplateData objective,
            EconomyAssetData turnToken,
            TaxonomyTermData sharedWalletTag,
            TaxonomyTermData itemTag,
            TaxonomyTermData boardWalletTag,
            TaxonomyTermData selectedTag,
            TaxonomyTermData boardCellTag)
        {
            var operation = new EconomyMutationOperationData();
            SetField(operation, "mutation", new EconomyOperationData(EconomyOperation.Consume,
                new EconomyAssetAmountEntry(turnToken, 1L, EconomyFormType.Stack),
                new List<TaxonomyTermData> { sharedWalletTag }, null));
            var confirmation = new ObjectiveConditionEconomyOperation();
            SetField(confirmation, "operation", operation);
            var payment = new ObjectiveNode("chainrush-board-consume-turn", null,
                new List<ObjectiveCondition> { new ObjectiveConditionParentActive() },
                new List<ObjectiveCondition> { confirmation });
            var emptyConditions = new List<ObjectiveCondition>();
            foreach (string content in ContentNames)
                emptyConditions.Add(new ObjectiveConditionEconomyMetric(
                    new List<TaxonomyTermData> { boardWalletTag }, EconomyFormType.Token,
                    LoadRequired<CapabilityHostData>(BoardRoot + "/Economy/" + content + "BoardBase.asset"),
                    0L, CompareOperation.Equal, new List<TaxonomyTermData> { itemTag }, null));
            var clear = new ObjectiveNode("chainrush-board-clear", null,
                new List<ObjectiveCondition>
                {
                    new ObjectiveConditionParentActive(),
                    new ObjectiveConditionObjectiveState(payment.Id, ObjectiveState.Completed)
                }, emptyConditions);
            var fill = new ObjectiveNode("chainrush-board-fill-markers", null,
                new List<ObjectiveCondition>
                {
                    new ObjectiveConditionParentActive(),
                    new ObjectiveConditionObjectiveState(clear.Id, ObjectiveState.Completed)
                },
                new List<ObjectiveCondition>
                {
                    new ObjectiveConditionMaterializedRegionCoverage(null, EconomyFormType.Token,
                        new List<TaxonomyTermData> { itemTag }, null, CreateBoardRegionQuery(boardCellTag), 0L, CompareOperation.Equal)
                });
            var root = new ObjectiveNode("chainrush-board-population", null,
                new List<ObjectiveCondition>
                {
                    new ObjectiveConditionEconomyMetric(new List<TaxonomyTermData> { sharedWalletTag },
                        EconomyFormType.Stack, turnToken, 1L, CompareOperation.GreaterOrEqual, null, null),
                    new ObjectiveConditionEconomyMetric(new List<TaxonomyTermData> { boardWalletTag },
                        EconomyFormType.Token, null, 0L, CompareOperation.Equal,
                        new List<TaxonomyTermData> { itemTag }, new List<TaxonomyTermData> { selectedTag }),
                    new ObjectiveConditionMaterializedRegionCoverage(null, EconomyFormType.Token,
                        new List<TaxonomyTermData> { itemTag }, null, CreateBoardRegionQuery(boardCellTag), 0L, CompareOperation.Greater)
                },
                new List<ObjectiveCondition>
                {
                    new ObjectiveConditionTargetNodesState(new List<ObjectiveNode> { payment, clear, fill })
                });
            SetField(objective, "root", root);
            SetField(objective, "completionPolicyType", ObjectiveCompletionPolicyType.ResetOnConditions);
            SetField(objective, "resetConditions", new List<ObjectiveCondition>
            {
                new ObjectiveConditionMaterializedRegionCoverage(null, EconomyFormType.Token,
                    new List<TaxonomyTermData> { itemTag }, null, CreateBoardRegionQuery(boardCellTag), 0L, CompareOperation.Greater)
            });
            EditorUtility.SetDirty(objective);
        }

        static void ConfigureBoardCleanupOperation(OrchestratorAIBrainData brain, TaxonomyTermData term,
            TaxonomyTermData content, TaxonomyTermData wallet, TaxonomyTermData selected)
        {
            var operation = new EconomyOperationDecompOpData();
            SetField(operation, "operatorId", term);
            SetField(operation, "operation", EconomyOperation.Destroy);
            SetField(operation, "selection", new EconomyEntrySelectionData(null, EconomyFormType.Token,
                new List<TaxonomyTermData> { wallet }, new List<TaxonomyTermData> { content }, null, null, null));
            brain.Operators.RemoveAll(item => item.OperatorId == term);
            brain.Operators.Add(operation);
            brain.DecisionGraph.Nodes.RemoveAll(item => item.DecisionId == "board-clear");
            var clear = CreateDecision("board-clear", OrchestrationFactType.EconomyAmount, term, false,
                OrchestrationDecompositionScopeType.GlobalObjective);
            var decrease = new CompareOperationDecisionConditionData();
            SetField(decrease, "compareOperations", new List<CompareOperation> { CompareOperation.Equal });
            SetField(decrease, "requireZeroTargetForEqual", true);
            clear.Conditions.Add(decrease);
            brain.DecisionGraph.Nodes.Insert(0, clear);
            foreach (var node in brain.DecisionGraph.Nodes)
            {
                if (!(node is OrchestrationDecisionData decision)) continue;
                bool cleanup = decision.DecisionId == "board-clear";
                bool consumption = decision.DecisionId == "board-production-input"
                    || decision.DecisionId.StartsWith("board-consume-", StringComparison.Ordinal)
                        && decision.DecisionId != "board-consume-turn";
                if (!cleanup && !consumption) continue;
                decision.Conditions.RemoveAll(condition => condition is EconomyRequiredRuntimeTagsDecisionConditionData);
                var tags = new EconomyRequiredRuntimeTagsDecisionConditionData();
                SetField(tags, cleanup ? "excludedTags" : "requiredTags", new List<TaxonomyTermData> { selected });
                decision.Conditions.Add(tags);
            }
            EditorUtility.SetDirty(brain);
        }

        [MenuItem("ChainRush/Activities/Board/Configure Population Run")]
        public static void ConfigurePopulationRun()
        {
            AgentDefinitionData agent = LoadRequired<AgentDefinitionData>(PopulationAgentPath);
            var population = (PopulationAgentData)agent.Agent;
            ConfigurePopulationSettings(population);
            SetField(agent, "matchConditions", new List<ObjectiveCondition> { CreatePopulationCoverage() });
            SetField(agent, "targetSelectionCriteria", CreatePopulationProducerCriteria());
            EditorUtility.SetDirty(agent);
            ObjectiveTemplateData objective = LoadRequired<ObjectiveTemplateData>(PopulationObjectivePath);
            var children = ((ObjectiveConditionTargetNodesState)objective.Root.SuccessConditions[0]).TargetNodes;
            foreach (ObjectiveNode child in children)
                if (child.Id == "chainrush-board-fill-markers")
                {
                    child.SuccessConditions.Clear();
                    child.SuccessConditions.Add(CreatePopulationCoverage());
                }
            EditorUtility.SetDirty(objective);
            ConfigurePopulationDecision();
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(new List<string> { PopulationAgentPath });
            ValidatePopulationProducerWiring();
        }

        internal static void ValidatePopulationProducerWiring()
        {
            var agent = LoadRequired<AgentDefinitionData>(PopulationAgentPath);
            EnsurePopulationAgentExecutor(agent, LoadRequired<CapabilityHostData>(BoardHostPath));
            var criteria = agent.TargetSelectionCriteria;
            if (criteria.Count != 2
                || !(criteria[0].Criterion is CapabilityHostCriterionData producer)
                || producer.Definition != null
                || !producer.RequiredAssetTags.Contains(LoadRequired<TaxonomyTermData>(BoardProducerTagPath))
                || !(criteria[1].Criterion is OwnerCriterionData))
                throw new InvalidOperationException("Board Population must select its participant's tagged cell producers.");
        }

        static void ConfigurePopulationDecision()
        {
            var brain = LoadRequired<OrchestratorAIBrainData>(BrainPath);
            var decision = brain.DecisionGraph.Nodes.Find(node => node.DecisionId == "board-population-agent");
            if (decision == null)
                throw new InvalidOperationException("Board Population decision is missing.");
            var condition = decision.Conditions.Find(item => item is FactTypeDecisionConditionData);
            if (condition == null)
                throw new InvalidOperationException("Board Population fact condition is missing.");
            SetField(condition, "factType", OrchestrationFactType.MaterializedRegionCoverage);
            EditorUtility.SetDirty(brain);
        }

        static ObjectiveConditionMaterializedRegionCoverage CreatePopulationCoverage() =>
            new ObjectiveConditionMaterializedRegionCoverage(null,
                EconomyFormType.Token, new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(BoardContentTagPath) }, null,
                CreateBoardRegionQuery(LoadRequired<TaxonomyTermData>(BoardCellTagPath)), 0L, CompareOperation.Equal);

        static List<EntityCriterionEntryData> CreatePopulationProducerCriteria() =>
            new List<EntityCriterionEntryData>
            {
                Required(CreateCapabilityHostCriterion(null, null,
                    new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(BoardProducerTagPath) })),
                Required(CreateOwnerCriterion())
            };

        static void ConfigurePopulationSettings(PopulationAgentData population)
        {
            var shapes = LoadBoardSpatialShapes();
            var size = new Vector3Int(1000, 1, 1000);
            var lineSize = new Vector3Int(2, 1, 1);
            SetField(population, "progress", new PopulationProgressData());
            SetField(population, "volume", new LongFlatProgressionData(16));
            SetField(population, "singleAssetPerShape", true);
            SetField(population, "distribution", new GridPopulationDistributionAlgorithmData());
            SetField(population, "workBudget", 256);
            var shapeRules = new List<PopulationShapeRuleData>
            {
                new PopulationShapeRuleData(shapes.Line, new List<SpatialShapeUsageData>
                {
                    new SpatialShapeUsageData(SpatialShapeFillType.Inside, Vector3Int.zero, lineSize, Vector3Int.zero, size, Vector3Int.zero),
                    new SpatialShapeUsageData(SpatialShapeFillType.Inside, Vector3Int.zero, lineSize, new Vector3Int(0, 90, 0), size, Vector3Int.zero)
                }, 0.5f, new IntRange(0, 8)),
                new PopulationShapeRuleData(shapes.Single, new List<SpatialShapeUsageData>
                {
                    new SpatialShapeUsageData(SpatialShapeFillType.Inside, Vector3Int.zero, Vector3Int.one, Vector3Int.zero, size, Vector3Int.zero)
                }, 0.5f, new IntRange(0, 16))
            };
            SetField(population, "releases", new List<PopulationReleaseData>
            {
                new PopulationReleaseData(new ProgressInterval(0, 0, false),
                    CreateBoardRegionQuery(LoadRequired<TaxonomyTermData>(BoardCellTagPath)), shapeRules,
                    new List<PopulationContentRuleData>
                    {
                        new PopulationContentRuleData(new PopulationCatalogContentSourceData(LoadRequired<ProductionCatalogData>(PopulationCatalogPath)), 0.25f),
                        new PopulationContentRuleData(new PopulationCatalogContentSourceData(LoadRequired<ProductionCatalogData>(BoardRoot + "/Production/BuffsPopulationCatalog.asset")), 0.25f),
                        new PopulationContentRuleData(new PopulationCatalogContentSourceData(LoadRequired<ProductionCatalogData>(BoardRoot + "/Production/SkillsPopulationCatalog.asset")), 0.25f),
                        new PopulationContentRuleData(new PopulationCatalogContentSourceData(LoadRequired<ProductionCatalogData>(BoardRoot + "/Production/GoldPopulationCatalog.asset")), 0.25f)
                    })
            });
        }

        static SpaceRegionQueryData CreateBoardRegionQuery(TaxonomyTermData tag)
        {
            return new SpaceRegionQueryData(SpaceRegionScopeType.ActivityRoot,
                new List<TaxonomyTermData> { tag }, null, null, null);
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

        static EntityCriterionEntryData Required(EntityCriterionData criterion)
        {
            return new EntityCriterionEntryData(CriterionRequirementType.Required, criterion);
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

        static ActivityTeamObjectiveData CreateTeamObjective(ObjectiveTemplateData objective)
        {
            ActivityTeamObjectiveData teamObjective = default;
            SetStructField(ref teamObjective, "template", objective);
            SetStructField(ref teamObjective, "successScoreDelta", 0);
            SetStructField(ref teamObjective, "failScoreDelta", 0);
            return teamObjective;
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
