using System;
using System.Collections.Generic;
using System.Reflection;
using ChainRush.Board;
using Core;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Economy;
using Core.Economy.Authoring;
using Core.GameRuntime;
using Core.GameRuntime.Installers;
using Core.Objectives;
using Core.Orchestration;
using Core.Production;
using Core.Production.Authoring;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using EntityId = Core.Entities.EntityId;
using FrameworkResourceData = Core.Economy.Modules.ResourceEconomyModule.ResourceData;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    public static class ChainRushBoardPlannerAuthoring
    {
        const string BoardRoot = "Assets/Game/Activities/Board";
        const string SharedRoot = "Assets/Game/Activities/Shared";
        const string PopulationRoot = BoardRoot + "/Population";
        const string PlannerRoot = PopulationRoot + "/Planner";
        const string AgentsRoot = BoardRoot + "/Agents";
        const string ObjectivesRoot = BoardRoot + "/Objectives";
        const string OrchestrationRoot = BoardRoot + "/Orchestration";
        const string OrchestrationModulesRoot = OrchestrationRoot + "/Modules";
        const string OrchestrationTaxonomyRoot = OrchestrationRoot + "/Taxonomy";
        const string SharedUnitsRoot = SharedRoot + "/Units";
        const string SharedWaterRoot = SharedUnitsRoot + "/Water";

        const string PlannerPath = PlannerRoot + "/ChainRushBoardPlanner.asset";
        const string BoardActivityPath = BoardRoot + "/Definition/ChainRushBoardActivity.asset";
        const string BoardHostPath = BoardRoot + "/Economy/ChainRushBoardHost.asset";
        const string BoardWalletPath = BoardRoot + "/Economy/ChainRushBoardWallet.asset";
        const string BoardWalletTagPath = BoardRoot + "/Economy/ChainRushBoardWalletTag.asset";
        const string WaterPath = BoardRoot + "/Economy/ChainRushWaterBoardBase.asset";
        const string WaterTagPath = BoardRoot + "/Taxonomy/ChainRushWaterBoardItem.asset";
        const string BoardCellTagPath = BoardRoot + "/Taxonomy/ChainRushBoardCellTag.asset";
        const string MergeRecipePath = BoardRoot + "/Production/ChainRushBoardMergeRecipe.asset";
        const string MergeProductionPath = BoardRoot + "/Production/ChainRushBoardProduction.asset";
        const string MergeCatalogPath = BoardRoot + "/Production/ChainRushBoardProductionCatalog.asset";
        const string MergeSkillPath = BoardRoot + "/Skills/ChainRushBoardMergeSkill.asset";
        const string BoardUIPrefabPath = BoardRoot + "/UI/ChainRushBoardUI.prefab";
        const string WaterProjectionPrefabPath =
            BoardRoot + "/Projection/ChainRushWaterBoardBase.prefab";
        const string SharedWalletPath = SharedRoot + "/Economy/ChainRushActivityWallet.asset";
        const string SharedWalletTagPath = SharedRoot + "/Economy/ChainRushActivityWalletTag.asset";

        const string TurnTokenPath = SharedRoot + "/Economy/ChainRushBoardTurnToken.asset";
        const string WaterUnitPath = SharedWaterRoot + "/ChainRushWaterUnit.asset";
        const string PopulationProducerPath = BoardRoot + "/Economy/ChainRushBoardPopulationProducer.asset";
        const string RefreshRecipePath = BoardRoot + "/Production/ChainRushBoardRefreshRecipe.asset";
        const string WaterRecipePath = BoardRoot + "/Production/ChainRushWaterBoardBaseRecipe.asset";
        const string PopulationProductionPath = BoardRoot + "/Production/ChainRushBoardPopulationProduction.asset";
        const string PopulationCatalogPath = BoardRoot + "/Production/ChainRushBoardPopulationCatalog.asset";
        const string PopulationAgentPath = AgentsRoot + "/ChainRushBoardPopulationAgent.asset";
        const string ProductionAgentPath = AgentsRoot + "/ChainRushBoardProductionAgent.asset";
        const string PopulationObjectivePath = ObjectivesRoot + "/ChainRushBoardPopulationObjective.asset";
        const string OperatorFamilyPath = OrchestrationTaxonomyRoot + "/ChainRushBoardOperatorFamily.asset";
        const string PopulationAgentOperatorPath = OrchestrationTaxonomyRoot + "/ChainRushBoardPopulationAgentOperator.asset";
        const string ProductionAgentOperatorPath = OrchestrationTaxonomyRoot + "/ChainRushBoardProductionAgentOperator.asset";
        const string ProductionYieldOperatorPath = OrchestrationTaxonomyRoot + "/ChainRushBoardProductionYieldOperator.asset";
        const string ProductionAvailableOperatorPath = OrchestrationTaxonomyRoot + "/ChainRushBoardProductionAvailableOperator.asset";
        const string MaterializedProductionOperatorPath = OrchestrationTaxonomyRoot + "/ChainRushBoardMaterializedProductionOperator.asset";
        const string EconomyStateModulePath = OrchestrationModulesRoot + "/ChainRushBoardEconomyState.asset";
        const string ProductionStateModulePath = OrchestrationModulesRoot + "/ChainRushBoardProductionState.asset";
        const string ProjectionStateModulePath = OrchestrationModulesRoot + "/ChainRushBoardProjectionState.asset";
        const string BrainPath = OrchestrationRoot + "/ChainRushBoardBrain.asset";
        const string OrchestrationPath = OrchestrationRoot + "/ChainRushBoardOrchestration.asset";

        const string RuntimeProfilePath = "Assets/Game/Runtime/Host/ChainRushGameRuntimeProfile.asset";
        const string EconomyDefinitionsInstallerPath = "Assets/Game/Runtime/Installers/ChainRushEconomyDefinitionsInstaller.asset";
        const string TaxonomyInstallerPath = "Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset";
        const string SkillsInstallerPath = "Assets/Game/Runtime/Installers/ChainRushGameplaySkillsInstaller.asset";
        const string FoundationInstallerPath = "Assets/Game/Runtime/Installers/ChainRushGameplayFoundationInstaller.asset";
        const string ProductionInstallerPath = "Assets/Game/Runtime/Installers/ChainRushProductionRuntimeInstaller.asset";
        const string SimulationControlInstallerPath = "Assets/Game/Runtime/Installers/ChainRushSimulationControlInstaller.asset";
        const string ProjectionInstallerPath = "Assets/Game/Runtime/Installers/ChainRushProjectionRuntimeInstaller.asset";

        const string UpgradedAssetPath = BoardRoot + "/Economy/ChainRushWaterBoardUpgraded.asset";
        const string UpgradedPrefabPath = BoardRoot + "/Projection/ChainRushWaterBoardUpgraded.prefab";

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
            ProductionAgentPath,
            PopulationObjectivePath,
            OperatorFamilyPath,
            PopulationAgentOperatorPath,
            ProductionAgentOperatorPath,
            ProductionYieldOperatorPath,
            ProductionAvailableOperatorPath,
            MaterializedProductionOperatorPath,
            EconomyStateModulePath,
            ProductionStateModulePath,
            ProjectionStateModulePath,
            BrainPath,
            OrchestrationPath,
        };

        [MenuItem("ChainRush/Activities/Board/Create Population Planner Assets")]
        public static void CreatePopulationPlannerAssets()
        {
            EnsureAssetDoesNotExist(PlannerPath);
            CapabilityHostData water = LoadRequired<CapabilityHostData>(WaterPath);

            EnsureFolder(PlannerRoot);
            try
            {
                ProgressivePlannerData planner = ScriptableObject.CreateInstance<ProgressivePlannerData>();
                planner.name = "ChainRushBoardPlanner";
                ConfigurePlanner(planner, water);
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

        [MenuItem("ChainRush/Activities/Board/Wire Vertical Slice Assets")]
        public static void WireVerticalSliceAssets()
        {
            EnsureVerticalSliceTargetsDoNotExist();

            ActivityData boardActivity = LoadRequired<ActivityData>(BoardActivityPath);
            CapabilityHostData boardHost = LoadRequired<CapabilityHostData>(BoardHostPath);
            EconomyWalletData boardWallet = LoadRequired<EconomyWalletData>(BoardWalletPath);
            TaxonomyTermData boardWalletTag = LoadRequired<TaxonomyTermData>(BoardWalletTagPath);
            CapabilityHostData water = LoadRequired<CapabilityHostData>(WaterPath);
            TaxonomyTermData waterTag = LoadRequired<TaxonomyTermData>(WaterTagPath);
            TaxonomyTermData boardCellTag = LoadRequired<TaxonomyTermData>(BoardCellTagPath);
            ProductionRecipeData mergeRecipe = LoadRequired<ProductionRecipeData>(MergeRecipePath);
            ProductionData mergeProduction = LoadRequired<ProductionData>(MergeProductionPath);
            ProductionCatalogData mergeCatalog = LoadRequired<ProductionCatalogData>(MergeCatalogPath);
            FrameworkSkillData mergeSkill = LoadRequired<FrameworkSkillData>(MergeSkillPath);
            ProgressivePlannerData planner = LoadRequired<ProgressivePlannerData>(PlannerPath);
            EconomyWalletData sharedWallet = LoadRequired<EconomyWalletData>(SharedWalletPath);
            TaxonomyTermData sharedWalletTag = LoadRequired<TaxonomyTermData>(SharedWalletTagPath);

            EnsureExistingVerticalSliceTargetsAreEmpty(
                boardActivity,
                boardHost,
                mergeRecipe,
                mergeProduction,
                mergeCatalog,
                mergeSkill);
            EnsureVerticalSliceFolders();

            var createdPaths = new List<string>(VerticalSliceCreatedPaths.Length);
            try
            {
                FrameworkResourceData turnToken = CreateEconomyAsset<FrameworkResourceData>(
                    TurnTokenPath,
                    "ChainRushBoardTurnToken",
                    "chainrush.resource.board-turn-token",
                    EconomyOperation.Require | EconomyOperation.Issue | EconomyOperation.Consume,
                    createdPaths);
                CapabilityHostData waterUnit = CreateCapabilityHost(
                    WaterUnitPath,
                    "ChainRushWaterUnit",
                    "chainrush.unit.water",
                    new List<CapabilityEntry>(0),
                    new List<WalletEntry>(0),
                    createdPaths);
                CapabilityHostData populationProducer = CreateCapabilityHost(
                    PopulationProducerPath,
                    "ChainRushBoardPopulationProducer",
                    "chainrush.board.population-producer",
                    new List<CapabilityEntry>
                    {
                        CreateCapabilityEntry(CapabilityHostType.ProductionOwner),
                    },
                    new List<WalletEntry>(0),
                    createdPaths);

                ProductionRecipeData refreshRecipe = CreateEconomyAsset<ProductionRecipeData>(
                    RefreshRecipePath,
                    "ChainRushBoardRefreshRecipe",
                    "chainrush.production.board.refresh.recipe",
                    EconomyOperation.Require | EconomyOperation.Issue,
                    createdPaths);
                ProductionRecipeData waterRecipe = CreateEconomyAsset<ProductionRecipeData>(
                    WaterRecipePath,
                    "ChainRushWaterBoardBaseRecipe",
                    "chainrush.production.board.water-base.recipe",
                    EconomyOperation.Require | EconomyOperation.Issue,
                    createdPaths);
                ProductionCatalogData populationCatalog = CreateEconomyAsset<ProductionCatalogData>(
                    PopulationCatalogPath,
                    "ChainRushBoardPopulationCatalog",
                    "chainrush.production.board.population.catalog",
                    EconomyOperation.Require | EconomyOperation.Issue,
                    createdPaths);
                ProductionData populationProduction = CreateEconomyAsset<ProductionData>(
                    PopulationProductionPath,
                    "ChainRushBoardPopulationProduction",
                    "chainrush.production.board.population",
                    EconomyOperation.Require | EconomyOperation.Issue,
                    createdPaths);

                ConfigureRefreshRecipe(refreshRecipe, turnToken, sharedWalletTag);
                ConfigureWaterRecipe(waterRecipe, water, boardWalletTag);
                ConfigureCatalog(populationCatalog, refreshRecipe, waterRecipe);
                ConfigureProduction(populationProduction, populationCatalog, boardCellTag);
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

                TaxonomyFamilyData operatorFamily = CreateTaxonomyFamily(
                    OperatorFamilyPath,
                    "ChainRushBoardOperatorFamily",
                    "chainrush.orchestration.board.operator",
                    "ChainRush Board Operator",
                    createdPaths);
                TaxonomyTermData populationAgentOperator = CreateTaxonomyTerm(
                    PopulationAgentOperatorPath,
                    "ChainRushBoardPopulationAgentOperator",
                    "chainrush.orchestration.board.agent.population",
                    "Board Population Agent",
                    operatorFamily,
                    0,
                    createdPaths);
                TaxonomyTermData productionAgentOperator = CreateTaxonomyTerm(
                    ProductionAgentOperatorPath,
                    "ChainRushBoardProductionAgentOperator",
                    "chainrush.orchestration.board.agent.production",
                    "Board Production Agent",
                    operatorFamily,
                    1,
                    createdPaths);
                TaxonomyTermData productionYieldOperator = CreateTaxonomyTerm(
                    ProductionYieldOperatorPath,
                    "ChainRushBoardProductionYieldOperator",
                    "chainrush.orchestration.board.production-yield",
                    "Board Production Yield",
                    operatorFamily,
                    2,
                    createdPaths);
                TaxonomyTermData productionAvailableOperator = CreateTaxonomyTerm(
                    ProductionAvailableOperatorPath,
                    "ChainRushBoardProductionAvailableOperator",
                    "chainrush.orchestration.board.production-available",
                    "Board Production Available",
                    operatorFamily,
                    3,
                    createdPaths);
                TaxonomyTermData materializedProductionOperator = CreateTaxonomyTerm(
                    MaterializedProductionOperatorPath,
                    "ChainRushBoardMaterializedProductionOperator",
                    "chainrush.orchestration.board.production-materialized",
                    "Board Materialized Production",
                    operatorFamily,
                    4,
                    createdPaths);

                ObjectiveTemplateData populationObjective = CreatePopulationObjective(
                    turnToken,
                    sharedWalletTag,
                    boardCellTag,
                    createdPaths);
                ActivityAgentDefinitionData populationAgent = CreatePopulationAgent(
                    planner,
                    refreshRecipe,
                    boardHost,
                    boardCellTag,
                    createdPaths);
                ActivityAgentDefinitionData productionAgent = CreateProductionAgent(
                    water,
                    createdPaths);
                EconomyStateOrchestrationModuleData economyState = CreateAsset<EconomyStateOrchestrationModuleData>(
                    EconomyStateModulePath,
                    "ChainRushBoardEconomyState",
                    createdPaths);
                ProductionStateOrchestrationModuleData productionState = CreateAsset<ProductionStateOrchestrationModuleData>(
                    ProductionStateModulePath,
                    "ChainRushBoardProductionState",
                    createdPaths);
                ProjectionStateOrchestrationModuleData projectionState = CreateAsset<ProjectionStateOrchestrationModuleData>(
                    ProjectionStateModulePath,
                    "ChainRushBoardProjectionState",
                    createdPaths);
                OrchestratorAIBrainData brain = CreateBrain(
                    populationAgent,
                    productionAgent,
                    populationAgentOperator,
                    productionAgentOperator,
                    productionYieldOperator,
                    productionAvailableOperator,
                    materializedProductionOperator,
                    createdPaths);
                ActivityOrchestrationConfigData orchestration = CreateOrchestration(
                    brain,
                    economyState,
                    productionState,
                    projectionState,
                    createdPaths);

                ConfigureMergeRecipe(mergeRecipe, water, waterUnit, boardWalletTag, sharedWalletTag);
                ConfigureCatalog(mergeCatalog, mergeRecipe);
                ConfigureProduction(mergeProduction, mergeCatalog, null);
                ConfigureMergeSkill(mergeSkill, mergeRecipe);
                ConfigureBoardHost(boardHost, boardWallet, mergeSkill, mergeProduction);
                ConfigureBoardWater(water, waterTag);
                ConfigureAddressable(
                    WaterProjectionPrefabPath,
                    "ChainRush-Activity-Board");
                ConfigureBoardActivity(
                    boardActivity,
                    sharedWallet,
                    boardWallet,
                    turnToken,
                    boardHost,
                    populationProducer,
                    populationObjective,
                    orchestration,
                    boardCellTag);
                ConfigureRuntime(
                    turnToken,
                    waterUnit,
                    populationProducer,
                    refreshRecipe,
                    waterRecipe,
                    populationProduction,
                    populationCatalog,
                    populationObjective,
                    populationAgent,
                    productionAgent,
                    operatorFamily,
                    waterTag,
                    populationAgentOperator,
                    productionAgentOperator,
                    productionYieldOperator,
                    productionAvailableOperator,
                    materializedProductionOperator,
                    economyState,
                    productionState,
                    projectionState,
                    brain,
                    orchestration,
                    boardWallet,
                    boardHost,
                    water,
                    mergeRecipe,
                    mergeProduction,
                    mergeCatalog,
                    mergeSkill,
                    boardActivity);
                ConfigureBoardUIPrefab(boardHost, mergeSkill);

                AssetDatabase.DeleteAsset(UpgradedAssetPath);
                AssetDatabase.DeleteAsset(UpgradedPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = orchestration;
                EditorGUIUtility.PingObject(orchestration);
                Debug.Log("[ChainRush] Board vertical slice assets were created and wired.");
            }
            catch
            {
                for (int i = createdPaths.Count - 1; i >= 0; i--)
                    AssetDatabase.DeleteAsset(createdPaths[i]);
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
            ActivityAgentDefinitionData populationAgent =
                LoadRequired<ActivityAgentDefinitionData>(PopulationAgentPath);
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
            TaxonomyTermData productionAgentOperator =
                LoadRequired<TaxonomyTermData>(ProductionAgentOperatorPath);
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
                productionAgentOperator,
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
                materializedProductionOperator.name = "ChainRushBoardMaterializedProductionOperator";
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
                economyState.name = "ChainRushBoardEconomyState";
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
                productionAgentOperator,
                "chainrush.orchestration.board.agent.production",
                "Board Production Agent",
                operatorFamily,
                1);
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
            ConfigureBrain(
                brain,
                populationAgent,
                LoadRequired<ActivityAgentDefinitionData>(ProductionAgentPath),
                populationAgentOperator,
                productionAgentOperator,
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
                productionAgentOperator,
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

        static void ConfigurePlanner(
            ProgressivePlannerData planner,
            CapabilityHostData water)
        {
            var serialized = new SerializedObject(planner);
            SerializedProperty patterns = serialized.FindProperty("patternRules");
            patterns.arraySize = 2;

            ConfigurePattern(
                patterns.GetArrayElementAtIndex(0),
                ProgressivePlannerData.PatternType.Line,
                3L,
                1L,
                1L);
            ConfigurePattern(
                patterns.GetArrayElementAtIndex(1),
                ProgressivePlannerData.PatternType.Single,
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
            ProgressivePlannerData.PatternType patternType,
            long size,
            long weight,
            long minimumCount)
        {
            property.FindPropertyRelative("patternType").enumValueIndex = (int)patternType;
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
            recipe.Inputs.Add(new ProductionInputData(new EconomyOperationData(
                EconomyOperation.Consume,
                new EconomyAssetAmountEntry(turnToken, 1L, EconomyFormType.Stack),
                new List<TaxonomyTermData> { sharedWalletTag })));
            recipe.Outputs.Clear();
            EditorUtility.SetDirty(recipe);
        }

        static void ConfigureWaterRecipe(
            ProductionRecipeData recipe,
            CapabilityHostData water,
            TaxonomyTermData boardWalletTag)
        {
            recipe.Inputs.Clear();
            recipe.Outputs.Clear();
            recipe.Outputs.Add(new ProductionOutputData(new EconomyOutputEntry(
                new EconomyAssetAmountEntry(water, 1L, EconomyFormType.Token),
                new List<TaxonomyTermData> { boardWalletTag })));
            EditorUtility.SetDirty(recipe);
        }

        static void ConfigureMergeRecipe(
            ProductionRecipeData recipe,
            CapabilityHostData water,
            CapabilityHostData waterUnit,
            TaxonomyTermData boardWalletTag,
            TaxonomyTermData sharedWalletTag)
        {
            recipe.Inputs.Clear();
            for (int i = 0; i < 3; i++)
            {
                recipe.Inputs.Add(new ProductionInputData(new EconomyOperationData(
                    EconomyOperation.Consume,
                    new EconomyAssetAmountEntry(water, 1L, EconomyFormType.Token),
                    new List<TaxonomyTermData> { boardWalletTag })));
            }

            recipe.Outputs.Clear();
            recipe.Outputs.Add(new ProductionOutputData(new EconomyOutputEntry(
                new EconomyAssetAmountEntry(waterUnit, 1L, EconomyFormType.Token),
                new List<TaxonomyTermData> { sharedWalletTag })));
            EditorUtility.SetDirty(recipe);
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

        static void ConfigureMergeSkill(
            FrameworkSkillData mergeSkill,
            ProductionRecipeData mergeRecipe)
        {
            if (mergeSkill.Effects.Count != 1
                || !(mergeSkill.Effects[0] is SkillProductionEffectData effect)
                || effect.InputMappings.Count != 3)
            {
                throw new InvalidOperationException(
                    "ChainRushBoardMergeSkill must contain exactly one Production effect with three input mappings.");
            }

            SetField(effect, "recipe", mergeRecipe);
            EditorUtility.SetDirty(mergeSkill);
        }

        static void ConfigureBoardHost(
            CapabilityHostData boardHost,
            EconomyWalletData boardWallet,
            FrameworkSkillData mergeSkill,
            ProductionData mergeProduction)
        {
            SetField(
                boardHost,
                "capabilities",
                new List<CapabilityEntry>
                {
                    CreateCapabilityEntry(CapabilityHostType.SkillOwner),
                    CreateCapabilityEntry(CapabilityHostType.ProductionOwner),
                    CreateCapabilityEntry(CapabilityHostType.ControlOwner),
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
                            new SeedEntry(mergeSkill, 1L, EconomyFormType.Stack),
                            new SeedEntry(mergeProduction, 1L, EconomyFormType.Stack),
                        }),
                });
            SetField(
                boardHost,
                "allowedOperations",
                EconomyOperation.Require
                | EconomyOperation.Issue
                | EconomyOperation.Consume
                | EconomyOperation.Transfer
                | EconomyOperation.Destroy);
            EditorUtility.SetDirty(boardHost);
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
            TaxonomyTermData boardCellTag,
            List<string> createdPaths)
        {
            var activation = new ObjectiveConditionEconomyMetric(
                new List<TaxonomyTermData> { sharedWalletTag },
                EconomyFormType.Stack,
                turnToken,
                1L,
                CompareOperation.GreaterOrEqual);
            var success = new ObjectiveConditionMarkerAvailability(
                null,
                EconomyFormType.Token,
                new List<TaxonomyTermData> { boardCellTag },
                0L,
                CompareOperation.Equal);
            var root = new ObjectiveNode(
                "chainrush-board-population",
                null,
                new List<ObjectiveCondition> { activation },
                new List<ObjectiveCondition> { success },
                new List<ObjectiveCondition>(0));
            ObjectiveTemplateData objective = ScriptableObject.CreateInstance<ObjectiveTemplateData>();
            objective.name = "ChainRushBoardPopulationObjective";
            SetField(objective, "root", root);
            AssetDatabase.CreateAsset(objective, PopulationObjectivePath);
            createdPaths.Add(PopulationObjectivePath);
            return objective;
        }

        static ActivityAgentDefinitionData CreatePopulationAgent(
            ProgressivePlannerData planner,
            ProductionRecipeData refreshRecipe,
            CapabilityHostData executorHost,
            TaxonomyTermData boardCellTag,
            List<string> createdPaths)
        {
            var agentData = new PopulationActivityOrchestrationAgentData();
            SetField(agentData, "planner", planner);
            SetField(agentData, "completionRecipe", refreshRecipe);
            SetField(agentData, "markerTags", new List<TaxonomyTermData> { boardCellTag });

            var match = new ObjectiveConditionMarkerAvailability(
                null,
                EconomyFormType.Token,
                new List<TaxonomyTermData> { boardCellTag },
                0L,
                CompareOperation.Equal);
            return CreateAgentDefinition(
                PopulationAgentPath,
                "ChainRushBoardPopulationAgent",
                "chainrush-board-population",
                100,
                new List<ObjectiveCondition> { match },
                new List<ActivityAgentSelectionCriterionData>
                {
                    CreateMaterializedCriterion(executorHost, null),
                    CreateOwnerCriterion(),
                },
                agentData,
                createdPaths);
        }

        static void ConfigurePopulationAgentExecutor(
            ActivityAgentDefinitionData populationAgent,
            CapabilityHostData executorHost)
        {
            SetField(
                populationAgent,
                "executorSelectionCriteria",
                new List<ActivityAgentSelectionCriterionData>
                {
                    CreateMaterializedCriterion(executorHost, null),
                    CreateOwnerCriterion(),
                });
            EditorUtility.SetDirty(populationAgent);
        }

        static void EnsurePopulationAgentExecutor(
            ActivityAgentDefinitionData populationAgent,
            CapabilityHostData executorHost)
        {
            List<ActivityAgentSelectionCriterionData> criteria =
                populationAgent.ExecutorSelectionCriteria;
            if (criteria.Count != 2
                || !(criteria[0] is MaterializedEntitySelectionCriterionData materialized)
                || materialized.EconomyAsset != executorHost
                || !(criteria[1] is EntityOwnerSelectionCriterionData))
            {
                throw new InvalidOperationException(
                    "Board Population Agent must use the Board host as its executor.");
            }
        }

        static ActivityAgentDefinitionData CreateProductionAgent(
            CapabilityHostData water,
            List<string> createdPaths)
        {
            var match = new ObjectiveConditionMaterializedEntity(
                EntityId.Invalid,
                water,
                EconomyFormType.Token,
                new List<TaxonomyTermData>(0),
                new List<CapabilityHostType>(0),
                1L,
                CompareOperation.GreaterOrEqual,
                SpatialMarkerRef.Invalid);
            return CreateAgentDefinition(
                ProductionAgentPath,
                "ChainRushBoardProductionAgent",
                "chainrush-board-production",
                90,
                new List<ObjectiveCondition> { match },
                new List<ActivityAgentSelectionCriterionData>
                {
                    CreateMaterializedCriterion(
                        null,
                        new List<CapabilityHostType> { CapabilityHostType.ProductionOwner }),
                    CreateOwnerCriterion(),
                },
                new ProductionActivityOrchestrationAgentData(),
                createdPaths);
        }

        static ActivityAgentDefinitionData CreateAgentDefinition(
            string path,
            string name,
            string id,
            int priority,
            List<ObjectiveCondition> matchConditions,
            List<ActivityAgentSelectionCriterionData> executorCriteria,
            ActivityOrchestrationAgentData agent,
            List<string> createdPaths)
        {
            ActivityAgentDefinitionData definition = ScriptableObject.CreateInstance<ActivityAgentDefinitionData>();
            definition.name = name;
            SetField(definition, "agentId", id);
            SetField(definition, "basePriority", priority);
            SetField(definition, "updateInterval", 1);
            SetField(definition, "matchConditions", matchConditions);
            SetField(definition, "executorSelectionCriteria", executorCriteria);
            SetField(definition, "targetSelectionCriteria", new List<ActivityAgentSelectionCriterionData>(0));
            SetField(definition, "controlType", ActivityAgentControlType.Endpoint);
            SetField(definition, "agent", agent);
            SetField(definition, "stopPolicyType", ActivityAgentStopPolicyType.None);
            SetField(definition, "executorBusyPolicyType", AgentExecutorBusyPolicyType.Wait);
            SetField(definition, "executorReservationPolicyType", ExecutorReservationPolicyType.PerWork);
            AssetDatabase.CreateAsset(definition, path);
            createdPaths.Add(path);
            return definition;
        }

        static MaterializedEntitySelectionCriterionData CreateMaterializedCriterion(
            EconomyAssetData asset,
            List<CapabilityHostType> capabilities)
        {
            var criterion = new MaterializedEntitySelectionCriterionData();
            SetField(criterion, "requirementType", ActivityAgentCriterionRequirementType.Required);
            SetField(criterion, "weight", 1);
            SetField(criterion, "economyAsset", asset);
            SetField(criterion, "economyFormType", EconomyFormType.Token);
            SetField(criterion, "requiredAssetTags", new List<TaxonomyTermData>(0));
            SetField(
                criterion,
                "requiredCapabilityTypes",
                capabilities ?? new List<CapabilityHostType>(0));
            SetField(criterion, "compareOperation", CompareOperation.GreaterOrEqual);
            SetField(criterion, "targetValue", 1L);
            return criterion;
        }

        static EntityOwnerSelectionCriterionData CreateOwnerCriterion()
        {
            var criterion = new EntityOwnerSelectionCriterionData();
            SetField(criterion, "requirementType", ActivityAgentCriterionRequirementType.Required);
            SetField(criterion, "weight", 1);
            SetField(criterion, "ownerSelectionType", ActivityAgentOwnerSelectionType.ParticipantOwner);
            return criterion;
        }

        static OrchestratorAIBrainData CreateBrain(
            ActivityAgentDefinitionData populationAgent,
            ActivityAgentDefinitionData productionAgent,
            TaxonomyTermData populationAgentOperator,
            TaxonomyTermData productionAgentOperator,
            TaxonomyTermData productionYieldOperator,
            TaxonomyTermData productionAvailableOperator,
            TaxonomyTermData materializedProductionOperator,
            List<string> createdPaths)
        {
            OrchestratorAIBrainData brain = ScriptableObject.CreateInstance<OrchestratorAIBrainData>();
            brain.name = "ChainRushBoardBrain";
            ConfigureBrain(
                brain,
                populationAgent,
                productionAgent,
                populationAgentOperator,
                productionAgentOperator,
                productionYieldOperator,
                productionAvailableOperator,
                materializedProductionOperator);
            AssetDatabase.CreateAsset(brain, BrainPath);
            createdPaths.Add(BrainPath);
            return brain;
        }

        static void ConfigureBrain(
            OrchestratorAIBrainData brain,
            ActivityAgentDefinitionData populationAgent,
            ActivityAgentDefinitionData productionAgent,
            TaxonomyTermData populationAgentOperator,
            TaxonomyTermData productionAgentOperator,
            TaxonomyTermData productionYieldOperator,
            TaxonomyTermData productionAvailableOperator,
            TaxonomyTermData materializedProductionOperator)
        {
            var populationOperation = new AgentDecompOpData();
            SetField(populationOperation, "operatorId", populationAgentOperator);
            SetField(populationOperation, "agentDefinition", populationAgent);
            var productionOperation = new AgentDecompOpData();
            SetField(productionOperation, "operatorId", productionAgentOperator);
            SetField(productionOperation, "agentDefinition", productionAgent);
            var yieldOperation = new ProductionYieldDecompOpData();
            SetField(yieldOperation, "operatorId", productionYieldOperator);
            var availableOperation = new ProductionAvailableDecompOpData();
            SetField(availableOperation, "operatorId", productionAvailableOperator);
            var materializedProductionOperation = new ProductionMaterializedEntityDecompOpData();
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
                        "board-production-agent",
                        OrchestrationFactType.MaterializedEntity,
                        productionAgentOperator,
                        true,
                        OrchestrationDecompositionScopeType.GlobalObjective),
                    CreateDecision(
                        "board-materialized-production",
                        OrchestrationFactType.MaterializedEntity,
                        materializedProductionOperator,
                        false,
                        OrchestrationDecompositionScopeType.AgentLocal),
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
                    productionOperation,
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
            orchestration.name = "ChainRushBoardOrchestration";
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
            TaxonomyTermData boardCellTag)
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
                        ActivitySeedMaterializationType.None),
                });

            ActivityTeamWalletData boardWalletData = default;
            SetStructField(ref boardWalletData, "wallet", boardWallet);
            SetStructField(
                ref boardWalletData,
                "seed",
                new List<ActivityWalletSeedEntryData>
                {
                    new ActivityWalletSeedEntryData(
                        new SeedEntry(boardHost, 1L, EconomyFormType.Token),
                        ActivitySeedMaterializationType.NonSpatial),
                    new ActivityWalletSeedEntryData(
                        new SeedEntry(populationProducer, 1L, EconomyFormType.Token),
                        ActivitySeedMaterializationType.NonSpatial),
                });

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

            if (activity.Space == null
                || activity.Space.MarkerProviders.Count != 1
                || !(activity.Space.MarkerProviders[0] is SpatialGridProviderData grid))
            {
                throw new InvalidOperationException(
                    "ChainRushBoardActivity must contain exactly one SpatialGridProviderData.");
            }

            SetField(grid, "providerType", boardCellTag);
            EditorUtility.SetDirty(activity);
        }

        static void ConfigureRuntime(
            EconomyAssetData turnToken,
            CapabilityHostData waterUnit,
            CapabilityHostData populationProducer,
            ProductionRecipeData refreshRecipe,
            ProductionRecipeData waterRecipe,
            ProductionData populationProduction,
            ProductionCatalogData populationCatalog,
            ObjectiveTemplateData populationObjective,
            ActivityAgentDefinitionData populationAgent,
            ActivityAgentDefinitionData productionAgent,
            TaxonomyFamilyData operatorFamily,
            TaxonomyTermData waterTag,
            TaxonomyTermData populationAgentOperator,
            TaxonomyTermData productionAgentOperator,
            TaxonomyTermData productionYieldOperator,
            TaxonomyTermData productionAvailableOperator,
            TaxonomyTermData materializedProductionOperator,
            EconomyStateOrchestrationModuleData economyState,
            ProductionStateOrchestrationModuleData productionState,
            ProjectionStateOrchestrationModuleData projectionState,
            OrchestratorAIBrainData brain,
            ActivityOrchestrationConfigData orchestration,
            EconomyWalletData boardWallet,
            CapabilityHostData boardHost,
            CapabilityHostData water,
            ProductionRecipeData mergeRecipe,
            ProductionData mergeProduction,
            ProductionCatalogData mergeCatalog,
            FrameworkSkillData mergeSkill,
            ActivityData boardActivity)
        {
            EconomyDefinitionsInstallerData economyInstaller =
                LoadRequired<EconomyDefinitionsInstallerData>(EconomyDefinitionsInstallerPath);
            var assets = new List<EconomyAssetData>(GetField<List<EconomyAssetData>>(economyInstaller, "assets"));
            AddUnique(
                assets,
                turnToken,
                waterUnit,
                populationProducer,
                refreshRecipe,
                waterRecipe,
                populationProduction,
                populationCatalog,
                boardHost,
                water,
                mergeRecipe,
                mergeProduction,
                mergeCatalog,
                mergeSkill,
                boardActivity);
            SetField(economyInstaller, "assets", assets);

            var wallets = new List<EconomyWalletData>(GetField<List<EconomyWalletData>>(economyInstaller, "wallets"));
            AddUnique(wallets, boardWallet);
            SetField(economyInstaller, "wallets", wallets);
            EditorUtility.SetDirty(economyInstaller);

            TaxonomyRuntimeInstallerData taxonomyInstaller =
                LoadRequired<TaxonomyRuntimeInstallerData>(TaxonomyInstallerPath);
            var families = new List<TaxonomyFamilyData>(
                GetField<TaxonomyFamilyData[]>(taxonomyInstaller, "families")
                ?? new TaxonomyFamilyData[0]);
            AddUnique(families, operatorFamily);
            SetField(taxonomyInstaller, "families", families.ToArray());
            var terms = new List<TaxonomyTermData>(
                GetField<TaxonomyTermData[]>(taxonomyInstaller, "terms")
                ?? new TaxonomyTermData[0]);
            AddUnique(
                terms,
                waterTag,
                populationAgentOperator,
                productionAgentOperator,
                productionYieldOperator,
                productionAvailableOperator,
                materializedProductionOperator);
            SetField(taxonomyInstaller, "terms", terms.ToArray());
            EditorUtility.SetDirty(taxonomyInstaller);

            GameplaySkillsInstallerData skillsInstaller =
                LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var skills = new List<FrameworkSkillData>(
                GetField<List<FrameworkSkillData>>(skillsInstaller, "skills"));
            AddUnique(skills, mergeSkill);
            SetField(skillsInstaller, "skills", skills);
            EditorUtility.SetDirty(skillsInstaller);

            GameRuntimeProfileData profile = LoadRequired<GameRuntimeProfileData>(RuntimeProfilePath);
            var installers = new List<GameRuntimeInstallerData>(profile.Installers);
            AddUnique(
                installers,
                LoadRequired<GameRuntimeInstallerData>(FoundationInstallerPath),
                LoadRequired<GameRuntimeInstallerData>(SkillsInstallerPath),
                LoadRequired<GameRuntimeInstallerData>(ProductionInstallerPath),
                LoadRequired<GameRuntimeInstallerData>(SimulationControlInstallerPath),
                LoadRequired<GameRuntimeInstallerData>(ProjectionInstallerPath));
            SetField(profile, "installers", installers);
            EditorUtility.SetDirty(profile);

            EditorUtility.SetDirty(populationObjective);
            EditorUtility.SetDirty(populationAgent);
            EditorUtility.SetDirty(productionAgent);
            EditorUtility.SetDirty(economyState);
            EditorUtility.SetDirty(productionState);
            EditorUtility.SetDirty(projectionState);
            EditorUtility.SetDirty(brain);
            EditorUtility.SetDirty(orchestration);
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
            FrameworkSkillData mergeSkill)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BoardUIPrefabPath);
            try
            {
                ChainRushBoardUIController controller =
                    root.GetComponent<ChainRushBoardUIController>();
                if (controller == null)
                {
                    throw new InvalidOperationException(
                        "ChainRushBoardUI prefab has no ChainRushBoardUIController.");
                }

                var serialized = new SerializedObject(controller);
                serialized.FindProperty("boardHostDefinition").objectReferenceValue = boardHost;
                serialized.FindProperty("mergeSkill").objectReferenceValue = mergeSkill;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BoardUIPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
            TaxonomyTermData productionAgentOperator,
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
                    productionAgentOperator,
                    "chainrush.orchestration.board.agent.production",
                    operatorFamily,
                    1)
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
            TaxonomyTermData productionAgentOperator,
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
                    productionAgentOperator,
                    "chainrush.orchestration.board.agent.production",
                    operatorFamily,
                    1)
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

        static void EnsureExistingVerticalSliceTargetsAreEmpty(
            ActivityData boardActivity,
            CapabilityHostData boardHost,
            ProductionRecipeData mergeRecipe,
            ProductionData mergeProduction,
            ProductionCatalogData mergeCatalog,
            FrameworkSkillData mergeSkill)
        {
            if (boardActivity.Teams.Count != 1
                || boardActivity.Teams[0].Objectives.Count != 0
                || boardActivity.Teams[0].Features.Count != 0
                || boardActivity.Teams[0].Wallets.Count != 1
                || boardHost.WalletEntries.Count != 0
                || mergeRecipe.Inputs.Count != 0
                || mergeRecipe.Outputs.Count != 0
                || mergeProduction.SupportedCatalogs.Count != 0
                || mergeCatalog.Entries.Count != 0
                || mergeSkill.Effects.Count != 1
                || !(mergeSkill.Effects[0] is SkillProductionEffectData effect)
                || effect.Recipe != null)
            {
                throw new InvalidOperationException(
                    "Board vertical slice skeleton is not in the expected unwired state. Refusing to rewrite existing authoring.");
            }
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
