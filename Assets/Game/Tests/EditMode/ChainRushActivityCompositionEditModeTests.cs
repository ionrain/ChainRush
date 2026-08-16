using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Economy;
using Core.Activities.GameRuntime.Installers;
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
using UnityEngine;

namespace ChainRush.Tests.EditMode
{
    public sealed class ChainRushActivityCompositionEditModeTests
    {
        const string ActivitiesRoot = "Assets/Game/Activities";
        const string SharedRoot = ActivitiesRoot + "/Shared";
        const string AutobattleRoot = ActivitiesRoot + "/Autobattle";
        const string BoardRoot = ActivitiesRoot + "/Board";

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
        const string RuntimeProfilePath =
            "Assets/Game/Runtime/Host/ChainRushGameRuntimeProfile.asset";
        const string StartupPlanPath =
            "Assets/Game/Runtime/Startup/ChainRushGameStartupPlan.asset";
        const string BoardObjectivePath =
            BoardRoot + "/Objectives/BoardPopulationObjective.asset";
        const string BoardPopulationAgentPath =
            BoardRoot + "/Agents/BoardPopulationAgent.asset";
        const string BoardProductionAgentPath =
            BoardRoot + "/Agents/BoardProductionAgent.asset";
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
        const string BoardMergeRecipePath =
            BoardRoot + "/Production/BoardMergeRecipe.asset";
        const string BoardMergeSkillPath =
            BoardRoot + "/Skills/BoardMergeSkill.asset";
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

        const string AutobattleActivityTypeId = "chainrush.activity-type.autobattle";
        const string BoardActivityTypeId = "chainrush.activity-type.board";
        const string BoardActivationTermId = "chainrush.activity.activation.board";
        const string BoardCellTagId = "chainrush.board.cell";

        [Test]
        public void ActivityAssets_ReferenceOnlyTheirOwnerOrSharedAssets()
        {
            Assert.IsTrue(AssetDatabase.IsValidFolder(SharedRoot), $"Missing folder: {SharedRoot}");
            Assert.IsTrue(AssetDatabase.IsValidFolder(AutobattleRoot), $"Missing folder: {AutobattleRoot}");
            Assert.IsTrue(AssetDatabase.IsValidFolder(BoardRoot), $"Missing folder: {BoardRoot}");

            AssertFolderDependenciesStayWithinBoundary(AutobattleRoot, BoardRoot);
            AssertFolderDependenciesStayWithinBoundary(BoardRoot, AutobattleRoot);
            AssertFolderDependenciesStayWithinBoundary(SharedRoot, AutobattleRoot, BoardRoot);
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
            Assert.AreEqual(0, autobattle.Teams[0].Objectives.Count);
            Assert.AreEqual(0, autobattle.Teams[1].Objectives.Count);
            Assert.AreEqual(0, autobattle.Teams[0].Features.Count);
            Assert.AreEqual(0, autobattle.Teams[1].Features.Count);
            AssertTopology(
                autobattle.Topology,
                TopologyType.Free,
                TopologyCoordinateOccupationPolicy.MultipleOccupants);

            Assert.NotNull(board.ActivityType);
            Assert.AreEqual(BoardActivityTypeId, board.ActivityType.Id);
            Assert.AreEqual(1, board.Teams.Count);
            Assert.AreEqual(1, board.Teams[0].SlotCount);
            Assert.IsFalse(board.AllowBots);
            Assert.AreEqual(ActivityEndMode.Manual, board.Result.EndMode);
            Assert.AreEqual(1, board.Teams[0].Objectives.Count);
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
            Assert.NotNull(boardSpace.Presentation);
            Assert.AreEqual(1, boardSpace.MarkerProviders.Count);
            Assert.IsInstanceOf<SpatialGridProviderData>(boardSpace.MarkerProviders[0]);
            var boardGrid = (SpatialGridProviderData)boardSpace.MarkerProviders[0];
            Assert.AreEqual(new Vector2Int(4, 4), boardGrid.Size);
            Assert.AreEqual(Vector2Int.zero, boardGrid.Origin);
            Assert.AreEqual(1, boardGrid.MarkerTags.Count);
            Assert.AreEqual(BoardCellTagId, boardGrid.MarkerTags[0].Id);
            Assert.AreEqual(1, boardSpace.ProjectionMarkerTags.Count);
            Assert.AreSame(boardGrid.MarkerTags[0], boardSpace.ProjectionMarkerTags[0]);
            Assert.NotNull(boardSpace.ProjectionSettings);
            Assert.IsTrue(boardSpace.ProjectionSettings.IsValid);
        }

