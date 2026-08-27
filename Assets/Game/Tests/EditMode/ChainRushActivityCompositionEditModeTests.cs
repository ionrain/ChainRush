using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core;
using Core.AI;
using Core.AI.Actions;
using Core.AI.Conditions;
using Core.Activities;
using Core.Activities.Selection;
using Core.CapabilityHosts;
using Core.Economy;
using Core.Activities.GameRuntime.Installers;
using Core.Drops;
using Core.Drops.GameRuntime.Installers;
using Core.GameFlow;
using Core.GameFlow.GameRuntime;
using Core.GameFlow.GameRuntime.Installers;
using Core.GameRuntime;
using Core.GameRuntime.Installers;
using Core.Objectives;
using Core.Orchestration;
using Core.Production.Authoring;
using Core.Projection;
using Core.Scheduling;
using Core.Simulation;
using Core.SimulationControl;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChainRush.Tests.EditMode
{
    public sealed class ChainRushActivityCompositionEditModeTests
    {
        const string ActivitiesRoot = "Assets/Game/Activities";
        const string SharedRoot = ActivitiesRoot + "/Shared";
        const string AutobattleRoot = ActivitiesRoot + "/Autobattle";
        const string BoardRoot = ActivitiesRoot + "/Board";
        const string OccupancyRoot = SharedRoot + "/Spatial/Occupancy";
        const string OccupancyFamilyPath = OccupancyRoot + "/SpatialOccupancyFamily.asset";
        const string MobileSolidPath = OccupancyRoot + "/MobileSolid.asset";
        const string StaticSolidPath = OccupancyRoot + "/StaticSolid.asset";
        const string PlacementObstaclePath = OccupancyRoot + "/PlacementObstacle.asset";
        const string NonOccupyingPath = OccupancyRoot + "/NonOccupying.asset";
        const string OccupancyMatrixPath = OccupancyRoot + "/SpatialOccupancyMatrix.asset";
        const string FoundationInstallerPath =
            "Assets/Game/Runtime/Installers/ChainRushGameplayFoundationInstaller.asset";
        const string TaxonomyInstallerPath =
            "Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset";

        const string AutobattleActivityPath =
            AutobattleRoot + "/Definition/AutobattleActivity.asset";
        const string BoardActivityPath =
            BoardRoot + "/Definition/BoardActivity.asset";
        const string AutobattleFlowPath =
            AutobattleRoot + "/GameFlow/AutobattleFlow.asset";
        const string BoardFlowPath =
            BoardRoot + "/GameFlow/BoardFlow.asset";
        const string AutobattleSpacePath =
            AutobattleRoot + "/Space/AutobattleSpace.prefab";
        const string BoardWaterProjectionPath =
            BoardRoot + "/Projection/WaterBoardBase.prefab";
        const string BoardPlaneShapePath =
            BoardRoot + "/Space/Shapes/BoardPlane.asset";
        const string SingleShapePath =
            BoardRoot + "/Space/Shapes/Single.asset";
        const string LineShapePath =
            BoardRoot + "/Space/Shapes/Line.asset";
        const string CornerShapePath =
            BoardRoot + "/Space/Shapes/Corner.asset";
        const string BoxShapePath =
            BoardRoot + "/Space/Shapes/Box.asset";
        const string ZigzagShapePath =
            BoardRoot + "/Space/Shapes/Zigzag.asset";
        const string SpawnAreaShapePath =
            AutobattleRoot + "/Space/Shapes/SpawnArea.asset";
        const string RuntimeProfilePath =
            "Assets/Game/Runtime/Host/ChainRushGameRuntimeProfile.asset";
        const string ActivityDiplomacyModulePath =
            "Assets/Game/Runtime/Diplomacy/ActivityDiplomacyModule.asset";
        const string CapabilityHostDiplomacyModulePath =
            "Assets/Game/Runtime/Diplomacy/CapabilityHostDiplomacyModule.asset";
        const string StartupPlanPath =
            "Assets/Game/Runtime/Startup/ChainRushGameStartupPlan.asset";
        const string BoardObjectivePath =
            BoardRoot + "/Objectives/BoardPopulationObjective.asset";
        const string BoardPopulationAgentPath =
            BoardRoot + "/Agents/BoardPopulationAgent.asset";
        const string BoardSelectionAgentPath =
            BoardRoot + "/Agents/BoardSelectionAgent.asset";
        const string BoardBrainPath = BoardRoot + "/Orchestration/BoardBrain.asset";
        const string BoardProductionInputOperatorPath =
            BoardRoot + "/Orchestration/Taxonomy/BoardProductionInputOperator.asset";
        const string BoardSelectionObjectivePath =
            BoardRoot + "/Objectives/BoardSelectionObjective.asset";
        const string BoardMergeObjectivePath =
            BoardRoot + "/Objectives/BoardMergeObjective.asset";
        const string BoardPopulationProducerPath =
            BoardRoot + "/Economy/BoardPopulationProducer.asset";
        const string BoardHostPath =
            BoardRoot + "/Economy/BoardHost.asset";
        const string BoardWaterBasePath =
            BoardRoot + "/Economy/WaterBoardBase.asset";
        const string BoardRefreshRecipePath =
            BoardRoot + "/Production/BoardRefreshRecipe.asset";
        const string BoardWaterBaseRecipePath =
            BoardRoot + "/Production/WaterBoardBaseRecipe.asset";
        const string BoardPopulationProductionPath =
            BoardRoot + "/Production/BoardPopulationProduction.asset";
        const string BoardMergeProductionPath =
            BoardRoot + "/Production/BoardProduction.asset";
        const string BoardMergeCatalogPath =
            BoardRoot + "/Production/BoardProductionCatalog.asset";
        const string BoardMergeRecipe1Path =
            BoardRoot + "/Production/BoardMergeRecipe1.asset";
        const string BoardMergeRecipe2Path =
            BoardRoot + "/Production/BoardMergeRecipe2.asset";
        const string BoardMergeRecipe3Path =
            BoardRoot + "/Production/BoardMergeRecipe3.asset";
        const string BoardMergeRecipe4Path =
            BoardRoot + "/Production/BoardMergeRecipe4.asset";
        const string BoardMergeSelectionPath =
            BoardRoot + "/Taxonomy/BoardMergeSelection.asset";
        const string BoardMergeSelectedPath =
            BoardRoot + "/Taxonomy/BoardMergeSelected.asset";
        const string BoardUIPrefabPath =
            BoardRoot + "/UI/BoardUI.prefab";
        const string BoardOrchestrationPath =
            BoardRoot + "/Orchestration/BoardOrchestration.asset";
        const string SharedWalletTagPath =
            SharedRoot + "/Economy/ActivityWalletTag.asset";
        const string BoardWalletTagPath =
            BoardRoot + "/Economy/BoardWalletTag.asset";
        const string BoardTurnTokenPath =
            SharedRoot + "/Economy/BoardTurnToken.asset";
        const string ExperiencePath =
            SharedRoot + "/Economy/Experience.asset";
        const string ExperienceToTurnTokenRecipePath =
            AutobattleRoot + "/Production/ExperienceToTurnTokenRecipe.asset";
        const string WaterUnitPath =
            SharedRoot + "/Units/Water/WaterUnit.asset";
        const string PlayerSpawnerPath =
            AutobattleRoot + "/Economy/PlayerSpawner.asset";
        const string EnemySpawnerPath =
            AutobattleRoot + "/Economy/EnemySpawner.asset";
        const string EnemyPath =
            AutobattleRoot + "/Economy/BugBrownSmall.asset";
        const string ExperienceDropPath =
            AutobattleRoot + "/Economy/ExperienceDrop.asset";
        const string ExperienceCollectorPath =
            AutobattleRoot + "/Economy/ExperienceCollector.asset";
        const string DeploymentRecipePath =
            AutobattleRoot + "/Production/WaterUnitDeploymentRecipe.asset";
        const string EnemyWaveRecipePath =
            AutobattleRoot + "/Production/EnemyWaveRecipe.asset";
        const string PlayerProductionPath =
            AutobattleRoot + "/Production/PlayerProduction.asset";
        const string EnemyProductionPath =
            AutobattleRoot + "/Production/EnemyWaveProduction.asset";
        const string PlayerBrainPath =
            AutobattleRoot + "/Orchestration/PlayerBrain.asset";
        const string EnemyBrainPath =
            AutobattleRoot + "/Orchestration/EnemyBrain.asset";
        const string AwaitFactOperatorPath =
            AutobattleRoot + "/Orchestration/Taxonomy/AwaitFactOperator.asset";
        const string DropProfilePath =
            AutobattleRoot + "/Drops/ExperienceDropProfile.asset";
        const string AlliedCombatBrainPath = AutobattleRoot + "/AI/AlliedCombatBrain.asset";
        const string EnemyCombatBrainPath = AutobattleRoot + "/AI/EnemyCombatBrain.asset";
        const string CollectionBrainPath =
            AutobattleRoot + "/AI/ExperienceCollectorBrain.asset";
        const string SearchStatePath =
            AutobattleRoot + "/AI/Taxonomy/SearchState.asset";
        const string WaitingStatePath =
            AutobattleRoot + "/AI/Taxonomy/WaitingState.asset";
        const string CollectionStatePath =
            AutobattleRoot + "/AI/Taxonomy/CollectState.asset";
        const string CollectionSkillPath =
            AutobattleRoot + "/Skills/ExperienceCollectionSkill.asset";
        const string MovementPath =
            AutobattleRoot + "/Movement/UnitMovement.asset";
        const string ExperienceDropPrefabPath =
            AutobattleRoot + "/Projection/ExperienceDrop.prefab";
        const string ExperienceCollectorPrefabPath =
            AutobattleRoot + "/Projection/ExperienceCollector.prefab";
        const string ExperienceUIPrefabPath =
            AutobattleRoot + "/UI/ExperienceUI.prefab";
        const string IntegrationScenePath =
            "Assets/Game/Scenes/Integration/ChainRushFrameworkIntegration.unity";
        const string ExperienceProgressTargetPath =
            AutobattleRoot + "/Taxonomy/ExperienceProgressTarget.asset";
        const string IntegrationRuntimeTagPath =
            AutobattleRoot + "/Definition/IntegrationAutobattle.asset";

        const string AutobattleActivityTypeId = "chainrush.activity-type.autobattle";
        const string BoardActivityTypeId = "chainrush.activity-type.board";
        const string BoardActivationTermId = "chainrush.activity.activation.board";
        const string BoardCellTagId = "chainrush.board.cell";

        [Test]
        public void ActivityAssets_DoNotReferenceOtherActivityDirectly()
        {
            Assert.IsTrue(AssetDatabase.IsValidFolder(SharedRoot), $"Missing folder: {SharedRoot}");
            Assert.IsTrue(AssetDatabase.IsValidFolder(AutobattleRoot), $"Missing folder: {AutobattleRoot}");
            Assert.IsTrue(AssetDatabase.IsValidFolder(BoardRoot), $"Missing folder: {BoardRoot}");

            AssertFolderDependenciesStayWithinBoundary(AutobattleRoot, BoardRoot);
            AssertFolderDependenciesStayWithinBoundary(BoardRoot, AutobattleRoot);
        }

        [Test]
        public void ActivityDefinitions_MatchRootAndChildContract()
        {
            ActivityData autobattle = LoadRequiredAsset<ActivityData>(AutobattleActivityPath);
            ActivityData board = LoadRequiredAsset<ActivityData>(BoardActivityPath);

            AssertActivitySchedule(autobattle);
            AssertActivitySchedule(board);
            AssertSimulationPolicy(autobattle);
            AssertSimulationPolicy(board);

            Assert.NotNull(autobattle.ActivityType);
            Assert.AreEqual(AutobattleActivityTypeId, autobattle.ActivityType.Id);
            Assert.AreEqual(2, autobattle.Teams.Count);
            Assert.AreEqual(1, autobattle.Teams[0].SlotCount);
            Assert.AreEqual(1, autobattle.Teams[1].SlotCount);
            Assert.IsTrue(autobattle.AllowBots);
            Assert.AreEqual(ActivityEndMode.Manual, autobattle.Result.EndMode);
            Assert.AreEqual(2, autobattle.Teams[0].Objectives.Count);
            Assert.AreEqual(1, autobattle.Teams[1].Objectives.Count);
            Assert.AreEqual(1, autobattle.Teams[0].Features.Count);
            Assert.AreEqual(1, autobattle.Teams[1].Features.Count);
            Assert.AreEqual(1, autobattle.WorldWallets.Count);
            AssertTopology(
                autobattle.Topology,
                TopologyType.Free,
                TopologyCoordinateOccupationPolicy.SingleOccupant);

            Assert.NotNull(board.ActivityType);
            Assert.AreEqual(BoardActivityTypeId, board.ActivityType.Id);
            Assert.AreEqual(1, board.Teams.Count);
            Assert.AreEqual(1, board.Teams[0].SlotCount);
            Assert.IsFalse(board.AllowBots);
            Assert.AreEqual(ActivityEndMode.Manual, board.Result.EndMode);
            Assert.AreEqual(3, board.Teams[0].Objectives.Count);
            Assert.AreEqual(1, board.Teams[0].Features.Count);
            AssertTopology(
                board.Topology,
                TopologyType.Grid,
                TopologyCoordinateOccupationPolicy.SingleOccupant);

            Assert.AreEqual(1, autobattle.Teams[0].Wallets.Count);
            Assert.AreEqual(1, autobattle.Teams[1].Wallets.Count);
            Assert.AreEqual(2, board.Teams[0].Wallets.Count);
            Assert.AreSame(
                autobattle.Teams[0].Wallets[0].Wallet,
                autobattle.Teams[1].Wallets[0].Wallet);
            Assert.AreSame(
                autobattle.Teams[0].Wallets[0].Wallet,
                board.Teams[0].Wallets[0].Wallet);

            Assert.IsInstanceOf<ActivityPrefabSpaceData>(autobattle.Space);
            var autobattleSpace = (ActivityPrefabSpaceData)autobattle.Space;
            Assert.IsTrue(autobattleSpace.PrefabReference.RuntimeKeyIsValid());
            Assert.AreEqual(0, autobattleSpace.MarkerProviders.Count);

            Assert.IsInstanceOf<ActivityUISpaceData>(board.Space);
            var boardSpace = (ActivityUISpaceData)board.Space;
            SpatialShapeData boardPlane =
                LoadRequiredAsset<SpatialShapeData>(BoardPlaneShapePath);
            Assert.NotNull(boardSpace.Presentation);
            Assert.AreEqual(1, boardSpace.MarkerProviders.Count);
            Assert.IsInstanceOf<SpatialShapeProviderData>(boardSpace.MarkerProviders[0]);
            var boardProvider = (SpatialShapeProviderData)boardSpace.MarkerProviders[0];
            Assert.AreSame(boardPlane, boardProvider.Shape);
            Assert.AreEqual(new Vector3Int(4, 1, 4), boardProvider.Usage.Size);
            Assert.AreEqual(Vector3Int.zero, boardProvider.Usage.Position);
            Assert.AreEqual(new Vector3Int(1000, 1, 1000), boardProvider.Usage.CellSize);
            Assert.AreEqual(Vector3Int.zero, boardProvider.Usage.CellOffset);
            Assert.AreEqual(1, boardProvider.MarkerTags.Count);
            Assert.AreEqual(BoardCellTagId, boardProvider.MarkerTags[0].Id);
            Assert.AreEqual(1, boardSpace.ProjectionMarkerTags.Count);
            Assert.AreSame(boardProvider.MarkerTags[0], boardSpace.ProjectionMarkerTags[0]);
            Assert.NotNull(boardSpace.ProjectionSettings);
            Assert.IsTrue(boardSpace.ProjectionSettings.IsValid);
        }

        [Test]
        public void SpatialOccupancyAssets_CentralizeAllActivityHostConflicts()
        {
            TaxonomyFamilyData family = LoadRequiredAsset<TaxonomyFamilyData>(OccupancyFamilyPath);
            TaxonomyTermData mobileSolid = LoadRequiredAsset<TaxonomyTermData>(MobileSolidPath);
            TaxonomyTermData staticSolid = LoadRequiredAsset<TaxonomyTermData>(StaticSolidPath);
            TaxonomyTermData placementObstacle =
                LoadRequiredAsset<TaxonomyTermData>(PlacementObstaclePath);
            TaxonomyTermData nonOccupying = LoadRequiredAsset<TaxonomyTermData>(NonOccupyingPath);
            SpatialOccupancyMatrixData matrix =
                LoadRequiredAsset<SpatialOccupancyMatrixData>(OccupancyMatrixPath);

            Assert.AreEqual("SpatialOccupancy", family.Id);
            Assert.AreEqual(TaxonomyCardinality.Multiple, family.Cardinality);
            AssertOccupancyTerm(mobileSolid, family, "MobileSolid");
            AssertOccupancyTerm(staticSolid, family, "StaticSolid");
            AssertOccupancyTerm(placementObstacle, family, "PlacementObstacle");
            AssertOccupancyTerm(nonOccupying, family, "NonOccupying");

            Assert.AreSame(family, matrix.OccupancyFamily);
            Assert.AreEqual(4, matrix.Rows.Count);
            AssertOccupancyRow(matrix.Rows[0], mobileSolid, mobileSolid, staticSolid);
            AssertOccupancyRow(
                matrix.Rows[1],
                staticSolid,
                mobileSolid,
                staticSolid,
                placementObstacle);
            AssertOccupancyRow(
                matrix.Rows[2],
                placementObstacle,
                staticSolid,
                placementObstacle);
            AssertOccupancyRow(matrix.Rows[3], nonOccupying);

            TaxonomyRuntimeInstallerData taxonomyInstaller =
                LoadRequiredAsset<TaxonomyRuntimeInstallerData>(TaxonomyInstallerPath);
            CollectionAssert.Contains(
                ReadObjectReferences<TaxonomyFamilyData>(taxonomyInstaller, "families"),
                family);
            List<TaxonomyTermData> installedTerms =
                ReadObjectReferences<TaxonomyTermData>(taxonomyInstaller, "terms");
            CollectionAssert.Contains(installedTerms, mobileSolid);
            CollectionAssert.Contains(installedTerms, staticSolid);
            CollectionAssert.Contains(installedTerms, placementObstacle);
            CollectionAssert.Contains(installedTerms, nonOccupying);

            GameplayFoundationInstallerData foundationInstaller =
                LoadRequiredAsset<GameplayFoundationInstallerData>(FoundationInstallerPath);
            var serializedInstaller = new SerializedObject(foundationInstaller);
            SerializedProperty matrixProperty =
                serializedInstaller.FindProperty("spatialOccupancyMatrix");
            Assert.NotNull(matrixProperty);
            Assert.AreSame(matrix, matrixProperty.objectReferenceValue);

            AssertHostOccupancyTag(EnemyPath, family, mobileSolid);
            AssertHostOccupancyTag(WaterUnitPath, family, mobileSolid);
            AssertHostOccupancyTag(PlayerSpawnerPath, family, staticSolid);
            AssertHostOccupancyTag(EnemySpawnerPath, family, staticSolid);
            AssertHostOccupancyTag(ExperienceDropPath, family, placementObstacle);
            AssertHostOccupancyTag(BoardWaterBasePath, family, placementObstacle);
            AssertHostOccupancyTag(ExperienceCollectorPath, family, nonOccupying);
            AssertHostOccupancyTag(BoardHostPath, family, nonOccupying);
            AssertHostOccupancyTag(BoardPopulationProducerPath, family, nonOccupying);
        }

        [Test]
        public void BoardVerticalSlice_WiresObjectivePopulationAndMergeProduction()
        {
            ActivityData board = LoadRequiredAsset<ActivityData>(BoardActivityPath);
            ObjectiveTemplateData objective =
                LoadRequiredAsset<ObjectiveTemplateData>(BoardObjectivePath);
            ObjectiveTemplateData selectionObjective =
                LoadRequiredAsset<ObjectiveTemplateData>(BoardSelectionObjectivePath);
            ObjectiveTemplateData mergeObjective =
                LoadRequiredAsset<ObjectiveTemplateData>(BoardMergeObjectivePath);
            ActivityAgentDefinitionData populationAgent =
                LoadRequiredAsset<ActivityAgentDefinitionData>(BoardPopulationAgentPath);
            ActivityAgentDefinitionData selectionAgent =
                LoadRequiredAsset<ActivityAgentDefinitionData>(BoardSelectionAgentPath);
            OrchestratorAIBrainData boardBrain =
                LoadRequiredAsset<OrchestratorAIBrainData>(BoardBrainPath);
            TaxonomyTermData productionInputOperator =
                LoadRequiredAsset<TaxonomyTermData>(BoardProductionInputOperatorPath);
            CapabilityHostData boardHost =
                LoadRequiredAsset<CapabilityHostData>(BoardHostPath);
            CapabilityHostData populationProducer =
                LoadRequiredAsset<CapabilityHostData>(BoardPopulationProducerPath);
            CapabilityHostData waterBase =
                LoadRequiredAsset<CapabilityHostData>(BoardWaterBasePath);
            CapabilityHostData waterUnit = LoadRequiredAsset<CapabilityHostData>(WaterUnitPath);
            EconomyAssetData turnToken = LoadRequiredAsset<EconomyAssetData>(BoardTurnTokenPath);
            TaxonomyTermData sharedWalletTag =
                LoadRequiredAsset<TaxonomyTermData>(SharedWalletTagPath);
            TaxonomyTermData boardWalletTag =
                LoadRequiredAsset<TaxonomyTermData>(BoardWalletTagPath);
            TaxonomyTermData mergeSelection =
                LoadRequiredAsset<TaxonomyTermData>(BoardMergeSelectionPath);
            TaxonomyTermData mergeSelected =
                LoadRequiredAsset<TaxonomyTermData>(BoardMergeSelectedPath);
            SpatialShapeData boardPlane =
                LoadRequiredAsset<SpatialShapeData>(BoardPlaneShapePath);
            SpatialShapeData singleShape =
                LoadRequiredAsset<SpatialShapeData>(SingleShapePath);
            SpatialShapeData lineShape =
                LoadRequiredAsset<SpatialShapeData>(LineShapePath);
            SpatialShapeData cornerShape =
                LoadRequiredAsset<SpatialShapeData>(CornerShapePath);
            SpatialShapeData boxShape =
                LoadRequiredAsset<SpatialShapeData>(BoxShapePath);
            SpatialShapeData zigzagShape =
                LoadRequiredAsset<SpatialShapeData>(ZigzagShapePath);
            ProductionRecipeData refreshRecipe =
                LoadRequiredAsset<ProductionRecipeData>(BoardRefreshRecipePath);
            ProductionRecipeData waterBaseRecipe =
                LoadRequiredAsset<ProductionRecipeData>(BoardWaterBaseRecipePath);
            ProductionData populationProduction =
                LoadRequiredAsset<ProductionData>(BoardPopulationProductionPath);
            ProductionData mergeProduction =
                LoadRequiredAsset<ProductionData>(BoardMergeProductionPath);
            ProductionCatalogData mergeCatalog =
                LoadRequiredAsset<ProductionCatalogData>(BoardMergeCatalogPath);
            List<ProductionRecipeData> mergeRecipes = new List<ProductionRecipeData>
            {
                LoadRequiredAsset<ProductionRecipeData>(BoardMergeRecipe4Path),
                LoadRequiredAsset<ProductionRecipeData>(BoardMergeRecipe3Path),
                LoadRequiredAsset<ProductionRecipeData>(BoardMergeRecipe2Path),
                LoadRequiredAsset<ProductionRecipeData>(BoardMergeRecipe1Path),
            };
            ActivityOrchestrationConfigData orchestration =
                LoadRequiredAsset<ActivityOrchestrationConfigData>(BoardOrchestrationPath);
            TaxonomyFamilyData occupancyFamily =
                LoadRequiredAsset<TaxonomyFamilyData>(OccupancyFamilyPath);
            TaxonomyTermData waterTag = waterBase.Tags.Single(
                tag => tag != null && tag.Family != occupancyFamily);
            TaxonomyRuntimeInstallerData taxonomyInstaller =
                LoadRequiredAsset<TaxonomyRuntimeInstallerData>(
                    "Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset");

            CollectionAssert.AreEquivalent(
                new[] { objective, selectionObjective, mergeObjective },
                board.Teams[0].Objectives.Select(entry => entry.Template).ToList());
            Assert.AreEqual(ObjectiveCompletionPolicyType.Reset, objective.CompletionPolicyType);
            Assert.AreEqual(
                ObjectiveCompletionPolicyType.Reset,
                selectionObjective.CompletionPolicyType);
            Assert.AreEqual(
                ObjectiveCompletionPolicyType.Reset,
                mergeObjective.CompletionPolicyType);
            Assert.AreEqual(
                ActivityAgentStopPolicyType.AssignmentSuccess,
                populationAgent.StopPolicyType);
            Assert.AreEqual(
                ActivityAgentStopPolicyType.AssignmentSuccess,
                selectionAgent.StopPolicyType);
            Assert.AreEqual(6, boardBrain.Operators.Count);
            List<AgentDecompOpData> agentOperators = boardBrain.Operators
                .OfType<AgentDecompOpData>()
                .ToList();
            Assert.AreEqual(2, agentOperators.Count);
            CollectionAssert.AreEquivalent(
                new[] { populationAgent, selectionAgent },
                agentOperators.Select(operation => operation.AgentDefinition).ToList());
            ProductionInputConsumptionDecompOpData productionInput = boardBrain.Operators
                .OfType<ProductionInputConsumptionDecompOpData>()
                .Single();
            Assert.AreSame(productionInputOperator, productionInput.OperatorId);
            OrchestrationDecisionData productionInputDecision = boardBrain.DecisionGraph.Nodes
                .OfType<OrchestrationDecisionData>()
                .Single(decision => decision.DecisionId == "board-production-input");
            Assert.AreSame(productionInputOperator, productionInputDecision.OperatorId);
            Assert.IsEmpty(
                productionInputDecision.Conditions.OfType<AgentMatchDecisionConditionData>());
            ScopeDecisionConditionData productionInputScope = productionInputDecision.Conditions
                .OfType<ScopeDecisionConditionData>()
                .Single();
            Assert.AreEqual(
                OrchestrationDecompositionScopeType.GlobalObjective,
                ReadField<OrchestrationDecompositionScopeType>(productionInputScope, "scopeType"));
            MaterializedEntityProductionDecompOpData materializedProduction = boardBrain.Operators
                .OfType<MaterializedEntityProductionDecompOpData>()
                .Single();
            OrchestrationDecisionData materializedProductionDecision = boardBrain.DecisionGraph.Nodes
                .OfType<OrchestrationDecisionData>()
                .Single(decision => decision.DecisionId == "board-materialized-production");
            Assert.AreSame(materializedProduction.OperatorId, materializedProductionDecision.OperatorId);
            Assert.AreEqual(
                OrchestrationDecompositionScopeType.GlobalObjective,
                ReadField<OrchestrationDecompositionScopeType>(
                    materializedProductionDecision.Conditions
                        .OfType<ScopeDecisionConditionData>()
                        .Single(),
                    "scopeType"));
            CollectionAssert.Contains(
                ReadObjectReferences<TaxonomyTermData>(taxonomyInstaller, "terms"),
                productionInputOperator);
            Assert.IsFalse(ReadObjectReferences<TaxonomyTermData>(taxonomyInstaller, "terms")
                .Any(term => term != null
                    && term.Id == "chainrush.orchestration.board.agent.production"));
            Assert.AreSame(orchestration, board.Teams[0].Features.Single());
            CollectionAssert.Contains(
                ReadObjectReferences<TaxonomyTermData>(taxonomyInstaller, "terms"),
                waterTag,
                "The taxonomy installer must register the Water Board item term before economy queries run.");

            Assert.AreEqual(2, objective.Root.ActivateConditions.Count);
            List<ObjectiveConditionEconomyMetric> populationActivations = objective.Root
                .ActivateConditions
                .OfType<ObjectiveConditionEconomyMetric>()
                .ToList();
            Assert.AreEqual(2, populationActivations.Count);
            ObjectiveConditionEconomyMetric turnTokenActivation = populationActivations
                .Single(condition => condition.Asset == turnToken);
            Assert.AreEqual(EconomyFormType.Stack, turnTokenActivation.FormType);
            Assert.AreEqual(
                CompareOperation.GreaterOrEqual,
                turnTokenActivation.CompareOperation);
            Assert.AreEqual(1L, turnTokenActivation.TargetValue);
            Assert.AreEqual(1, turnTokenActivation.WalletTags.Count);
            Assert.AreSame(sharedWalletTag, turnTokenActivation.WalletTags[0]);
            ObjectiveConditionEconomyMetric mergeCompleteActivation = populationActivations
                .Single(condition => condition.Asset == waterBase);
            AssertMergeSelectionMetric(
                mergeCompleteActivation,
                waterBase,
                boardWalletTag,
                mergeSelected,
                0L,
                CompareOperation.Equal);

            Assert.AreEqual(1, objective.Root.SuccessConditions.Count);
            var success = objective.Root.SuccessConditions.Single()
                as ObjectiveConditionMarkerAvailability;
            Assert.NotNull(success);
            Assert.AreSame(waterBase, success.EconomyAsset);
            Assert.AreEqual(EconomyFormType.Token, success.EconomyFormType);
            Assert.AreEqual(CompareOperation.Equal, success.CompareOperation);
            Assert.AreEqual(0L, success.TargetValue);
            Assert.AreEqual(BoardCellTagId, success.MarkerTags.Single().Id);

            var selectionActivation = selectionObjective.Root.ActivateConditions.Single()
                as ObjectiveConditionSelectionRequest;
            var selectionSuccess = selectionObjective.Root.SuccessConditions.Single()
                as ObjectiveConditionSelectionRequest;
            Assert.NotNull(selectionActivation);
            Assert.NotNull(selectionSuccess);
            Assert.AreSame(mergeSelection, selectionActivation.RequestType);
            Assert.AreEqual(CompareOperation.GreaterOrEqual, selectionActivation.CompareOperation);
            Assert.AreEqual(1L, selectionActivation.TargetValue);
            Assert.AreSame(mergeSelection, selectionSuccess.RequestType);
            Assert.AreEqual(CompareOperation.Equal, selectionSuccess.CompareOperation);
            Assert.AreEqual(0L, selectionSuccess.TargetValue);

            var mergeActivation = mergeObjective.Root.ActivateConditions.Single()
                as ObjectiveConditionEconomyMetric;
            var mergeSuccess = mergeObjective.Root.SuccessConditions.Single()
                as ObjectiveConditionEconomyMetric;
            Assert.NotNull(mergeActivation);
            Assert.NotNull(mergeSuccess);
            AssertMergeSelectionMetric(
                mergeActivation,
                waterBase,
                boardWalletTag,
                mergeSelected,
                1L,
                CompareOperation.GreaterOrEqual);
            AssertMergeSelectionMetric(
                mergeSuccess,
                waterBase,
                boardWalletTag,
                mergeSelected,
                0L,
                CompareOperation.Equal);

            Assert.AreEqual(3, orchestration.Modules.Count);
            Assert.IsInstanceOf<EconomyStateOrchestrationModuleData>(orchestration.Modules[0]);
            Assert.IsInstanceOf<ProductionStateOrchestrationModuleData>(orchestration.Modules[1]);
            Assert.IsInstanceOf<ProjectionStateOrchestrationModuleData>(orchestration.Modules[2]);

            Assert.IsInstanceOf<PopulationActivityOrchestrationAgentData>(populationAgent.Agent);
            Assert.IsInstanceOf<SelectionAgentData>(selectionAgent.Agent);
            Assert.AreEqual(
                ObjectiveCommandFailurePolicyType.FailObjective,
                populationAgent.CommandFailurePolicyType);
            var selection = (SelectionAgentData)selectionAgent.Agent;
            CollectionAssert.AreEqual(
                new[] { mergeSelected },
                selection.ResultTags);
            Assert.AreEqual(4, selectionAgent.TargetSelectionCriteria.Count);
            var targetMaterialization = selectionAgent.TargetSelectionCriteria
                .OfType<MaterializedEntitySelectionCriterionData>()
                .Single();
            Assert.IsNull(targetMaterialization.EconomyAsset);
            Assert.AreEqual(EconomyFormType.Token, targetMaterialization.EconomyFormType);
            CollectionAssert.AreEqual(
                new[] { waterTag },
                targetMaterialization.RequiredAssetTags);
            Assert.AreEqual(
                ActivityAgentOwnerSelectionType.ParticipantOwner,
                selectionAgent.TargetSelectionCriteria
                    .OfType<EntityOwnerSelectionCriterionData>()
                    .Single()
                    .OwnerSelectionType);
            Assert.AreEqual(
                1,
                selectionAgent.TargetSelectionCriteria.OfType<SameAssetCriterionData>().Count());
            var distanceCriterion = selectionAgent.TargetSelectionCriteria
                .OfType<StepDistanceCriterionData>()
                .Single();
            Assert.AreEqual(1000, distanceCriterion.MinimumDistance);
            Assert.AreEqual(1000, distanceCriterion.MaximumDistance);
            var population = (PopulationActivityOrchestrationAgentData)populationAgent.Agent;
            Assert.NotNull(population.Planner);
            Assert.AreSame(refreshRecipe, population.CompletionRecipe);
            Assert.AreEqual(BoardCellTagId, population.MarkerTags.Single().Id);
            Assert.AreSame(boardWalletTag, population.ShapeWalletTags.Single());
            List<ActivityWalletSeedEntryData> shapeSeeds = board.Teams[0].Wallets
                .SelectMany(wallet => wallet.Seed)
                .Where(seed => seed.Seed.Asset is SpatialShapeData)
                .ToList();
            Assert.AreEqual(6, shapeSeeds.Count);
            CollectionAssert.AreEquivalent(
                new SpatialShapeData[]
                {
                    boardPlane,
                    singleShape,
                    lineShape,
                    cornerShape,
                    boxShape,
                    zigzagShape,
                },
                shapeSeeds.Select(seed => seed.Seed.Asset).ToList());
            Assert.IsTrue(shapeSeeds.All(seed =>
                seed.Seed.FormType == EconomyFormType.Stack
                && seed.Seed.Amount == 1L
                && seed.MaterializationType == ActivitySeedMaterializationType.None));
            var planner = new SerializedObject(population.Planner);
            SerializedProperty patternRules = planner.FindProperty("patternRules");
            Assert.NotNull(patternRules);
            Assert.AreEqual(2, patternRules.arraySize);
            Assert.AreSame(
                lineShape,
                patternRules.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("shape")
                    .objectReferenceValue);
            Assert.AreSame(
                singleShape,
                patternRules.GetArrayElementAtIndex(1)
                    .FindPropertyRelative("shape")
                    .objectReferenceValue);
            var producerCriterion = populationAgent.ExecutorSelectionCriteria
                .OfType<MaterializedEntitySelectionCriterionData>()
                .Single();
            Assert.AreSame(boardHost, producerCriterion.EconomyAsset);
            Assert.Contains(CapabilityHostType.ProductionOwner, populationProducer.Capabilities
                .Select(entry => entry.CapabilityType)
                .ToList());
            Assert.IsTrue(populationProducer.WalletEntries
                .SelectMany(entry => entry.Seed)
                .Any(seed => seed.Asset == populationProduction
                    && seed.FormType == EconomyFormType.Stack
                    && seed.Amount == 1L));

            Assert.AreEqual(1, refreshRecipe.Inputs.Count);
            AssertEconomyOperation(
                refreshRecipe.Inputs[0],
                turnToken,
                EconomyFormType.Stack,
                1L,
                sharedWalletTag);
            Assert.AreEqual(0, refreshRecipe.Outputs.Count);

            Assert.AreEqual(0, waterBaseRecipe.Inputs.Count);
            Assert.AreEqual(1, waterBaseRecipe.Outputs.Count);
            AssertEconomyOutput(
                waterBaseRecipe.Outputs[0],
                waterBase,
                EconomyFormType.Token,
                1L,
                boardWalletTag);
            Assert.AreEqual(BoardCellTagId, populationProduction.MaterializationProviderType.Id);

            for (int recipeIndex = 0; recipeIndex < mergeRecipes.Count; recipeIndex++)
            {
                ProductionRecipeData mergeRecipe = mergeRecipes[recipeIndex];
                long selectedAmount = 4L - recipeIndex;
                Assert.AreEqual(1, mergeRecipe.Inputs.Count);
                AssertEconomyOperation(
                    mergeRecipe.Inputs[0],
                    waterBase,
                    EconomyFormType.Token,
                    selectedAmount,
                    boardWalletTag);
                CollectionAssert.AreEqual(
                    new[] { mergeSelected },
                    mergeRecipe.Inputs[0].RequiredRuntimeTags);

                Assert.AreEqual(1, mergeRecipe.Outputs.Count);
                AssertEconomyOutput(
                    mergeRecipe.Outputs[0],
                    waterUnit,
                    EconomyFormType.Stack,
                    1L,
                    sharedWalletTag);
            }

            CollectionAssert.AreEqual(
                mergeRecipes,
                mergeCatalog.Entries.Select(entry => entry.Recipe).ToList());
            CollectionAssert.AreEqual(
                new[] { mergeCatalog },
                mergeProduction.SupportedCatalogs);
            Assert.IsNull(mergeProduction.MaterializationProviderType);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    CapabilityHostType.SelectionOwner,
                    CapabilityHostType.ProductionOwner,
                },
                boardHost.Capabilities.Select(entry => entry.CapabilityType).ToList());

            CollectionAssert.AreEquivalent(
                new[]
                {
                    CapabilityHostType.SkillOwner,
                    CapabilityHostType.MovementOwner,
                    CapabilityHostType.AIBrainOwner,
                },
                waterUnit.Capabilities.Select(entry => entry.CapabilityType));
            Assert.IsTrue(waterUnit.ProjectionPrefabReference.RuntimeKeyIsValid());
        }

        [Test]
        public void ExperienceToTurnTokenRecipe_UsesSharedWalletAndStepProgression()
        {
            EconomyAssetData experience = LoadRequiredAsset<EconomyAssetData>(ExperiencePath);
            EconomyAssetData turnToken =
                LoadRequiredAsset<EconomyAssetData>(BoardTurnTokenPath);
            TaxonomyTermData sharedWalletTag =
                LoadRequiredAsset<TaxonomyTermData>(SharedWalletTagPath);
            ProductionRecipeData recipe =
                LoadRequiredAsset<ProductionRecipeData>(ExperienceToTurnTokenRecipePath);
            EconomyDefinitionsInstallerData economyInstaller =
                LoadRequiredAsset<EconomyDefinitionsInstallerData>(
                    "Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset");

            List<EconomyAssetData> registeredAssets =
                ReadObjectReferences<EconomyAssetData>(economyInstaller, "assets");
            CollectionAssert.Contains(registeredAssets, experience);
            CollectionAssert.Contains(registeredAssets, recipe);

            Assert.AreEqual(1, recipe.Inputs.Count);
            ProductionInputData input = recipe.Inputs[0];
            AssertEconomyOperation(
                input,
                experience,
                EconomyFormType.Stack,
                6L,
                sharedWalletTag);
            Assert.IsInstanceOf<LongStepProgressionData>(input.AmountProgression);
            var progression = (LongStepProgressionData)input.AmountProgression;
            Assert.AreEqual(6L, progression.BaseValue);
            Assert.AreEqual(2L, progression.FirstStep);
            Assert.AreEqual(1L, progression.StepDelta);

            long[] expectedAmounts = { 6L, 8L, 11L, 15L, 20L };
            for (int index = 0; index < expectedAmounts.Length; index++)
            {
                Assert.IsTrue(
                    recipe.TryResolveInputs(
                        index + 1L,
                        out List<Core.Economy.Authoring.EconomyOperationData> resolved,
                        out string failure),
                    failure);
                Assert.AreEqual(1, resolved.Count);
                Assert.AreEqual(expectedAmounts[index], resolved[0].Amount);
            }

            Assert.AreEqual(1, recipe.Outputs.Count);
            AssertEconomyOutput(
                recipe.Outputs[0],
                turnToken,
                EconomyFormType.Stack,
                1L,
                sharedWalletTag);
        }

        [Test]
        public void AutobattleVerticalSlice_WiresWavesDeploymentDropsAndBoardOutput()
        {
            ActivityData activity = LoadRequiredAsset<ActivityData>(AutobattleActivityPath);
            ActivityData board = LoadRequiredAsset<ActivityData>(BoardActivityPath);
            CapabilityHostData playerSpawner =
                LoadRequiredAsset<CapabilityHostData>(PlayerSpawnerPath);
            CapabilityHostData enemySpawner =
                LoadRequiredAsset<CapabilityHostData>(EnemySpawnerPath);
            CapabilityHostData enemy = LoadRequiredAsset<CapabilityHostData>(EnemyPath);
            CapabilityHostData experienceDrop =
                LoadRequiredAsset<CapabilityHostData>(ExperienceDropPath);
            CapabilityHostData experienceCollector =
                LoadRequiredAsset<CapabilityHostData>(ExperienceCollectorPath);
            CapabilityHostData waterUnit = LoadRequiredAsset<CapabilityHostData>(WaterUnitPath);
            EconomyAssetData experience = LoadRequiredAsset<EconomyAssetData>(ExperiencePath);
            EconomyAssetData turnToken = LoadRequiredAsset<EconomyAssetData>(BoardTurnTokenPath);
            TaxonomyTermData sharedWalletTag =
                LoadRequiredAsset<TaxonomyTermData>(SharedWalletTagPath);
            ProductionRecipeData deployment =
                LoadRequiredAsset<ProductionRecipeData>(DeploymentRecipePath);
            ProductionRecipeData wave =
                LoadRequiredAsset<ProductionRecipeData>(EnemyWaveRecipePath);
            ProductionData playerProduction =
                LoadRequiredAsset<ProductionData>(PlayerProductionPath);
            ProductionData enemyProduction =
                LoadRequiredAsset<ProductionData>(EnemyProductionPath);
            OrchestratorAIBrainData playerBrain =
                LoadRequiredAsset<OrchestratorAIBrainData>(PlayerBrainPath);
            OrchestratorAIBrainData enemyBrain =
                LoadRequiredAsset<OrchestratorAIBrainData>(EnemyBrainPath);
            TaxonomyTermData awaitFactOperator =
                LoadRequiredAsset<TaxonomyTermData>(AwaitFactOperatorPath);
            ProductionRecipeData merge =
                LoadRequiredAsset<ProductionRecipeData>(BoardMergeRecipe4Path);
            DropProfileData dropProfile = LoadRequiredAsset<DropProfileData>(DropProfilePath);
            AIBrainData alliedCombatBrain =
                LoadRequiredAsset<AIBrainData>(AlliedCombatBrainPath);
            AIBrainData enemyCombatBrain =
                LoadRequiredAsset<AIBrainData>(EnemyCombatBrainPath);
            AIBrainData collectionBrain = LoadRequiredAsset<AIBrainData>(CollectionBrainPath);
            TaxonomyTermData searchState =
                LoadRequiredAsset<TaxonomyTermData>(SearchStatePath);
            TaxonomyTermData waitingState =
                LoadRequiredAsset<TaxonomyTermData>(WaitingStatePath);
            TaxonomyTermData collectionState =
                LoadRequiredAsset<TaxonomyTermData>(CollectionStatePath);
            SkillData collectionSkill = LoadRequiredAsset<SkillData>(CollectionSkillPath);
            MovementData movement = LoadRequiredAsset<MovementData>(MovementPath);
            TaxonomyTermData experienceProgressTarget =
                LoadRequiredAsset<TaxonomyTermData>(ExperienceProgressTargetPath);
            TaxonomyTermData integrationRuntimeTag =
                LoadRequiredAsset<TaxonomyTermData>(IntegrationRuntimeTagPath);

            Assert.AreEqual(2, activity.Teams[0].Objectives.Count);
            Assert.AreEqual(1, activity.Teams[1].Objectives.Count);
            Assert.IsTrue(activity.Teams
                .SelectMany(team => team.Objectives)
                .All(entry => entry.Template.CompletionPolicyType == ObjectiveCompletionPolicyType.Reset));
            Assert.AreEqual(1, activity.Teams[0].Features.Count);
            Assert.AreEqual(1, activity.Teams[1].Features.Count);
            Assert.AreEqual(1, activity.WorldWallets.Count);

            ActivityTeamWalletData playerWallet = activity.Teams[0].Wallets.Single();
            Assert.IsTrue(playerWallet.Seed.Any(seed =>
                seed.Seed.Asset == playerSpawner
                && seed.Seed.FormType == EconomyFormType.Token
                && seed.MaterializationType == ActivitySeedMaterializationType.Spatial));
            ActivityWalletSeedEntryData collectorSeed = playerWallet.Seed.Single(seed =>
                seed.Seed.Asset == experienceCollector);
            Assert.AreEqual(EconomyFormType.Token, collectorSeed.Seed.FormType);
            Assert.AreEqual(ActivitySeedMaterializationType.NonSpatial, collectorSeed.MaterializationType);
            Assert.AreEqual(1, collectorSeed.ProjectionTargetTags.Count);
            Assert.AreSame(experienceProgressTarget, collectorSeed.ProjectionTargetTags[0]);
            Assert.AreEqual(0, collectorSeed.MaterializationMarkerTags.Count);
            Assert.IsTrue(playerWallet.Seed.Any(seed =>
                seed.Seed.Asset == waterUnit
                && seed.Seed.FormType == EconomyFormType.Stack
                && seed.Seed.Amount == 1L
                && seed.MaterializationType == ActivitySeedMaterializationType.None));
            ActivityTeamWalletData enemyWallet = activity.Teams[1].Wallets.Single();
            Assert.IsTrue(enemyWallet.Seed.Any(seed =>
                seed.Seed.Asset == enemySpawner
                && seed.Seed.FormType == EconomyFormType.Token
                && seed.MaterializationType == ActivitySeedMaterializationType.Spatial));

            Assert.AreEqual(1, deployment.Inputs.Count);
            AssertEconomyOperation(
                deployment.Inputs[0],
                waterUnit,
                EconomyFormType.Stack,
                1L,
                sharedWalletTag);
            Assert.AreEqual(1, deployment.Outputs.Count);
            AssertEconomyOutput(
                deployment.Outputs[0],
                waterUnit,
                EconomyFormType.Token,
                1L,
                sharedWalletTag);

            Assert.AreEqual(1, wave.Outputs.Count);
            Assert.AreSame(enemy, wave.Outputs[0].Asset);
            Assert.AreEqual(EconomyFormType.Token, wave.Outputs[0].FormType);
            Assert.IsInstanceOf<LongCappedProgressionData>(wave.Outputs[0].AmountProgression);
            var capped = (LongCappedProgressionData)wave.Outputs[0].AmountProgression;
            Assert.AreEqual(20L, capped.Maximum);
            Assert.IsInstanceOf<LongLinearProgressionData>(capped.Source);
            long[] ordinals = { 1L, 2L, 19L, 20L };
            long[] amounts = { 2L, 3L, 20L, 20L };
            for (int i = 0; i < ordinals.Length; i++)
            {
                Assert.IsTrue(wave.Outputs[0].TryResolveAmount(
                    ordinals[i], out long amount, out string failure), failure);
                Assert.AreEqual(amounts[i], amount);
            }

            Assert.NotNull(playerProduction.MaterializationProviderType);
            Assert.AreEqual(
                "chainrush.autobattle.marker.allied-spawn",
                playerProduction.MaterializationProviderType.Id);
            Assert.NotNull(enemyProduction.MaterializationProviderType);
            Assert.AreEqual(
                "chainrush.autobattle.marker.enemy-spawn",
                enemyProduction.MaterializationProviderType.Id);

            Assert.AreEqual(8, playerBrain.Operators.Count);
            Assert.AreEqual(
                1,
                playerBrain.Operators.OfType<MaterializedEntityProductionDecompOpData>().Count());
            Assert.AreEqual(
                1,
                enemyBrain.Operators.OfType<MaterializedEntityProductionDecompOpData>().Count());
            AwaitFactDecompOpData awaitFact = playerBrain.Operators
                .OfType<AwaitFactDecompOpData>()
                .Single();
            Assert.AreSame(awaitFactOperator, awaitFact.OperatorId);
            CollectionAssert.AreEqual(
                new[] { OrchestrationPlanningFactType.EconomyAmount },
                awaitFact.InputFactTypes);
            Assert.AreEqual(9, playerBrain.DecisionGraph.Nodes.Count);
            var globalProduction = playerBrain.DecisionGraph.Nodes[2]
                as OrchestrationDecisionData;
            var awaitExternal = playerBrain.DecisionGraph.Nodes[3]
                as OrchestrationDecisionData;
            Assert.NotNull(globalProduction);
            Assert.NotNull(awaitExternal);
            Assert.AreEqual("global-production-economy", globalProduction.DecisionId);
            Assert.IsInstanceOf<ProductionEconomyDecompOpData>(
                playerBrain.Operators.Single(operation =>
                    operation.OperatorId == globalProduction.OperatorId));
            Assert.AreEqual("await-external-economy", awaitExternal.DecisionId);
            Assert.AreSame(awaitFactOperator, awaitExternal.OperatorId);
            ScopeDecisionConditionData awaitScope = awaitExternal.Conditions
                .OfType<ScopeDecisionConditionData>()
                .Single();
            Assert.AreEqual(
                OrchestrationDecompositionScopeType.GlobalObjective,
                ReadField<OrchestrationDecompositionScopeType>(awaitScope, "scopeType"));
            PlanIntentDecisionConditionData awaitIntent = awaitExternal.Conditions
                .OfType<PlanIntentDecisionConditionData>()
                .Single();
            CollectionAssert.AreEqual(
                new[] { PlanActionType.Push },
                ReadField<List<PlanActionType>>(awaitIntent, "actionTypes"));
            TaxonomyRuntimeInstallerData taxonomyInstaller =
                LoadRequiredAsset<TaxonomyRuntimeInstallerData>(
                    "Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset");
            CollectionAssert.Contains(
                ReadObjectReferences<TaxonomyTermData>(taxonomyInstaller, "terms"),
                awaitFactOperator);

            Assert.IsTrue(enemy.WalletEntries
                .SelectMany(entry => entry.Seed)
                .Any(seed => seed.Asset == experience
                    && seed.FormType == EconomyFormType.Stack
                    && seed.Amount == 3L));
            Assert.AreEqual(1, dropProfile.Preparations.Count);
            Assert.IsInstanceOf<ContainerDropPreparationData>(dropProfile.Preparations[0]);
            Assert.AreEqual(1, dropProfile.WorldWalletTags.Count);
            Assert.IsFalse(experienceDrop.SupportsCapability(CapabilityHostType.SkillOwner));
            Assert.IsFalse(experienceDrop.SupportsCapability(CapabilityHostType.MovementOwner));
            Assert.IsFalse(experienceDrop.SupportsCapability(CapabilityHostType.AIBrainOwner));
            Assert.IsFalse(experienceDrop.WalletEntries
                .SelectMany(entry => entry.Seed)
                .Any(seed => seed.Asset == movement || seed.Asset is SkillData || seed.Asset is AIBrainData));
            Assert.IsTrue(experienceCollector.SupportsCapability(CapabilityHostType.SkillOwner));
            Assert.IsTrue(experienceCollector.SupportsCapability(CapabilityHostType.AIBrainOwner));
            Assert.IsFalse(experienceCollector.SupportsCapability(CapabilityHostType.MovementOwner));
            AssertAIBrainBinding(experienceCollector, collectionBrain);
            Assert.IsTrue(experienceCollector.WalletEntries
                .SelectMany(entry => entry.Seed)
                .Any(seed => seed.Asset == collectionSkill));
            Assert.AreEqual(10L, collectionSkill.StartDelay);
            Assert.AreEqual(2, collectionSkill.Effects.Count);
            Assert.IsTrue(collectionSkill.Effects.All(
                effect => effect is SkillEconomyEntryEffectData));
            var transferEffect = (SkillEconomyEntryEffectData)collectionSkill.Effects[0];
            Assert.AreEqual(EffectRecipient.Target, transferEffect.Recipient);
            Assert.AreEqual(SkillEconomyEntrySourceType.Wallet, transferEffect.SourceType);
            Assert.AreEqual(SkillEconomyOwnerType.Host, transferEffect.SourceOwnerType);
            Assert.AreEqual(EconomyOperation.Transfer, transferEffect.Operation);
            Assert.AreEqual(EffectRecipient.Owner, transferEffect.DestinationRecipient);
            Assert.AreEqual(SkillEconomyOwnerType.Root, transferEffect.DestinationOwnerType);
            var destroyEffect = (SkillEconomyEntryEffectData)collectionSkill.Effects[1];
            Assert.AreEqual(EffectRecipient.Target, destroyEffect.Recipient);
            Assert.AreEqual(SkillEconomyEntrySourceType.BackingEntry, destroyEffect.SourceType);
            Assert.AreEqual(EconomyOperation.Destroy, destroyEffect.Operation);

            Assert.AreEqual(1, collectionBrain.Nodes.Count);
            AIBrainNodeData collectionNode = collectionBrain.Nodes.Single();
            Assert.AreSame(searchState, collectionNode.EntryState);
            Assert.AreEqual(3, collectionNode.States.Count);
            AIBrainStateData search = collectionNode.States.Single(state => state.Tag == searchState);
            AIBrainStateData waiting = collectionNode.States.Single(state => state.Tag == waitingState);
            AIBrainStateData collectState = collectionNode.States.Single(
                state => state.Tag == collectionState);
            Assert.AreEqual(1, search.OnEnterActions.Count);
            Assert.IsInstanceOf<SelectActivityTargetAIBrainActionData>(
                search.OnEnterActions[0]);
            Assert.AreEqual(0, waiting.OnEnterActions.Count);
            Assert.AreEqual(1, collectState.OnEnterActions.Count);
            Assert.IsInstanceOf<UseSkillAIBrainActionData>(collectState.OnEnterActions[0]);
            Assert.AreSame(
                collectionSkill,
                ReadField<SkillData>(collectState.OnEnterActions[0], "skill"));
            Assert.AreEqual(6, collectionBrain.Transitions.Count);
            AIBrainTransitionData failedSearchRetry = collectionBrain.Transitions.Single(
                transition => transition.FromStates.Contains(searchState)
                    && transition.Conditions.Any(
                        condition => condition is CurrentStateResultMatchesAIBrainConditionData));
            Assert.AreSame(waitingState, failedSearchRetry.ToState);
            Assert.IsFalse(collectionBrain.Transitions.Any(transition =>
                transition.FromStates.Contains(transition.ToState)));
            AssertAIBrainBinding(waterUnit, alliedCombatBrain);
            AssertAIBrainBinding(enemy, enemyCombatBrain);
            AssertCombatDefeatActions(
                alliedCombatBrain,
                typeof(RemoveEntityAIBrainActionData));
            AssertCombatDefeatActions(
                enemyCombatBrain,
                typeof(DropAIBrainActionData),
                typeof(RemoveEntityAIBrainActionData));

            string[] projectionPaths =
            {
                AutobattleRoot + "/Projection/PlayerSpawner.prefab",
                AutobattleRoot + "/Projection/EnemySpawner.prefab",
                AutobattleRoot + "/Projection/WaterUnit.prefab",
                AutobattleRoot + "/Projection/BugBrownSmall.prefab",
                ExperienceDropPrefabPath,
                ExperienceCollectorPrefabPath,
            };
            GameObject enemyProjection = LoadRequiredAsset<GameObject>(
                AutobattleRoot + "/Projection/BugBrownSmall.prefab");
            SpatialShapeData spawnArea =
                LoadRequiredAsset<SpatialShapeData>(SpawnAreaShapePath);
            GameObject playerSpawnerProjection =
                LoadRequiredAsset<GameObject>(projectionPaths[0]);
            GameObject enemySpawnerProjection =
                LoadRequiredAsset<GameObject>(projectionPaths[1]);
            AssertSpawnerShapeProvider(
                playerSpawnerProjection,
                spawnArea,
                Vector3Int.zero);
            AssertSpawnerShapeProvider(
                enemySpawnerProjection,
                spawnArea,
                new Vector3Int(-6000, 0, 0));
            GameObject experienceProjection = LoadRequiredAsset<GameObject>(ExperienceDropPrefabPath);
            GameObject collectorProjection =
                LoadRequiredAsset<GameObject>(ExperienceCollectorPrefabPath);
            Assert.IsNull(
                experienceProjection.GetComponent<AIBrainTransitionUnityEventController>());
            Assert.NotNull(experienceProjection.GetComponent<ProjectionMovementController>());
            Assert.NotNull(collectorProjection.GetComponent<ProjectionBindingController>());
            SkillTargetProjectionController transition =
                collectorProjection.GetComponent<SkillTargetProjectionController>();
            Assert.NotNull(transition);
            Assert.AreSame(collectionSkill, ReadField<SkillData>(transition, "skill"));
            Assert.IsEmpty(collectorProjection.GetComponentsInChildren<Renderer>(true));
            GameObject experienceUI = LoadRequiredAsset<GameObject>(ExperienceUIPrefabPath);
            Assert.IsNull(experienceUI.transform.Find("CollectionLayer"));
            UIProjectionContextController uiContext =
                experienceUI.GetComponent<UIProjectionContextController>();
            Assert.NotNull(uiContext);
            ActivityRuntimeSelectorData uiSelector =
                ReadField<ActivityRuntimeSelectorData>(uiContext, "activitySelector");
            Assert.AreSame(activity, uiSelector.Definition);
            Assert.AreEqual(1, uiSelector.RequiredRuntimeTags.Count);
            Assert.AreSame(integrationRuntimeTag, uiSelector.RequiredRuntimeTags[0]);
            UIProjectionTargetController uiTarget =
                experienceUI.GetComponentInChildren<UIProjectionTargetController>(true);
            Assert.NotNull(uiTarget);
            List<TaxonomyTermData> uiTargetTags =
                ReadField<List<TaxonomyTermData>>(uiTarget, "targetTags");
            Assert.AreEqual(1, uiTargetTags.Count);
            Assert.AreSame(experienceProgressTarget, uiTargetTags[0]);
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(
                AutobattleRoot + "/Skills/ExperienceAttractionSkill.asset"));
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(
                AutobattleRoot + "/AI/ExperienceCollectionBrain.asset"));
            PrefabMarkerCollectorController enemyDropProvider =
                enemyProjection.GetComponent<PrefabMarkerCollectorController>();
            Assert.NotNull(enemyDropProvider);
            Assert.AreEqual(
                SpatialMarkerRefreshPolicyType.OnUse,
                enemyDropProvider.RefreshPolicyType);
            List<string> poolKeys = projectionPaths
                .Select(path => LoadRequiredAsset<GameObject>(path)
                    .GetComponent<ProjectionBindingController>())
                .Select(binding =>
                {
                    Assert.NotNull(binding);
                    return binding.PoolKey.Value;
                })
                .ToList();
            Assert.AreEqual(poolKeys.Count, poolKeys.Distinct(StringComparer.Ordinal).Count());
            Assert.IsFalse(poolKeys.Any(key => string.Equals(
                key, "pool.default", StringComparison.Ordinal)));

            Assert.AreEqual(1, merge.Outputs.Count);
            AssertEconomyOutput(
                merge.Outputs[0],
                waterUnit,
                EconomyFormType.Stack,
                1L,
                sharedWalletTag);
            Assert.IsFalse(board.Teams[0].Wallets
                .SelectMany(wallet => wallet.Seed)
                .Any(seed => seed.Seed.Asset == turnToken));
        }

        [Test]
        public void GameFlows_ResolveRootAndLaunchBoardChildThroughOneActivationTerm()
        {
            ActivityData autobattle = LoadRequiredAsset<ActivityData>(AutobattleActivityPath);
            ActivityData board = LoadRequiredAsset<ActivityData>(BoardActivityPath);
            GameFlowTemplateData autobattleFlow =
                LoadRequiredAsset<GameFlowTemplateData>(AutobattleFlowPath);
            GameFlowTemplateData boardFlow =
                LoadRequiredAsset<GameFlowTemplateData>(BoardFlowPath);

            ActivityFlowContainerData autobattleContainer =
                RequireActivityContainer(autobattleFlow);
            Assert.AreSame(autobattle, autobattleContainer.Activity);
            Assert.AreEqual(4, autobattleContainer.Steps.Count);
            Assert.IsInstanceOf<GameFlowResolveParticipantsExecutorData>(
                autobattleContainer.Steps[0].Executor);
            Assert.IsInstanceOf<GameFlowLaunchActivityExecutorData>(
                autobattleContainer.Steps[1].Executor);
            var rootLaunch = (GameFlowLaunchActivityExecutorData)
                autobattleContainer.Steps[1].Executor;
            Assert.AreEqual(1, rootLaunch.RuntimeTags.Count);
            Assert.AreSame(
                LoadRequiredAsset<TaxonomyTermData>(IntegrationRuntimeTagPath),
                rootLaunch.RuntimeTags[0]);
            Assert.IsNull(autobattleContainer.Steps[2].Executor);
            Assert.IsInstanceOf<GameFlowPublishChildActivationExecutorData>(
                autobattleContainer.Steps[3].Executor);

            var publish = (GameFlowPublishChildActivationExecutorData)
                autobattleContainer.Steps[3].Executor;
            Assert.AreEqual(1, publish.Taxonomy.Count);
            Assert.AreEqual(BoardActivationTermId, publish.Taxonomy[0].Id);

            ActivityFlowContainerData boardContainer = RequireActivityContainer(boardFlow);
            Assert.AreSame(board, boardContainer.Activity);
            Assert.AreEqual(1, boardContainer.ActivateConditions.Count);
            Assert.IsInstanceOf<GameFlowConditionActivityChildActivation>(
                boardContainer.ActivateConditions[0]);
            var activation = (GameFlowConditionActivityChildActivation)
                boardContainer.ActivateConditions[0];
            Assert.AreEqual(1, activation.Taxonomy.Count);
            Assert.AreEqual(BoardActivationTermId, activation.Taxonomy[0].Id);
            Assert.AreEqual(2, boardContainer.Steps.Count);
            Assert.IsInstanceOf<GameFlowLaunchChildActivityExecutorData>(
                boardContainer.Steps[0].Executor);
            var childLaunch = (GameFlowLaunchChildActivityExecutorData)
                boardContainer.Steps[0].Executor;
            Assert.AreEqual(0, childLaunch.RuntimeTags.Count);
            Assert.IsNull(boardContainer.Steps[1].Executor);
        }

        [Test]
        public void IntegrationPresentation_BindsViewportToTaggedAutobattleRuntime()
        {
            ActivityData activity = LoadRequiredAsset<ActivityData>(AutobattleActivityPath);
            TaxonomyTermData runtimeTag =
                LoadRequiredAsset<TaxonomyTermData>(IntegrationRuntimeTagPath);
            Scene scene = EditorSceneManager.OpenScene(IntegrationScenePath, OpenSceneMode.Additive);
            try
            {
                ActivityViewportController[] viewports = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ActivityViewportController>(true))
                    .ToArray();
                Assert.AreEqual(1, viewports.Length);
                ActivityRuntimeSelectorData selector =
                    ReadField<ActivityRuntimeSelectorData>(viewports[0], "activitySelector");
                Assert.AreSame(activity, selector.Definition);
                Assert.AreEqual(1, selector.RequiredRuntimeTags.Count);
                Assert.AreSame(runtimeTag, selector.RequiredRuntimeTags[0]);
                Assert.AreSame(
                    viewports[0].GetComponent<Camera>(),
                    ReadField<Camera>(viewports[0], "viewport"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void RuntimeComposition_UsesApprovedInstallersAndRegistersBoardBeforeAutobattle()
        {
            GameRuntimeProfileData profile =
                LoadRequiredAsset<GameRuntimeProfileData>(RuntimeProfilePath);
            GameStartupPlanData startup =
                LoadRequiredAsset<GameStartupPlanData>(StartupPlanPath);

            Type[] expectedInstallerTypes =
            {
                typeof(LocalRealtimeDriverInstallerData),
                typeof(EconomyRuntimeInstallerData),
                typeof(SimulationRuntimeInstallerData),
                typeof(TaxonomyRuntimeInstallerData),
                typeof(EconomyDefinitionsInstallerData),
                typeof(PlayerDefinitionsInstallerData),
                typeof(ActivityRuntimeInstallerData),
                typeof(GameFlowDefinitionsInstallerData),
                typeof(GameplayFoundationInstallerData),
                typeof(GameplaySkillsInstallerData),
                typeof(ProductionRuntimeInstallerData),
                typeof(SimulationControlRuntimeInstallerData),
                typeof(ProjectionServiceRuntimeData),
                typeof(GameplayHostValuesInstallerData),
                typeof(DropRuntimeInstallerData),
                typeof(DiplomacyRuntimeInstallerData),
            };

            CollectionAssert.AreEqual(
                expectedInstallerTypes,
                profile.Installers.Select(installer => installer.GetType()).ToList());

            DiplomacyRuntimeInstallerData diplomacyInstaller = profile.Installers
                .OfType<DiplomacyRuntimeInstallerData>()
                .Single();
            EconomyRuntimeInstallerData economyRuntimeInstaller = profile.Installers
                .OfType<EconomyRuntimeInstallerData>()
                .Single();
            CollectionAssert.Contains(
                ReadField<List<EconomyDomainType>>(economyRuntimeInstaller, "domains"),
                EconomyDomainType.Spatial);
            var serializedDiplomacyInstaller = new SerializedObject(diplomacyInstaller);
            SerializedProperty diplomacyModules =
                serializedDiplomacyInstaller.FindProperty("modules");
            Assert.NotNull(diplomacyModules);
            Assert.AreEqual(2, diplomacyModules.arraySize);
            Assert.AreSame(
                LoadRequiredAsset<UnityEngine.Object>(ActivityDiplomacyModulePath),
                diplomacyModules.GetArrayElementAtIndex(0).objectReferenceValue);
            Assert.AreSame(
                LoadRequiredAsset<UnityEngine.Object>(CapabilityHostDiplomacyModulePath),
                diplomacyModules.GetArrayElementAtIndex(1).objectReferenceValue);

            Assert.AreEqual(2, startup.Actions.Length);
            Assert.IsInstanceOf<AddGameFlowRuntimeActionData>(startup.Actions[0]);
            Assert.IsInstanceOf<AddGameFlowRuntimeActionData>(startup.Actions[1]);

            GameFlowTemplateData boardFlow =
                LoadRequiredAsset<GameFlowTemplateData>(BoardFlowPath);
            GameFlowTemplateData autobattleFlow =
                LoadRequiredAsset<GameFlowTemplateData>(AutobattleFlowPath);
            Assert.AreSame(boardFlow, ReadTemplate(startup.Actions[0]));
            Assert.AreSame(autobattleFlow, ReadTemplate(startup.Actions[1]));
        }

        [Test]
        public void AutobattleWorldSpace_IsAddressable()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.NotNull(settings, "AddressableAssetSettings are not configured.");

            AssertAddressableGroup(
                settings,
                AutobattleSpacePath,
                "ChainRush-Activity-Autobattle");

            GameObject space = LoadRequiredAsset<GameObject>(AutobattleSpacePath);
            Transform floor = space.transform.Find("Floor");
            Assert.NotNull(floor);
            Assert.NotNull(floor.GetComponent<NavigationSurfaceController>());
        }

        [Test]
        public void BoardWaterProjection_IsAddressable()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.NotNull(settings, "AddressableAssetSettings are not configured.");

            AssertAddressableGroup(
                settings,
                BoardWaterProjectionPath,
                "ChainRush-Activity-Board");
        }

        [Test]
        public void BoardUI_MatchingSelectionRejection_UnlocksAndClearsSelection()
        {
            GameObject instance = InstantiateBoardUI(out MonoBehaviour controller);
            try
            {
                TaxonomyTermData requestType =
                    LoadRequiredAsset<TaxonomyTermData>(BoardMergeSelectionPath);
                var activityId = new ActivityId(31);
                var hostEntityId = new Core.Entities.EntityId(47);
                SelectionIntentEvent request = SelectionIntentEvent.Begin(
                    activityId,
                    requestType,
                    Core.Entities.EntityId.Invalid,
                    hostEntityId);
                PrepareBoardSelectionState(controller, activityId, hostEntityId, requestType, request);

                InvokeSelectionResult(controller, new SelectionResultEvent(
                    request.RequestId,
                    activityId,
                    requestType,
                    Core.Entities.EntityId.Invalid,
                    hostEntityId,
                    SelectionResultType.Rejected,
                    new List<Core.Entities.EntityId>(0),
                    "Rejected by test setup."));

                Assert.IsFalse(ReadField<bool>(controller, "_selectionLocked"));
                Assert.IsFalse(ReadField<bool>(controller, "_awaitingBoardRefresh"));
                Assert.IsFalse(ReadField<bool>(controller, "_isSelecting"));
                Assert.IsFalse(ReadField<SelectionIntentEvent>(controller, "_pendingBeginIntent")
                    .RequestId.IsValid);
                Assert.AreEqual(0, ReadListField(controller, "_selectedEntities").Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BoardUI_UnrelatedSelectionResult_DoesNotChangePendingSelection()
        {
            GameObject instance = InstantiateBoardUI(out MonoBehaviour controller);
            try
            {
                TaxonomyTermData requestType =
                    LoadRequiredAsset<TaxonomyTermData>(BoardMergeSelectionPath);
                var activityId = new ActivityId(32);
                var hostEntityId = new Core.Entities.EntityId(48);
                SelectionIntentEvent pending = SelectionIntentEvent.Begin(
                    activityId,
                    requestType,
                    Core.Entities.EntityId.Invalid,
                    hostEntityId);
                SelectionIntentEvent unrelated = SelectionIntentEvent.Begin(
                    activityId,
                    requestType,
                    Core.Entities.EntityId.Invalid,
                    hostEntityId);
                PrepareBoardSelectionState(controller, activityId, hostEntityId, requestType, pending);

                InvokeSelectionResult(controller, new SelectionResultEvent(
                    unrelated.RequestId,
                    activityId,
                    requestType,
                    Core.Entities.EntityId.Invalid,
                    hostEntityId,
                    SelectionResultType.Rejected,
                    new List<Core.Entities.EntityId>(0),
                    "Unrelated rejection."));

                Assert.IsTrue(ReadField<bool>(controller, "_selectionLocked"));
                Assert.IsTrue(ReadField<bool>(controller, "_awaitingBoardRefresh"));
                Assert.AreEqual(
                    pending.RequestId,
                    ReadField<SelectionIntentEvent>(controller, "_pendingBeginIntent").RequestId);
                Assert.AreEqual(1, ReadListField(controller, "_selectedEntities").Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BoardUI_CommittedSelection_StaysLockedUntilAuthoritativeRefresh()
        {
            GameObject instance = InstantiateBoardUI(out MonoBehaviour controller);
            try
            {
                TaxonomyTermData requestType =
                    LoadRequiredAsset<TaxonomyTermData>(BoardMergeSelectionPath);
                var activityId = new ActivityId(33);
                var hostEntityId = new Core.Entities.EntityId(49);
                SelectionIntentEvent request = SelectionIntentEvent.Begin(
                    activityId,
                    requestType,
                    Core.Entities.EntityId.Invalid,
                    hostEntityId);
                PrepareBoardSelectionState(controller, activityId, hostEntityId, requestType, request);

                InvokeSelectionResult(controller, new SelectionResultEvent(
                    request.RequestId,
                    activityId,
                    requestType,
                    Core.Entities.EntityId.Invalid,
                    hostEntityId,
                    SelectionResultType.Committed,
                    new List<Core.Entities.EntityId> { hostEntityId },
                    null));

                Assert.IsTrue(ReadField<bool>(controller, "_selectionLocked"));
                Assert.IsTrue(ReadField<bool>(controller, "_awaitingBoardRefresh"));
                Assert.IsFalse(ReadField<SelectionIntentEvent>(controller, "_pendingBeginIntent")
                    .RequestId.IsValid);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BoardUI_FirstConsumedEntity_PreservesRemainingSelection()
        {
            GameObject instance = InstantiateBoardUI(out MonoBehaviour controller);
            try
            {
                var firstEntityId = new Core.Entities.EntityId(51);
                var secondEntityId = new Core.Entities.EntityId(52);
                SetField(controller, "_selectionLocked", true);
                SetField(controller, "_awaitingBoardRefresh", true);
                ReadListField(controller, "_selectedEntities").Add(firstEntityId);
                ReadListField(controller, "_selectedEntities").Add(secondEntityId);

                MethodInfo method = controller.GetType().GetMethod(
                    "OnCellEntityRemoved",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(method);
                method.Invoke(controller, new object[] { null, firstEntityId });

                IList selectedEntities = ReadListField(controller, "_selectedEntities");
                Assert.AreEqual(1, selectedEntities.Count);
                Assert.AreEqual(secondEntityId, selectedEntities[0]);
                Assert.IsTrue(ReadField<bool>(controller, "_selectionLocked"));
                Assert.IsTrue(ReadField<bool>(controller, "_awaitingBoardRefresh"));
                Assert.IsTrue(ReadField<bool>(controller, "_boardRefreshObserved"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        static void AssertActivitySchedule(ActivityData activity)
        {
            Assert.IsTrue(
                activity.Schedule.TryBuildRequest(out ScheduleRequest request, out string message),
                message);
            Assert.AreEqual(ScheduleType.Realtime, request.Type);
            Assert.AreEqual(1f / 30f, request.TickDelta, 0.0001f);
            Assert.AreEqual(RealtimeCatchUpPolicyType.Budgeted, request.CatchUpPolicyType);
            Assert.AreEqual(1, request.MaxStepsPerFrame);
        }

        static void AssertAIBrainBinding(CapabilityHostData host, AIBrainData brain)
        {
            CapabilityEntry capability = host.Capabilities
                .Single(entry => entry.CapabilityType == CapabilityHostType.AIBrainOwner);
            Assert.AreEqual(1, capability.SelectorTags.Count);
            SeedEntry seed = host.WalletEntries
                .SelectMany(entry => entry.Seed)
                .Single(entry => entry.Asset == brain);
            Assert.AreEqual(1, seed.RuntimeTags.Count);
            Assert.AreSame(capability.SelectorTags[0], seed.RuntimeTags[0]);
        }

        static void AssertCombatDefeatActions(AIBrainData brain, params Type[] actionTypes)
        {
            AIBrainStateData defeat = brain.Nodes
                .SelectMany(node => node.States)
                .Single(state => state.OnEnterActions.Any(
                    action => action is RemoveEntityAIBrainActionData));
            CollectionAssert.AreEqual(
                actionTypes,
                defeat.OnEnterActions.Select(action => action.GetType()).ToList());
        }

        static GameObject InstantiateBoardUI(out MonoBehaviour controller)
        {
            GameObject prefab = LoadRequiredAsset<GameObject>(BoardUIPrefabPath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            controller = instance
                .GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component != null
                    && string.Equals(
                        component.GetType().FullName,
                        "ChainRush.Board.BoardUIController",
                        StringComparison.Ordinal));
            Assert.NotNull(controller, "BoardUI prefab does not contain BoardUIController.");
            return instance;
        }

        static void PrepareBoardSelectionState(
            MonoBehaviour controller,
            ActivityId activityId,
            Core.Entities.EntityId hostEntityId,
            TaxonomyTermData requestType,
            SelectionIntentEvent beginIntent)
        {
            SetField(controller, "_context", new ActivityUIContext(
                activityId,
                default,
                new List<ActivityUICell>(),
                null,
                null));
            SetField(controller, "_boardHostEntityId", hostEntityId);
            SetField(controller, "selectionRequestType", requestType);
            SetField(controller, "_selectionLocked", true);
            SetField(controller, "_awaitingBoardRefresh", true);
            SetField(controller, "_boardRefreshObserved", false);
            SetField(controller, "_isSelecting", true);
            SetField(controller, "_pendingBeginIntent", beginIntent);
            ReadListField(controller, "_selectedEntities").Add(hostEntityId);
        }

        static void InvokeSelectionResult(MonoBehaviour controller, SelectionResultEvent result)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "OnEvent",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(SelectionResultEvent) },
                null);
            Assert.NotNull(method, "BoardUIController does not handle SelectionResultEvent.");
            method.Invoke(controller, new object[] { result });
        }

        static IList ReadListField(object owner, string fieldName)
        {
            object value = ReadField(owner, fieldName);
            Assert.IsInstanceOf<IList>(value, string.Concat("Field is not an IList: ", fieldName));
            return (IList)value;
        }

        static T ReadField<T>(object owner, string fieldName)
        {
            return (T)ReadField(owner, fieldName);
        }

        static object ReadField(object owner, string fieldName)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, string.Concat("Missing field: ", owner.GetType().Name, ".", fieldName));
            return field.GetValue(owner);
        }

        static void SetField(object owner, string fieldName, object value)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, string.Concat("Missing field: ", owner.GetType().Name, ".", fieldName));
            field.SetValue(owner, value);
        }

        static void AssertSimulationPolicy(ActivityData activity)
        {
            Assert.AreEqual(ActivitySimulationBindingType.OwnContext, activity.Simulation.BindingType);
            Assert.AreEqual(SimulationScope.Activity, activity.Simulation.Scope);
        }

        static void AssertMergeSelectionMetric(
            ObjectiveConditionEconomyMetric condition,
            EconomyAssetData asset,
            TaxonomyTermData walletTag,
            TaxonomyTermData runtimeTag,
            long targetValue,
            CompareOperation compareOperation)
        {
            Assert.AreSame(asset, condition.Asset);
            Assert.AreEqual(EconomyFormType.Token, condition.FormType);
            CollectionAssert.AreEqual(new[] { walletTag }, condition.WalletTags);
            CollectionAssert.AreEqual(new[] { runtimeTag }, condition.RequiredRuntimeTags);
            Assert.AreEqual(targetValue, condition.TargetValue);
            Assert.AreEqual(compareOperation, condition.CompareOperation);
        }

        static void AssertEconomyOperation(
            ProductionInputData input,
            EconomyAssetData asset,
            EconomyFormType formType,
            long amount,
            TaxonomyTermData walletTag)
        {
            Assert.NotNull(input.AmountProgression);
            Assert.IsTrue(input.TryResolve(
                1L,
                out Core.Economy.Authoring.EconomyOperationData operation,
                out string failure), failure);
            Assert.AreEqual(EconomyOperation.Consume, operation.Operation);
            Assert.AreSame(asset, operation.Asset);
            Assert.AreEqual(formType, operation.FormType);
            Assert.AreEqual(amount, operation.Amount);
            Assert.AreEqual(1, operation.WalletTags.Count);
            Assert.AreSame(walletTag, operation.WalletTags[0]);
        }

        static void AssertEconomyOutput(
            ProductionOutputData authoredOutput,
            EconomyAssetData asset,
            EconomyFormType formType,
            long amount,
            TaxonomyTermData walletTag)
        {
            Assert.NotNull(authoredOutput.AmountProgression);
            Assert.IsTrue(authoredOutput.TryResolve(
                1L,
                out EconomyOutputEntry output,
                out string failure), failure);
            Assert.AreSame(asset, output.Entry.Asset);
            Assert.AreEqual(formType, output.Entry.FormType);
            Assert.AreEqual(amount, output.Entry.Amount);
            Assert.AreEqual(1, output.WalletTags.Count);
            Assert.AreSame(walletTag, output.WalletTags[0]);
        }

        static void AssertSpawnerShapeProvider(
            GameObject projection,
            SpatialShapeData shape,
            Vector3Int expectedPosition)
        {
            Assert.AreEqual(1, projection.GetComponents<SpatialMarkerProviderController>().Length);
            SpatialShapeProviderController provider =
                projection.GetComponent<SpatialShapeProviderController>();
            Assert.NotNull(provider);
            Assert.AreSame(shape, provider.Shape);
            Assert.AreEqual(SpatialMarkerRefreshPolicyType.OnUse, provider.RefreshPolicyType);
            Assert.AreEqual(new Vector3Int(7, 1, 21), provider.Usage.Size);
            Assert.AreEqual(expectedPosition, provider.Usage.Position);
            Assert.AreEqual(new Vector3Int(1000, 1, 1000), provider.Usage.CellSize);
            Assert.AreEqual(Vector3Int.zero, provider.Usage.CellOffset);
            Assert.AreEqual(SpatialMarkerReusePolicyType.ReuseAllowed, provider.UsagePolicy.ReusePolicyType);
        }

        static void AssertTopology(
            TopologyDefinitionData topology,
            TopologyType topologyType,
            TopologyCoordinateOccupationPolicy occupationPolicy)
        {
            Assert.NotNull(topology);
            Assert.AreEqual(TopologyDimensionType.TwoDimensional, topology.DimensionType);
            Assert.AreEqual(TopologyUpAxisType.Y, topology.UpAxisType);
            Assert.AreEqual(topologyType, topology.TopologyType);
            Assert.AreEqual(NavigationFrameType.Planar, topology.NavigationFrameType);
            Assert.AreEqual(NavigationAlgorithmType.StraightLine, topology.NavigationAlgorithmType);
            Assert.AreEqual(occupationPolicy, topology.CoordinateOccupationPolicy);
        }

        static void AssertOccupancyTerm(
            TaxonomyTermData term,
            TaxonomyFamilyData family,
            string expectedId)
        {
            Assert.NotNull(term);
            Assert.AreEqual(expectedId, term.Id);
            Assert.AreSame(family, term.Family);
        }

        static void AssertOccupancyRow(
            SpatialOccupancyMatrixRowData row,
            TaxonomyTermData tag,
            params TaxonomyTermData[] blockedTags)
        {
            Assert.NotNull(row);
            Assert.AreSame(tag, row.Tag);
            CollectionAssert.AreEqual(blockedTags, row.BlockedTags);
        }

        static void AssertHostOccupancyTag(
            string path,
            TaxonomyFamilyData family,
            TaxonomyTermData expectedTag)
        {
            CapabilityHostBaseData host = LoadRequiredAsset<CapabilityHostBaseData>(path);
            List<TaxonomyTermData> occupancyTags = host.Tags
                .Where(tag => tag != null && tag.Family == family)
                .ToList();
            Assert.AreEqual(1, occupancyTags.Count, path);
            Assert.AreSame(expectedTag, occupancyTags[0], path);
        }

        static ActivityFlowContainerData RequireActivityContainer(GameFlowTemplateData template)
        {
            Assert.NotNull(template);
            Assert.IsInstanceOf<ActivityFlowContainerData>(template.Root);
            return (ActivityFlowContainerData)template.Root;
        }

        static GameFlowTemplateData ReadTemplate(GameStartupActionData action)
        {
            var serialized = new SerializedObject(action);
            SerializedProperty template = serialized.FindProperty("template");
            Assert.NotNull(template);
            return template.objectReferenceValue as GameFlowTemplateData;
        }

        static void AssertAddressableGroup(
            AddressableAssetSettings settings,
            string assetPath,
            string expectedGroupName)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            Assert.IsFalse(string.IsNullOrWhiteSpace(guid), $"Missing asset: {assetPath}");
            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            Assert.NotNull(entry, $"Asset is not addressable: {assetPath}");
            Assert.NotNull(entry.parentGroup);
            Assert.AreEqual(expectedGroupName, entry.parentGroup.Name);
        }

        static T LoadRequiredAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.NotNull(asset, $"Missing {typeof(T).Name} at {path}");
            return asset;
        }

        static List<T> ReadObjectReferences<T>(UnityEngine.Object owner, string fieldName)
            where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(fieldName);
            Assert.NotNull(property, $"Missing serialized field '{fieldName}' on {owner.name}.");
            Assert.IsTrue(property.isArray, $"Serialized field '{fieldName}' is not an array.");
            var values = new List<T>(property.arraySize);
            for (int index = 0; index < property.arraySize; index++)
            {
                values.Add(property.GetArrayElementAtIndex(index).objectReferenceValue as T);
            }

            return values;
        }

        static void AssertFolderDependenciesStayWithinBoundary(
            string ownerRoot,
            params string[] forbiddenRoots)
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { ownerRoot });
            var violations = new List<string>();
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.IsValidFolder(assetPath))
                    continue;

                string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    string dependency = dependencies[dependencyIndex];
                    for (int rootIndex = 0; rootIndex < forbiddenRoots.Length; rootIndex++)
                    {
                        if (!IsWithin(dependency, forbiddenRoots[rootIndex]))
                            continue;

                        violations.Add($"{assetPath} -> {dependency}");
                    }
                }
            }

            Assert.IsEmpty(
                violations,
                $"Assets under {ownerRoot} cross an Activity ownership boundary:\n" +
                string.Join("\n", violations.Distinct().OrderBy(value => value)));
        }

        static bool IsWithin(string path, string root)
        {
            return string.Equals(path, root, StringComparison.Ordinal)
                || path.StartsWith(root + "/", StringComparison.Ordinal);
        }
    }
}
