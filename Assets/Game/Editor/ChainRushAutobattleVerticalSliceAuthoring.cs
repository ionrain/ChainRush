using System;
using System.Collections.Generic;
using System.Reflection;
using ChainRush.Autobattle;
using Core;
using Core.AI;
using Core.AI.Actions;
using Core.AI.Conditions;
using Core.Activities;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Diplomacy;
using Core.Diplomacy;
using Core.Diplomacy.Authoring;
using Core.Drops;
using Core.Drops.GameRuntime.Installers;
using Core.Economy;
using Core.Economy.Authoring;
using Core.GameRuntime;
using Core.GameRuntime.Installers;
using Core.GameFlow;
using Core.HostValues;
using Core.Objectives;
using Core.Orchestration;
using Core.Production;
using Core.Production.Authoring;
using Core.Projection;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using FrameworkMovementData = Core.World.MovementData;
using FrameworkResourceData = Core.Economy.Modules.ResourceEconomyModule.ResourceData;
using FrameworkSkillData = Core.Skills.SkillData;

namespace ChainRush.Editor
{
    /// <summary>
    /// Explicit authoring for the Autobattle + Board working model.
    /// Existing assets are rewritten only after their expected pre-wiring state is verified.
    /// </summary>
    public static class ChainRushAutobattleVerticalSliceAuthoring
    {
        const string AutobattleRoot = "Assets/Game/Activities/Autobattle";
        const string BoardRoot = "Assets/Game/Activities/Board";
        const string SharedRoot = "Assets/Game/Activities/Shared";
        const string RuntimeRoot = "Assets/Game/Runtime";
        const string OccupancyRoot = SharedRoot + "/Spatial/Occupancy";
        const string OccupancyFamilyPath = OccupancyRoot + "/SpatialOccupancyFamily.asset";
        const string MobileSolidPath = OccupancyRoot + "/MobileSolid.asset";
        const string StaticSolidPath = OccupancyRoot + "/StaticSolid.asset";
        const string PlacementObstaclePath = OccupancyRoot + "/PlacementObstacle.asset";
        const string NonOccupyingPath = OccupancyRoot + "/NonOccupying.asset";
        const string OccupancyMatrixPath = OccupancyRoot + "/SpatialOccupancyMatrix.asset";

        const string ActivityPath = AutobattleRoot + "/Definition/AutobattleActivity.asset";
        const string TopologyPath = AutobattleRoot + "/Topology/AutobattleTopology.asset";
        const string SpacePrefabPath = AutobattleRoot + "/Space/AutobattleSpace.prefab";
        const string IntegrationScenePath =
            "Assets/Game/Scenes/Integration/ChainRushFrameworkIntegration.unity";
        const string AutobattleFlowPath = AutobattleRoot + "/GameFlow/AutobattleFlow.asset";
        const string BoardFlowPath = BoardRoot + "/GameFlow/BoardFlow.asset";
        const string LegacyEnemyPrefabPath = "Assets/Game/Prefabs/Enemies/BugBrownSmall.prefab";

        const string SharedWalletPath = SharedRoot + "/Economy/ActivityWallet.asset";
        const string SharedWalletTagPath = SharedRoot + "/Economy/ActivityWalletTag.asset";
        const string ExperiencePath = SharedRoot + "/Economy/Experience.asset";
        const string TurnTokenPath = SharedRoot + "/Economy/BoardTurnToken.asset";
        const string WaterUnitPath = SharedRoot + "/Units/Water/WaterUnit.asset";
        const string ActivityTaxonomyFamilyPath =
            SharedRoot + "/Taxonomy/ActivityTaxonomyFamily.asset";
        const string ExperienceRecipePath =
            AutobattleRoot + "/Production/ExperienceToTurnTokenRecipe.asset";

        const string BoardActivityPath = BoardRoot + "/Definition/BoardActivity.asset";
        const string BoardPopulationObjectivePath =
            BoardRoot + "/Objectives/BoardPopulationObjective.asset";
        const string BoardWaterBasePath = BoardRoot + "/Economy/WaterBoardBase.asset";
        const string BoardMergeRecipePath = BoardRoot + "/Production/BoardMergeRecipe4.asset";

        const string EconomyInstallerPath =
            RuntimeRoot + "/Installers/ChainRushEconomyDefinitionsInstaller.asset";
        const string EconomyRuntimeInstallerPath =
            RuntimeRoot + "/Installers/ChainRushEconomyRuntimeInstaller.asset";
        const string TaxonomyInstallerPath =
            RuntimeRoot + "/Installers/ChainRushTaxonomyRuntimeInstaller.asset";
        const string FoundationInstallerPath =
            RuntimeRoot + "/Installers/ChainRushGameplayFoundationInstaller.asset";
        const string SkillsInstallerPath =
            RuntimeRoot + "/Installers/ChainRushGameplaySkillsInstaller.asset";
        const string SkillsCatalogPath = BoardRoot + "/Skills/SkillAdapters.asset";
        const string RuntimeProfilePath =
            RuntimeRoot + "/Host/ChainRushGameRuntimeProfile.asset";

        const string EconomyRoot = AutobattleRoot + "/Economy";
        const string AgentsRoot = AutobattleRoot + "/Agents";
        const string AIRoot = AutobattleRoot + "/AI";
        const string AITaxonomyRoot = AIRoot + "/Taxonomy";
        const string DropsRoot = AutobattleRoot + "/Drops";
        const string HostValuesRoot = AutobattleRoot + "/HostValues";
        const string MovementRoot = AutobattleRoot + "/Movement";
        const string ObjectivesRoot = AutobattleRoot + "/Objectives";
        const string OrchestrationRoot = AutobattleRoot + "/Orchestration";
        const string OrchestrationModulesRoot = OrchestrationRoot + "/Modules";
        const string OrchestrationTaxonomyRoot = OrchestrationRoot + "/Taxonomy";
        const string ProductionRoot = AutobattleRoot + "/Production";
        const string ProjectionRoot = AutobattleRoot + "/Projection";
        const string SpaceRoot = AutobattleRoot + "/Space";
        const string ShapesRoot = SpaceRoot + "/Shapes";
        const string SkillsRoot = AutobattleRoot + "/Skills";
        const string TaxonomyRoot = AutobattleRoot + "/Taxonomy";
        const string UIRoot = AutobattleRoot + "/UI";

        const string UnitWalletPath = EconomyRoot + "/UnitWallet.asset";
        const string DropContentsWalletPath = EconomyRoot + "/DropContentsWallet.asset";
        const string WorldWalletPath = EconomyRoot + "/WorldWallet.asset";
        const string CollectorWalletPath = EconomyRoot + "/ExperienceCollectorWallet.asset";
        const string UnitWalletTagPath = EconomyRoot + "/UnitWalletTag.asset";
        const string DropContentsWalletTagPath = EconomyRoot + "/DropContentsWalletTag.asset";
        const string WorldWalletTagPath = EconomyRoot + "/WorldWalletTag.asset";
        const string CollectorWalletTagPath = EconomyRoot + "/ExperienceCollectorWalletTag.asset";

        const string PlayerSpawnerPath = EconomyRoot + "/PlayerSpawner.asset";
        const string EnemySpawnerPath = EconomyRoot + "/EnemySpawner.asset";
        const string EnemyPath = EconomyRoot + "/BugBrownSmall.asset";
        const string ExperienceDropPath = EconomyRoot + "/ExperienceDrop.asset";
        const string ExperienceCollectorPath = EconomyRoot + "/ExperienceCollector.asset";

        const string HealthPath = HostValuesRoot + "/Health.asset";
        const string MovementPath = MovementRoot + "/UnitMovement.asset";
        const string ApproachSkillPath = SkillsRoot + "/ApproachSkill.asset";
        const string AttackSkillPath = SkillsRoot + "/AttackSkill.asset";
        const string CollectionSkillPath = SkillsRoot + "/ExperienceCollectionSkill.asset";
        const string AlliedCombatBrainPath = AIRoot + "/AlliedCombatBrain.asset";
        const string EnemyCombatBrainPath = AIRoot + "/EnemyCombatBrain.asset";
        const string CollectorBrainPath = AIRoot + "/ExperienceCollectorBrain.asset";
        const string DropProfilePath = DropsRoot + "/ExperienceDropProfile.asset";

        const string PlayerProductionPath = ProductionRoot + "/PlayerProduction.asset";
        const string PlayerCatalogPath = ProductionRoot + "/PlayerProductionCatalog.asset";
        const string DeploymentRecipePath = ProductionRoot + "/WaterUnitDeploymentRecipe.asset";
        const string EnemyProductionPath = ProductionRoot + "/EnemyWaveProduction.asset";
        const string EnemyCatalogPath = ProductionRoot + "/EnemyWaveCatalog.asset";
        const string EnemyWaveRecipePath = ProductionRoot + "/EnemyWaveRecipe.asset";
        const string DropProductionPath = ProductionRoot + "/ExperienceDropProduction.asset";
        const string DropCatalogPath = ProductionRoot + "/ExperienceDropCatalog.asset";
        const string DropRecipePath = ProductionRoot + "/ExperienceDropRecipe.asset";

        const string PlayerDeploymentObjectivePath =
            ObjectivesRoot + "/PlayerDeploymentObjective.asset";
        const string TurnTokenObjectivePath = ObjectivesRoot + "/TurnTokenObjective.asset";
        const string EnemyWaveObjectivePath = ObjectivesRoot + "/EnemyWaveObjective.asset";
        const string TurnTokenAgentPath = AgentsRoot + "/TurnTokenProductionAgent.asset";
        const string EnemyWaveAgentPath = AgentsRoot + "/EnemyWaveAgent.asset";

        const string EconomyStatePath = OrchestrationModulesRoot + "/EconomyState.asset";
        const string ProductionStatePath = OrchestrationModulesRoot + "/ProductionState.asset";
        const string ProjectionStatePath = OrchestrationModulesRoot + "/ProjectionState.asset";
        const string PlayerBrainPath = OrchestrationRoot + "/PlayerBrain.asset";
        const string EnemyBrainPath = OrchestrationRoot + "/EnemyBrain.asset";
        const string PlayerOrchestrationPath = OrchestrationRoot + "/PlayerOrchestration.asset";
        const string EnemyOrchestrationPath = OrchestrationRoot + "/EnemyOrchestration.asset";

        const string RoleFamilyPath = TaxonomyRoot + "/AutobattleRoleFamily.asset";
        const string PlayerSpawnerRolePath = TaxonomyRoot + "/PlayerSpawnerRole.asset";
        const string EnemySpawnerRolePath = TaxonomyRoot + "/EnemySpawnerRole.asset";
        const string CombatantRolePath = TaxonomyRoot + "/CombatantRole.asset";
        const string AlliedUnitRolePath = TaxonomyRoot + "/AlliedUnitRole.asset";
        const string EnemyUnitRolePath = TaxonomyRoot + "/EnemyUnitRole.asset";
        const string ExperienceDropRolePath = TaxonomyRoot + "/ExperienceDropRole.asset";
        const string ProjectionTargetFamilyPath = TaxonomyRoot + "/ProjectionTargetFamily.asset";
        const string ExperienceProgressTargetPath = TaxonomyRoot + "/ExperienceProgressTarget.asset";
        const string IntegrationRuntimeTagPath =
            AutobattleRoot + "/Definition/IntegrationAutobattle.asset";

        const string MarkerFamilyPath = TaxonomyRoot + "/AutobattleMarkerFamily.asset";
        const string PlayerAnchorPath = TaxonomyRoot + "/PlayerSpawnerAnchor.asset";
        const string EnemyAnchorPath = TaxonomyRoot + "/EnemySpawnerAnchor.asset";
        const string AlliedSpawnPath = TaxonomyRoot + "/AlliedSpawn.asset";
        const string EnemySpawnPath = TaxonomyRoot + "/EnemySpawn.asset";
        const string DropPositionPath = TaxonomyRoot + "/DropPosition.asset";

        const string AIFamilyPath = AITaxonomyRoot + "/AIStateFamily.asset";
        const string CombatNodePath = AITaxonomyRoot + "/CombatNode.asset";
        const string CombatStateAPath = AITaxonomyRoot + "/CombatStateA.asset";
        const string CombatStateBPath = AITaxonomyRoot + "/CombatStateB.asset";
        const string DefeatStatePath = AITaxonomyRoot + "/DefeatState.asset";
        const string CollectionNodePath = AITaxonomyRoot + "/CollectionNode.asset";
        const string WaitingStatePath = AITaxonomyRoot + "/WaitingState.asset";
        const string CollectStatePath = AITaxonomyRoot + "/CollectState.asset";
        const string SearchStatePath = AITaxonomyRoot + "/SearchState.asset";
        const string CombatTargetPath = AITaxonomyRoot + "/CombatTarget.asset";
        const string CollectionTargetPath = AITaxonomyRoot + "/CollectionTarget.asset";

        const string OperatorFamilyPath =
            OrchestrationTaxonomyRoot + "/AutobattleOperatorFamily.asset";
        const string TurnTokenAgentOperatorPath =
            OrchestrationTaxonomyRoot + "/TurnTokenAgentOperator.asset";
        const string EnemyWaveAgentOperatorPath =
            OrchestrationTaxonomyRoot + "/EnemyWaveAgentOperator.asset";
        const string ProductionInputOperatorPath =
            OrchestrationTaxonomyRoot + "/ProductionInputOperator.asset";
        const string ProductionEconomyOperatorPath =
            OrchestrationTaxonomyRoot + "/ProductionEconomyOperator.asset";
        const string AwaitFactOperatorPath =
            OrchestrationTaxonomyRoot + "/AwaitFactOperator.asset";
        const string ProductionMaterializedOperatorPath =
            OrchestrationTaxonomyRoot + "/ProductionMaterializedOperator.asset";
        const string ProductionYieldOperatorPath =
            OrchestrationTaxonomyRoot + "/ProductionYieldOperator.asset";
        const string ProductionAvailableOperatorPath =
            OrchestrationTaxonomyRoot + "/ProductionAvailableOperator.asset";

        const string PlayerSpawnerPrefabPath = ProjectionRoot + "/PlayerSpawner.prefab";
        const string EnemySpawnerPrefabPath = ProjectionRoot + "/EnemySpawner.prefab";
        const string WaterUnitPrefabPath = ProjectionRoot + "/WaterUnit.prefab";
        const string EnemyPrefabPath = ProjectionRoot + "/BugBrownSmall.prefab";
        const string ExperienceDropPrefabPath = ProjectionRoot + "/ExperienceDrop.prefab";
        const string ExperienceCollectorPrefabPath = ProjectionRoot + "/ExperienceCollector.prefab";
        const string SpawnAreaShapePath = ShapesRoot + "/SpawnArea.asset";
        const string PlayerSpawnerPoolKey = "chainrush.autobattle.player-spawner";
        const string EnemySpawnerPoolKey = "chainrush.autobattle.enemy-spawner";
        const string WaterUnitPoolKey = "chainrush.autobattle.water-unit";
        const string EnemyPoolKey = "chainrush.autobattle.enemy.bug-brown-small";
        const string ExperienceDropPoolKey = "chainrush.autobattle.experience-drop";
        const string ExperienceCollectorPoolKey = "chainrush.autobattle.experience-collector";
        const string AlliedMaterialPath = ProjectionRoot + "/AlliedMaterial.mat";
        const string EnemyMaterialPath = ProjectionRoot + "/EnemyMaterial.mat";
        const string NeutralMaterialPath = ProjectionRoot + "/NeutralMaterial.mat";
        const string ExperienceMaterialPath = ProjectionRoot + "/ExperienceMaterial.mat";
        const string ExperienceUIPrefabPath = UIRoot + "/ExperienceUI.prefab";

        const string HostValuesInstallerPath =
            RuntimeRoot + "/Installers/ChainRushGameplayHostValuesInstaller.asset";
        const string DropInstallerPath =
            RuntimeRoot + "/Installers/ChainRushDropRuntimeInstaller.asset";
        const string DiplomacyInstallerPath =
            RuntimeRoot + "/Installers/ChainRushDiplomacyRuntimeInstaller.asset";
        const string DiplomacyRoot = RuntimeRoot + "/Diplomacy";
        const string ActivityDiplomacyPath = DiplomacyRoot + "/ActivityDiplomacyModule.asset";
        const string CapabilityHostDiplomacyPath =
            DiplomacyRoot + "/CapabilityHostDiplomacyModule.asset";

        const string AddressablesGroup = "ChainRush-Activity-Autobattle";

        static readonly string[] CreatedPaths =
        {
            UnitWalletPath,
            DropContentsWalletPath,
            WorldWalletPath,
            CollectorWalletPath,
            UnitWalletTagPath,
            DropContentsWalletTagPath,
            WorldWalletTagPath,
            CollectorWalletTagPath,
            PlayerSpawnerPath,
            EnemySpawnerPath,
            EnemyPath,
            ExperienceDropPath,
            ExperienceCollectorPath,
            HealthPath,
            MovementPath,
            ApproachSkillPath,
            AttackSkillPath,
            CollectionSkillPath,
            AlliedCombatBrainPath,
            EnemyCombatBrainPath,
            CollectorBrainPath,
            DropProfilePath,
            PlayerProductionPath,
            PlayerCatalogPath,
            DeploymentRecipePath,
            EnemyProductionPath,
            EnemyCatalogPath,
            EnemyWaveRecipePath,
            DropProductionPath,
            DropCatalogPath,
            DropRecipePath,
            PlayerDeploymentObjectivePath,
            TurnTokenObjectivePath,
            EnemyWaveObjectivePath,
            TurnTokenAgentPath,
            EnemyWaveAgentPath,
            EconomyStatePath,
            ProductionStatePath,
            ProjectionStatePath,
            PlayerBrainPath,
            EnemyBrainPath,
            PlayerOrchestrationPath,
            EnemyOrchestrationPath,
            RoleFamilyPath,
            PlayerSpawnerRolePath,
            EnemySpawnerRolePath,
            CombatantRolePath,
            AlliedUnitRolePath,
            EnemyUnitRolePath,
            ExperienceDropRolePath,
            ProjectionTargetFamilyPath,
            ExperienceProgressTargetPath,
            IntegrationRuntimeTagPath,
            MarkerFamilyPath,
            PlayerAnchorPath,
            EnemyAnchorPath,
            AlliedSpawnPath,
            EnemySpawnPath,
            DropPositionPath,
            AIFamilyPath,
            CombatNodePath,
            CombatStateAPath,
            CombatStateBPath,
            DefeatStatePath,
            CollectionNodePath,
            WaitingStatePath,
            CollectStatePath,
            SearchStatePath,
            CombatTargetPath,
            CollectionTargetPath,
            OperatorFamilyPath,
            TurnTokenAgentOperatorPath,
            EnemyWaveAgentOperatorPath,
            ProductionInputOperatorPath,
            ProductionEconomyOperatorPath,
            AwaitFactOperatorPath,
            ProductionMaterializedOperatorPath,
            ProductionYieldOperatorPath,
            ProductionAvailableOperatorPath,
            PlayerSpawnerPrefabPath,
            EnemySpawnerPrefabPath,
            WaterUnitPrefabPath,
            EnemyPrefabPath,
            ExperienceDropPrefabPath,
            ExperienceCollectorPrefabPath,
            SpawnAreaShapePath,
            AlliedMaterialPath,
            EnemyMaterialPath,
            NeutralMaterialPath,
            ExperienceMaterialPath,
            ExperienceUIPrefabPath,
            HostValuesInstallerPath,
            DropInstallerPath,
            DiplomacyInstallerPath,
            ActivityDiplomacyPath,
            CapabilityHostDiplomacyPath,
        };