        [Test]
        public void BoardVerticalSlice_WiresObjectivePopulationAndMergeProduction()
        {
            ActivityData board = LoadRequiredAsset<ActivityData>(BoardActivityPath);
            ObjectiveTemplateData objective =
                LoadRequiredAsset<ObjectiveTemplateData>(BoardObjectivePath);
            ActivityAgentDefinitionData populationAgent =
                LoadRequiredAsset<ActivityAgentDefinitionData>(BoardPopulationAgentPath);
            ActivityAgentDefinitionData productionAgent =
                LoadRequiredAsset<ActivityAgentDefinitionData>(BoardProductionAgentPath);
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
            ProductionRecipeData refreshRecipe =
                LoadRequiredAsset<ProductionRecipeData>(BoardRefreshRecipePath);
            ProductionRecipeData waterBaseRecipe =
                LoadRequiredAsset<ProductionRecipeData>(BoardWaterBaseRecipePath);
            ProductionData populationProduction =
                LoadRequiredAsset<ProductionData>(BoardPopulationProductionPath);
            ProductionData mergeProduction =
                LoadRequiredAsset<ProductionData>(BoardMergeProductionPath);
            ProductionRecipeData mergeRecipe =
                LoadRequiredAsset<ProductionRecipeData>(BoardMergeRecipePath);
            SkillData mergeSkill = LoadRequiredAsset<SkillData>(BoardMergeSkillPath);
            ActivityOrchestrationConfigData orchestration =
                LoadRequiredAsset<ActivityOrchestrationConfigData>(BoardOrchestrationPath);
            TaxonomyTermData waterTag = waterBase.Tags.Single();
            TaxonomyRuntimeInstallerData taxonomyInstaller =
                LoadRequiredAsset<TaxonomyRuntimeInstallerData>(
                    "Assets/Game/Runtime/Installers/ChainRushTaxonomyRuntimeInstaller.asset");

            Assert.AreSame(objective, board.Teams[0].Objectives.Single().Template);
            Assert.AreEqual(ObjectiveCompletionPolicyType.Reset, objective.CompletionPolicyType);
            Assert.AreSame(orchestration, board.Teams[0].Features.Single());
            CollectionAssert.Contains(
                ReadObjectReferences<TaxonomyTermData>(taxonomyInstaller, "terms"),
                waterTag,
                "The taxonomy installer must register the Water Board item term before economy queries run.");

            Assert.AreEqual(1, objective.Root.ActivateConditions.Count);
            var activation = objective.Root.ActivateConditions.Single()
                as ObjectiveConditionEconomyMetric;
            Assert.NotNull(activation);
            Assert.AreSame(turnToken, activation.Asset);
            Assert.AreEqual(EconomyFormType.Stack, activation.FormType);
            Assert.AreEqual(CompareOperation.GreaterOrEqual, activation.CompareOperation);
            Assert.AreEqual(1L, activation.TargetValue);
            Assert.AreEqual(1, activation.WalletTags.Count);
            Assert.AreSame(sharedWalletTag, activation.WalletTags[0]);

            Assert.AreEqual(1, objective.Root.SuccessConditions.Count);
            var success = objective.Root.SuccessConditions.Single()
                as ObjectiveConditionMarkerAvailability;
            Assert.NotNull(success);
            Assert.IsNull(success.EconomyAsset);
            Assert.AreEqual(EconomyFormType.Token, success.EconomyFormType);
            Assert.AreEqual(CompareOperation.Equal, success.CompareOperation);
            Assert.AreEqual(0L, success.TargetValue);
            Assert.AreEqual(BoardCellTagId, success.MarkerTags.Single().Id);

            Assert.AreEqual(3, orchestration.Modules.Count);
            Assert.IsInstanceOf<EconomyStateOrchestrationModuleData>(orchestration.Modules[0]);
            Assert.IsInstanceOf<ProductionStateOrchestrationModuleData>(orchestration.Modules[1]);
            Assert.IsInstanceOf<ProjectionStateOrchestrationModuleData>(orchestration.Modules[2]);

            Assert.IsInstanceOf<PopulationActivityOrchestrationAgentData>(populationAgent.Agent);
            Assert.AreEqual(
                ObjectiveCommandFailurePolicyType.FailObjective,
                populationAgent.CommandFailurePolicyType);
            Assert.AreEqual(
                ObjectiveCommandFailurePolicyType.Replan,
                productionAgent.CommandFailurePolicyType);
            var population = (PopulationActivityOrchestrationAgentData)populationAgent.Agent;
            Assert.NotNull(population.Planner);
            Assert.AreSame(refreshRecipe, population.CompletionRecipe);
            Assert.AreEqual(BoardCellTagId, population.MarkerTags.Single().Id);
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
                refreshRecipe.Inputs[0].Operation,
                turnToken,
                EconomyFormType.Stack,
                1L,
                sharedWalletTag);
            Assert.AreEqual(0, refreshRecipe.Outputs.Count);

