using System;
using System.Collections.Generic;
using System.Linq;
using Core.Activities;
using Core.Activities.GameRuntime.Installers;
using Core.GameFlow;
using Core.GameFlow.GameRuntime;
using Core.GameFlow.GameRuntime.Installers;
using Core.GameRuntime;
using Core.GameRuntime.Installers;
using Core.Scheduling;
using Core.Simulation;
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
            AutobattleRoot + "/Definition/ChainRushAutobattleActivity.asset";
        const string BoardActivityPath =
            BoardRoot + "/Definition/ChainRushBoardActivity.asset";
        const string AutobattleFlowPath =
            AutobattleRoot + "/GameFlow/ChainRushAutobattleFlow.asset";
        const string BoardFlowPath =
            BoardRoot + "/GameFlow/ChainRushBoardFlow.asset";
        const string AutobattleSpacePath =
            AutobattleRoot + "/Space/ChainRushAutobattleSpace.prefab";
        const string BoardSpacePath =
            BoardRoot + "/Space/ChainRushBoardSpace.prefab";
        const string RuntimeProfilePath =
            "Assets/Game/Runtime/Host/ChainRushGameRuntimeProfile.asset";
        const string StartupPlanPath =
            "Assets/Game/Runtime/Startup/ChainRushGameStartupPlan.asset";

        const string AutobattleActivityTypeId = "chainrush.activity-type.autobattle";
        const string BoardActivityTypeId = "chainrush.activity-type.board";
        const string BoardActivationTermId = "chainrush.activity.activation.board";

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
            Assert.AreEqual(0, board.Teams[0].Objectives.Count);
            Assert.AreEqual(0, board.Teams[0].Features.Count);
            AssertTopology(
                board.Topology,
                TopologyType.Grid,
                TopologyCoordinateOccupationPolicy.SingleOccupant);

            Assert.AreEqual(1, autobattle.Teams[0].Wallets.Count);
            Assert.AreEqual(1, autobattle.Teams[1].Wallets.Count);
            Assert.AreEqual(1, board.Teams[0].Wallets.Count);
            Assert.AreSame(
                autobattle.Teams[0].Wallets[0].Wallet,
                autobattle.Teams[1].Wallets[0].Wallet);
            Assert.AreSame(
                autobattle.Teams[0].Wallets[0].Wallet,
                board.Teams[0].Wallets[0].Wallet);

            Assert.IsInstanceOf<ActivityPrefabSpaceData>(autobattle.Space);
            Assert.IsInstanceOf<ActivityPrefabSpaceData>(board.Space);
            Assert.IsTrue(((ActivityPrefabSpaceData)autobattle.Space).PrefabReference.RuntimeKeyIsValid());
            Assert.IsTrue(((ActivityPrefabSpaceData)board.Space).PrefabReference.RuntimeKeyIsValid());
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
        public void ActivitySpaces_AreAddressableInSeparateGroups()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.NotNull(settings, "AddressableAssetSettings are not configured.");

            AssertAddressableGroup(
                settings,
                AutobattleSpacePath,
                "ChainRush-Activity-Autobattle");
            AssertAddressableGroup(
                settings,
                BoardSpacePath,
                "ChainRush-Activity-Board");
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

        static void AssertSimulationPolicy(ActivityData activity)
        {
            Assert.AreEqual(ActivitySimulationBindingType.OwnContext, activity.Simulation.BindingType);
            Assert.AreEqual(SimulationScope.Activity, activity.Simulation.Scope);
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