        static readonly List<string> MobileSolidHostPaths = new List<string>
        {
            EnemyPath,
            WaterUnitPath
        };

        static readonly List<string> StaticSolidHostPaths = new List<string>
        {
            PlayerSpawnerPath,
            EnemySpawnerPath
        };

        static readonly List<string> PlacementObstacleHostPaths = new List<string>
        {
            ExperienceDropPath,
            BoardWaterBasePath
        };

        static readonly List<string> NonOccupyingHostPaths = new List<string>
        {
            ExperienceCollectorPath,
            BoardRoot + "/Economy/BoardHost.asset",
            BoardRoot + "/Economy/BoardPopulationProducer.asset"
        };

        sealed class Content
        {
            public EconomyWalletData SharedWallet;
            public TaxonomyTermData SharedWalletTag;
            public FrameworkResourceData Experience;
            public FrameworkResourceData TurnToken;
            public CapabilityHostData WaterUnit;
            public ProductionRecipeData ExperienceRecipe;

            public EconomyWalletData UnitWallet;
            public EconomyWalletData DropContentsWallet;
            public EconomyWalletData WorldWallet;
            public EconomyWalletData CollectorWallet;
            public TaxonomyTermData UnitWalletTag;
            public TaxonomyTermData DropContentsWalletTag;
            public TaxonomyTermData WorldWalletTag;
            public TaxonomyTermData CollectorWalletTag;

            public CapabilityHostData PlayerSpawner;
            public CapabilityHostData EnemySpawner;
            public CapabilityHostData Enemy;
            public CapabilityHostData ExperienceDrop;
            public CapabilityHostData ExperienceCollector;
            public HostValueData Health;
            public FrameworkMovementData Movement;
            public FrameworkSkillData ApproachSkill;
            public FrameworkSkillData AttackSkill;
            public FrameworkSkillData CollectionSkill;
            public SpatialShapeData SpawnArea;
            public AIBrainData AlliedCombatBrain;
            public AIBrainData EnemyCombatBrain;
            public AIBrainData CollectorBrain;
            public DropProfileData DropProfile;

            public ProductionRecipeData DeploymentRecipe;
            public ProductionRecipeData EnemyWaveRecipe;
            public ProductionRecipeData DropRecipe;
            public ProductionCatalogData PlayerCatalog;
            public ProductionCatalogData EnemyCatalog;
            public ProductionCatalogData DropCatalog;
            public ProductionData PlayerProduction;
            public ProductionData EnemyProduction;
            public ProductionData DropProduction;

            public ObjectiveTemplateData PlayerDeploymentObjective;
            public ObjectiveTemplateData TurnTokenObjective;
            public ObjectiveTemplateData EnemyWaveObjective;
            public AgentDefinitionData TurnTokenAgent;
            public AgentDefinitionData EnemyWaveAgent;
            public EconomyStateOrchestrationModuleData EconomyState;
            public ProductionStateOrchestrationModuleData ProductionState;
            public ProjectionStateOrchestrationModuleData ProjectionState;
            public OrchestratorAIBrainData PlayerBrain;
            public OrchestratorAIBrainData EnemyBrain;
            public ActivityOrchestrationConfigData PlayerOrchestration;
            public ActivityOrchestrationConfigData EnemyOrchestration;

            public readonly List<TaxonomyFamilyData> Families = new List<TaxonomyFamilyData>();
            public readonly List<TaxonomyTermData> Terms = new List<TaxonomyTermData>();

            public TaxonomyTermData PlayerSpawnerRole;
            public TaxonomyTermData EnemySpawnerRole;
            public TaxonomyTermData CombatantRole;
            public TaxonomyTermData AlliedUnitRole;
            public TaxonomyTermData EnemyUnitRole;
            public TaxonomyTermData ExperienceDropRole;
            public TaxonomyTermData ExperienceProgressTarget;
            public TaxonomyTermData IntegrationRuntimeTag;
            public TaxonomyTermData PlayerAnchor;
            public TaxonomyTermData EnemyAnchor;
            public TaxonomyTermData AlliedSpawn;
            public TaxonomyTermData EnemySpawn;
            public TaxonomyTermData DropPosition;
            public TaxonomyTermData CombatNode;
            public TaxonomyTermData CombatStateA;
            public TaxonomyTermData CombatStateB;
            public TaxonomyTermData DefeatState;
            public TaxonomyTermData CollectionNode;
            public TaxonomyTermData WaitingState;
            public TaxonomyTermData CollectState;
            public TaxonomyTermData SearchState;
            public TaxonomyTermData CombatTarget;
            public TaxonomyTermData CollectionTarget;
            public TaxonomyTermData TurnTokenAgentOperator;
            public TaxonomyTermData EnemyWaveAgentOperator;
            public TaxonomyTermData ProductionInputOperator;
            public TaxonomyTermData ProductionEconomyOperator;
            public TaxonomyTermData AwaitFactOperator;
            public TaxonomyTermData ProductionMaterializedOperator;
            public TaxonomyTermData ProductionYieldOperator;
            public TaxonomyTermData ProductionAvailableOperator;
        }

        [MenuItem("ChainRush/Activities/Autobattle/Create Vertical Slice")]
        public static void CreateVerticalSlice()
        {
            EnsureCreatedTargetsDoNotExist();
            EnsureFolders();

            ActivityData activity = LoadRequired<ActivityData>(ActivityPath);
            TopologyDefinitionData topology = LoadRequired<TopologyDefinitionData>(TopologyPath);
            ActivityData boardActivity = LoadRequired<ActivityData>(BoardActivityPath);
            ProductionRecipeData boardMergeRecipe =
                LoadRequired<ProductionRecipeData>(BoardMergeRecipePath);

            var content = new Content
            {
                SharedWallet = LoadRequired<EconomyWalletData>(SharedWalletPath),
                SharedWalletTag = LoadRequired<TaxonomyTermData>(SharedWalletTagPath),
                Experience = LoadRequired<FrameworkResourceData>(ExperiencePath),
                TurnToken = LoadRequired<FrameworkResourceData>(TurnTokenPath),
                WaterUnit = LoadRequired<CapabilityHostData>(WaterUnitPath),
                ExperienceRecipe = LoadRequired<ProductionRecipeData>(ExperienceRecipePath),
            };

            EnsureExpectedExistingState(activity, boardActivity, boardMergeRecipe, content);

            var createdPaths = new List<string>(CreatedPaths.Length);
            try
            {
                CreateTaxonomy(content, createdPaths);
                CreateWallets(content, createdPaths);
                CreateRuntimeDefinitions(content, createdPaths);
                CreateSpatialShapes(content, createdPaths);
                CreateProduction(content, createdPaths);
                CreateDrops(content, createdPaths);
                CreateAI(content, createdPaths);
                CreateObjectivesAndOrchestration(content, createdPaths);
                CreateProjectionAssets(content, createdPaths);
                ConfigureExistingAssets(activity, topology, boardActivity, boardMergeRecipe, content);
                ConfigureRuntimeInstallers(content, createdPaths);
                ConfigureIntegrationScene(content);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = activity;
                EditorGUIUtility.PingObject(activity);
                Debug.Log("[ChainRush] Autobattle + Board vertical slice assets were created and wired.");
            }
            catch
            {
                for (int i = createdPaths.Count - 1; i >= 0; i--)
                    AssetDatabase.DeleteAsset(createdPaths[i]);
                AssetDatabase.SaveAssets();
                throw;
            }
        }

        [MenuItem("ChainRush/Spatial/Create Occupancy Matrix Assets")]
        public static void CreateOccupancyMatrixAssets()
        {
            TaxonomyFamilyData family =
                AssetDatabase.LoadAssetAtPath<TaxonomyFamilyData>(OccupancyFamilyPath);
            TaxonomyTermData mobileSolid =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(MobileSolidPath);
            TaxonomyTermData staticSolid =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(StaticSolidPath);
            TaxonomyTermData placementObstacle =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(PlacementObstaclePath);
            TaxonomyTermData nonOccupying =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(NonOccupyingPath);
            SpatialOccupancyMatrixData matrix =
                AssetDatabase.LoadAssetAtPath<SpatialOccupancyMatrixData>(OccupancyMatrixPath);

            int existingCount = CountExisting(
                family,
                mobileSolid,
                staticSolid,
                placementObstacle,
                nonOccupying,
                matrix);
            if (existingCount == 6)
            {
                ValidateOccupancyAuthoring(
                    family,
                    mobileSolid,
                    staticSolid,
                    placementObstacle,
                    nonOccupying,
                    matrix);
                EnsureOccupancyConsumerWiring();
                ValidateOccupancyConsumerWiring();
                Debug.Log("[ChainRush] Spatial occupancy authoring is already complete and valid.");
                return;
            }

            if (existingCount != 0)
            {
                throw new InvalidOperationException(
                    "Spatial occupancy assets are partially authored. Refusing to repair or overwrite them.");
            }

            TaxonomyRuntimeInstallerData taxonomyInstaller =
                LoadRequired<TaxonomyRuntimeInstallerData>(TaxonomyInstallerPath);
            GameplayFoundationInstallerData foundationInstaller =
                LoadRequired<GameplayFoundationInstallerData>(FoundationInstallerPath);
            EnsureOccupancyInstallerState(taxonomyInstaller, foundationInstaller);
            EnsureOccupancyIdentityAvailable();
            EnsureOccupancyHostPlanMatchesAssets();

            TaxonomyFamilyData[] originalFamilies =
                GetField<TaxonomyFamilyData[]>(taxonomyInstaller, "families")
                ?? new TaxonomyFamilyData[0];
            TaxonomyTermData[] originalTerms =
                GetField<TaxonomyTermData[]>(taxonomyInstaller, "terms")
                ?? new TaxonomyTermData[0];
            SpatialOccupancyMatrixData originalMatrix =
                GetField<SpatialOccupancyMatrixData>(foundationInstaller, "spatialOccupancyMatrix");
            Dictionary<CapabilityHostBaseData, List<TaxonomyTermData>> originalTags =
                CaptureOccupancyHostTags();
            var createdPaths = new List<string>(6);
            bool occupancyFolderExisted = AssetDatabase.IsValidFolder(OccupancyRoot);

            try
            {
                EnsureFolder(OccupancyRoot);
                family = CreateAsset<TaxonomyFamilyData>(
                    OccupancyFamilyPath,
                    "SpatialOccupancyFamily",
                    createdPaths);
                SetField(family, "id", "SpatialOccupancy");
                SetField(family, "displayName", "Spatial Occupancy");
                SetField(family, "cardinality", TaxonomyCardinality.Multiple);
                EditorUtility.SetDirty(family);
                mobileSolid = CreateOccupancyTerm(
                    "MobileSolid",
                    0,
                    family,
                    MobileSolidPath,
                    createdPaths);
                staticSolid = CreateOccupancyTerm(
                    "StaticSolid",
                    1,
                    family,
                    StaticSolidPath,
                    createdPaths);
                placementObstacle = CreateOccupancyTerm(
                    "PlacementObstacle",
                    2,
                    family,
                    PlacementObstaclePath,
                    createdPaths);
                nonOccupying = CreateOccupancyTerm(
                    "NonOccupying",
                    3,
                    family,
                    NonOccupyingPath,
                    createdPaths);

                matrix = CreateAsset<SpatialOccupancyMatrixData>(
                    OccupancyMatrixPath,
                    "SpatialOccupancyMatrix",
                    createdPaths);
                SetField(matrix, "occupancyFamily", family);
                SetField(
                    matrix,
                    "rows",
                    new List<SpatialOccupancyMatrixRowData>
                    {
                        new SpatialOccupancyMatrixRowData(
                            mobileSolid,
                            new List<TaxonomyTermData> { mobileSolid, staticSolid }),
                        new SpatialOccupancyMatrixRowData(
                            staticSolid,
                            new List<TaxonomyTermData>
                            {
                                mobileSolid,
                                staticSolid,
                                placementObstacle
                            }),
                        new SpatialOccupancyMatrixRowData(
                            placementObstacle,
                            new List<TaxonomyTermData> { staticSolid, placementObstacle }),
                        new SpatialOccupancyMatrixRowData(
                            nonOccupying,
                            new List<TaxonomyTermData>(0))
                    });
                EditorUtility.SetDirty(matrix);

                var families = new List<TaxonomyFamilyData>(originalFamilies);
                families.Add(family);
                SetField(taxonomyInstaller, "families", families.ToArray());
                var terms = new List<TaxonomyTermData>(originalTerms);
                terms.Add(mobileSolid);
                terms.Add(staticSolid);
                terms.Add(placementObstacle);
                terms.Add(nonOccupying);
                SetField(taxonomyInstaller, "terms", terms.ToArray());
                EditorUtility.SetDirty(taxonomyInstaller);

                SetField(foundationInstaller, "spatialOccupancyMatrix", matrix);
                EditorUtility.SetDirty(foundationInstaller);

                AssignOccupancyTag(MobileSolidHostPaths, mobileSolid);
                AssignOccupancyTag(StaticSolidHostPaths, staticSolid);
                AssignOccupancyTag(PlacementObstacleHostPaths, placementObstacle);
                AssignOccupancyTag(NonOccupyingHostPaths, nonOccupying);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ValidateOccupancyAuthoring(
                    family,
                    mobileSolid,
                    staticSolid,
                    placementObstacle,
                    nonOccupying,
                    matrix);
                EnsureOccupancyConsumerWiring();
                ValidateOccupancyConsumerWiring();
                Debug.Log("[ChainRush] Spatial occupancy assets were created and wired.");
            }
            catch
            {
                SetField(taxonomyInstaller, "families", originalFamilies);
                SetField(taxonomyInstaller, "terms", originalTerms);
                EditorUtility.SetDirty(taxonomyInstaller);
                SetField(foundationInstaller, "spatialOccupancyMatrix", originalMatrix);
                EditorUtility.SetDirty(foundationInstaller);
                RestoreOccupancyHostTags(originalTags);
                for (int i = createdPaths.Count - 1; i >= 0; i--)
                    AssetDatabase.DeleteAsset(createdPaths[i]);
                if (!occupancyFolderExisted && AssetDatabase.IsValidFolder(OccupancyRoot))
                    AssetDatabase.DeleteAsset(OccupancyRoot);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                throw;
            }
        }

        [MenuItem("ChainRush/Activities/Autobattle/Apply Runtime Bindings")]
        public static void ApplyRuntimeBindings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before applying Autobattle runtime bindings.");

            CapabilityHostData waterUnit = LoadRequired<CapabilityHostData>(WaterUnitPath);
            CapabilityHostData enemy = LoadRequired<CapabilityHostData>(EnemyPath);
            CapabilityHostData experienceCollector =
                LoadRequired<CapabilityHostData>(ExperienceCollectorPath);
            AIBrainData alliedCombatBrain = LoadRequired<AIBrainData>(AlliedCombatBrainPath);
            AIBrainData enemyCombatBrain = LoadRequired<AIBrainData>(EnemyCombatBrainPath);
            AIBrainData collectorBrain = LoadRequired<AIBrainData>(CollectorBrainPath);
            TaxonomyTermData combatSelector = LoadRequired<TaxonomyTermData>(CombatNodePath);
            TaxonomyTermData collectionSelector = LoadRequired<TaxonomyTermData>(CollectionNodePath);

            ConfigureAIBrainBinding(waterUnit, alliedCombatBrain, combatSelector);
            ConfigureAIBrainBinding(enemy, enemyCombatBrain, combatSelector);
            ConfigureAIBrainBinding(experienceCollector, collectorBrain, collectionSelector);
            ConfigureCombatRetryTransitions(alliedCombatBrain);
            ConfigureCombatRetryTransitions(enemyCombatBrain);

            SetProjectionPoolKey(PlayerSpawnerPrefabPath, PlayerSpawnerPoolKey);
            SetProjectionPoolKey(EnemySpawnerPrefabPath, EnemySpawnerPoolKey);
            SetProjectionPoolKey(WaterUnitPrefabPath, WaterUnitPoolKey);
            SetProjectionPoolKey(EnemyPrefabPath, EnemyPoolKey);
            SetProjectionPoolKey(ExperienceDropPrefabPath, ExperienceDropPoolKey);
            SetProjectionPoolKey(ExperienceCollectorPrefabPath, ExperienceCollectorPoolKey);
            ConfigureSpaceNavigationSurface(SpacePrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ChainRush] Autobattle runtime bindings were applied.");
        }