            Assert.AreEqual(0, waterBaseRecipe.Inputs.Count);
            Assert.AreEqual(1, waterBaseRecipe.Outputs.Count);
            AssertEconomyOutput(
                waterBaseRecipe.Outputs[0].Output,
                waterBase,
                EconomyFormType.Token,
                1L,
                boardWalletTag);
            Assert.AreEqual(BoardCellTagId, populationProduction.MaterializationProviderType.Id);

            Assert.AreEqual(3, mergeRecipe.Inputs.Count);
            for (int inputIndex = 0; inputIndex < mergeRecipe.Inputs.Count; inputIndex++)
            {
                AssertEconomyOperation(
                    mergeRecipe.Inputs[inputIndex].Operation,
                    waterBase,
                    EconomyFormType.Token,
                    1L,
                    boardWalletTag);
            }

            Assert.AreEqual(1, mergeRecipe.Outputs.Count);
            AssertEconomyOutput(
                mergeRecipe.Outputs[0].Output,
                waterUnit,
                EconomyFormType.Token,
                1L,
                sharedWalletTag);
            Assert.IsNull(mergeProduction.MaterializationProviderType);

            Assert.AreEqual(SkillTargetType.Entities, mergeSkill.TargetType);
            Assert.AreEqual(3, mergeSkill.TargetCount.Min);
            Assert.AreEqual(3, mergeSkill.TargetCount.Max);
            var productionEffect = mergeSkill.Effects.OfType<SkillProductionEffectData>().Single();
            Assert.AreSame(mergeRecipe, productionEffect.Recipe);
            Assert.AreEqual(3, productionEffect.InputMappings.Count);
            for (int mappingIndex = 0; mappingIndex < productionEffect.InputMappings.Count; mappingIndex++)
            {
                Assert.AreEqual(mappingIndex, productionEffect.InputMappings[mappingIndex].RecipeInputIndex);
                Assert.AreEqual(mappingIndex, productionEffect.InputMappings[mappingIndex].TargetIndex);
            }