        [MenuItem("ChainRush/Activities/Autobattle/Apply Materialization Endpoint Wiring")]
        public static void ApplyMaterializationEndpointWiring()
        {
            TaxonomyTermData operatorId =
                LoadRequired<TaxonomyTermData>(ProductionMaterializedOperatorPath);
            ReplaceMaterializationOperator(
                LoadRequired<OrchestratorAIBrainData>(PlayerBrainPath),
                operatorId,
                PlayerBrainPath);
            ReplaceMaterializationOperator(
                LoadRequired<OrchestratorAIBrainData>(EnemyBrainPath),
                operatorId,
                EnemyBrainPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(
                new List<string> { PlayerBrainPath, EnemyBrainPath });
            AssetDatabase.Refresh();
            Debug.Log("[ChainRush] Autobattle materialization endpoint wiring was applied.");
        }

        static void ReplaceMaterializationOperator(
            OrchestratorAIBrainData brain,
            TaxonomyTermData operatorId,
            string brainPath)
        {
            List<OrchestrationDecompOpData> operators = brain.Operators;
            int replacementIndex = -1;
            for (int i = 0; i < operators.Count; i++)
            {
                if (operators[i] is MaterializedEntityProductionDecompOpData existing)
                {
                    if (replacementIndex >= 0)
                        throw new InvalidOperationException($"Brain '{brainPath}' has duplicate materialization operators.");
                    SetField(existing, "operatorId", operatorId);
                    replacementIndex = i;
                    continue;
                }
                if (operators[i] != null)
                    continue;
                if (replacementIndex >= 0)
                    throw new InvalidOperationException($"Brain '{brainPath}' has multiple unresolved operators.");
                replacementIndex = i;
            }

            if (replacementIndex < 0)
                throw new InvalidOperationException($"Brain '{brainPath}' has no materialization operator slot.");
            if (operators[replacementIndex] == null)
            {
                var replacement = new MaterializedEntityProductionDecompOpData();
                SetField(replacement, "operatorId", operatorId);
                operators[replacementIndex] = replacement;
            }

            SetField(brain, "operators", operators);
            EditorUtility.SetDirty(brain);
        }

        static void CreateTaxonomy(Content content, List<string> createdPaths)
        {
            TaxonomyFamilyData roleFamily = CreateFamily(
                RoleFamilyPath,
                "AutobattleRoleFamily",
                "chainrush.autobattle.role",
                "Autobattle Role",
                createdPaths);
            content.Families.Add(roleFamily);
            content.PlayerSpawnerRole = CreateTerm(
                PlayerSpawnerRolePath, "PlayerSpawnerRole", "chainrush.autobattle.role.player-spawner",
                "Player Spawner", roleFamily, 0, content, createdPaths);
            content.EnemySpawnerRole = CreateTerm(
                EnemySpawnerRolePath, "EnemySpawnerRole", "chainrush.autobattle.role.enemy-spawner",
                "Enemy Spawner", roleFamily, 1, content, createdPaths);
            content.CombatantRole = CreateTerm(
                CombatantRolePath, "CombatantRole", "chainrush.autobattle.role.combatant",
                "Combatant", roleFamily, 2, content, createdPaths);
            content.AlliedUnitRole = CreateTerm(
                AlliedUnitRolePath, "AlliedUnitRole", "chainrush.autobattle.role.allied-unit",
                "Allied Unit", roleFamily, 3, content, createdPaths);
            content.EnemyUnitRole = CreateTerm(
                EnemyUnitRolePath, "EnemyUnitRole", "chainrush.autobattle.role.enemy-unit",
                "Enemy Unit", roleFamily, 4, content, createdPaths);
            content.ExperienceDropRole = CreateTerm(
                ExperienceDropRolePath, "ExperienceDropRole", "chainrush.autobattle.role.experience-drop",
                "Experience Drop", roleFamily, 5, content, createdPaths);

            TaxonomyFamilyData projectionTargetFamily = CreateFamily(
                ProjectionTargetFamilyPath,
                "ProjectionTargetFamily",
                "chainrush.autobattle.projection-target",
                "Autobattle Projection Target",
                createdPaths);
            content.Families.Add(projectionTargetFamily);
            content.ExperienceProgressTarget = CreateTerm(
                ExperienceProgressTargetPath,
                "ExperienceProgressTarget",
                "chainrush.autobattle.projection-target.experience-progress",
                "Experience Progress",
                projectionTargetFamily,
                0,
                content,
                createdPaths);

            TaxonomyFamilyData activityFamily =
                LoadRequired<TaxonomyFamilyData>(ActivityTaxonomyFamilyPath);
            content.IntegrationRuntimeTag = CreateTerm(
                IntegrationRuntimeTagPath,
                "IntegrationAutobattle",
                "chainrush.activity.runtime.integration-autobattle",
                "Integration Autobattle",
                activityFamily,
                100,
                content,
                createdPaths);

            TaxonomyFamilyData markerFamily = CreateFamily(
                MarkerFamilyPath,
                "AutobattleMarkerFamily",
                "chainrush.autobattle.marker",
                "Autobattle Marker",
                createdPaths);
            content.Families.Add(markerFamily);
            content.PlayerAnchor = CreateTerm(
                PlayerAnchorPath, "PlayerSpawnerAnchor", "chainrush.autobattle.marker.player-anchor",
                "Player Spawner Anchor", markerFamily, 0, content, createdPaths);
            content.EnemyAnchor = CreateTerm(
                EnemyAnchorPath, "EnemySpawnerAnchor", "chainrush.autobattle.marker.enemy-anchor",
                "Enemy Spawner Anchor", markerFamily, 1, content, createdPaths);
            content.AlliedSpawn = CreateTerm(
                AlliedSpawnPath, "AlliedSpawn", "chainrush.autobattle.marker.allied-spawn",
                "Allied Spawn", markerFamily, 2, content, createdPaths);
            content.EnemySpawn = CreateTerm(
                EnemySpawnPath, "EnemySpawn", "chainrush.autobattle.marker.enemy-spawn",
                "Enemy Spawn", markerFamily, 3, content, createdPaths);
            content.DropPosition = CreateTerm(
                DropPositionPath, "DropPosition", "chainrush.autobattle.marker.drop-position",
                "Drop Position", markerFamily, 4, content, createdPaths);

            TaxonomyFamilyData aiFamily = CreateFamily(
                AIFamilyPath,
                "AIStateFamily",
                "chainrush.autobattle.ai",
                "Autobattle AI",
                createdPaths);
            content.Families.Add(aiFamily);
            content.CombatNode = CreateTerm(
                CombatNodePath, "CombatNode", "chainrush.autobattle.ai.combat-node",
                "Combat Node", aiFamily, 0, content, createdPaths);
            content.CombatStateA = CreateTerm(
                CombatStateAPath, "CombatStateA", "chainrush.autobattle.ai.combat-a",
                "Combat A", aiFamily, 1, content, createdPaths);
            content.CombatStateB = CreateTerm(
                CombatStateBPath, "CombatStateB", "chainrush.autobattle.ai.combat-b",
                "Combat B", aiFamily, 2, content, createdPaths);
            content.DefeatState = CreateTerm(
                DefeatStatePath, "DefeatState", "chainrush.autobattle.ai.defeat",
                "Defeat", aiFamily, 3, content, createdPaths);
            content.CollectionNode = CreateTerm(
                CollectionNodePath, "CollectionNode", "chainrush.autobattle.ai.collection-node",
                "Collection Node", aiFamily, 4, content, createdPaths);
            content.WaitingState = CreateTerm(
                WaitingStatePath, "WaitingState", "chainrush.autobattle.ai.waiting",
                "Waiting", aiFamily, 5, content, createdPaths);
            content.CollectState = CreateTerm(
                CollectStatePath, "CollectState", "chainrush.autobattle.ai.collect",
                "Collect", aiFamily, 6, content, createdPaths);
            content.SearchState = CreateTerm(
                SearchStatePath, "SearchState", "chainrush.autobattle.ai.search",
                "Search", aiFamily, 7, content, createdPaths);
            content.CombatTarget = CreateTerm(
                CombatTargetPath, "CombatTarget", "chainrush.autobattle.ai.target.combat",
                "Combat Target", aiFamily, 8, content, createdPaths);
            content.CollectionTarget = CreateTerm(
                CollectionTargetPath, "CollectionTarget", "chainrush.autobattle.ai.target.collection",
                "Collection Target", aiFamily, 9, content, createdPaths);

            TaxonomyFamilyData operatorFamily = CreateFamily(
                OperatorFamilyPath,
                "AutobattleOperatorFamily",
                "chainrush.autobattle.operator",
                "Autobattle Operator",
                createdPaths);
            content.Families.Add(operatorFamily);
            content.TurnTokenAgentOperator = CreateTerm(
                TurnTokenAgentOperatorPath, "TurnTokenAgentOperator",
                "chainrush.autobattle.operator.agent.turn-token", "Turn Token Agent",
                operatorFamily, 1, content, createdPaths);
            content.EnemyWaveAgentOperator = CreateTerm(
                EnemyWaveAgentOperatorPath, "EnemyWaveAgentOperator",
                "chainrush.autobattle.operator.agent.enemy-wave", "Enemy Wave Agent",
                operatorFamily, 2, content, createdPaths);
            content.ProductionInputOperator = CreateTerm(
                ProductionInputOperatorPath, "ProductionInputOperator",
                "chainrush.autobattle.operator.production-input", "Production Input",
                operatorFamily, 3, content, createdPaths);
            content.ProductionEconomyOperator = CreateTerm(
                ProductionEconomyOperatorPath, "ProductionEconomyOperator",
                "chainrush.autobattle.operator.production-economy", "Production Economy",
                operatorFamily, 4, content, createdPaths);
            content.ProductionMaterializedOperator = CreateTerm(
                ProductionMaterializedOperatorPath, "ProductionMaterializedOperator",
                "chainrush.autobattle.operator.production-materialized", "Production Materialized",
                operatorFamily, 5, content, createdPaths);
            content.ProductionYieldOperator = CreateTerm(
                ProductionYieldOperatorPath, "ProductionYieldOperator",
                "chainrush.autobattle.operator.production-yield", "Production Yield",
                operatorFamily, 6, content, createdPaths);
            content.ProductionAvailableOperator = CreateTerm(
                ProductionAvailableOperatorPath, "ProductionAvailableOperator",
                "chainrush.autobattle.operator.production-available", "Production Available",
                operatorFamily, 7, content, createdPaths);
            content.AwaitFactOperator = CreateTerm(
                AwaitFactOperatorPath, "AwaitFactOperator",
                "chainrush.autobattle.operator.await-fact", "Await Fact",
                operatorFamily, 8, content, createdPaths);

            TaxonomyFamilyData walletFamily = content.SharedWalletTag.Family;
            if (walletFamily == null)
                throw new InvalidOperationException("ActivityWalletTag has no taxonomy family.");
            content.UnitWalletTag = CreateTerm(
                UnitWalletTagPath, "UnitWalletTag", "chainrush.wallet.unit.tag",
                "Unit Wallet", walletFamily, 100, content, createdPaths);
            content.DropContentsWalletTag = CreateTerm(
                DropContentsWalletTagPath, "DropContentsWalletTag", "chainrush.wallet.drop-contents.tag",
                "Drop Contents Wallet", walletFamily, 101, content, createdPaths);
            content.WorldWalletTag = CreateTerm(
                WorldWalletTagPath, "WorldWalletTag", "chainrush.wallet.world.tag",
                "World Wallet", walletFamily, 102, content, createdPaths);
            content.CollectorWalletTag = CreateTerm(
                CollectorWalletTagPath,
                "ExperienceCollectorWalletTag",
                "chainrush.wallet.experience-collector.tag",
                "Experience Collector Wallet",
                walletFamily,
                103,
                content,
                createdPaths);
        }

        static void CreateWallets(Content content, List<string> createdPaths)
        {
            content.UnitWallet = CreateWallet(
                UnitWalletPath,
                "UnitWallet",
                "chainrush.wallet.autobattle.unit",
                content.UnitWalletTag,
                createdPaths);
            content.DropContentsWallet = CreateWallet(
                DropContentsWalletPath,
                "DropContentsWallet",
                "chainrush.wallet.autobattle.drop-contents",
                content.DropContentsWalletTag,
                createdPaths);
            content.WorldWallet = CreateWallet(
                WorldWalletPath,
                "WorldWallet",
                "chainrush.wallet.autobattle.world",
                content.WorldWalletTag,
                createdPaths);
            content.CollectorWallet = CreateWallet(
                CollectorWalletPath,
                "ExperienceCollectorWallet",
                "chainrush.wallet.autobattle.experience-collector",
                content.CollectorWalletTag,
                createdPaths);
        }

        static void CreateRuntimeDefinitions(Content content, List<string> createdPaths)
        {
            content.Health = CreateEconomyAsset<HostValueData>(
                HealthPath,
                "Health",
                "chainrush.host-value.health",
                AllMutableOperations,
                createdPaths);

            content.Movement = CreateEconomyAsset<FrameworkMovementData>(
                MovementPath,
                "UnitMovement",
                "chainrush.movement.unit",
                AllMutableOperations,
                createdPaths);
            SetField(content.Movement, "spacing", 250);
            SetField(content.Movement, "avoidancePriority", 0);
            SetField(
                content.Movement,
                "destinationFallback",
                new MovementDestinationFallbackData(
                    MovementDestinationFallbackType.NearestReachable,
                    1400));
            EditorUtility.SetDirty(content.Movement);

            content.ApproachSkill = CreateSkill(
                ApproachSkillPath,
                "ApproachSkill",
                "chainrush.skill.autobattle.approach",
                SkillTargetType.Entities,
                DiplomacyDispositionType.Hostile,
                new List<TaxonomyTermData> { content.CombatantRole },
                startDelay: 0L,
                reloadTime: 0L,
                createdPaths);
            var moveEffect = new SkillMoveToTargetEffectData();
            ConfigureEffect(moveEffect, EffectRecipient.Owner, 650L);
            SetField(moveEffect, "targetTrackingMode", TargetTrackingMode.OnPositionChange);
            SetField(moveEffect, "repathDistanceThreshold", 250);
            SetField(moveEffect, "accelerationDuration", 2);
            SetField(moveEffect, "decelerationDuration", 2);
            SetField(moveEffect, "approachInteractionTags", new List<TaxonomyTermData>(0));
            SetField(content.ApproachSkill, "effects", new List<SkillEffectData> { moveEffect });

            content.AttackSkill = CreateSkill(
                AttackSkillPath,
                "AttackSkill",
                "chainrush.skill.autobattle.attack",
                SkillTargetType.Entities,
                DiplomacyDispositionType.Hostile,
                new List<TaxonomyTermData> { content.CombatantRole },
                startDelay: 0L,
                reloadTime: 8L,
                createdPaths);
            var distanceRequirement = new SkillTargetDistanceRequirementData();
            SetField(distanceRequirement, "distance", 1600);
            SetField(distanceRequirement, "compareOperation", CompareOperation.LessOrEqual);
            SetField(distanceRequirement, "interactionTags", new List<TaxonomyTermData>(0));
            var damageEffect = new SkillHostValueEffectData();
            ConfigureEffect(damageEffect, EffectRecipient.Target, -1L);
            SetField(damageEffect, "hostValue", content.Health);
            SetField(damageEffect, "formula", new SkillHostValueFormulaData());
            SetField(
                content.AttackSkill,
                "requirements",
                new List<SkillRequirementData> { distanceRequirement });
            SetField(content.AttackSkill, "effects", new List<SkillEffectData> { damageEffect });

            content.CollectionSkill = CreateSkill(
                CollectionSkillPath,
                "ExperienceCollectionSkill",
                "chainrush.skill.autobattle.experience-collection",
                SkillTargetType.Entities,
                DiplomacyDispositionType.Neutral,
                new List<TaxonomyTermData> { content.ExperienceDropRole },
                startDelay: 10L,
                reloadTime: 0L,
                createdPaths);

            content.PlayerSpawner = CreateCapabilityHost(
                PlayerSpawnerPath,
                "PlayerSpawner",
                "chainrush.autobattle.player-spawner",
                new List<TaxonomyTermData> { content.PlayerSpawnerRole },
                new List<CapabilityEntry> { CreateCapability(CapabilityHostType.ProductionOwner) },
                new Vector3Int(1000, 0, 1000),
                createdPaths);
            content.EnemySpawner = CreateCapabilityHost(
                EnemySpawnerPath,
                "EnemySpawner",
                "chainrush.autobattle.enemy-spawner",
                new List<TaxonomyTermData> { content.EnemySpawnerRole },
                new List<CapabilityEntry> { CreateCapability(CapabilityHostType.ProductionOwner) },
                new Vector3Int(1000, 0, 1000),
                createdPaths);
            content.Enemy = CreateCapabilityHost(
                EnemyPath,
                "BugBrownSmall",
                "chainrush.autobattle.enemy.bug-brown-small",
                new List<TaxonomyTermData> { content.CombatantRole, content.EnemyUnitRole },
                new List<CapabilityEntry>
                {
                    CreateCapability(CapabilityHostType.SkillOwner),
                    CreateCapability(CapabilityHostType.MovementOwner),
                    CreateCapability(CapabilityHostType.AIBrainOwner, content.CombatNode),
                    CreateCapability(CapabilityHostType.ProductionOwner),
                },
                new Vector3Int(1000, 0, 1000),
                createdPaths);
            content.ExperienceDrop = CreateCapabilityHost(
                ExperienceDropPath,
                "ExperienceDrop",
                "chainrush.autobattle.experience-drop",
                new List<TaxonomyTermData> { content.ExperienceDropRole },
                new List<CapabilityEntry>(0),
                new Vector3Int(1000, 0, 1000),
                createdPaths);
            content.ExperienceCollector = CreateCapabilityHost(
                ExperienceCollectorPath,
                "ExperienceCollector",
                "chainrush.autobattle.experience-collector",
                new List<TaxonomyTermData>(0),
                new List<CapabilityEntry>
                {
                    CreateCapability(CapabilityHostType.SkillOwner),
                    CreateCapability(CapabilityHostType.AIBrainOwner, content.CollectionNode),
                },
                Vector3Int.zero,
                createdPaths);

            ConfigureEconomyAsset(content.WaterUnit, "chainrush.unit.water", AllMutableOperations);
            content.WaterUnit.Tags.Clear();
            content.WaterUnit.Tags.Add(content.CombatantRole);
            content.WaterUnit.Tags.Add(content.AlliedUnitRole);
            SetField(
                content.WaterUnit,
                "capabilities",
                new List<CapabilityEntry>
                {
                    CreateCapability(CapabilityHostType.SkillOwner),
                    CreateCapability(CapabilityHostType.MovementOwner),
                    CreateCapability(CapabilityHostType.AIBrainOwner, content.CombatNode),
                });
            SetField(content.WaterUnit, "footprintSize", new Vector3Int(1000, 0, 1000));
            EditorUtility.SetDirty(content.WaterUnit);

            ConfigureEconomyAsset(content.Experience, "chainrush.resource.experience", AllMutableOperations);
            EditorUtility.SetDirty(content.Experience);
        }

        static List<AIBrainActionData> CreateExperienceCollectionActions(Content content)
        {
            return new List<AIBrainActionData>
            {
                CreateUseSkillAction(
                    content.CollectionSkill,
                    content.CollectionTarget,
                    SkillCompletionPolicyType.OnExecutionComplete),
            };
        }

        static void ConfigureExperienceCollectionHosts(Content content)
        {
            SetField(
                content.ExperienceDrop,
                "capabilities",
                new List<CapabilityEntry>(0));
            SetField(
                content.ExperienceDrop,
                "walletEntries",
                new List<WalletEntry>
                {
                    CreateWalletEntry(content.DropContentsWallet),
                });
            SetField(
                content.ExperienceCollector,
                "walletEntries",
                new List<WalletEntry>
                {
                    CreateWalletEntry(
                        content.CollectorWallet,
                        new SeedEntry(
                            content.CollectorBrain,
                            1L,
                            EconomyFormType.Stack,
                            new List<TaxonomyTermData> { content.CollectionNode }),
                        new SeedEntry(content.CollectionSkill, 1L, EconomyFormType.Stack)),
                });
            EditorUtility.SetDirty(content.ExperienceDrop);
            EditorUtility.SetDirty(content.ExperienceCollector);
        }

        static void CreateProduction(Content content, List<string> createdPaths)
        {
            content.DeploymentRecipe = CreateEconomyAsset<ProductionRecipeData>(
                DeploymentRecipePath,
                "WaterUnitDeploymentRecipe",
                "chainrush.production.autobattle.water-unit-deployment.recipe",
                EconomyOperation.Require | EconomyOperation.Issue,
                createdPaths);
            content.DeploymentRecipe.Inputs.Add(new ProductionInputData(
                EconomyOperation.Consume,
                content.WaterUnit,
                EconomyFormType.Stack,
                new List<TaxonomyTermData> { content.SharedWalletTag },
                null,
                new LongFlatProgressionData(1L)));
            content.DeploymentRecipe.Outputs.Add(new ProductionOutputData(
                content.WaterUnit,
                EconomyFormType.Token,
                new List<TaxonomyTermData> { content.SharedWalletTag },
                new LongFlatProgressionData(1L)));

            content.EnemyWaveRecipe = CreateEconomyAsset<ProductionRecipeData>(
                EnemyWaveRecipePath,
                "EnemyWaveRecipe",
                "chainrush.production.autobattle.enemy-wave.recipe",
                EconomyOperation.Require | EconomyOperation.Issue,
                createdPaths);
            content.EnemyWaveRecipe.Outputs.Add(new ProductionOutputData(
                content.Enemy,
                EconomyFormType.Token,
                new List<TaxonomyTermData> { content.SharedWalletTag },
                new LongCappedProgressionData(new LongLinearProgressionData(2L, 1L), 20L)));

            content.DropRecipe = CreateEconomyAsset<ProductionRecipeData>(
                DropRecipePath,
                "ExperienceDropRecipe",
                "chainrush.production.autobattle.experience-drop.recipe",
                EconomyOperation.Require | EconomyOperation.Issue,
                createdPaths);
            content.DropRecipe.Outputs.Add(new ProductionOutputData(
                content.ExperienceDrop,
                EconomyFormType.Token,
                new List<TaxonomyTermData> { content.UnitWalletTag },
                new LongFlatProgressionData(1L)));

            content.PlayerCatalog = CreateCatalog(
                PlayerCatalogPath,
                "PlayerProductionCatalog",
                "chainrush.production.autobattle.player.catalog",
                new List<ProductionRecipeData> { content.DeploymentRecipe, content.ExperienceRecipe },
                createdPaths);
            content.EnemyCatalog = CreateCatalog(
                EnemyCatalogPath,
                "EnemyWaveCatalog",
                "chainrush.production.autobattle.enemy-wave.catalog",
                new List<ProductionRecipeData> { content.EnemyWaveRecipe },
                createdPaths);
            content.DropCatalog = CreateCatalog(
                DropCatalogPath,
                "ExperienceDropCatalog",
                "chainrush.production.autobattle.experience-drop.catalog",
                new List<ProductionRecipeData> { content.DropRecipe },
                createdPaths);

            content.PlayerProduction = CreateProductionDefinition(
                PlayerProductionPath,
                "PlayerProduction",
                "chainrush.production.autobattle.player",
                content.PlayerCatalog,
                content.AlliedSpawn,
                createdPaths);
            content.EnemyProduction = CreateProductionDefinition(
                EnemyProductionPath,
                "EnemyWaveProduction",
                "chainrush.production.autobattle.enemy-wave",
                content.EnemyCatalog,
                content.EnemySpawn,
                createdPaths);
            content.DropProduction = CreateProductionDefinition(
                DropProductionPath,
                "ExperienceDropProduction",
                "chainrush.production.autobattle.experience-drop",
                content.DropCatalog,
                null,
                createdPaths);

            SetField(
                content.PlayerSpawner,
                "walletEntries",
                new List<WalletEntry>
                {
                    CreateWalletEntry(
                        content.UnitWallet,
                        new SeedEntry(content.PlayerProduction, 1L, EconomyFormType.Stack)),
                });
            SetField(
                content.EnemySpawner,
                "walletEntries",
                new List<WalletEntry>
                {
                    CreateWalletEntry(
                        content.UnitWallet,
                        new SeedEntry(content.EnemyProduction, 1L, EconomyFormType.Stack)),
                });
            EditorUtility.SetDirty(content.PlayerSpawner);
            EditorUtility.SetDirty(content.EnemySpawner);
        }

        static void CreateDrops(Content content, List<string> createdPaths)
        {
            content.DropProfile = CreateAsset<DropProfileData>(
                DropProfilePath,
                "ExperienceDropProfile",
                createdPaths);

            EconomyEntrySelectionData automaticSelection = CreateSelection(
                content.UnitWalletTag,
                EconomyFormType.Stack,
                content.Experience);
            EconomyEntrySelectionData preparationSelection = CreateSelection(
                content.UnitWalletTag,
                EconomyFormType.Stack,
                content.Experience);
            var preparation = new ContainerDropPreparationData();
            SetField(preparation, "selectionCriteria", preparationSelection);
            SetField(preparation, "containerRecipe", content.DropRecipe);
            SetField(
                preparation,
                "targetLocalWalletTags",
                new List<TaxonomyTermData> { content.DropContentsWalletTag });

            var placement = new DropPlacementRuleData();
            SetField(
                placement,
                "requiredMarkerTags",
                new List<TaxonomyTermData> { content.DropPosition });
            SetField(placement, "excludedMarkerTags", new List<TaxonomyTermData>(0));
            SetField(placement, "providerType", content.DropPosition);
            SetField<string>(placement, "providerId", null);

            SetField(content.DropProfile, "automaticSelectionCriteria", automaticSelection);
            SetField(
                content.DropProfile,
                "preparations",
                new List<DropPreparationData> { preparation });
            SetField(
                content.DropProfile,
                "worldWalletTags",
                new List<TaxonomyTermData> { content.WorldWalletTag });
            SetField(
                content.DropProfile,
                "placementRules",
                new List<DropPlacementRuleData> { placement });
            SetField(content.DropProfile, "reservationPriority", 100);
            EditorUtility.SetDirty(content.DropProfile);

            ConfigureCollectionSkillEffects(content);
        }

        static void ConfigureCollectionSkillEffects(Content content)
        {
            SkillEconomyEntryEffectData transfer = CreateEconomyEntryEffect(
                EffectRecipient.Target,
                SkillEconomyEntrySourceType.Wallet,
                SkillEconomyOwnerType.Host,
                CreateSelection(
                    content.DropContentsWalletTag,
                    EconomyFormType.Stack,
                    content.Experience),
                EconomyOperation.Transfer,
                EffectRecipient.Owner,
                SkillEconomyOwnerType.Root,
                new List<TaxonomyTermData> { content.SharedWalletTag });
            SkillEconomyEntryEffectData destroy = CreateEconomyEntryEffect(
                EffectRecipient.Target,
                SkillEconomyEntrySourceType.BackingEntry,
                SkillEconomyOwnerType.Host,
                CreateSelection(
                    content.WorldWalletTag,
                    EconomyFormType.Token,
                    content.ExperienceDrop),
                EconomyOperation.Destroy,
                EffectRecipient.Target,
                SkillEconomyOwnerType.Host,
                new List<TaxonomyTermData>(0));
            SetField(
                content.CollectionSkill,
                "effects",
                new List<SkillEffectData> { transfer, destroy });
            EditorUtility.SetDirty(content.CollectionSkill);
        }

        static void CreateAI(Content content, List<string> createdPaths)
        {
            content.AlliedCombatBrain = CreateCombatBrain(
                content,
                AlliedCombatBrainPath,
                "AlliedCombatBrain",
                "chainrush.ai.autobattle.allied-combat",
                new List<AIBrainActionData>
                {
                    new RemoveEntityAIBrainActionData(),
                },
                createdPaths);
            content.EnemyCombatBrain = CreateCombatBrain(
                content,
                EnemyCombatBrainPath,
                "EnemyCombatBrain",
                "chainrush.ai.autobattle.enemy-combat",
                new List<AIBrainActionData>
                {
                    CreateDropAction(content.DropProfile),
                    new RemoveEntityAIBrainActionData(),
                },
                createdPaths);

            content.CollectorBrain = CreateEconomyAsset<AIBrainData>(
                CollectorBrainPath,
                "ExperienceCollectorBrain",
                "chainrush.ai.autobattle.experience-collector",
                AllMutableOperations,
                createdPaths);
            ConfigureCollectorBrain(content);

            SetField(
                content.WaterUnit,
                "walletEntries",
                new List<WalletEntry>
                {
                    CreateWalletEntry(
                        content.UnitWallet,
                        new SeedEntry(content.Movement, 1L, EconomyFormType.Stack),
                        new SeedEntry(
                            content.AlliedCombatBrain,
                            1L,
                            EconomyFormType.Stack,
                            new List<TaxonomyTermData> { content.CombatNode }),
                        new SeedEntry(content.ApproachSkill, 1L, EconomyFormType.Stack),
                        new SeedEntry(content.AttackSkill, 1L, EconomyFormType.Stack),
                        new SeedEntry(content.Health, 20L, EconomyFormType.Stack)),
                });
            SetField(
                content.Enemy,
                "walletEntries",
                new List<WalletEntry>
                {
                    CreateWalletEntry(
                        content.UnitWallet,
                        new SeedEntry(content.Movement, 1L, EconomyFormType.Stack),
                        new SeedEntry(
                            content.EnemyCombatBrain,
                            1L,
                            EconomyFormType.Stack,
                            new List<TaxonomyTermData> { content.CombatNode }),
                        new SeedEntry(content.ApproachSkill, 1L, EconomyFormType.Stack),
                        new SeedEntry(content.AttackSkill, 1L, EconomyFormType.Stack),
                        new SeedEntry(content.Health, 3L, EconomyFormType.Stack),
                        new SeedEntry(content.Experience, 3L, EconomyFormType.Stack),
                        new SeedEntry(content.DropProduction, 1L, EconomyFormType.Stack)),
                });
            ConfigureExperienceCollectionHosts(content);
            EditorUtility.SetDirty(content.WaterUnit);
            EditorUtility.SetDirty(content.Enemy);
        }

        static void ConfigureCollectorBrain(Content content)
        {
            SetField(content.CollectorBrain, "thinkInterval", 1);
            SetField(content.CollectorBrain, "defaultControlNodeId", content.CollectionNode);

            var search = new AIBrainStateData();
            SetField(search, "tag", content.SearchState);
            SetField(
                search,
                "onEnterActions",
                new List<AIBrainActionData> { CreateCollectionTargetAction(content) });
            SetField(search, "onTickActions", new List<AIBrainActionData>(0));
            SetField(search, "onExitActions", new List<AIBrainExitActionData>(0));

            var waiting = new AIBrainStateData();
            SetField(waiting, "tag", content.WaitingState);
            SetField(waiting, "onEnterActions", new List<AIBrainActionData>(0));
            SetField(waiting, "onTickActions", new List<AIBrainActionData>(0));
            SetField(waiting, "onExitActions", new List<AIBrainExitActionData>(0));

            var collect = new AIBrainStateData();
            SetField(collect, "tag", content.CollectState);
            SetField(
                collect,
                "onEnterActions",
                CreateExperienceCollectionActions(content));
            SetField(collect, "onTickActions", new List<AIBrainActionData>(0));
            SetField(collect, "onExitActions", new List<AIBrainExitActionData>(0));

            var collectionNode = new AIBrainNodeData();
            SetField(collectionNode, "nodeId", content.CollectionNode);
            SetField(collectionNode, "entryState", content.SearchState);
            SetField(
                collectionNode,
                "states",
                new List<AIBrainStateData> { search, waiting, collect });
            SetField(content.CollectorBrain, "nodes", new List<AIBrainNodeData> { collectionNode });

            var delay = new TimeInStateAIBrainConditionData();
            SetField(delay, "minimumStateDuration", 15);
            SetField(
                content.CollectorBrain,
                "transitions",
                new List<AIBrainTransitionData>
                {
                    CreateBrainTransition(
                        content.SearchState,
                        content.CollectionNode,
                        content.WaitingState,
                        CreateTargetExistsCondition(content.CollectionTarget, true)),
                    CreateBrainTransition(
                        content.SearchState,
                        content.CollectionNode,
                        content.WaitingState,
                        CreateStateResultCondition(
                            AIBrainStateResultMask.Fail | AIBrainStateResultMask.Interrupted)),
                    CreateBrainTransition(
                        content.WaitingState,
                        content.CollectionNode,
                        content.SearchState,
                        CreateTargetExistsCondition(content.CollectionTarget, false)),
                    CreateBrainTransition(
                        content.WaitingState,
                        content.CollectionNode,
                        content.CollectState,
                        CreateTargetExistsCondition(content.CollectionTarget, true),
                        delay),
                    CreateBrainTransition(
                        content.CollectState,
                        content.CollectionNode,
                        content.SearchState,
                        CreateTargetExistsCondition(content.CollectionTarget, false)),
                    CreateBrainTransition(
                        content.CollectState,
                        content.CollectionNode,
                        content.SearchState,
                        CreateStateResultCondition(
                            AIBrainStateResultMask.Fail | AIBrainStateResultMask.Interrupted)),
                });
            EditorUtility.SetDirty(content.CollectorBrain);
        }

        static AIBrainData CreateCombatBrain(
            Content content,
            string path,
            string name,
            string id,
            List<AIBrainActionData> defeatActions,
            List<string> createdPaths)
        {
            AIBrainData brain = CreateEconomyAsset<AIBrainData>(
                path,
                name,
                id,
                AllMutableOperations,
                createdPaths);
            SetField(brain, "thinkInterval", 1);
            SetField(brain, "defaultControlNodeId", content.CombatNode);

            AIBrainStateData combatA = CreateCombatState(content.CombatStateA, content);
            AIBrainStateData combatB = CreateCombatState(content.CombatStateB, content);
            var defeat = new AIBrainStateData();
            SetField(defeat, "tag", content.DefeatState);
            SetField(defeat, "onEnterActions", defeatActions);
            SetField(defeat, "onTickActions", new List<AIBrainActionData>(0));
            SetField(defeat, "onExitActions", new List<AIBrainExitActionData>(0));

            var combatNode = new AIBrainNodeData();
            SetField(combatNode, "nodeId", content.CombatNode);
            SetField(combatNode, "entryState", content.CombatStateA);
            SetField(
                combatNode,
                "states",
                new List<AIBrainStateData> { combatA, combatB, defeat });
            SetField(brain, "nodes", new List<AIBrainNodeData> { combatNode });
            SetField(
                brain,
                "transitions",
                new List<AIBrainTransitionData>
                {
                    CreateDefeatTransition(content),
                    CreateRetryTransition(content.CombatStateA, content.CombatNode, content.CombatStateB),
                    CreateRetryTransition(content.CombatStateB, content.CombatNode, content.CombatStateA),
                });
            EditorUtility.SetDirty(brain);
            return brain;
        }

        static void CreateObjectivesAndOrchestration(Content content, List<string> createdPaths)
        {
            content.PlayerDeploymentObjective = CreateResetObjective(
                PlayerDeploymentObjectivePath,
                "PlayerDeploymentObjective",
                "chainrush-autobattle-player-deployment",
                new ObjectiveConditionEconomyMetric(
                    new List<TaxonomyTermData> { content.SharedWalletTag },
                    EconomyFormType.Stack,
                    content.WaterUnit,
                    1L,
                    CompareOperation.GreaterOrEqual,
                    null,
                    null),
                new ObjectiveConditionEconomyMetric(
                    new List<TaxonomyTermData> { content.SharedWalletTag },
                    EconomyFormType.Stack,
                    content.WaterUnit,
                    0L,
                    CompareOperation.LessOrEqual,
                    null,
                    null),
                createdPaths);
            content.TurnTokenObjective = CreateResetObjective(
                TurnTokenObjectivePath,
                "TurnTokenObjective",
                "chainrush-autobattle-turn-token",
                new ObjectiveConditionEconomyMetric(
                    new List<TaxonomyTermData> { content.SharedWalletTag },
                    EconomyFormType.Stack,
                    content.TurnToken,
                    0L,
                    CompareOperation.LessOrEqual,
                    null,
                    null),
                new ObjectiveConditionEconomyMetric(
                    new List<TaxonomyTermData> { content.SharedWalletTag },
                    EconomyFormType.Stack,
                    content.TurnToken,
                    1L,
                    CompareOperation.GreaterOrEqual,
                    null,
                    null),
                createdPaths);

            ObjectiveConditionMaterializedEntity noEnemies = CreateMaterializedCondition(
                content.Enemy,
                0L,
                CompareOperation.Equal);
            ObjectiveConditionMaterializedEntity hasEnemies = CreateMaterializedCondition(
                content.Enemy,
                1L,
                CompareOperation.GreaterOrEqual);
            content.EnemyWaveObjective = CreateResetObjective(
                EnemyWaveObjectivePath,
                "EnemyWaveObjective",
                "chainrush-autobattle-enemy-wave",
                noEnemies,
                hasEnemies,
                createdPaths);

            content.TurnTokenAgent = CreateProductionAgent(
                TurnTokenAgentPath,
                "TurnTokenProductionAgent",
                "chainrush-autobattle-agent-turn-token",
                new ObjectiveConditionEconomyMetric(
                    new List<TaxonomyTermData> { content.SharedWalletTag },
                    EconomyFormType.Stack,
                    content.TurnToken,
                    1L,
                    CompareOperation.GreaterOrEqual,
                    null,
                    null),
                content.PlayerSpawner,
                createdPaths);
            content.EnemyWaveAgent = CreateProductionAgent(
                EnemyWaveAgentPath,
                "EnemyWaveAgent",
                "chainrush-autobattle-agent-enemy-wave",
                CreateMaterializedCondition(content.Enemy, 1L, CompareOperation.GreaterOrEqual),
                content.EnemySpawner,
                createdPaths);

            content.EconomyState = CreateAsset<EconomyStateOrchestrationModuleData>(
                EconomyStatePath, "EconomyState", createdPaths);
            content.ProductionState = CreateAsset<ProductionStateOrchestrationModuleData>(
                ProductionStatePath, "ProductionState", createdPaths);
            content.ProjectionState = CreateAsset<ProjectionStateOrchestrationModuleData>(
                ProjectionStatePath, "ProjectionState", createdPaths);

            content.PlayerBrain = CreateAsset<OrchestratorAIBrainData>(
                PlayerBrainPath, "PlayerBrain", createdPaths);
            ConfigurePlayerOrchestrationBrain(content);
            content.EnemyBrain = CreateAsset<OrchestratorAIBrainData>(
                EnemyBrainPath, "EnemyBrain", createdPaths);
            ConfigureEnemyOrchestrationBrain(content);

            content.PlayerOrchestration = CreateOrchestration(
                PlayerOrchestrationPath,
                "PlayerOrchestration",
                "Autobattle Player",
                content.PlayerBrain,
                content,
                createdPaths);
            content.EnemyOrchestration = CreateOrchestration(
                EnemyOrchestrationPath,
                "EnemyOrchestration",
                "Autobattle Enemy",
                content.EnemyBrain,
                content,
                createdPaths);
        }

        static void CreateProjectionAssets(Content content, List<string> createdPaths)
        {
            Material alliedMaterial = CreateMaterial(
                AlliedMaterialPath,
                "AlliedMaterial",
                new Color(0.12f, 0.62f, 0.92f, 1f),
                createdPaths);
            Material enemyMaterial = CreateMaterial(
                EnemyMaterialPath,
                "EnemyMaterial",
                new Color(0.85f, 0.24f, 0.18f, 1f),
                createdPaths);
            Material neutralMaterial = CreateMaterial(
                NeutralMaterialPath,
                "NeutralMaterial",
                new Color(0.32f, 0.36f, 0.42f, 1f),
                createdPaths);
            Material experienceMaterial = CreateMaterial(
                ExperienceMaterialPath,
                "ExperienceMaterial",
                new Color(0.35f, 0.95f, 0.45f, 1f),
                createdPaths);

            CreateSpawnerPrefab(
                PlayerSpawnerPrefabPath,
                "PlayerSpawner",
                content.AlliedSpawn,
                content.SpawnArea,
                Vector3Int.zero,
                new Vector3Int(7, 1, 21),
                PlayerSpawnerPoolKey,
                createdPaths);
            CreateSpawnerPrefab(
                EnemySpawnerPrefabPath,
                "EnemySpawner",
                content.EnemySpawn,
                content.SpawnArea,
                new Vector3Int(-6000, 0, 0),
                new Vector3Int(7, 1, 21),
                EnemySpawnerPoolKey,
                createdPaths);
            CreateSimpleProjectionPrefab(
                WaterUnitPrefabPath,
                "WaterUnit",
                PrimitiveType.Capsule,
                alliedMaterial,
                new Vector3(0.7f, 0.7f, 0.7f),
                WaterUnitPoolKey,
                createdPaths);
            CreateEnemyPrefab(content, enemyMaterial, createdPaths);
            CreateExperienceDropPrefab(experienceMaterial, createdPaths);
            CreateExperienceCollectorPrefab(content, createdPaths);

            SetProjection(content.PlayerSpawner, PlayerSpawnerPrefabPath);
            SetProjection(content.EnemySpawner, EnemySpawnerPrefabPath);
            SetProjection(content.WaterUnit, WaterUnitPrefabPath);
            SetProjection(content.Enemy, EnemyPrefabPath);
            SetProjection(content.ExperienceDrop, ExperienceDropPrefabPath);
            SetProjection(content.ExperienceCollector, ExperienceCollectorPrefabPath);

            CreateExperienceUIPrefab(content, createdPaths);
            ConfigureSpacePrefab(content);
        }

        static void CreateSpatialShapes(Content content, List<string> createdPaths)
        {
            content.SpawnArea = CreateEconomyAsset<SpatialShapeData>(
                SpawnAreaShapePath,
                "SpawnArea",
                "chainrush.spatial.shape.spawn-area",
                EconomyOperation.Require
                | EconomyOperation.Issue
                | EconomyOperation.Consume
                | EconomyOperation.Transfer
                | EconomyOperation.Reserve
                | EconomyOperation.DirectSet,
                createdPaths);
            SetField(content.SpawnArea, "shapeType", SpatialShapeType.Box);
            SetField<SpatialShapeRuleData>(content.SpawnArea, "customRule", null);
            EditorUtility.SetDirty(content.SpawnArea);
        }

        static void ConfigureExistingAssets(
            ActivityData activity,
            TopologyDefinitionData topology,
            ActivityData boardActivity,
            ProductionRecipeData boardMergeRecipe,
            Content content)
        {
            SetField(
                topology,
                "coordinateOccupationPolicy",
                TopologyCoordinateOccupationPolicy.SingleOccupant);
            EditorUtility.SetDirty(topology);

            ActivityTeamWalletData playerWallet = CreateActivityWallet(
                content.SharedWallet,
                new ActivityWalletSeedEntryData(
                    new SeedEntry(content.PlayerSpawner, 1L, EconomyFormType.Token),
                    ActivitySeedMaterializationType.Spatial,
                    new List<TaxonomyTermData>(0),
                    new List<TaxonomyTermData> { content.PlayerAnchor }),
                new ActivityWalletSeedEntryData(
                    new SeedEntry(content.ExperienceCollector, 1L, EconomyFormType.Token),
                    ActivitySeedMaterializationType.NonSpatial,
                    new List<TaxonomyTermData> { content.ExperienceProgressTarget }),
                new ActivityWalletSeedEntryData(
                    new SeedEntry(content.WaterUnit, 1L, EconomyFormType.Stack),
                    ActivitySeedMaterializationType.None,
                    new List<TaxonomyTermData>(0)));
            ActivityTeamWalletData enemyWallet = CreateActivityWallet(
                content.SharedWallet,
                new ActivityWalletSeedEntryData(
                    new SeedEntry(content.EnemySpawner, 1L, EconomyFormType.Token),
                    ActivitySeedMaterializationType.Spatial,
                    new List<TaxonomyTermData>(0),
                    new List<TaxonomyTermData> { content.EnemyAnchor }));

            ActivityTeamData playerTeam = activity.Teams[0];
            SetStructField(
                ref playerTeam,
                "wallets",
                new List<ActivityTeamWalletData> { playerWallet });
            SetStructField(
                ref playerTeam,
                "objectives",
                new List<ActivityTeamObjectiveData>
                {
                    CreateTeamObjective(content.PlayerDeploymentObjective),
                    CreateTeamObjective(content.TurnTokenObjective),
                });
            SetStructField(
                ref playerTeam,
                "features",
                new List<ActivityFeatureData> { content.PlayerOrchestration });
            activity.Teams[0] = playerTeam;

            ActivityTeamData enemyTeam = activity.Teams[1];
            SetStructField(
                ref enemyTeam,
                "wallets",
                new List<ActivityTeamWalletData> { enemyWallet });
            SetStructField(
                ref enemyTeam,
                "objectives",
                new List<ActivityTeamObjectiveData>
                {
                    CreateTeamObjective(content.EnemyWaveObjective),
                });
            SetStructField(
                ref enemyTeam,
                "features",
                new List<ActivityFeatureData> { content.EnemyOrchestration });
            activity.Teams[1] = enemyTeam;
            SetField(
                activity,
                "worldWallets",
                new List<WalletEntry>
                {
                    new WalletEntry(content.WorldWallet, new List<SeedEntry>(0)),
                });
            EditorUtility.SetDirty(activity);

            ActivityTeamData boardTeam = boardActivity.Teams[0];
            for (int i = 0; i < boardTeam.Wallets.Count; i++)
            {
                ActivityTeamWalletData walletData = boardTeam.Wallets[i];
                if (walletData.Wallet != content.SharedWallet)
                    continue;

                SetStructField(
                    ref walletData,
                    "seed",
                    new List<ActivityWalletSeedEntryData>(0));
                boardTeam.Wallets[i] = walletData;
                break;
            }
            boardActivity.Teams[0] = boardTeam;
            EditorUtility.SetDirty(boardActivity);

            boardMergeRecipe.Outputs.Clear();
            boardMergeRecipe.Outputs.Add(new ProductionOutputData(
                content.WaterUnit,
                EconomyFormType.Stack,
                new List<TaxonomyTermData> { content.SharedWalletTag },
                new LongFlatProgressionData(1L)));
            EditorUtility.SetDirty(boardMergeRecipe);

            ConfigureGameFlowRuntimeTags(content);
        }

        static void ConfigureGameFlowRuntimeTags(Content content)
        {
            GameFlowTemplateData autobattleFlow =
                LoadRequired<GameFlowTemplateData>(AutobattleFlowPath);
            ActivityFlowContainerData autobattleContainer =
                autobattleFlow.Root as ActivityFlowContainerData;
            if (autobattleContainer == null)
                throw new InvalidOperationException("AutobattleFlow root is not an Activity flow container.");

            bool rootLaunchFound = false;
            List<GameFlowStepData> autobattleSteps = autobattleContainer.Steps;
            for (int i = 0; i < autobattleSteps.Count; i++)
            {
                if (!(autobattleSteps[i].Executor is GameFlowLaunchActivityExecutorData launch))
                    continue;

                SetField(
                    launch,
                    "runtimeTags",
                    new List<TaxonomyTermData> { content.IntegrationRuntimeTag });
                rootLaunchFound = true;
            }

            if (!rootLaunchFound)
                throw new InvalidOperationException("AutobattleFlow has no root Activity launch executor.");

            GameFlowTemplateData boardFlow = LoadRequired<GameFlowTemplateData>(BoardFlowPath);
            ActivityFlowContainerData boardContainer = boardFlow.Root as ActivityFlowContainerData;
            if (boardContainer == null)
                throw new InvalidOperationException("BoardFlow root is not an Activity flow container.");

            bool childLaunchFound = false;
            List<GameFlowStepData> boardSteps = boardContainer.Steps;
            for (int i = 0; i < boardSteps.Count; i++)
            {
                if (!(boardSteps[i].Executor is GameFlowLaunchChildActivityExecutorData launch))
                    continue;

                SetField(launch, "runtimeTags", new List<TaxonomyTermData>(0));
                childLaunchFound = true;
            }

            if (!childLaunchFound)
                throw new InvalidOperationException("BoardFlow has no child Activity launch executor.");

            EditorUtility.SetDirty(autobattleFlow);
            EditorUtility.SetDirty(boardFlow);
        }

        static void ConfigureRuntimeInstallers(Content content, List<string> createdPaths)
        {
            EconomyDefinitionsInstallerData economyInstaller =
                LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var assets = new List<EconomyAssetData>(
                GetField<List<EconomyAssetData>>(economyInstaller, "assets"));
            AddUnique(
                assets,
                content.PlayerSpawner,
                content.EnemySpawner,
                content.Enemy,
                content.ExperienceDrop,
                content.ExperienceCollector,
                content.Health,
                content.Movement,
                content.ApproachSkill,
                content.AttackSkill,
                content.CollectionSkill,
                content.AlliedCombatBrain,
                content.EnemyCombatBrain,
                content.CollectorBrain,
                content.DeploymentRecipe,
                content.EnemyWaveRecipe,
                content.DropRecipe,
                content.PlayerCatalog,
                content.EnemyCatalog,
                content.DropCatalog,
                content.PlayerProduction,
                content.EnemyProduction,
                content.DropProduction,
                content.WaterUnit,
                content.Experience,
                content.SpawnArea);
            SetField(economyInstaller, "assets", assets);
            var wallets = new List<EconomyWalletData>(
                GetField<List<EconomyWalletData>>(economyInstaller, "wallets"));
            AddUnique(
                wallets,
                content.UnitWallet,
                content.DropContentsWallet,
                content.WorldWallet,
                content.CollectorWallet);
            SetField(economyInstaller, "wallets", wallets);
            EditorUtility.SetDirty(economyInstaller);

            EconomyRuntimeInstallerData economyRuntime =
                LoadRequired<EconomyRuntimeInstallerData>(EconomyRuntimeInstallerPath);
            var domains = new List<EconomyDomainType>(
                GetField<List<EconomyDomainType>>(economyRuntime, "domains"));
            AddUniqueValue(
                domains,
                EconomyDomainType.HostValue,
                EconomyDomainType.AI,
                EconomyDomainType.Movement,
                EconomyDomainType.Spatial);
            SetField(economyRuntime, "domains", domains);
            EditorUtility.SetDirty(economyRuntime);

            TaxonomyRuntimeInstallerData taxonomyInstaller =
                LoadRequired<TaxonomyRuntimeInstallerData>(TaxonomyInstallerPath);
            var families = new List<TaxonomyFamilyData>(
                GetField<TaxonomyFamilyData[]>(taxonomyInstaller, "families")
                ?? new TaxonomyFamilyData[0]);
            for (int i = 0; i < content.Families.Count; i++)
                AddUnique(families, content.Families[i]);
            SetField(taxonomyInstaller, "families", families.ToArray());
            var terms = new List<TaxonomyTermData>(
                GetField<TaxonomyTermData[]>(taxonomyInstaller, "terms")
                ?? new TaxonomyTermData[0]);
            for (int i = 0; i < content.Terms.Count; i++)
                AddUnique(terms, content.Terms[i]);
            SetField(taxonomyInstaller, "terms", terms.ToArray());
            EditorUtility.SetDirty(taxonomyInstaller);

            GameplaySkillsInstallerData skillsInstaller =
                LoadRequired<GameplaySkillsInstallerData>(SkillsInstallerPath);
            var skills = new List<FrameworkSkillData>(
                GetField<List<FrameworkSkillData>>(skillsInstaller, "skills"));
            AddUnique(
                skills,
                content.ApproachSkill,
                content.AttackSkill,
                content.CollectionSkill);
            SetField(skillsInstaller, "skills", skills);
            EditorUtility.SetDirty(skillsInstaller);

            SkillEffectAdapterCatalogData adapterCatalog =
                LoadRequired<SkillEffectAdapterCatalogData>(SkillsCatalogPath);
            List<SkillEffectAdapterData> adapters = adapterCatalog.Adapters;
            for (int i = 0; i < adapters.Count; i++)
            {
                if (adapters[i] is SkillEconomyEntryEffectAdapterData)
                    throw new InvalidOperationException(
                        "Skill adapter catalog already contains SkillEconomyEntryEffectAdapterData.");
            }
            adapters.Add(new SkillEconomyEntryEffectAdapterData());
            EditorUtility.SetDirty(adapterCatalog);

            GameplayHostValuesInstallerData hostValuesInstaller =
                CreateAsset<GameplayHostValuesInstallerData>(
                    HostValuesInstallerPath,
                    "ChainRushGameplayHostValuesInstaller",
                    createdPaths);
            SetField(
                hostValuesInstaller,
                "values",
                new List<HostValueData> { content.Health });
            var healthDefinition = new HostValueDefinitionData();
            SetField(healthDefinition, "value", content.Health);
            SetField(
                healthDefinition,
                "initializationType",
                HostValueInitializationType.ProjectedSeed);
            SetField(
                healthDefinition,
                "minCap",
                new HostValueBoundryData(0L, HostValueBoundryPolicy.Clamp));
            SetField(
                healthDefinition,
                "maxCap",
                new HostValueBoundryData(0L, HostValueBoundryPolicy.Allow));
            SetField(
                hostValuesInstaller,
                "definitions",
                new List<HostValueDefinitionData> { healthDefinition });

            DropRuntimeInstallerData dropInstaller = CreateAsset<DropRuntimeInstallerData>(
                DropInstallerPath,
                "ChainRushDropRuntimeInstaller",
                createdPaths);
            SetField(dropInstaller, "resetRuntimeOnInstall", true);

            ActivityDiplomacyModuleData activityDiplomacy =
                CreateAsset<ActivityDiplomacyModuleData>(
                    ActivityDiplomacyPath,
                    "ActivityDiplomacyModule",
                    createdPaths);
            CapabilityHostDiplomacyModuleData capabilityDiplomacy =
                CreateAsset<CapabilityHostDiplomacyModuleData>(
                    CapabilityHostDiplomacyPath,
                    "CapabilityHostDiplomacyModule",
                    createdPaths);
            DiplomacyRuntimeInstallerData diplomacyInstaller =
                CreateAsset<DiplomacyRuntimeInstallerData>(
                    DiplomacyInstallerPath,
                    "ChainRushDiplomacyRuntimeInstaller",
                    createdPaths);
            SetField(
                diplomacyInstaller,
                "modules",
                new List<DiplomacyModuleData> { activityDiplomacy, capabilityDiplomacy });

            GameRuntimeProfileData profile = LoadRequired<GameRuntimeProfileData>(RuntimeProfilePath);
            var installers = new List<GameRuntimeInstallerData>(profile.Installers);
            AddUnique(installers, hostValuesInstaller, dropInstaller, diplomacyInstaller);
            SetField(profile, "installers", installers);
            EditorUtility.SetDirty(profile);
        }

        static void ConfigureIntegrationScene(Content content)
        {
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.OpenScene(IntegrationScenePath, OpenSceneMode.Single);
            if (GameObject.Find("AutobattleCamera") != null
                || GameObject.Find("ExperienceUI") != null)
            {
                throw new InvalidOperationException(
                    "Integration scene already contains AutobattleCamera or ExperienceUI.");
            }

            var cameraObject = new GameObject("AutobattleCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 16f, 0f);
            cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.06f, 1f);
            ActivityViewportController viewport =
                cameraObject.AddComponent<ActivityViewportController>();
            SetField(
                viewport,
                "activitySelector",
                CreateIntegrationActivitySelector(content));
            SetField(viewport, "viewport", camera);

            GameObject uiPrefab = LoadRequired<GameObject>(ExperienceUIPrefabPath);
            GameObject ui = PrefabUtility.InstantiatePrefab(uiPrefab, scene) as GameObject;
            if (ui == null)
                throw new InvalidOperationException("Failed to instantiate ExperienceUI prefab.");
            ui.name = "ExperienceUI";

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        const EconomyOperation AllMutableOperations =
            EconomyOperation.Require
            | EconomyOperation.Issue
            | EconomyOperation.Consume
            | EconomyOperation.Transfer
            | EconomyOperation.Destroy;

        static FrameworkSkillData CreateSkill(
            string path,
            string name,
            string id,
            SkillTargetType targetType,
            DiplomacyDispositionType supposedTarget,
            List<TaxonomyTermData> targetTags,
            long startDelay,
            long reloadTime,
            List<string> createdPaths)
        {
            FrameworkSkillData skill = CreateEconomyAsset<FrameworkSkillData>(
                path,
                name,
                id,
                AllMutableOperations,
                createdPaths);
            SetField(skill, "targetType", targetType);
            SetField(skill, "targetCount", new BoundIntValue(1, 1));
            SetField(skill, "analyticSupposedTarget", supposedTarget);
            SetField(skill, "targetTags", targetTags ?? new List<TaxonomyTermData>(0));
            SetField<TaxonomyTermData>(skill, "targetSlot", null);
            SetField<TaxonomyTermData>(skill, "executionSlot", null);
            SetField(skill, "startDelay", startDelay);
            SetField(skill, "endDelay", 0L);
            SetField(skill, "actionInterval", 0L);
            SetField(skill, "actionCount", 1);
            SetField(skill, "reloadTime", reloadTime);
            SetField(skill, "requirements", new List<SkillRequirementData>(0));
            SetField(skill, "effects", new List<SkillEffectData>(0));
            EditorUtility.SetDirty(skill);
            return skill;
        }

        static CapabilityHostData CreateCapabilityHost(
            string path,
            string name,
            string id,
            List<TaxonomyTermData> tags,
            List<CapabilityEntry> capabilities,
            Vector3Int footprintSize,
            List<string> createdPaths)
        {
            CapabilityHostData host = CreateEconomyAsset<CapabilityHostData>(
                path,
                name,
                id,
                AllMutableOperations,
                createdPaths);
            host.Tags.Clear();
            if (tags != null)
                host.Tags.AddRange(tags);
            SetField(host, "footprintSize", footprintSize);
            SetField(host, "capabilities", capabilities ?? new List<CapabilityEntry>(0));
            SetField(host, "walletEntries", new List<WalletEntry>(0));
            EditorUtility.SetDirty(host);
            return host;
        }

        static CapabilityEntry CreateCapability(
            CapabilityHostType capabilityType,
            params TaxonomyTermData[] selectorTags)
        {
            var capability = new CapabilityEntry();
            SetField(capability, "capabilityType", capabilityType);
            SetField(
                capability,
                "selectorTags",
                selectorTags == null
                    ? new List<TaxonomyTermData>(0)
                    : new List<TaxonomyTermData>(selectorTags));
            return capability;
        }

        static void ConfigureAIBrainBinding(
            CapabilityHostData host,
            AIBrainData brain,
            TaxonomyTermData selectorTag)
        {
            if (host == null || brain == null || selectorTag == null)
                throw new InvalidOperationException("AIBrain binding requires host, brain, and selector tag.");

            CapabilityEntry aiCapability = null;
            for (int i = 0; i < host.Capabilities.Count; i++)
            {
                CapabilityEntry candidate = host.Capabilities[i];
                if (candidate == null || candidate.CapabilityType != CapabilityHostType.AIBrainOwner)
                    continue;
                if (aiCapability != null)
                    throw new InvalidOperationException(string.Concat(
                        "Capability host has duplicate AIBrainOwner entries: ", host.name));
                aiCapability = candidate;
            }

            if (aiCapability == null)
                throw new InvalidOperationException(string.Concat(
                    "Capability host has no AIBrainOwner entry: ", host.name));

            SeedEntry brainSeed = null;
            for (int walletIndex = 0; walletIndex < host.WalletEntries.Count; walletIndex++)
            {
                WalletEntry wallet = host.WalletEntries[walletIndex];
                if (wallet == null)
                    continue;
                for (int seedIndex = 0; seedIndex < wallet.Seed.Count; seedIndex++)
                {
                    SeedEntry seed = wallet.Seed[seedIndex];
                    if (seed == null || seed.Asset != brain)
                        continue;
                    if (brainSeed != null)
                        throw new InvalidOperationException(string.Concat(
                            "Capability host has duplicate AIBrain seed entries: ", host.name));
                    brainSeed = seed;
                }
            }

            if (brainSeed == null)
                throw new InvalidOperationException(string.Concat(
                    "Capability host has no seed for brain '", brain.name, "': ", host.name));

            SetField(
                aiCapability,
                "selectorTags",
                new List<TaxonomyTermData> { selectorTag });
            SetField(
                brainSeed,
                "runtimeTags",
                new List<TaxonomyTermData> { selectorTag });
            EditorUtility.SetDirty(host);
        }

        static void ConfigureCombatRetryTransitions(AIBrainData combatBrain)
        {
            int configuredCount = 0;
            for (int transitionIndex = 0;
                 transitionIndex < combatBrain.Transitions.Count;
                 transitionIndex++)
            {
                AIBrainTransitionData transition = combatBrain.Transitions[transitionIndex];
                CurrentStateResultMatchesAIBrainConditionData condition = null;
                if (transition != null)
                {
                    for (int conditionIndex = 0;
                         conditionIndex < transition.Conditions.Count;
                         conditionIndex++)
                    {
                        condition = transition.Conditions[conditionIndex]
                            as CurrentStateResultMatchesAIBrainConditionData;
                        if (condition != null)
                            break;
                    }
                }
                if (condition == null)
                    continue;

                SetField(
                    condition,
                    "requiredResults",
                    AIBrainStateResultMask.Fail | AIBrainStateResultMask.Interrupted);
                SetField(transition, "exitResult", AIBrainStateResultType.Fail);
                configuredCount++;
            }

            if (configuredCount != 2)
            {
                throw new InvalidOperationException(string.Concat(
                    "Combat brain must contain exactly two retry transitions; found ",
                    configuredCount.ToString(),
                    "."));
            }

            EditorUtility.SetDirty(combatBrain);
        }

        static ProductionCatalogData CreateCatalog(
            string path,
            string name,
            string id,
            List<ProductionRecipeData> recipes,
            List<string> createdPaths)
        {
            ProductionCatalogData catalog = CreateEconomyAsset<ProductionCatalogData>(
                path,
                name,
                id,
                EconomyOperation.Require | EconomyOperation.Issue,
                createdPaths);
            catalog.Entries.Clear();
            for (int i = 0; recipes != null && i < recipes.Count; i++)
            {
                ProductionCatalogEntryData entry = default;
                SetStructField(ref entry, "recipe", recipes[i]);
                SetStructField(ref entry, "workDuration", 1);
                SetStructField(ref entry, "recoveryDuration", 0);
                SetStructField(
                    ref entry,
                    "reservationPolicy",
                    ProductionReservationPolicy.OnEnqueue);
                catalog.Entries.Add(entry);
            }
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static ProductionData CreateProductionDefinition(
            string path,
            string name,
            string id,
            ProductionCatalogData catalog,
            TaxonomyTermData materializationProviderType,
            List<string> createdPaths)
        {
            ProductionData production = CreateEconomyAsset<ProductionData>(
                path,
                name,
                id,
                AllMutableOperations,
                createdPaths);
            production.SupportedCatalogs.Clear();
            production.SupportedCatalogs.Add(catalog);
            SetField(production, "maxQueuedOrders", 1);
            SetField(production, "maxParallelPipelines", 1);
            SetField(
                production,
                "limitReachedPolicy",
                ProductionLimitReachedPolicy.DisableProduction);
            SetField(production, "startPolicy", ProductionStartPolicyType.Explicit);
            SetField(production, "materializationProviderType", materializationProviderType);
            EditorUtility.SetDirty(production);
            return production;
        }

        static EconomyEntrySelectionData CreateSelection(
            TaxonomyTermData walletTag,
            EconomyFormType formType,
            EconomyAssetData exactAsset)
        {
            var selection = new EconomyEntrySelectionData();
            SetField(
                selection,
                "walletTags",
                walletTag == null
                    ? new List<TaxonomyTermData>(0)
                    : new List<TaxonomyTermData> { walletTag });
            SetField(selection, "formTypes", new List<EconomyFormType> { formType });
            SetField(selection, "exactAsset", exactAsset);
            SetField(selection, "requiredAssetTags", new List<TaxonomyTermData>(0));
            SetField(selection, "excludedAssetTags", new List<TaxonomyTermData>(0));
            SetField(selection, "requiredRuntimeTags", new List<TaxonomyTermData>(0));
            SetField(selection, "excludedRuntimeTags", new List<TaxonomyTermData>(0));
            return selection;
        }

        static SkillEconomyEntryEffectData CreateEconomyEntryEffect(
            EffectRecipient recipient,
            SkillEconomyEntrySourceType sourceType,
            SkillEconomyOwnerType sourceOwnerType,
            EconomyEntrySelectionData selection,
            EconomyOperation operation,
            EffectRecipient destinationRecipient,
            SkillEconomyOwnerType destinationOwnerType,
            List<TaxonomyTermData> destinationWalletTags)
        {
            var effect = new SkillEconomyEntryEffectData();
            ConfigureEffect(effect, recipient, 0L);
            SetField(effect, "sourceType", sourceType);
            SetField(effect, "sourceOwnerType", sourceOwnerType);
            SetField(effect, "selection", selection);
            SetField(effect, "operation", operation);
            SetField(effect, "destinationRecipient", destinationRecipient);
            SetField(effect, "destinationOwnerType", destinationOwnerType);
            SetField(
                effect,
                "destinationWalletTags",
                destinationWalletTags ?? new List<TaxonomyTermData>(0));
            return effect;
        }

        static void ConfigureEffect(
            SkillEffectData effect,
            EffectRecipient recipient,
            long value)
        {
            SetField(effect, "recipient", recipient);
            SetField(effect, "requiredTags", new List<TaxonomyTermData>(0));
            SetField(effect, "value", value);
            SetField<Core.Attributes.AttributeData>(effect, "multiplier", null);
        }

        static AIBrainStateData CreateCombatState(
            TaxonomyTermData stateTag,
            Content content)
        {
            var state = new AIBrainStateData();
            SetField(state, "tag", stateTag);
            SetField(state, "onEnterActions", new List<AIBrainActionData>(0));
            SetField(
                state,
                "onTickActions",
                new List<AIBrainActionData>
                {
                    CreateCombatTargetAction(content),
                    CreateUseSkillAction(
                        content.ApproachSkill,
                        content.CombatTarget,
                        SkillCompletionPolicyType.OnExecutionComplete),
                    CreateUseSkillAction(
                        content.AttackSkill,
                        content.CombatTarget,
                        SkillCompletionPolicyType.OnActivation),
                });
            SetField(state, "onExitActions", new List<AIBrainExitActionData>(0));
            return state;
        }

        static SelectActivityTargetAIBrainActionData CreateCombatTargetAction(Content content)
        {
            var action = new SelectActivityTargetAIBrainActionData();
            SetField(action, "targetKey", content.CombatTarget);
            SetField(action, "additionalTargetKeys", new List<TaxonomyTermData>(0));
            SetField(action, "diplomacyChannel", DiplomacyChannelType.Military);
            SetField(action, "requiredDisposition", DiplomacyDispositionType.Hostile);
            SetField(
                action,
                "requiredTargetTags",
                new List<TaxonomyTermData> { content.CombatantRole });
            SetField(action, "blockedTargetTags", new List<TaxonomyTermData>(0));
            SetField(action, "requiredTargetStates", new List<TaxonomyTermData>(0));
            SetField(
                action,
                "blockedTargetStates",
                new List<TaxonomyTermData> { content.DefeatState });
            SetField(action, "compatibleSkill", content.AttackSkill);
            return action;
        }

        static SelectActivityTargetAIBrainActionData CreateCollectionTargetAction(Content content)
        {
            var action = new SelectActivityTargetAIBrainActionData();
            SetField(action, "targetKey", content.CollectionTarget);
            SetField(action, "additionalTargetKeys", new List<TaxonomyTermData>(0));
            SetField(action, "diplomacyChannel", DiplomacyChannelType.Military);
            SetField(action, "requiredDisposition", DiplomacyDispositionType.Neutral);
            SetField(
                action,
                "requiredTargetTags",
                new List<TaxonomyTermData> { content.ExperienceDropRole });
            SetField(action, "blockedTargetTags", new List<TaxonomyTermData>(0));
            SetField(action, "requiredTargetStates", new List<TaxonomyTermData>(0));
            SetField(action, "blockedTargetStates", new List<TaxonomyTermData>(0));
            SetField(action, "compatibleSkill", content.CollectionSkill);
            return action;
        }

        static TargetEntityExistsAIBrainConditionData CreateTargetExistsCondition(
            TaxonomyTermData targetKey,
            bool targetShouldExist)
        {
            var condition = new TargetEntityExistsAIBrainConditionData();
            SetField(condition, "targetKey", targetKey);
            SetField(condition, "targetShouldExist", targetShouldExist);
            return condition;
        }

        static CurrentStateResultMatchesAIBrainConditionData CreateStateResultCondition(
            AIBrainStateResultMask resultMask)
        {
            var condition = new CurrentStateResultMatchesAIBrainConditionData();
            SetField(condition, "requiredResults", resultMask);
            return condition;
        }

        static AIBrainTransitionData CreateBrainTransition(
            TaxonomyTermData fromState,
            TaxonomyTermData nodeId,
            TaxonomyTermData toState,
            params AIBrainConditionData[] conditions)
        {
            var transition = new AIBrainTransitionData();
            SetField(
                transition,
                "fromStates",
                new List<TaxonomyTermData> { fromState });
            SetField(transition, "toNodeId", nodeId);
            SetField(transition, "toState", toState);
            SetField(transition, "exitResult", AIBrainStateResultType.Success);
            SetField(
                transition,
                "conditions",
                conditions == null
                    ? new List<AIBrainConditionData>(0)
                    : new List<AIBrainConditionData>(conditions));
            return transition;
        }

        static UseSkillAIBrainActionData CreateUseSkillAction(
            FrameworkSkillData skill,
            TaxonomyTermData targetKey,
            SkillCompletionPolicyType completionPolicyType)
        {
            var action = new UseSkillAIBrainActionData();
            SetField(action, "skill", skill);
            SetField(action, "targetKey", targetKey);
            SetField(action, "completionPolicy", completionPolicyType);
            return action;
        }

        static DropAIBrainActionData CreateDropAction(DropProfileData profile)
        {
            var action = new DropAIBrainActionData();
            SetField(action, "profile", profile);
            return action;
        }

        static AIBrainTransitionData CreateDefeatTransition(Content content)
        {
            var condition = new HostValueConditionAIBrainConditionData();
            SetField(condition, "hostValue", content.Health);
            SetField(condition, "compareOperation", CompareOperation.LessOrEqual);
            SetField(condition, "targetValue", 0L);

            var transition = new AIBrainTransitionData();
            SetField(
                transition,
                "fromStates",
                new List<TaxonomyTermData> { content.CombatStateA, content.CombatStateB });
            SetField(transition, "toNodeId", content.CombatNode);
            SetField(transition, "toState", content.DefeatState);
            SetField(transition, "exitResult", AIBrainStateResultType.Interrupted);
            SetField(
                transition,
                "conditions",
                new List<AIBrainConditionData> { condition });
            return transition;
        }

        static AIBrainTransitionData CreateRetryTransition(
            TaxonomyTermData fromState,
            TaxonomyTermData node,
            TaxonomyTermData toState)
        {
            var condition = new CurrentStateResultMatchesAIBrainConditionData();
            SetField(
                condition,
                "requiredResults",
                AIBrainStateResultMask.Fail
                | AIBrainStateResultMask.Interrupted);

            var transition = new AIBrainTransitionData();
            SetField(
                transition,
                "fromStates",
                new List<TaxonomyTermData> { fromState });
            SetField(transition, "toNodeId", node);
            SetField(transition, "toState", toState);
            SetField(transition, "exitResult", AIBrainStateResultType.Fail);
            SetField(
                transition,
                "conditions",
                new List<AIBrainConditionData> { condition });
            return transition;
        }

        static ObjectiveTemplateData CreateResetObjective(
            string path,
            string name,
            string id,
            ObjectiveCondition activation,
            ObjectiveCondition success,
            List<string> createdPaths)
        {
            var root = new ObjectiveNode(
                id,
                null,
                new List<ObjectiveCondition> { activation },
                new List<ObjectiveCondition> { success },
                new List<ObjectiveCondition>(0));
            ObjectiveTemplateData objective = CreateAsset<ObjectiveTemplateData>(
                path,
                name,
                createdPaths);
            SetField(objective, "root", root);
            SetField(
                objective,
                "completionPolicyType",
                ObjectiveCompletionPolicyType.Reset);
            EditorUtility.SetDirty(objective);
            return objective;
        }

        static ObjectiveConditionMaterializedEntity CreateMaterializedCondition(
            EconomyAssetData asset,
            long targetValue,
            CompareOperation compareOperation)
        {
            return new ObjectiveConditionMaterializedEntity(
                Core.Entities.EntityId.Invalid,
                asset,
                EconomyFormType.Token,
                new List<TaxonomyTermData>(0),
                new List<CapabilityHostType>(0),
                targetValue,
                compareOperation,
                SpatialMarkerRef.Invalid);
        }

        static AgentDefinitionData CreateProductionAgent(
            string path,
            string name,
            string id,
            ObjectiveCondition matchCondition,
            CapabilityHostData executor,
            List<string> createdPaths)
        {
            AgentDefinitionData definition =
                CreateAsset<AgentDefinitionData>(path, name, createdPaths);
            SetField(definition, "agentId", id);
            SetField(definition, "basePriority", 100);
            SetField(definition, "updateInterval", 1);
            SetField(
                definition,
                "matchConditions",
                new List<ObjectiveCondition> { matchCondition });
            SetField(
                definition,
                "executorSelectionCriteria",
                new List<EntityCriterionEntryData>
                {
                    Required(CreateCapabilityHostCriterion(executor)),
                    Required(CreateOwnerCriterion()),
                });
            SetField(
                definition,
                "targetSelectionCriteria",
                new List<EntityCriterionEntryData>(0));
            SetField(definition, "controlType", AgentControlType.Endpoint);
            SetField(definition, "agent", new ProductionAgentData());
            SetField(definition, "stopPolicyType", AgentStopPolicyType.None);
            SetField(
                definition,
                "executorBusyPolicyType",
                AgentExecutorBusyPolicyType.Wait);
            SetField(
                definition,
                "executorReservationPolicyType",
                ExecutorReservationPolicyType.PerWork);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        static CapabilityHostCriterionData CreateCapabilityHostCriterion(
            CapabilityHostBaseData definition)
        {
            var criterion = new CapabilityHostCriterionData();
            SetField(criterion, "definition", definition);
            SetField(criterion, "requiredAssetTags", new List<TaxonomyTermData>(0));
            SetField(criterion, "requiredCapabilityTypes", new List<CapabilityHostType>(0));
            return criterion;
        }

        static OwnerCriterionData CreateOwnerCriterion()
        {
            var criterion = new OwnerCriterionData();
            SetField(
                criterion,
                "ownerSelectionType",
                AgentOwnerSelectionType.ParticipantOwner);
            return criterion;
        }

        static EntityCriterionEntryData Required(EntityCriterionData criterion)
        {
            return new EntityCriterionEntryData(CriterionRequirementType.Required, criterion);
        }

        static ActivityTeamObjectiveData CreateTeamObjective(ObjectiveTemplateData objective)
        {
            ActivityTeamObjectiveData teamObjective = default;
            SetStructField(ref teamObjective, "template", objective);
            SetStructField(ref teamObjective, "successScoreDelta", 0);
            SetStructField(ref teamObjective, "failScoreDelta", 0);
            return teamObjective;
        }

        static ActivityTeamWalletData CreateActivityWallet(
            EconomyWalletData wallet,
            params ActivityWalletSeedEntryData[] seed)
        {
            ActivityTeamWalletData walletData = default;
            SetStructField(ref walletData, "wallet", wallet);
            SetStructField(
                ref walletData,
                "seed",
                seed == null
                    ? new List<ActivityWalletSeedEntryData>(0)
                    : new List<ActivityWalletSeedEntryData>(seed));
            return walletData;
        }

        static void ConfigurePlayerOrchestrationBrain(Content content)
        {
            AgentDecompOpData turnTokenAgent = CreateAgentOperation(
                content.TurnTokenAgentOperator,
                content.TurnTokenAgent);
            ProductionInputConsumptionDecompOpData input =
                CreateOperation<ProductionInputConsumptionDecompOpData>(
                    content.ProductionInputOperator);
            ProductionEconomyDecompOpData economy =
                CreateOperation<ProductionEconomyDecompOpData>(
                    content.ProductionEconomyOperator);
            AwaitFactDecompOpData awaitFact = CreateAwaitFactOperation(
                content.AwaitFactOperator);
            MaterializedEntityProductionDecompOpData materialized =
                CreateOperation<MaterializedEntityProductionDecompOpData>(
                    content.ProductionMaterializedOperator);
            ProductionYieldDecompOpData yield = CreateOperation<ProductionYieldDecompOpData>(
                content.ProductionYieldOperator);
            ProductionAvailableDecompOpData available =
                CreateOperation<ProductionAvailableDecompOpData>(
                    content.ProductionAvailableOperator);

            var graph = new OrchestrationDecisionGraphData();
            SetField(
                graph,
                "nodes",
                new List<OrchestrationDecisionNodeData>
                {
                    CreateDecision(
                        "turn-token-agent",
                        OrchestrationFactType.EconomyAmount,
                        content.TurnTokenAgentOperator,
                        OrchestrationDecompositionScopeType.GlobalObjective,
                        matchAgent: true),
                    CreateEconomyDecision(
                        "global-production-economy",
                        content.ProductionEconomyOperator,
                        OrchestrationDecompositionScopeType.GlobalObjective,
                        new List<CompareOperation>
                        {
                            CompareOperation.Greater,
                            CompareOperation.GreaterOrEqual,
                        },
                        requireZeroTargetForEqual: false),
                    CreateEconomyDecision(
                        "global-production-input",
                        content.ProductionInputOperator,
                        OrchestrationDecompositionScopeType.GlobalObjective,
                        new List<CompareOperation>
                        {
                            CompareOperation.Equal,
                            CompareOperation.Less,
                            CompareOperation.LessOrEqual,
                        },
                        requireZeroTargetForEqual: true),
                    CreateAwaitFactDecision(content.AwaitFactOperator),
                    CreateEconomyDecision(
                        "production-input",
                        content.ProductionInputOperator,
                        OrchestrationDecompositionScopeType.AgentLocal,
                        new List<CompareOperation>
                        {
                            CompareOperation.Equal,
                            CompareOperation.Less,
                            CompareOperation.LessOrEqual,
                        },
                        requireZeroTargetForEqual: true),
                    CreateEconomyDecision(
                        "production-economy",
                        content.ProductionEconomyOperator,
                        OrchestrationDecompositionScopeType.AgentLocal,
                        new List<CompareOperation>
                        {
                            CompareOperation.Greater,
                            CompareOperation.GreaterOrEqual,
                        },
                        requireZeroTargetForEqual: false),
                    CreateDecision(
                        "production-materialized",
                        OrchestrationFactType.MaterializedEntity,
                        content.ProductionMaterializedOperator,
                        OrchestrationDecompositionScopeType.GlobalObjective,
                        matchAgent: false),
                    CreateDecision(
                        "production-yield",
                        OrchestrationFactType.ProductionYield,
                        content.ProductionYieldOperator,
                        OrchestrationDecompositionScopeType.AgentLocal,
                        matchAgent: false),
                    CreateDecision(
                        "production-available",
                        OrchestrationFactType.ProductionAvailable,
                        content.ProductionAvailableOperator,
                        OrchestrationDecompositionScopeType.AgentLocal,
                        matchAgent: false),
                });

            SetField(
                content.PlayerBrain,
                "operators",
                new List<OrchestrationDecompOpData>
                {
                    turnTokenAgent,
                    input,
                    economy,
                    awaitFact,
                    materialized,
                    yield,
                    available,
                });
            SetField(content.PlayerBrain, "decisionGraph", graph);
            EditorUtility.SetDirty(content.PlayerBrain);
        }

        static void ConfigureEnemyOrchestrationBrain(Content content)
        {
            AgentDecompOpData waveAgent = CreateAgentOperation(
                content.EnemyWaveAgentOperator,
                content.EnemyWaveAgent);
            MaterializedEntityProductionDecompOpData materialized =
                CreateOperation<MaterializedEntityProductionDecompOpData>(
                    content.ProductionMaterializedOperator);
            ProductionYieldDecompOpData yield = CreateOperation<ProductionYieldDecompOpData>(
                content.ProductionYieldOperator);
            ProductionAvailableDecompOpData available =
                CreateOperation<ProductionAvailableDecompOpData>(
                    content.ProductionAvailableOperator);

            var graph = new OrchestrationDecisionGraphData();
            SetField(
                graph,
                "nodes",
                new List<OrchestrationDecisionNodeData>
                {
                    CreateDecision(
                        "enemy-wave-agent",
                        OrchestrationFactType.MaterializedEntity,
                        content.EnemyWaveAgentOperator,
                        OrchestrationDecompositionScopeType.GlobalObjective,
                        matchAgent: true),
                    CreateDecision(
                        "enemy-materialized-production",
                        OrchestrationFactType.MaterializedEntity,
                        content.ProductionMaterializedOperator,
                        OrchestrationDecompositionScopeType.AgentLocal,
                        matchAgent: false),
                    CreateDecision(
                        "enemy-production-yield",
                        OrchestrationFactType.ProductionYield,
                        content.ProductionYieldOperator,
                        OrchestrationDecompositionScopeType.AgentLocal,
                        matchAgent: false),
                    CreateDecision(
                        "enemy-production-available",
                        OrchestrationFactType.ProductionAvailable,
                        content.ProductionAvailableOperator,
                        OrchestrationDecompositionScopeType.AgentLocal,
                        matchAgent: false),
                });

            SetField(
                content.EnemyBrain,
                "operators",
                new List<OrchestrationDecompOpData>
                {
                    waveAgent,
                    materialized,
                    yield,
                    available,
                });
            SetField(content.EnemyBrain, "decisionGraph", graph);
            EditorUtility.SetDirty(content.EnemyBrain);
        }

        static AgentDecompOpData CreateAgentOperation(
            TaxonomyTermData operatorId,
            AgentDefinitionData agent)
        {
            var operation = new AgentDecompOpData();
            SetField(operation, "operatorId", operatorId);
            SetField(operation, "agentDefinition", agent);
            return operation;
        }

        static T CreateOperation<T>(TaxonomyTermData operatorId)
            where T : OrchestrationDecompOpData, new()
        {
            var operation = new T();
            SetField(operation, "operatorId", operatorId);
            return operation;
        }

        static AwaitFactDecompOpData CreateAwaitFactOperation(TaxonomyTermData operatorId)
        {
            AwaitFactDecompOpData operation = CreateOperation<AwaitFactDecompOpData>(operatorId);
            SetField(
                operation,
                "inputFactTypes",
                new List<OrchestrationPlanningFactType>
                {
                    OrchestrationPlanningFactType.EconomyAmount,
                });
            return operation;
        }

        static OrchestrationDecisionData CreateDecision(
            string id,
            OrchestrationFactType factType,
            TaxonomyTermData operatorId,
            OrchestrationDecompositionScopeType scopeType,
            bool matchAgent)
        {
            var fact = new FactTypeDecisionConditionData();
            SetField(fact, "factType", factType);
            var scope = new ScopeDecisionConditionData();
            SetField(scope, "scopeType", scopeType);
            var conditions = new List<OrchestrationDecisionConditionData> { fact, scope };
            if (matchAgent)
                conditions.Add(new AgentMatchDecisionConditionData());

            var decision = new OrchestrationDecisionData();
            SetField(decision, "decisionId", id);
            SetField(decision, "conditions", conditions);
            SetField(decision, "operatorId", operatorId);
            return decision;
        }

        static OrchestrationDecisionData CreateEconomyDecision(
            string id,
            TaxonomyTermData operatorId,
            OrchestrationDecompositionScopeType scopeType,
            List<CompareOperation> compareOperations,
            bool requireZeroTargetForEqual)
        {
            OrchestrationDecisionData decision = CreateDecision(
                id,
                OrchestrationFactType.EconomyAmount,
                operatorId,
                scopeType,
                matchAgent: false);
            List<OrchestrationDecisionConditionData> conditions =
                GetField<List<OrchestrationDecisionConditionData>>(decision, "conditions");
            var comparison = new CompareOperationDecisionConditionData();
            SetField(comparison, "compareOperations", compareOperations);
            SetField(comparison, "requireZeroTargetForEqual", requireZeroTargetForEqual);
            conditions.Add(comparison);
            return decision;
        }

        static OrchestrationDecisionData CreateAwaitFactDecision(
            TaxonomyTermData operatorId)
        {
            OrchestrationDecisionData decision = CreateDecision(
                "await-external-economy",
                OrchestrationFactType.EconomyAmount,
                operatorId,
                OrchestrationDecompositionScopeType.GlobalObjective,
                matchAgent: false);
            List<OrchestrationDecisionConditionData> conditions =
                GetField<List<OrchestrationDecisionConditionData>>(decision, "conditions");
            var planIntent = new PlanIntentDecisionConditionData();
            SetField(
                planIntent,
                "actionTypes",
                new List<PlanActionType>
                {
                    PlanActionType.Push,
                });
            SetField(planIntent, "requirePlanningEnabled", true);
            conditions.Add(planIntent);
            return decision;
        }

        static ActivityOrchestrationConfigData CreateOrchestration(
            string path,
            string name,
            string debugName,
            OrchestratorAIBrainData brain,
            Content content,
            List<string> createdPaths)
        {
            ActivityOrchestrationConfigData orchestration =
                CreateAsset<ActivityOrchestrationConfigData>(path, name, createdPaths);
            SetField(orchestration, "orchestratorBrain", brain);
            SetField(
                orchestration,
                "modules",
                new List<OrchestrationDomainModuleData>
                {
                    content.EconomyState,
                    content.ProductionState,
                    content.ProjectionState,
                });
            SetField(orchestration, "debugName", debugName);
            EditorUtility.SetDirty(orchestration);
            return orchestration;
        }

        static Material CreateMaterial(
            string path,
            string name,
            Color color,
            List<string> createdPaths)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                throw new InvalidOperationException("No supported unlit shader was found.");

            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            createdPaths.Add(path);
            return material;
        }

        static void CreateSpawnerPrefab(
            string path,
            string name,
            TaxonomyTermData providerType,
            SpatialShapeData shape,
            Vector3Int position,
            Vector3Int size,
            string poolKey,
            List<string> createdPaths)
        {
            var root = new GameObject(name);
            try
            {
                ConfigureProjectionBinding(root, poolKey);
                SpatialShapeProviderController provider =
                    root.AddComponent<SpatialShapeProviderController>();
                ConfigureProviderBase(
                    provider,
                    string.Concat(name.ToLowerInvariant(), "-spawn-provider"),
                    providerType,
                    SpatialMarkerReusePolicyType.ReuseAllowed);
                SetField(provider, "refreshPolicyType", SpatialMarkerRefreshPolicyType.OnUse);
                SetField(provider, "shape", shape);
                SetField(
                    provider,
                    "usage",
                    CreateSpawnAreaUsage(position, size));
                SetField(provider, "markerTags", new List<TaxonomyTermData> { providerType });
                SetField(provider, "drawGizmo", true);

                SavePrefab(root, path, createdPaths);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static SpatialShapeUsageData CreateSpawnAreaUsage(
            Vector3Int position,
            Vector3Int size)
        {
            return new SpatialShapeUsageData(
                SpatialShapeFillType.Inside,
                position,
                size,
                Vector3Int.zero,
                new Vector3Int(1000, 1, 1000),
                Vector3Int.zero);
        }

        static void CreateSimpleProjectionPrefab(
            string path,
            string name,
            PrimitiveType primitiveType,
            Material material,
            Vector3 scale,
            string poolKey,
            List<string> createdPaths)
        {
            var root = new GameObject(name);
            try
            {
                ConfigureProjectionBinding(root, poolKey);
                GameObject visual = CreatePrimitiveVisual(
                    root.transform,
                    "Visual",
                    primitiveType,
                    material,
                    scale);
                visual.transform.localPosition = new Vector3(0f, scale.y, 0f);
                SavePrefab(root, path, createdPaths);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void CreateEnemyPrefab(
            Content content,
            Material fallbackMaterial,
            List<string> createdPaths)
        {
            GameObject source = LoadRequired<GameObject>(LegacyEnemyPrefabPath);
            Transform sourceVisual = FindChild(source.transform, "Spine");
            if (sourceVisual == null)
                throw new InvalidOperationException("BugBrownSmall prefab has no Spine visual child.");

            var root = new GameObject("BugBrownSmall");
            try
            {
                ConfigureProjectionBinding(root, EnemyPoolKey);
                GameObject visual = UnityEngine.Object.Instantiate(sourceVisual.gameObject, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = Vector3.one * 0.8f;
                StripNonSpineBehaviours(visual);
                if (visual.GetComponentInChildren<Renderer>(true) == null)
                {
                    UnityEngine.Object.DestroyImmediate(visual);
                    visual = CreatePrimitiveVisual(
                        root.transform,
                        "Visual",
                        PrimitiveType.Capsule,
                        fallbackMaterial,
                        new Vector3(0.7f, 0.7f, 0.7f));
                    visual.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                }

                PrefabMarkerCollectorController provider =
                    root.AddComponent<PrefabMarkerCollectorController>();
                ConfigureProviderBase(
                    provider,
                    "enemy-drop-position",
                    content.DropPosition,
                    SpatialMarkerReusePolicyType.ReuseAllowed);
                SetField(provider, "refreshPolicyType", SpatialMarkerRefreshPolicyType.OnUse);
                SetField(
                    provider,
                    "coordinateSourceType",
                    SpatialMarkerCoordinateSourceType.EntitySpatialPose);
                CreateMarkerSocket(
                    root.transform,
                    "DropPosition",
                    new Vector3Int(0, 0, 0),
                    new List<TaxonomyTermData> { content.DropPosition });

                SavePrefab(root, EnemyPrefabPath, createdPaths);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void CreateExperienceDropPrefab(
            Material material,
            List<string> createdPaths)
        {
            var root = new GameObject("ExperienceDrop");
            try
            {
                ConfigureProjectionBinding(root, ExperienceDropPoolKey);
                root.AddComponent<ProjectionMovementController>();
                GameObject visual = CreatePrimitiveVisual(
                    root.transform,
                    "Visual",
                    PrimitiveType.Sphere,
                    material,
                    Vector3.one * 0.38f);
                visual.transform.localPosition = new Vector3(0f, 0.4f, 0f);

                SavePrefab(root, ExperienceDropPrefabPath, createdPaths);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void CreateExperienceCollectorPrefab(
            Content content,
            List<string> createdPaths)
        {
            var root = new GameObject("ExperienceCollector");
            try
            {
                ConfigureProjectionBinding(root, ExperienceCollectorPoolKey);
                SkillTargetProjectionController transition =
                    root.AddComponent<SkillTargetProjectionController>();
                SetField(transition, "skill", content.CollectionSkill);
                SetField(
                    transition,
                    "curve",
                    AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
                SavePrefab(root, ExperienceCollectorPrefabPath, createdPaths);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static GameObject CreatePrimitiveVisual(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Material material,
            Vector3 scale)
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = scale;
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            return visual;
        }

        static void ConfigureProviderBase(
            SpatialMarkerProviderController provider,
            string providerId,
            TaxonomyTermData providerType,
            SpatialMarkerReusePolicyType reusePolicyType)
        {
            SetField(provider, "providerId", providerId);
            SetField(provider, "providerType", providerType);
            SetField(
                provider,
                "usagePolicy",
                new SpatialMarkerUsagePolicyData(
                    SpatialMarkerSelectionType.Next,
                    reusePolicyType));
        }

        static void CreateMarkerSocket(
            Transform parent,
            string name,
            Vector3Int topologyCoordinates,
            List<TaxonomyTermData> tags)
        {
            var socketObject = new GameObject(name);
            socketObject.transform.SetParent(parent, false);
            SpatialMarkerSocket socket = socketObject.AddComponent<SpatialMarkerSocket>();
            SetField(socket, "tags", tags ?? new List<TaxonomyTermData>(0));
            SetField(socket, "topologyCoordinates", topologyCoordinates);
        }

        static void SavePrefab(GameObject root, string path, List<string> createdPaths)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null)
                throw new InvalidOperationException(string.Concat("Failed to create prefab: ", path));
            createdPaths.Add(path);
            ConfigureAddressable(path, AddressablesGroup);
        }

        static void ConfigureProjectionBinding(GameObject root, string poolKey)
        {
            if (root == null || string.IsNullOrWhiteSpace(poolKey))
                throw new InvalidOperationException("Projection binding requires a root and a pool key.");

            ProjectionBindingController binding = root.AddComponent<ProjectionBindingController>();
            SetField(binding, "poolKey", poolKey.Trim());
        }

        static void SetProjectionPoolKey(string prefabPath, string poolKey)
        {
            if (string.IsNullOrWhiteSpace(poolKey))
                throw new InvalidOperationException("Projection pool key is required.");

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                throw new InvalidOperationException(string.Concat(
                    "Could not load projection prefab contents: ", prefabPath));

            try
            {
                ProjectionBindingController binding =
                    root.GetComponent<ProjectionBindingController>();
                if (binding == null)
                    throw new InvalidOperationException(string.Concat(
                        "Projection prefab has no root ProjectionBindingController: ", prefabPath));

                SetField(binding, "poolKey", poolKey.Trim());
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ConfigureSpaceNavigationSurface(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                throw new InvalidOperationException(string.Concat(
                    "Could not load activity space prefab contents: ", prefabPath));

            try
            {
                Transform floor = root.transform.Find("Floor");
                if (floor == null || floor.GetComponent<MeshFilter>() == null)
                {
                    throw new InvalidOperationException(string.Concat(
                        "Activity space prefab has no mesh Floor: ", prefabPath));
                }

                if (floor.GetComponent<NavigationSurfaceController>() == null)
                    floor.gameObject.AddComponent<NavigationSurfaceController>();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetProjection(CapabilityHostData host, string prefabPath)
        {
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException(string.Concat("Projection prefab GUID is missing: ", prefabPath));
            SetField(host, "projectionPrefabReference", new ProjectionPrefabReference(guid));
            EditorUtility.SetDirty(host);
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        static void StripNonSpineBehaviours(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;
                string typeNamespace = behaviour.GetType().Namespace;
                if (!string.IsNullOrWhiteSpace(typeNamespace)
                    && typeNamespace.StartsWith("Spine", StringComparison.Ordinal))
                {
                    continue;
                }
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }

        static void CreateExperienceUIPrefab(
            Content content,
            List<string> createdPaths)
        {
            var root = new GameObject("ExperienceUI");
            try
            {
                RectTransform rootRect = root.AddComponent<RectTransform>();
                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                root.AddComponent<GraphicRaycaster>();
                UIProjectionContextController projectionContext =
                    root.AddComponent<UIProjectionContextController>();
                SetField(
                    projectionContext,
                    "activitySelector",
                    CreateIntegrationActivitySelector(content));
                SetField(projectionContext, "canvas", canvas);

                RectTransform panel = CreateUIRect(
                    rootRect,
                    "ExperiencePanel",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(24f, -24f),
                    new Vector2(280f, 82f));
                Image panelImage = panel.gameObject.AddComponent<Image>();
                panelImage.color = new Color(0.04f, 0.06f, 0.08f, 0.92f);

                RectTransform titleRect = CreateUIRect(
                    panel,
                    "Title",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(14f, -10f),
                    new Vector2(120f, 22f));
                TMP_Text title = titleRect.gameObject.AddComponent<TextMeshProUGUI>();
                title.text = "EXPERIENCE";
                title.fontSize = 15f;
                title.color = Color.white;
                title.alignment = TextAlignmentOptions.Left;

                RectTransform valueRect = CreateUIRect(
                    panel,
                    "Value",
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-14f, -10f),
                    new Vector2(100f, 22f));
                TMP_Text value = valueRect.gameObject.AddComponent<TextMeshProUGUI>();
                value.text = "0 / 6";
                value.fontSize = 15f;
                value.color = new Color(0.65f, 1f, 0.72f, 1f);
                value.alignment = TextAlignmentOptions.Right;

                RectTransform sliderRect = CreateUIRect(
                    panel,
                    "Progress",
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(14f, 13f),
                    new Vector2(-28f, 24f));
                Slider slider = sliderRect.gameObject.AddComponent<Slider>();
                slider.interactable = false;
                slider.transition = Selectable.Transition.None;

                RectTransform background = CreateStretchedUIRect(sliderRect, "Background", 0f);
                Image backgroundImage = background.gameObject.AddComponent<Image>();
                backgroundImage.color = new Color(0.12f, 0.16f, 0.2f, 1f);
                RectTransform fillArea = CreateStretchedUIRect(sliderRect, "Fill Area", 3f);
                RectTransform fill = CreateStretchedUIRect(fillArea, "Fill", 0f);
                Image fillImage = fill.gameObject.AddComponent<Image>();
                fillImage.color = new Color(0.25f, 0.9f, 0.4f, 1f);
                slider.fillRect = fill;
                slider.targetGraphic = fillImage;
                slider.direction = Slider.Direction.LeftToRight;
                UIProjectionTargetController projectionTarget =
                    sliderRect.gameObject.AddComponent<UIProjectionTargetController>();
                SetField(
                    projectionTarget,
                    "targetTags",
                    new List<TaxonomyTermData> { content.ExperienceProgressTarget });
                SetField(projectionTarget, "target", sliderRect);

                ExperienceUIController controller = root.AddComponent<ExperienceUIController>();
                SetField(controller, "playerSpawnerDefinition", content.PlayerSpawner);
                SetField(controller, "experience", content.Experience);
                SetField(controller, "turnTokenRecipe", content.ExperienceRecipe);
                SetField(controller, "progressBar", slider);
                SetField(controller, "valueLabel", value);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExperienceUIPrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("Failed to create ExperienceUI prefab.");
                createdPaths.Add(ExperienceUIPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static ActivityRuntimeSelectorData CreateIntegrationActivitySelector(Content content)
        {
            return new ActivityRuntimeSelectorData(
                LoadRequired<ActivityData>(ActivityPath),
                new List<TaxonomyTermData> { content.IntegrationRuntimeTag });
        }

        static RectTransform CreateUIRect(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var gameObject = new GameObject(name);
            RectTransform rect = gameObject.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(
                Mathf.Approximately(anchorMin.x, 1f) ? 1f : 0f,
                Mathf.Approximately(anchorMin.y, 1f) ? 1f : 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        static RectTransform CreateStretchedUIRect(
            RectTransform parent,
            string name,
            float inset)
        {
            var gameObject = new GameObject(name);
            RectTransform rect = gameObject.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        static void ConfigureSpacePrefab(Content content)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SpacePrefabPath);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                PrefabMarkerCollectorController existing =
                    root.GetComponent<PrefabMarkerCollectorController>();
                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing);

                Material floorMaterial = LoadRequired<Material>(NeutralMaterialPath);
                GameObject floor = CreatePrimitiveVisual(
                    root.transform,
                    "Floor",
                    PrimitiveType.Cube,
                    floorMaterial,
                    new Vector3(14f, 0.1f, 10f));
                floor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                floor.AddComponent<NavigationSurfaceController>();

                PrefabMarkerCollectorController provider =
                    root.AddComponent<PrefabMarkerCollectorController>();
                ConfigureProviderBase(
                    provider,
                    "autobattle-space-anchors",
                    null,
                    SpatialMarkerReusePolicyType.ExhaustBeforeReuse);
                SetField(provider, "refreshPolicyType", SpatialMarkerRefreshPolicyType.OnBind);
                SetField(
                    provider,
                    "coordinateSourceType",
                    SpatialMarkerCoordinateSourceType.TopologyCoordinates);
                CreateMarkerSocket(
                    root.transform,
                    "PlayerSpawnerAnchor",
                    new Vector3Int(-6000, 0, 0),
                    new List<TaxonomyTermData> { content.PlayerAnchor });
                CreateMarkerSocket(
                    root.transform,
                    "EnemySpawnerAnchor",
                    new Vector3Int(6000, 0, 0),
                    new List<TaxonomyTermData> { content.EnemyAnchor });

                PrefabUtility.SaveAsPrefabAsset(root, SpacePrefabPath);
                ConfigureAddressable(SpacePrefabPath, AddressablesGroup);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static EconomyWalletData CreateWallet(
            string path,
            string name,
            string id,
            TaxonomyTermData tag,
            List<string> createdPaths)
        {
            EconomyWalletData wallet = CreateAsset<EconomyWalletData>(path, name, createdPaths);
            SetField(wallet, "id", id);
            SetField(
                wallet,
                "tags",
                tag == null
                    ? new List<TaxonomyTermData>(0)
                    : new List<TaxonomyTermData> { tag });
            SetField(wallet, "aggregationRules", new List<EconomyAggregationRuleData>(0));
            EditorUtility.SetDirty(wallet);
            return wallet;
        }

        static WalletEntry CreateWalletEntry(
            EconomyWalletData wallet,
            params SeedEntry[] seed)
        {
            return new WalletEntry(
                wallet,
                seed == null ? new List<SeedEntry>(0) : new List<SeedEntry>(seed));
        }

        static TaxonomyFamilyData CreateFamily(
            string path,
            string name,
            string id,
            string displayName,
            List<string> createdPaths)
        {
            TaxonomyFamilyData family = CreateAsset<TaxonomyFamilyData>(path, name, createdPaths);
            SetField(family, "id", id);
            SetField(family, "displayName", displayName);
            SetField(family, "cardinality", TaxonomyCardinality.Multiple);
            EditorUtility.SetDirty(family);
            return family;
        }

        static TaxonomyTermData CreateTerm(
            string path,
            string name,
            string id,
            string displayName,
            TaxonomyFamilyData family,
            int sortOrder,
            Content content,
            List<string> createdPaths)
        {
            TaxonomyTermData term = CreateAsset<TaxonomyTermData>(path, name, createdPaths);
            SetField(term, "id", id);
            SetField(term, "displayName", displayName);
            SetField(term, "family", family);
            SetField(term, "sortOrder", sortOrder);
            EditorUtility.SetDirty(term);
            content.Terms.Add(term);
            return term;
        }

        static TaxonomyTermData CreateOccupancyTerm(
            string id,
            int sortOrder,
            TaxonomyFamilyData family,
            string path,
            List<string> createdPaths)
        {
            TaxonomyTermData term = CreateAsset<TaxonomyTermData>(path, id, createdPaths);
            SetField(term, "id", id);
            SetField(term, "displayName", id);
            SetField(term, "family", family);
            SetField(term, "sortOrder", sortOrder);
            EditorUtility.SetDirty(term);
            return term;
        }

        static void EnsureOccupancyInstallerState(
            TaxonomyRuntimeInstallerData taxonomyInstaller,
            GameplayFoundationInstallerData foundationInstaller)
        {
            TaxonomyFamilyData[] families =
                GetField<TaxonomyFamilyData[]>(taxonomyInstaller, "families")
                ?? new TaxonomyFamilyData[0];
            for (int i = 0; i < families.Length; i++)
            {
                TaxonomyFamilyData family = families[i];
                if (family != null && family.Id == "SpatialOccupancy")
                {
                    throw new InvalidOperationException(
                        "SpatialOccupancy taxonomy family is already registered.");
                }
            }

            var ids = new HashSet<string>(StringComparer.Ordinal)
            {
                "MobileSolid",
                "StaticSolid",
                "PlacementObstacle",
                "NonOccupying"
            };
            TaxonomyTermData[] terms =
                GetField<TaxonomyTermData[]>(taxonomyInstaller, "terms")
                ?? new TaxonomyTermData[0];
            for (int i = 0; i < terms.Length; i++)
            {
                TaxonomyTermData term = terms[i];
                if (term != null && ids.Contains(term.Id))
                {
                    throw new InvalidOperationException(
                        $"Spatial occupancy taxonomy term '{term.Id}' is already registered.");
                }
            }

            if (GetField<SpatialOccupancyMatrixData>(
                    foundationInstaller,
                    "spatialOccupancyMatrix") != null)
            {
                throw new InvalidOperationException(
                    "Gameplay foundation already references a spatial occupancy matrix.");
            }
        }

        static void EnsureOccupancyIdentityAvailable()
        {
            string[] familyGuids = AssetDatabase.FindAssets("t:TaxonomyFamilyData");
            for (int i = 0; i < familyGuids.Length; i++)
            {
                TaxonomyFamilyData family = AssetDatabase.LoadAssetAtPath<TaxonomyFamilyData>(
                    AssetDatabase.GUIDToAssetPath(familyGuids[i]));
                if (family != null && family.Id == "SpatialOccupancy")
                {
                    throw new InvalidOperationException(
                        "Taxonomy family id 'SpatialOccupancy' already exists.");
                }
            }

            var ids = new HashSet<string>(StringComparer.Ordinal)
            {
                "MobileSolid",
                "StaticSolid",
                "PlacementObstacle",
                "NonOccupying"
            };
            string[] termGuids = AssetDatabase.FindAssets("t:TaxonomyTermData");
            for (int i = 0; i < termGuids.Length; i++)
            {
                TaxonomyTermData term = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(
                    AssetDatabase.GUIDToAssetPath(termGuids[i]));
                if (term != null && ids.Contains(term.Id))
                    throw new InvalidOperationException($"Taxonomy term id '{term.Id}' already exists.");
            }
        }

        static void EnsureOccupancyHostPlanMatchesAssets()
        {
            var expected = new HashSet<string>(StringComparer.Ordinal);
            AddHostPaths(expected, MobileSolidHostPaths);
            AddHostPaths(expected, StaticSolidHostPaths);
            AddHostPaths(expected, PlacementObstacleHostPaths);
            AddHostPaths(expected, NonOccupyingHostPaths);

            var actual = new HashSet<string>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[]
            {
                "Assets/Game/Activities"
            });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.LoadAssetAtPath<CapabilityHostBaseData>(path) != null)
                    actual.Add(path);
            }

            if (actual.SetEquals(expected))
                return;

            var missing = new List<string>(expected);
            missing.RemoveAll(actual.Contains);
            var unexpected = new List<string>(actual);
            unexpected.RemoveAll(expected.Contains);
            throw new InvalidOperationException(
                "Spatial occupancy host plan does not match ChainRush Activity assets. " +
                $"Missing=[{string.Join(", ", missing)}] Unexpected=[{string.Join(", ", unexpected)}].");
        }

        static Dictionary<CapabilityHostBaseData, List<TaxonomyTermData>> CaptureOccupancyHostTags()
        {
            var result = new Dictionary<CapabilityHostBaseData, List<TaxonomyTermData>>();
            CaptureHostTags(result, MobileSolidHostPaths);
            CaptureHostTags(result, StaticSolidHostPaths);
            CaptureHostTags(result, PlacementObstacleHostPaths);
            CaptureHostTags(result, NonOccupyingHostPaths);
            return result;
        }

        static void CaptureHostTags(
            Dictionary<CapabilityHostBaseData, List<TaxonomyTermData>> destination,
            List<string> paths)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                CapabilityHostBaseData host = LoadRequired<CapabilityHostBaseData>(paths[i]);
                destination.Add(host, new List<TaxonomyTermData>(host.Tags));
            }
        }

        static void AssignOccupancyTag(List<string> paths, TaxonomyTermData tag)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                CapabilityHostBaseData host = LoadRequired<CapabilityHostBaseData>(paths[i]);
                var tags = new List<TaxonomyTermData>(host.Tags) { tag };
                SetField(host, "tags", tags);
                EditorUtility.SetDirty(host);
            }
        }

        static void RestoreOccupancyHostTags(
            Dictionary<CapabilityHostBaseData, List<TaxonomyTermData>> originalTags)
        {
            foreach (KeyValuePair<CapabilityHostBaseData, List<TaxonomyTermData>> pair in originalTags)
            {
                SetField(pair.Key, "tags", pair.Value);
                EditorUtility.SetDirty(pair.Key);
            }
        }

        static void ValidateOccupancyAuthoring(
            TaxonomyFamilyData family,
            TaxonomyTermData mobileSolid,
            TaxonomyTermData staticSolid,
            TaxonomyTermData placementObstacle,
            TaxonomyTermData nonOccupying,
            SpatialOccupancyMatrixData matrix)
        {
            if (family == null || family.Id != "SpatialOccupancy" ||
                family.Cardinality != TaxonomyCardinality.Multiple)
            {
                throw new InvalidOperationException("Spatial occupancy family is invalid.");
            }

            ValidateOccupancyTerm(mobileSolid, family, "MobileSolid");
            ValidateOccupancyTerm(staticSolid, family, "StaticSolid");
            ValidateOccupancyTerm(placementObstacle, family, "PlacementObstacle");
            ValidateOccupancyTerm(nonOccupying, family, "NonOccupying");

            if (matrix == null || matrix.OccupancyFamily != family || matrix.Rows.Count != 4)
                throw new InvalidOperationException("Spatial occupancy matrix is invalid.");
            ValidateOccupancyRow(matrix.Rows[0], mobileSolid, mobileSolid, staticSolid);
            ValidateOccupancyRow(
                matrix.Rows[1],
                staticSolid,
                mobileSolid,
                staticSolid,
                placementObstacle);
            ValidateOccupancyRow(
                matrix.Rows[2],
                placementObstacle,
                staticSolid,
                placementObstacle);
            ValidateOccupancyRow(matrix.Rows[3], nonOccupying);

            TaxonomyRuntimeInstallerData taxonomyInstaller =
                LoadRequired<TaxonomyRuntimeInstallerData>(TaxonomyInstallerPath);
            RequireSingleReference(
                GetField<TaxonomyFamilyData[]>(taxonomyInstaller, "families"),
                family);
            TaxonomyTermData[] terms = GetField<TaxonomyTermData[]>(taxonomyInstaller, "terms");
            RequireSingleReference(terms, mobileSolid);
            RequireSingleReference(terms, staticSolid);
            RequireSingleReference(terms, placementObstacle);
            RequireSingleReference(terms, nonOccupying);

            GameplayFoundationInstallerData foundationInstaller =
                LoadRequired<GameplayFoundationInstallerData>(FoundationInstallerPath);
            if (GetField<SpatialOccupancyMatrixData>(
                    foundationInstaller,
                    "spatialOccupancyMatrix") != matrix)
            {
                throw new InvalidOperationException(
                    "Gameplay foundation has an invalid spatial occupancy matrix reference.");
            }

            ValidateOccupancyHostTags(MobileSolidHostPaths, mobileSolid, family);
            ValidateOccupancyHostTags(StaticSolidHostPaths, staticSolid, family);
            ValidateOccupancyHostTags(PlacementObstacleHostPaths, placementObstacle, family);
            ValidateOccupancyHostTags(NonOccupyingHostPaths, nonOccupying, family);
            EnsureOccupancyHostPlanMatchesAssets();
        }

        static void ValidateOccupancyTerm(
            TaxonomyTermData term,
            TaxonomyFamilyData family,
            string expectedId)
        {
            if (term == null || term.Family != family || term.Id != expectedId)
                throw new InvalidOperationException($"Spatial occupancy term '{expectedId}' is invalid.");
        }

        static void ValidateOccupancyRow(
            SpatialOccupancyMatrixRowData row,
            TaxonomyTermData tag,
            params TaxonomyTermData[] blockedTags)
        {
            if (row == null || row.Tag != tag || row.BlockedTags.Count != blockedTags.Length)
                throw new InvalidOperationException($"Spatial occupancy row '{tag.name}' is invalid.");
            for (int i = 0; i < blockedTags.Length; i++)
            {
                if (row.BlockedTags[i] != blockedTags[i])
                    throw new InvalidOperationException($"Spatial occupancy row '{tag.name}' is invalid.");
            }
        }

        static void ValidateOccupancyHostTags(
            List<string> paths,
            TaxonomyTermData expectedTag,
            TaxonomyFamilyData family)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                CapabilityHostBaseData host = LoadRequired<CapabilityHostBaseData>(paths[i]);
                int occupancyTagCount = 0;
                TaxonomyTermData actualTag = null;
                for (int tagIndex = 0; tagIndex < host.Tags.Count; tagIndex++)
                {
                    TaxonomyTermData tag = host.Tags[tagIndex];
                    if (tag == null || tag.Family != family)
                        continue;
                    occupancyTagCount++;
                    actualTag = tag;
                }

                if (occupancyTagCount != 1 || actualTag != expectedTag)
                {
                    throw new InvalidOperationException(
                        $"CapabilityHost '{paths[i]}' must have exactly one '{expectedTag.Id}' occupancy tag.");
                }
            }
        }

        static void EnsureOccupancyConsumerWiring()
        {
            CapabilityHostData waterBase = LoadRequired<CapabilityHostData>(BoardWaterBasePath);
            ObjectiveConditionMaterializedMarkerCoverage condition =
                ResolveBoardPopulationMarkerCondition();
            if (condition.EconomyAsset == waterBase)
                return;
            if (condition.EconomyAsset != null)
            {
                throw new InvalidOperationException(
                    "Board Population marker condition references an unexpected occupancy candidate asset.");
            }

            try
            {
                SetField(condition, "economyAsset", waterBase);
                EditorUtility.SetDirty(
                    LoadRequired<ObjectiveTemplateData>(BoardPopulationObjectivePath));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ValidateOccupancyConsumerWiring();
            }
            catch
            {
                SetField<CapabilityHostBaseData>(condition, "economyAsset", null);
                EditorUtility.SetDirty(
                    LoadRequired<ObjectiveTemplateData>(BoardPopulationObjectivePath));
                AssetDatabase.SaveAssets();
                throw;
            }
        }

        static void ValidateOccupancyConsumerWiring()
        {
            CapabilityHostData waterBase = LoadRequired<CapabilityHostData>(BoardWaterBasePath);
            ObjectiveConditionMaterializedMarkerCoverage condition =
                ResolveBoardPopulationMarkerCondition();
            if (condition.EconomyAsset != waterBase)
            {
                throw new InvalidOperationException(
                    "Board Population marker condition must use WaterBoardBase as its occupancy candidate.");
            }
        }

        static ObjectiveConditionMaterializedMarkerCoverage ResolveBoardPopulationMarkerCondition()
        {
            ObjectiveTemplateData objective =
                LoadRequired<ObjectiveTemplateData>(BoardPopulationObjectivePath);
            if (objective.Root == null || objective.Root.SuccessConditions.Count != 1
                || !(objective.Root.SuccessConditions[0] is ObjectiveConditionMaterializedMarkerCoverage condition))
            {
                throw new InvalidOperationException(
                    "Board Population Objective does not contain the expected materialized marker coverage success condition.");
            }

            return condition;
        }

        static void RequireSingleReference<T>(T[] references, T expected)
            where T : UnityEngine.Object
        {
            int count = 0;
            for (int i = 0; references != null && i < references.Length; i++)
            {
                if (references[i] == expected)
                    count++;
            }
            if (count != 1)
            {
                throw new InvalidOperationException(
                    $"Installer must contain exactly one reference to '{expected.name}', but contains {count}.");
            }
        }

        static int CountExisting(params UnityEngine.Object[] assets)
        {
            int count = 0;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] != null)
                    count++;
            }
            return count;
        }

        static void AddHostPaths(HashSet<string> destination, List<string> paths)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                if (!destination.Add(paths[i]))
                    throw new InvalidOperationException($"Duplicate occupancy host path '{paths[i]}'.");
            }
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

        static void ConfigureAddressable(string path, string groupName)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException("Addressable Asset Settings are not configured.");
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
                throw new InvalidOperationException(string.Concat("Addressables group is missing: ", groupName));
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException(string.Concat("Addressable asset is missing: ", path));
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = path;
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
        }

        static void EnsureExpectedExistingState(
            ActivityData activity,
            ActivityData boardActivity,
            ProductionRecipeData boardMergeRecipe,
            Content content)
        {
            if (activity.Teams.Count != 2
                || activity.Teams[0].Objectives.Count != 0
                || activity.Teams[1].Objectives.Count != 0
                || activity.Teams[0].Features.Count != 0
                || activity.Teams[1].Features.Count != 0
                || activity.Teams[0].Wallets.Count != 1
                || activity.Teams[1].Wallets.Count != 1
                || activity.Teams[0].Wallets[0].Wallet != content.SharedWallet
                || activity.Teams[1].Wallets[0].Wallet != content.SharedWallet
                || activity.WorldWallets.Count != 0)
            {
                throw new InvalidOperationException(
                    "AutobattleActivity is not in the exact pre-wiring state.");
            }

            if (content.WaterUnit.Capabilities.Count != 0
                || content.WaterUnit.WalletEntries.Count != 0)
            {
                throw new InvalidOperationException(
                    "WaterUnit is not in the exact pre-wiring state.");
            }

            if (boardActivity.Teams.Count != 1
                || !HasSeed(
                    boardActivity.Teams[0],
                    content.SharedWallet,
                    content.TurnToken,
                    EconomyFormType.Stack,
                    1L))
            {
                throw new InvalidOperationException(
                    "BoardActivity does not contain the expected initial BoardTurnToken seed.");
            }

            if (boardMergeRecipe.Outputs.Count != 1
                || boardMergeRecipe.Outputs[0].Asset != content.WaterUnit
                || boardMergeRecipe.Outputs[0].FormType != EconomyFormType.Stack)
            {
                throw new InvalidOperationException(
                    "BoardMergeRecipe4 is not in the expected Stack-output state.");
            }
        }

        static bool HasSeed(
            ActivityTeamData team,
            EconomyWalletData wallet,
            EconomyAssetData asset,
            EconomyFormType formType,
            long amount)
        {
            for (int walletIndex = 0; walletIndex < team.Wallets.Count; walletIndex++)
            {
                ActivityTeamWalletData walletData = team.Wallets[walletIndex];
                if (walletData.Wallet != wallet)
                    continue;
                for (int seedIndex = 0; seedIndex < walletData.Seed.Count; seedIndex++)
                {
                    SeedEntry seed = walletData.Seed[seedIndex].Seed;
                    if (seed != null
                        && seed.Asset == asset
                        && seed.FormType == formType
                        && seed.Amount == amount)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        static void EnsureCreatedTargetsDoNotExist()
        {
            for (int i = 0; i < CreatedPaths.Length; i++)
            {
                if (AssetDatabase.LoadMainAssetAtPath(CreatedPaths[i]) != null)
                {
                    throw new InvalidOperationException(string.Concat(
                        "Autobattle authoring target already exists: '",
                        CreatedPaths[i],
                        "'."));
                }
            }
        }

        static void EnsureFolders()
        {
            EnsureFolder(EconomyRoot);
            EnsureFolder(AgentsRoot);
            EnsureFolder(AIRoot);
            EnsureFolder(AITaxonomyRoot);
            EnsureFolder(DropsRoot);
            EnsureFolder(HostValuesRoot);
            EnsureFolder(MovementRoot);
            EnsureFolder(ObjectivesRoot);
            EnsureFolder(OrchestrationRoot);
            EnsureFolder(OrchestrationModulesRoot);
            EnsureFolder(OrchestrationTaxonomyRoot);
            EnsureFolder(ProductionRoot);
            EnsureFolder(ProjectionRoot);
            EnsureFolder(ShapesRoot);
            EnsureFolder(SkillsRoot);
            EnsureFolder(TaxonomyRoot);
            EnsureFolder(UIRoot);
            EnsureFolder(DiplomacyRoot);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException(string.Concat("Cannot create folder: ", path));
            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException(string.Concat("Unity failed to create folder: ", path));
        }

        static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException(string.Concat("Missing required asset: ", path));
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

        static void AddUniqueValue<T>(List<T> destination, params T[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (!destination.Contains(values[i]))
                    destination.Add(values[i]);
            }
        }

        static T GetField<T>(object target, string fieldName)
        {
            return (T)FindField(target, fieldName).GetValue(target);
        }

        static void SetField<T>(object target, string fieldName, T value)
        {
            FindField(target, fieldName).SetValue(target, value);
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