            Assert.IsEmpty(waterUnit.Capabilities);
            Assert.IsFalse(waterUnit.ProjectionPrefabReference.RuntimeKeyIsValid());
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
                input.Operation,
                experience,
                EconomyFormType.Stack,
                1L,
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
                recipe.Outputs[0].Output,
                turnToken,
                EconomyFormType.Stack,
                1L,
                sharedWalletTag);
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
            Assert.IsNull(boardContainer.Steps[1].Executor);
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
            };

            CollectionAssert.AreEqual(
                expectedInstallerTypes,
                profile.Installers.Select(installer => installer.GetType()).ToList());
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
        public void BoardUI_MatchingControlRejection_UnlocksAndClearsSelection()
        {
            GameObject instance = InstantiateBoardUI(out MonoBehaviour controller);
            try
            {
                SkillData skill = LoadRequiredAsset<SkillData>(BoardMergeSkillPath);
                var activityId = new ActivityId(31);
                var hostEntityId = new Core.Entities.EntityId(47);
                SimulationControlIntentEvent request = SimulationControlIntentEvent.ActivateSkillEntities(
                    activityId,
                    hostEntityId,
                    skill,
                    new List<Core.Entities.EntityId>());
                PrepareBoardControlState(controller, activityId, hostEntityId, skill, request.RequestId);

                InvokeControlResult(controller, new SimulationControlResultEvent(
                    request.RequestId,
                    activityId,
                    hostEntityId,
                    skill,
                    SimulationControlResultType.Rejected,
                    SimulationErrorCode.InvalidIntent,
                    "Rejected by test setup.",
                    SkillExecutionStatus.Failed,
                    SkillExecutionRef.Invalid));

                Assert.IsFalse(ReadField<bool>(controller, "_selectionLocked"));
                Assert.IsFalse(ReadField<bool>(controller, "_awaitingBoardRefresh"));
                Assert.IsFalse(ReadField<bool>(controller, "_isSelecting"));
                Assert.IsFalse(ReadField<SimulationControlRequestId>(controller, "_pendingRequestId").IsValid);
                Assert.AreEqual(0, ReadListField(controller, "_selectedEntities").Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BoardUI_UnrelatedControlResult_DoesNotChangePendingSelection()
        {
            GameObject instance = InstantiateBoardUI(out MonoBehaviour controller);
            try
            {
                SkillData skill = LoadRequiredAsset<SkillData>(BoardMergeSkillPath);
                var activityId = new ActivityId(32);
                var hostEntityId = new Core.Entities.EntityId(48);
                SimulationControlIntentEvent pending = SimulationControlIntentEvent.ActivateSkillEntities(
                    activityId,
                    hostEntityId,
                    skill,
                    new List<Core.Entities.EntityId>());
                SimulationControlIntentEvent unrelated = SimulationControlIntentEvent.ActivateSkillEntities(
                    activityId,
                    hostEntityId,
                    skill,
                    new List<Core.Entities.EntityId>());
                PrepareBoardControlState(controller, activityId, hostEntityId, skill, pending.RequestId);

                InvokeControlResult(controller, new SimulationControlResultEvent(
                    unrelated.RequestId,
                    activityId,
                    hostEntityId,
                    skill,
                    SimulationControlResultType.Rejected,
                    SimulationErrorCode.InvalidIntent,
                    "Unrelated rejection.",
                    SkillExecutionStatus.Failed,
                    SkillExecutionRef.Invalid));

                Assert.IsTrue(ReadField<bool>(controller, "_selectionLocked"));
                Assert.IsTrue(ReadField<bool>(controller, "_awaitingBoardRefresh"));
                Assert.AreEqual(
                    pending.RequestId,
                    ReadField<SimulationControlRequestId>(controller, "_pendingRequestId"));
                Assert.AreEqual(1, ReadListField(controller, "_selectedEntities").Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BoardUI_AcceptedRunningRequest_StaysLockedUntilAuthoritativeRefresh()
        {
            GameObject instance = InstantiateBoardUI(out MonoBehaviour controller);
            try
            {
                SkillData skill = LoadRequiredAsset<SkillData>(BoardMergeSkillPath);
                var activityId = new ActivityId(33);
                var hostEntityId = new Core.Entities.EntityId(49);
                SimulationControlIntentEvent request = SimulationControlIntentEvent.ActivateSkillEntities(
                    activityId,
                    hostEntityId,
                    skill,
                    new List<Core.Entities.EntityId>());
                var executionRef = new SkillExecutionRef(
                    hostEntityId,
                    new SkillId(5),
                    8L);
                PrepareBoardControlState(controller, activityId, hostEntityId, skill, request.RequestId);

                InvokeControlResult(controller, new SimulationControlResultEvent(
                    request.RequestId,
                    activityId,
                    hostEntityId,
                    skill,
                    SimulationControlResultType.Accepted,
                    SimulationErrorCode.None,
                    null,
                    SkillExecutionStatus.Running,
                    executionRef));

                Assert.IsTrue(ReadField<bool>(controller, "_selectionLocked"));
                Assert.IsTrue(ReadField<bool>(controller, "_awaitingBoardRefresh"));
                Assert.IsFalse(ReadField<SimulationControlRequestId>(controller, "_pendingRequestId").IsValid);
                Assert.AreEqual(
                    executionRef.ExecutionId,
                    ReadField<SkillExecutionRef>(controller, "_pendingExecutionRef").ExecutionId);

                InvokeSkillTerminated(controller, new SkillExecutionTerminatedEvent(
                    executionRef,
                    hostEntityId,
                    skill,
                    SkillExecutionObservation.Terminal(
                        SkillExecutionStatus.Completed,
                        SkillActivationFailureReason.None),
                    null));

                Assert.IsTrue(ReadField<bool>(controller, "_selectionLocked"));
                Assert.IsTrue(ReadField<bool>(controller, "_awaitingBoardRefresh"));
                Assert.IsFalse(ReadField<SkillExecutionRef>(controller, "_pendingExecutionRef").IsValid);
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

        static void PrepareBoardControlState(
            MonoBehaviour controller,
            ActivityId activityId,
            Core.Entities.EntityId hostEntityId,
            SkillData skill,
            SimulationControlRequestId requestId)
        {
            SetField(controller, "_context", new ActivityUIContext(
                activityId,
                default,
                new List<ActivityUICell>(),
                null,
                null));
            SetField(controller, "_boardHostEntityId", hostEntityId);
            SetField(controller, "mergeSkill", skill);
            SetField(controller, "_selectionLocked", true);
            SetField(controller, "_awaitingBoardRefresh", true);
            SetField(controller, "_boardRefreshObserved", false);
            SetField(controller, "_isSelecting", true);
            SetField(controller, "_pendingRequestId", requestId);
            SetField(controller, "_pendingExecutionRef", SkillExecutionRef.Invalid);
            ReadListField(controller, "_selectedEntities").Add(hostEntityId);
        }

        static void InvokeControlResult(MonoBehaviour controller, SimulationControlResultEvent result)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "OnEvent",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(SimulationControlResultEvent) },
                null);
            Assert.NotNull(method, "BoardUIController does not handle SimulationControlResultEvent.");
            method.Invoke(controller, new object[] { result });
        }

        static void InvokeSkillTerminated(MonoBehaviour controller, SkillExecutionTerminatedEvent result)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "OnEvent",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(SkillExecutionTerminatedEvent) },
                null);
            Assert.NotNull(method, "BoardUIController does not handle SkillExecutionTerminatedEvent.");
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

        static void AssertEconomyOperation(
            Core.Economy.Authoring.EconomyOperationData operation,
            EconomyAssetData asset,
            EconomyFormType formType,
            long amount,
            TaxonomyTermData walletTag)
        {
            Assert.AreEqual(EconomyOperation.Consume, operation.Operation);
            Assert.AreSame(asset, operation.Asset);
            Assert.AreEqual(formType, operation.FormType);
            Assert.AreEqual(amount, operation.Amount);
            Assert.AreEqual(1, operation.WalletTags.Count);
            Assert.AreSame(walletTag, operation.WalletTags[0]);
        }

        static void AssertEconomyOutput(
            EconomyOutputEntry output,
            EconomyAssetData asset,
            EconomyFormType formType,
            long amount,
            TaxonomyTermData walletTag)
        {
            Assert.AreSame(asset, output.Entry.Asset);
            Assert.AreEqual(formType, output.Entry.FormType);
            Assert.AreEqual(amount, output.Entry.Amount);
            Assert.AreEqual(1, output.WalletTags.Count);
            Assert.AreSame(walletTag, output.WalletTags[0]);
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

                string[] dependencies = AssetDatabase.GetDependencies(assetPath, true);
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
