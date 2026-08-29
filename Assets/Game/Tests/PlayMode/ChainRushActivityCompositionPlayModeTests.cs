using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Core.AI;
using Core.Activities;
using Core.Activities.Events;
using Core.Activities.Selection;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Diplomacy;
using Core.Economy;
using Core.Events;
using Core.GameRuntime;
using Core.HostValues;
using Core.Objectives;
using Core.Objectives.Events;
using Core.Orchestration;
using Core.Players;
using Core.Production;
using Core.Production.Authoring;
using Core.Production.Events;
using Core.Projection;
using Core.Runtime;
using Core.SimulationControl;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChainRush.Tests.PlayMode
{
    public sealed class ChainRushActivityCompositionPlayModeTests : IPrebuildSetup, IPostBuildCleanup
    {
        const string PlayMainEditorPrefKey = "Game/Play Game";
        const string HadPlayMainEditorPrefSessionKey =
            "ChainRush.Tests.ActivityComposition.HadPlayMainEditorPref";
        const string PlayMainEditorPrefValueSessionKey =
            "ChainRush.Tests.ActivityComposition.PlayMainEditorPrefValue";
        const string IntegrationScenePath =
            "Assets/Game/Scenes/Integration/ChainRushFrameworkIntegration.unity";
        const string BoardActivityPath =
            "Assets/Game/Activities/Board/Definition/BoardActivity.asset";
        const string BoardActivationTermPath =
            "Assets/Game/Activities/Shared/Taxonomy/BoardActivationTerm.asset";
        const string BoardCellTagPath =
            "Assets/Game/Activities/Board/Taxonomy/BoardCellTag.asset";
        const string BoardWaterBasePath =
            "Assets/Game/Activities/Board/Economy/WaterBoardBase.asset";
        const string BoardHostPath =
            "Assets/Game/Activities/Board/Economy/BoardHost.asset";
        const string BoardMergeSelectionPath =
            "Assets/Game/Activities/Board/Taxonomy/BoardMergeSelection.asset";
        const string BoardMergeRecipe1Path =
            "Assets/Game/Activities/Board/Production/BoardMergeRecipe1.asset";
        const string BoardMergeRecipe4Path =
            "Assets/Game/Activities/Board/Production/BoardMergeRecipe4.asset";
        const string BoardTurnTokenPath =
            "Assets/Game/Activities/Shared/Economy/BoardTurnToken.asset";
        const string WaterUnitPath =
            "Assets/Game/Activities/Shared/Units/Water/WaterUnit.asset";
        const string EnemyPath =
            "Assets/Game/Activities/Autobattle/Economy/BugBrownSmall.asset";
        const string ExperienceCollectorPath =
            "Assets/Game/Activities/Autobattle/Economy/ExperienceCollector.asset";
        const string ExperienceDropPath =
            "Assets/Game/Activities/Autobattle/Economy/ExperienceDrop.asset";
        const string ExperiencePath =
            "Assets/Game/Activities/Shared/Economy/Experience.asset";
        const string HealthPath =
            "Assets/Game/Activities/Autobattle/HostValues/Health.asset";
        const string IntegrationRuntimeTagPath =
            "Assets/Game/Activities/Autobattle/Definition/IntegrationAutobattle.asset";
        const string SharedWalletTagPath =
            "Assets/Game/Activities/Shared/Economy/ActivityWalletTag.asset";
        const string AutobattleActivityTypeId = "chainrush.activity-type.autobattle";
        const string BoardActivityTypeId = "chainrush.activity-type.board";
        const float StartupTimeoutSeconds = 10f;
        const float CollectorCycleTimeoutSeconds = 30f;

        public void Setup()
        {
            bool hadPreference = EditorPrefs.HasKey(PlayMainEditorPrefKey);
            SessionState.SetBool(HadPlayMainEditorPrefSessionKey, hadPreference);
            SessionState.SetBool(
                PlayMainEditorPrefValueSessionKey,
                hadPreference && EditorPrefs.GetBool(PlayMainEditorPrefKey));
            EditorPrefs.SetBool(PlayMainEditorPrefKey, false);
        }

        public void Cleanup()
        {
            if (SessionState.GetBool(HadPlayMainEditorPrefSessionKey, false))
            {
                EditorPrefs.SetBool(
                    PlayMainEditorPrefKey,
                    SessionState.GetBool(PlayMainEditorPrefValueSessionKey, false));
            }
            else
            {
                EditorPrefs.DeleteKey(PlayMainEditorPrefKey);
            }

            SessionState.EraseBool(HadPlayMainEditorPrefSessionKey);
            SessionState.EraseBool(PlayMainEditorPrefValueSessionKey);
        }

        [UnityTest]
        public IEnumerator RuntimeComposition_LaunchesBoardOnceAndParentCloseClosesIt()
        {
            Scene integrationScene = EditorSceneManager.LoadSceneInPlayMode(
                IntegrationScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.IsTrue(integrationScene.IsValid(), "Integration scene did not load.");
            float sceneDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (!integrationScene.isLoaded && Time.realtimeSinceStartup < sceneDeadline)
                yield return null;
            Assert.IsTrue(integrationScene.isLoaded, "Integration scene is not marked as loaded.");

            yield return null;

            GameRuntimeHost host = Object.FindFirstObjectByType<GameRuntimeHost>(
                FindObjectsInactive.Include);
            Assert.NotNull(host, "Integration scene does not contain GameRuntimeHost.");

            float startupDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (Time.realtimeSinceStartup < startupDeadline)
            {
                if (host.RuntimeContext != null
                    && host.RuntimeContext.IsInitialized
                    && TryFindRunningActivities(out _, out _))
                {
                    break;
                }

                yield return null;
            }

            Assert.IsTrue(host.RuntimeContext != null && host.RuntimeContext.IsInitialized);
            Assert.IsTrue(
                TryFindRunningActivities(out ActivityRuntimeSnapshot autobattle, out ActivityRuntimeSnapshot board),
                "Autobattle and Board did not both reach Running state.");

            TaxonomyTermData integrationRuntimeTag =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(IntegrationRuntimeTagPath);
            Assert.NotNull(integrationRuntimeTag);
            Assert.IsFalse(autobattle.ParentActivityId.IsValid);
            Assert.NotNull(autobattle.Definition);
            Assert.AreEqual(1, autobattle.RuntimeTags.Count);
            Assert.AreSame(integrationRuntimeTag, autobattle.RuntimeTags[0]);
            Assert.AreEqual(2, autobattle.Participants.Count);
            AssertParticipant(autobattle.Participants, 0, PlayerControlType.LocalHuman);
            AssertParticipant(autobattle.Participants, 1, PlayerControlType.Bot);

            Assert.AreEqual(autobattle.Id, board.ParentActivityId);
            Assert.AreEqual(0, board.RuntimeTags.Count);
            Assert.AreEqual(1, board.Participants.Count);
            AssertParticipant(board.Participants, 0, PlayerControlType.LocalHuman);
            Assert.AreEqual(3, board.ObjectiveRuntimeIds.Count);
            Assert.AreEqual(
                3,
                board.Objectives.Count(objective =>
                    objective.SourceType == ActivityObjectiveSourceType.Definition));
            CollectionAssert.AreEqual(
                new List<ActivityId> { board.Id },
                ActivityService.GetChildActivityIds(autobattle.Id));

            TaxonomyTermData boardCellTag =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardCellTagPath);
            Assert.NotNull(boardCellTag);
            Assert.AreEqual(
                16,
                SpatialMarkerService.GetMarkers(
                    board.Id,
                    board.ActivityRootEntityId,
                    new List<TaxonomyTermData> { boardCellTag }).Count);
            Assert.AreEqual(16, CountBoardUICells());
            AssertBoardUIVisible(host);

            CapabilityHostData waterUnitDefinition =
                AssetDatabase.LoadAssetAtPath<CapabilityHostData>(WaterUnitPath);
            CapabilityHostData enemyDefinition =
                AssetDatabase.LoadAssetAtPath<CapabilityHostData>(EnemyPath);
            CapabilityHostData collectorDefinition =
                AssetDatabase.LoadAssetAtPath<CapabilityHostData>(ExperienceCollectorPath);
            CapabilityHostData experienceDropDefinition =
                AssetDatabase.LoadAssetAtPath<CapabilityHostData>(ExperienceDropPath);
            EconomyAssetData experience =
                AssetDatabase.LoadAssetAtPath<EconomyAssetData>(ExperiencePath);
            HostValueData health = AssetDatabase.LoadAssetAtPath<HostValueData>(HealthPath);
            Assert.NotNull(waterUnitDefinition);
            Assert.NotNull(enemyDefinition);
            Assert.NotNull(collectorDefinition);
            Assert.NotNull(experienceDropDefinition);
            Assert.NotNull(experience);
            Assert.NotNull(health);

            float combatHostDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (Time.realtimeSinceStartup < combatHostDeadline
                && (!TryFindActivityHost(
                        autobattle.Id,
                        waterUnitDefinition,
                        out Core.Entities.EntityId waterUnitEntityId)
                    || !TryFindActivityHost(
                        autobattle.Id,
                        enemyDefinition,
                        out Core.Entities.EntityId enemyEntityId)
                    || !TryFindActivityHost(
                        autobattle.Id,
                        collectorDefinition,
                        out Core.Entities.EntityId collectorEntityId)
                    || !TryFindProjectionBinding(
                        autobattle.Id,
                        collectorEntityId,
                        out _)))
            {
                yield return null;
            }

            Assert.IsTrue(
                TryFindActivityHost(
                    autobattle.Id,
                    waterUnitDefinition,
                    out Core.Entities.EntityId waterUnitEntity),
                "Autobattle did not materialize a Water unit.");
            Assert.IsTrue(
                TryFindActivityHost(
                    autobattle.Id,
                    enemyDefinition,
                    out Core.Entities.EntityId enemyEntity),
                "Autobattle did not materialize an enemy.");
            Assert.IsTrue(
                TryFindActivityHost(
                    autobattle.Id,
                    collectorDefinition,
                    out Core.Entities.EntityId collectorEntity),
                "Autobattle did not register the Experience collector.");
            Assert.IsFalse(
                SpatialService.TryGetPosition(collectorEntity, out _),
                "Experience collector unexpectedly received a Spatial position.");
            Assert.IsTrue(
                TryFindProjectionBinding(
                    autobattle.Id,
                    collectorEntity,
                    out ProjectionBindingController collectorProjectionBinding,
                    out ProjectionBindingContext collectorProjection),
                "Experience collector did not bind to the progressbar projection target.");
            Assert.AreEqual(
                ProjectionCoordinateType.UI,
                collectorProjection.ProjectionTarget.CoordinateType);
            Assert.IsTrue(
                TryFindActivityViewport(out Camera autobattleViewport),
                "Integration scene does not contain an active Autobattle viewport.");
            Assert.IsTrue(
                DiplomacyService.TryGetRelation(
                    autobattle.Id,
                    waterUnitEntity,
                    enemyEntity,
                    DiplomacyChannelType.Military,
                    out DiplomacyRelationSnapshot combatRelation),
                "Diplomacy relation between materialized opposing units is unavailable.");
            Assert.AreEqual(
                DiplomacyDispositionType.Hostile,
                combatRelation.Disposition,
                "Materialized opposing units are not hostile.");
            Assert.AreEqual(autobattle.Id, combatRelation.ActivityId);

            CapabilityHostData waterBase =
                AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardWaterBasePath);
            EconomyAssetData turnToken =
                AssetDatabase.LoadAssetAtPath<EconomyAssetData>(BoardTurnTokenPath);
            TaxonomyTermData sharedWalletTag =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(SharedWalletTagPath);
            Assert.NotNull(waterBase);
            Assert.NotNull(turnToken);
            Assert.NotNull(sharedWalletTag);

            var lastDropScreenDistances = new Dictionary<long, float>();
            var dropProjectionParents = new Dictionary<long, Transform>();
            bool sawExperienceDropProjection = false;
            bool sawExperienceDropFlight = false;
            float populationDeadline = Time.realtimeSinceStartup + CollectorCycleTimeoutSeconds;
            while (Time.realtimeSinceStartup < populationDeadline
                && (CountMaterializedBoardAssets(board, boardCellTag, waterBase) != 16
                    || QueryAmount(
                        board,
                        sharedWalletTag,
                        EconomyFormType.Stack,
                        turnToken) != 0L))
            {
                ObserveExperienceDropFlight(
                    autobattle.Id,
                    experienceDropDefinition,
                    collectorProjectionBinding,
                    autobattleViewport,
                    lastDropScreenDistances,
                    dropProjectionParents,
                    ref sawExperienceDropProjection,
                    ref sawExperienceDropFlight);
                yield return null;
            }

            Assert.IsTrue(
                sawExperienceDropProjection,
                "The collection cycle completed without an observable ExperienceDrop projection.");
            Assert.IsTrue(
                sawExperienceDropFlight,
                "ExperienceDrop never moved toward the progressbar before collection.");
            Assert.AreEqual(
                16,
                CountMaterializedBoardAssets(board, boardCellTag, waterBase),
                string.Concat(
                    "The first Experience collection cycle did not produce a turn token and populate every Board marker.\n",
                    BuildPopulationDiagnostic(
                        autobattle,
                        board,
                        sharedWalletTag,
                        turnToken,
                        experience,
                        health,
                        collectorDefinition,
                        experienceDropDefinition)));
            Assert.AreEqual(
                0L,
                QueryAmount(board, sharedWalletTag, EconomyFormType.Stack, turnToken),
                "Board refresh production did not consume the first Experience-produced turn token.");

            CapabilityHostData boardHost =
                AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardHostPath);
            TaxonomyTermData mergeSelection =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardMergeSelectionPath);
            Assert.NotNull(boardHost);
            Assert.NotNull(mergeSelection);
            Assert.IsTrue(
                TryFindBoardHost(board.Id, boardHost, out Core.Entities.EntityId boardHostEntityId),
                "The hidden Board host was not registered for the Board Activity.");
            List<Core.Entities.EntityId> selectedEntities = ResolveConnectedMarkerSelection(
                board,
                boardCellTag,
                waterBase,
                6);
            Assert.AreEqual(6, selectedEntities.Count);
            HashSet<long> waterUnitsBeforeMerge = GetActivityHostEntityValues(
                autobattle.Id,
                waterUnitDefinition);
            EnableOrchestrationTraceDiagnostics();

            SelectionIntentEvent mergeRequest = SelectionIntentEvent.Begin(
                board.Id,
                mergeSelection,
                Core.Entities.EntityId.Invalid,
                boardHostEntityId);
            Assert.IsTrue(mergeRequest.RequestId.IsValid);
            var selectionResultCapture = new SelectionResultCapture(mergeRequest.RequestId);
            var productionOrderCapture = new ProductionOrderCapture(board.DomainId, boardHostEntityId);
            EventBus.Register<SelectionResultEvent>(selectionResultCapture);
            EventBus.Register<ProductionOrderStartedEvent>(productionOrderCapture);
            EventBus.Register<ProductionOrderFinishedEvent>(productionOrderCapture);
            EventBus.Register<OrchestrationProcessStateChangedEvent>(productionOrderCapture);
            EventBus.Register<ObjectiveNodeStateChangedEvent>(productionOrderCapture);
            EventBus.Register<ObjectiveRuntimeCompletedEvent>(productionOrderCapture);
            EventBus.Register<ObjectiveRuntimeResetEvent>(productionOrderCapture);
            EventBus.Trigger(mergeRequest);
            for (int selectedIndex = 0; selectedIndex < selectedEntities.Count; selectedIndex++)
            {
                EventBus.Trigger(SelectionIntentEvent.Target(
                    mergeRequest,
                    selectedEntities[selectedIndex]));
            }
            EventBus.Trigger(SelectionIntentEvent.Complete(mergeRequest));

            float mergeDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (Time.realtimeSinceStartup < mergeDeadline
                && (selectedEntities.Any(CapabilityHostService.Exists)
                    || GetActivityHostEntityValues(
                        autobattle.Id,
                        waterUnitDefinition).Count < waterUnitsBeforeMerge.Count + 2))
            {
                yield return null;
            }
            EventBus.Unregister<SelectionResultEvent>(selectionResultCapture);
            EventBus.Unregister<ProductionOrderStartedEvent>(productionOrderCapture);
            EventBus.Unregister<ProductionOrderFinishedEvent>(productionOrderCapture);
            EventBus.Unregister<OrchestrationProcessStateChangedEvent>(productionOrderCapture);
            EventBus.Unregister<ObjectiveNodeStateChangedEvent>(productionOrderCapture);
            EventBus.Unregister<ObjectiveRuntimeCompletedEvent>(productionOrderCapture);
            EventBus.Unregister<ObjectiveRuntimeResetEvent>(productionOrderCapture);

            Assert.AreEqual(
                1,
                selectionResultCapture.Count,
                string.Concat(
                    "Selection request did not publish exactly one terminal result.\n",
                    BuildExecutorDiagnostic(boardHostEntityId),
                    BuildPopulationDiagnostic(
                        autobattle,
                        board,
                        sharedWalletTag,
                        turnToken,
                        experience,
                        health,
                        collectorDefinition,
                        experienceDropDefinition)));
            Assert.AreEqual(
                SelectionResultType.Committed,
                selectionResultCapture.Result.Type,
                selectionResultCapture.Result.Message);
            CollectionAssert.AreEqual(
                selectedEntities,
                selectionResultCapture.Result.SelectedEntityIds);

            for (int selectedIndex = 0; selectedIndex < selectedEntities.Count; selectedIndex++)
            {
                Assert.IsFalse(
                    CapabilityHostService.Exists(selectedEntities[selectedIndex]),
                    string.Concat(
                        "A selected Board token remained materialized after committed merge production.\n",
                        BuildSelectedEntityDiagnostic(selectedEntities),
                        productionOrderCapture.BuildDiagnostic(),
                        BuildExecutorDiagnostic(boardHostEntityId),
                        BuildPopulationDiagnostic(
                            autobattle,
                            board,
                            sharedWalletTag,
                            turnToken,
                            experience,
                            health,
                            collectorDefinition,
                            experienceDropDefinition)));
            }
            Assert.IsTrue(
                HasNewActivityHost(
                    autobattle.Id,
                    waterUnitDefinition,
                    waterUnitsBeforeMerge),
                "Merge output was not deployed as a new physical Water unit.");
            HashSet<long> waterUnitsAfterMerge = GetActivityHostEntityValues(
                autobattle.Id,
                waterUnitDefinition);
            Assert.AreEqual(
                waterUnitsBeforeMerge.Count + 2,
                waterUnitsAfterMerge.Count,
                string.Concat(
                    "A six-token selection must resolve through sequential x4 and x2 recipe yields.\n",
                    productionOrderCapture.BuildDiagnostic(),
                    BuildPopulationDiagnostic(
                        autobattle,
                        board,
                        sharedWalletTag,
                        turnToken,
                        experience,
                        health,
                        collectorDefinition,
                        experienceDropDefinition)));

            float collectorCycleDeadline =
                Time.realtimeSinceStartup + CollectorCycleTimeoutSeconds;
            while (Time.realtimeSinceStartup < collectorCycleDeadline
                && CountMaterializedBoardAssets(board, boardCellTag, waterBase) != 16)
            {
                yield return null;
            }

            Assert.AreEqual(
                16,
                CountMaterializedBoardAssets(board, boardCellTag, waterBase),
                string.Concat(
                    "Collected Experience did not produce and consume the next Board turn token.\n",
                    BuildPopulationDiagnostic(
                        autobattle,
                        board,
                        sharedWalletTag,
                        turnToken,
                        experience,
                        health,
                        collectorDefinition,
                        experienceDropDefinition)));
            Assert.AreEqual(
                0L,
                QueryAmount(board, sharedWalletTag, EconomyFormType.Stack, turnToken),
                "The next Board turn token remained unconsumed after population refresh.");

            ProductionRecipeData mergeRecipe1 =
                AssetDatabase.LoadAssetAtPath<ProductionRecipeData>(BoardMergeRecipe1Path);
            ProductionRecipeData mergeRecipe4 =
                AssetDatabase.LoadAssetAtPath<ProductionRecipeData>(BoardMergeRecipe4Path);
            Assert.NotNull(mergeRecipe1);
            Assert.NotNull(mergeRecipe4);
            yield return AssertBoardMergeSequence(
                board,
                boardCellTag,
                waterBase,
                boardHostEntityId,
                mergeSelection,
                selectionCount: 5,
                expectedRecipeIds: new List<string>
                {
                    mergeRecipe4.Id,
                    mergeRecipe1.Id,
                });

            TaxonomyTermData activation =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardActivationTermPath);
            Assert.NotNull(activation);
            EventBus.Trigger(new ActivityChildActivationEvent(
                autobattle.Id,
                new List<TaxonomyTermData> { activation }));
            yield return null;
            Assert.AreEqual(1, ActivityService.GetChildActivityIds(autobattle.Id).Count);

            Assert.IsTrue(ActivityService.Close(autobattle.Id, ActivityCloseCauseType.Manual));
            Assert.IsTrue(ActivityService.TryGetSnapshot(board.Id, out board));
            Assert.AreEqual(ActivityState.Closed, board.State);
            Assert.AreEqual(ActivityCloseCauseType.ParentClosed, board.CloseCauseType);
            Assert.AreEqual(ActivityResultType.Cancelled, board.ResultType);
        }

        static IEnumerator AssertBoardMergeSequence(
            ActivityRuntimeSnapshot board,
            TaxonomyTermData boardCellTag,
            CapabilityHostData boardItem,
            Core.Entities.EntityId boardHostEntityId,
            TaxonomyTermData mergeSelection,
            int selectionCount,
            IReadOnlyList<string> expectedRecipeIds)
        {
            List<Core.Entities.EntityId> selectedEntities = ResolveConnectedMarkerSelection(
                board,
                boardCellTag,
                boardItem,
                selectionCount);
            Assert.AreEqual(selectionCount, selectedEntities.Count);

            SelectionIntentEvent request = SelectionIntentEvent.Begin(
                board.Id,
                mergeSelection,
                Core.Entities.EntityId.Invalid,
                boardHostEntityId);
            Assert.IsTrue(request.RequestId.IsValid);

            var selectionCapture = new SelectionResultCapture(request.RequestId);
            var productionCapture = new ProductionOrderCapture(board.DomainId, boardHostEntityId);
            EventBus.Register<SelectionResultEvent>(selectionCapture);
            EventBus.Register<ProductionOrderStartedEvent>(productionCapture);
            EventBus.Register<ProductionOrderFinishedEvent>(productionCapture);
            EventBus.Register<OrchestrationProcessStateChangedEvent>(productionCapture);
            EventBus.Register<ObjectiveNodeStateChangedEvent>(productionCapture);
            EventBus.Register<ObjectiveRuntimeCompletedEvent>(productionCapture);
            EventBus.Register<ObjectiveRuntimeResetEvent>(productionCapture);
            try
            {
                EventBus.Trigger(request);
                for (int selectedIndex = 0; selectedIndex < selectedEntities.Count; selectedIndex++)
                {
                    EventBus.Trigger(SelectionIntentEvent.Target(
                        request,
                        selectedEntities[selectedIndex]));
                }

                EventBus.Trigger(SelectionIntentEvent.Complete(request));

                float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline
                    && selectedEntities.Any(CapabilityHostService.Exists))
                {
                    yield return null;
                }
            }
            finally
            {
                EventBus.Unregister<SelectionResultEvent>(selectionCapture);
                EventBus.Unregister<ProductionOrderStartedEvent>(productionCapture);
                EventBus.Unregister<ProductionOrderFinishedEvent>(productionCapture);
                EventBus.Unregister<OrchestrationProcessStateChangedEvent>(productionCapture);
                EventBus.Unregister<ObjectiveNodeStateChangedEvent>(productionCapture);
                EventBus.Unregister<ObjectiveRuntimeCompletedEvent>(productionCapture);
                EventBus.Unregister<ObjectiveRuntimeResetEvent>(productionCapture);
            }

            Assert.AreEqual(1, selectionCapture.Count, productionCapture.BuildDiagnostic());
            Assert.AreEqual(
                SelectionResultType.Committed,
                selectionCapture.Result.Type,
                selectionCapture.Result.Message);
            CollectionAssert.AreEqual(
                selectedEntities,
                selectionCapture.Result.SelectedEntityIds);
            Assert.IsFalse(
                selectedEntities.Any(CapabilityHostService.Exists),
                string.Concat(
                    "Sequential Board merge left selected tokens materialized.\n",
                    BuildSelectedEntityDiagnostic(selectedEntities),
                    productionCapture.BuildDiagnostic()));
            Assert.AreEqual(
                expectedRecipeIds.Count,
                productionCapture.StartedCount,
                productionCapture.BuildDiagnostic());
            for (int recipeIndex = 0; recipeIndex < expectedRecipeIds.Count; recipeIndex++)
            {
                Assert.AreEqual(
                    1,
                    productionCapture.CountRecipe(expectedRecipeIds[recipeIndex]),
                    productionCapture.BuildDiagnostic());
            }
        }

        static int CountBoardUICells()
        {
            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null
                    || !string.Equals(
                        behaviour.GetType().FullName,
                        "ChainRush.Board.BoardUIController",
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                int count = 0;
                Transform[] descendants = behaviour.GetComponentsInChildren<Transform>(true);
                for (int descendantIndex = 0; descendantIndex < descendants.Length; descendantIndex++)
                {
                    if (descendants[descendantIndex] != null
                        && descendants[descendantIndex].name.StartsWith(
                            "BoardCell_",
                            System.StringComparison.Ordinal))
                    {
                        count++;
                    }
                }

                return count;
            }

            return 0;
        }

        static void AssertBoardUIVisible(GameRuntimeHost host)
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>(
                FindObjectsInactive.Include);
            Assert.NotNull(eventSystem, "Integration scene does not contain an EventSystem.");
            Assert.IsTrue(eventSystem.gameObject.activeInHierarchy, "EventSystem is inactive.");

            BaseInputModule inputModule = eventSystem.currentInputModule;
            if (inputModule == null)
                inputModule = eventSystem.GetComponent<BaseInputModule>();

            Assert.NotNull(inputModule, "EventSystem does not contain an input module.");
            Assert.IsTrue(inputModule.isActiveAndEnabled, "EventSystem input module is inactive.");
            Assert.AreEqual(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule",
                inputModule.GetType().FullName,
                "Integration scene must use InputSystemUIInputModule.");

            Transform uiPopupRoot = host.transform.Find("UIPopupRoot");
            Assert.NotNull(uiPopupRoot, "GameRuntimeHost does not contain UIPopupRoot.");
            Assert.IsTrue(uiPopupRoot.gameObject.activeInHierarchy, "UIPopupRoot is inactive.");
            Assert.Greater(Mathf.Abs(uiPopupRoot.lossyScale.x), Mathf.Epsilon, "UIPopupRoot scale X is zero.");
            Assert.Greater(Mathf.Abs(uiPopupRoot.lossyScale.y), Mathf.Epsilon, "UIPopupRoot scale Y is zero.");
            Assert.Greater(Mathf.Abs(uiPopupRoot.lossyScale.z), Mathf.Epsilon, "UIPopupRoot scale Z is zero.");

            Canvas.ForceUpdateCanvases();
            MonoBehaviour[] behaviours = uiPopupRoot.GetComponentsInChildren<MonoBehaviour>(true);
            MonoBehaviour boardController = null;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null
                    && string.Equals(
                        behaviours[i].GetType().FullName,
                        "ChainRush.Board.BoardUIController",
                        System.StringComparison.Ordinal))
                {
                    boardController = behaviours[i];
                    break;
                }
            }

            Assert.NotNull(boardController, "Board UI controller is missing under UIPopupRoot.");
            Assert.IsTrue(boardController.gameObject.activeInHierarchy, "Board UI controller is inactive.");

            bool hasVisibleCell = false;
            RectTransform[] rectTransforms = boardController.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rectTransforms.Length; i++)
            {
                RectTransform rectTransform = rectTransforms[i];
                if (rectTransform == null
                    || !rectTransform.name.StartsWith("BoardCell_", System.StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.IsTrue(rectTransform.gameObject.activeInHierarchy, "A Board UI cell is inactive.");
                if (rectTransform.rect.width > 0f && rectTransform.rect.height > 0f)
                    hasVisibleCell = true;
            }

            Assert.IsTrue(hasVisibleCell, "Board UI cells have no visible layout area.");
        }

        static int CountMaterializedBoardAssets(
            ActivityRuntimeSnapshot board,
            TaxonomyTermData markerTag,
            CapabilityHostData expectedAsset)
        {
            List<SpatialMarkerSnapshot> markers = SpatialMarkerService.GetMarkers(
                board.Id,
                board.ActivityRootEntityId,
                new List<TaxonomyTermData> { markerTag });
            var matchingEntities = new HashSet<long>();
            for (int markerIndex = 0; markerIndex < markers.Count; markerIndex++)
            {
                Core.Entities.EntityId[] occupants =
                    SpatialService.GetOccupants(markers[markerIndex].WorldPosition);
                for (int occupantIndex = 0; occupantIndex < occupants.Length; occupantIndex++)
                {
                    Core.Entities.EntityId entityId = occupants[occupantIndex];
                    if (!CapabilityHostService.TryGet(entityId, out CapabilityHostSnapshot host)
                        || host.Definition == null
                        || !string.Equals(
                            host.Definition.Id,
                            expectedAsset.Id,
                            System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    matchingEntities.Add(entityId.Value);
                }
            }

            return matchingEntities.Count;
        }

        static List<Core.Entities.EntityId> ResolveConnectedMarkerSelection(
            ActivityRuntimeSnapshot board,
            TaxonomyTermData markerTag,
            CapabilityHostData expectedAsset,
            int count)
        {
            List<SpatialMarkerSnapshot> markers = SpatialMarkerService.GetMarkers(
                board.Id,
                board.ActivityRootEntityId,
                new List<TaxonomyTermData> { markerTag });
            markers.Sort((left, right) => left.LocalIndex.CompareTo(right.LocalIndex));
            var entitiesByMarker = new Dictionary<int, Core.Entities.EntityId>();
            for (int markerIndex = 0; markerIndex < markers.Count; markerIndex++)
            {
                Core.Entities.EntityId[] occupants =
                    SpatialService.GetOccupants(markers[markerIndex].WorldPosition);
                Core.Entities.EntityId match = Core.Entities.EntityId.Invalid;
                for (int occupantIndex = 0; occupantIndex < occupants.Length; occupantIndex++)
                {
                    if (!CapabilityHostService.TryGet(
                            occupants[occupantIndex],
                            out CapabilityHostSnapshot host)
                        || host.Definition == null
                        || !string.Equals(
                            host.Definition.Id,
                            expectedAsset.Id,
                            System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    match = occupants[occupantIndex];
                    break;
                }

                if (match.IsValid)
                    entitiesByMarker.Add(markerIndex, match);
            }

            int adjacencyDistance = ResolveMinimumMarkerDistance(markers);
            if (adjacencyDistance <= 0)
                return new List<Core.Entities.EntityId>(0);

            for (int markerIndex = 0; markerIndex < markers.Count; markerIndex++)
            {
                if (!entitiesByMarker.ContainsKey(markerIndex))
                    continue;

                var markerPath = new List<int>(count) { markerIndex };
                var usedMarkers = new HashSet<int> { markerIndex };
                if (!TryBuildConnectedMarkerPath(
                        markers,
                        entitiesByMarker,
                        count,
                        adjacencyDistance,
                        markerPath,
                        usedMarkers))
                {
                    continue;
                }

                var selected = new List<Core.Entities.EntityId>(count);
                for (int pathIndex = 0; pathIndex < markerPath.Count; pathIndex++)
                    selected.Add(entitiesByMarker[markerPath[pathIndex]]);

                return selected;
            }

            return new List<Core.Entities.EntityId>(0);
        }

        static bool TryBuildConnectedMarkerPath(
            List<SpatialMarkerSnapshot> markers,
            Dictionary<int, Core.Entities.EntityId> entitiesByMarker,
            int count,
            int adjacencyDistance,
            List<int> markerPath,
            HashSet<int> usedMarkers)
        {
            if (markerPath.Count == count)
                return true;

            SpatialMarkerSnapshot previous = markers[markerPath[markerPath.Count - 1]];
            for (int markerIndex = 0; markerIndex < markers.Count; markerIndex++)
            {
                if (usedMarkers.Contains(markerIndex)
                    || !entitiesByMarker.ContainsKey(markerIndex)
                    || !TopologyService.TryGetDistance(
                        previous.WorldPosition,
                        markers[markerIndex].WorldPosition,
                        out int distance)
                    || distance != adjacencyDistance)
                {
                    continue;
                }

                markerPath.Add(markerIndex);
                usedMarkers.Add(markerIndex);
                if (TryBuildConnectedMarkerPath(
                        markers,
                        entitiesByMarker,
                        count,
                        adjacencyDistance,
                        markerPath,
                        usedMarkers))
                {
                    return true;
                }

                usedMarkers.Remove(markerIndex);
                markerPath.RemoveAt(markerPath.Count - 1);
            }

            return false;
        }

        static int ResolveMinimumMarkerDistance(List<SpatialMarkerSnapshot> markers)
        {
            int minimumDistance = int.MaxValue;
            for (int leftIndex = 0; leftIndex < markers.Count; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1; rightIndex < markers.Count; rightIndex++)
                {
                    if (TopologyService.TryGetDistance(
                            markers[leftIndex].WorldPosition,
                            markers[rightIndex].WorldPosition,
                            out int distance)
                        && distance > 0
                        && distance < minimumDistance)
                    {
                        minimumDistance = distance;
                    }
                }
            }

            return minimumDistance == int.MaxValue ? 0 : minimumDistance;
        }

        static bool TryFindBoardHost(
            ActivityId activityId,
            CapabilityHostData expectedDefinition,
            out Core.Entities.EntityId entityId)
        {
            entityId = Core.Entities.EntityId.Invalid;
            List<CapabilityHostSnapshot> hosts = CapabilityHostService.GetAll();
            for (int hostIndex = 0; hostIndex < hosts.Count; hostIndex++)
            {
                CapabilityHostSnapshot host = hosts[hostIndex];
                if (host.ActivityId != activityId
                    || host.Definition == null
                    || !string.Equals(
                        host.Definition.Id,
                        expectedDefinition.Id,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                entityId = host.EntityId;
                return true;
            }

            return false;
        }

        static bool TryFindProjectionBinding(
            ActivityId activityId,
            Core.Entities.EntityId entityId,
            out ProjectionBindingContext context)
        {
            return TryFindProjectionBinding(activityId, entityId, out _, out context);
        }

        static bool TryFindProjectionBinding(
            ActivityId activityId,
            Core.Entities.EntityId entityId,
            out ProjectionBindingController resultBinding,
            out ProjectionBindingContext context)
        {
            resultBinding = null;
            context = default;
            ProjectionBindingController[] bindings = Object.FindObjectsByType<ProjectionBindingController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < bindings.Length; i++)
            {
                ProjectionBindingController candidateBinding = bindings[i];
                if (candidateBinding == null
                    || !candidateBinding.TryGetContext(out ProjectionBindingContext candidate)
                    || candidate.Handle.ActivityId != activityId
                    || candidate.Handle.EntityId != entityId)
                {
                    continue;
                }

                resultBinding = candidate.Handle.IsValid ? candidateBinding : null;
                context = candidate;
                return resultBinding != null;
            }

            return false;
        }

        static bool TryFindActivityViewport(out Camera viewport)
        {
            viewport = null;
            ActivityViewportController[] controllers = Object.FindObjectsByType<ActivityViewportController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] == null
                    || !controllers[i].isActiveAndEnabled
                    || !controllers[i].TryGetComponent(out Camera candidate)
                    || candidate == null
                    || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                if (viewport != null)
                    return false;

                viewport = candidate;
            }

            return viewport != null;
        }

        static void ObserveExperienceDropFlight(
            ActivityId activityId,
            CapabilityHostData experienceDropDefinition,
            ProjectionBindingController collectorProjection,
            Camera viewport,
            Dictionary<long, float> lastScreenDistances,
            Dictionary<long, Transform> projectionParents,
            ref bool sawProjection,
            ref bool sawFlight)
        {
            if (experienceDropDefinition == null
                || collectorProjection == null
                || viewport == null)
            {
                return;
            }

            Canvas canvas = collectorProjection.GetComponentInParent<Canvas>(true);
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 collectorScreenCoordinates = RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                collectorProjection.transform.position);
            List<CapabilityHostSnapshot> hosts = CapabilityHostService.GetAll();
            for (int i = 0; i < hosts.Count; i++)
            {
                CapabilityHostSnapshot host = hosts[i];
                if (host.ActivityId != activityId
                    || host.Definition == null
                    || !host.Definition.Matches(experienceDropDefinition)
                    || !TryFindProjectionBinding(
                        activityId,
                        host.EntityId,
                        out ProjectionBindingController dropProjection,
                        out ProjectionBindingContext dropContext)
                    || dropContext.ProjectionTarget == null
                    || dropContext.ProjectionTarget.CoordinateType != ProjectionCoordinateType.World)
                {
                    continue;
                }

                sawProjection = true;
                long entityValue = host.EntityId.Value;
                if (!projectionParents.TryGetValue(entityValue, out Transform expectedParent))
                {
                    projectionParents.Add(entityValue, dropProjection.transform.parent);
                }
                else
                {
                    Assert.AreSame(
                        expectedParent,
                        dropProjection.transform.parent,
                        "ExperienceDrop projection changed parent during its transition.");
                }

                Vector3 dropScreenCoordinates = viewport.WorldToScreenPoint(
                    dropProjection.transform.position);
                float screenDistance = Vector2.Distance(
                    new Vector2(dropScreenCoordinates.x, dropScreenCoordinates.y),
                    collectorScreenCoordinates);
                if (lastScreenDistances.TryGetValue(entityValue, out float previousDistance)
                    && screenDistance < previousDistance - 0.5f)
                {
                    sawFlight = true;
                }

                lastScreenDistances[entityValue] = screenDistance;
            }
        }

        static HashSet<long> GetActivityHostEntityValues(
            ActivityId activityId,
            CapabilityHostData expectedDefinition)
        {
            var values = new HashSet<long>();
            List<CapabilityHostSnapshot> hosts = CapabilityHostService.GetAll();
            for (int i = 0; i < hosts.Count; i++)
            {
                CapabilityHostSnapshot host = hosts[i];
                if (host.ActivityId == activityId
                    && host.Definition != null
                    && host.Definition.Matches(expectedDefinition))
                {
                    values.Add(host.EntityId.Value);
                }
            }

            return values;
        }

        static bool HasNewActivityHost(
            ActivityId activityId,
            CapabilityHostData expectedDefinition,
            HashSet<long> previousEntityValues)
        {
            List<CapabilityHostSnapshot> hosts = CapabilityHostService.GetAll();
            for (int i = 0; i < hosts.Count; i++)
            {
                CapabilityHostSnapshot host = hosts[i];
                if (host.ActivityId == activityId
                    && host.Definition != null
                    && host.Definition.Matches(expectedDefinition)
                    && !previousEntityValues.Contains(host.EntityId.Value))
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryFindActivityHost(
            ActivityId activityId,
            CapabilityHostData expectedDefinition,
            out Core.Entities.EntityId entityId)
        {
            entityId = Core.Entities.EntityId.Invalid;
            List<CapabilityHostSnapshot> hosts = CapabilityHostService.GetAll();
            hosts.Sort((left, right) => left.EntityId.Value.CompareTo(right.EntityId.Value));
            for (int i = 0; i < hosts.Count; i++)
            {
                CapabilityHostSnapshot host = hosts[i];
                if (host.ActivityId != activityId
                    || host.Definition == null
                    || !host.Definition.Matches(expectedDefinition))
                {
                    continue;
                }

                entityId = host.EntityId;
                return entityId.IsValid;
            }

            return false;
        }

        static long QueryAmount(
            ActivityRuntimeSnapshot activity,
            TaxonomyTermData walletTag,
            EconomyFormType formType,
            EconomyAssetData asset)
        {
            ActivityParticipantBinding participant =
                activity.Participants.Single(binding => binding.TeamIndex == 0);
            EconomySelectionQueryResult result = EconomyService.Query(
                new EconomySelectionQuery(
                    participant.ParticipantEconomyOwner,
                    new List<TaxonomyTermData> { walletTag },
                    formType,
                    asset,
                    includeZeroBalance: true));
            long amount = 0L;
            for (int itemIndex = 0; itemIndex < result.Items.Length; itemIndex++)
                amount += result.Items[itemIndex].Amount;

            return amount;
        }

        static string BuildPopulationDiagnostic(
            ActivityRuntimeSnapshot autobattle,
            ActivityRuntimeSnapshot board,
            TaxonomyTermData sharedWalletTag,
            EconomyAssetData turnToken,
            EconomyAssetData experience,
            HostValueData health,
            CapabilityHostData collectorDefinition,
            CapabilityHostData experienceDropDefinition)
        {
            var message = new StringBuilder(1024);
            message.Append("TurnToken=")
                .Append(QueryAmount(
                    board,
                    sharedWalletTag,
                    EconomyFormType.Stack,
                    turnToken))
                .AppendLine();
            message.Append("Experience=")
                .Append(QueryAmount(
                    autobattle,
                    sharedWalletTag,
                    EconomyFormType.Stack,
                    experience))
                .AppendLine();
            AppendAutobattleHostDiagnostic(
                message,
                autobattle,
                experience,
                health,
                collectorDefinition,
                experienceDropDefinition);

            for (int objectiveIndex = 0;
                 objectiveIndex < board.ObjectiveRuntimeIds.Count;
                 objectiveIndex++)
            {
                ObjectiveRuntimeId runtimeId = board.ObjectiveRuntimeIds[objectiveIndex];
                ObjectiveRuntimeSnapshot objective = ObjectiveService.GetSnapshot(
                    board.DomainId,
                    runtimeId);
                message.Append("Objective ")
                    .Append(runtimeId.Value)
                    .Append(": ");
                for (int nodeIndex = 0;
                     objective.Nodes != null && nodeIndex < objective.Nodes.Length;
                     nodeIndex++)
                {
                    if (nodeIndex > 0)
                        message.Append(", ");
                    message.Append(objective.Nodes[nodeIndex].NodeId)
                        .Append('=')
                        .Append(objective.Nodes[nodeIndex].State);
                }
                message.AppendLine();
            }

            List<OrchestrationGoalSnapshot> goals = OrchestrationService.GetGoals(board.DomainId);
            List<OrchestrationProcessSnapshot> processes =
                OrchestrationService.GetProcesses(board.DomainId);
            message.Append("Goals=").Append(goals.Count).AppendLine();
            for (int goalIndex = 0; goalIndex < goals.Count; goalIndex++)
            {
                OrchestrationGoalSnapshot goal = goals[goalIndex];
                message.Append("  Goal ")
                    .Append(goal.SourceObjectiveRuntimeId.Value)
                    .Append(" state=")
                    .Append(goal.State)
                    .Append(" message=")
                    .Append(goal.Message ?? "<null>")
                    .AppendLine();
                int processCount = 0;
                for (int processIndex = 0; processIndex < processes.Count; processIndex++)
                {
                    OrchestrationProcessSnapshot process = processes[processIndex];
                    if (process.ObjectiveRuntimeId != goal.SourceObjectiveRuntimeId)
                        continue;

                    message.Append("      Process id=")
                        .Append(process.ProcessId.ToString())
                        .Append(" fact=")
                        .Append(process.DesiredFactStableKey ?? "<null>")
                        .Append(" state=")
                        .Append(process.StateType)
                        .Append(" attempt=")
                        .Append(process.AttemptOrdinal)
                        .Append(" utility=")
                        .Append(process.Utility)
                        .Append(" endpoint=")
                        .Append(process.SelectedEndpointKey.IsValid
                            ? process.SelectedEndpointKey.ToString()
                            : "<null>")
                        .Append(" message=")
                        .Append(process.Message ?? "<null>")
                        .AppendLine();
                    processCount++;
                }

                if (processCount == 0)
                    message.AppendLine("      Processes=<empty>");
            }

            if (AgentService.TryGetAssignmentBoardSnapshot(
                    board.DomainId,
                    out AgentAssignmentBoardSnapshot assignments))
            {
                message.Append("Assignments=")
                    .Append(assignments.Assignments.Count)
                    .AppendLine();
                for (int assignmentIndex = 0;
                     assignmentIndex < assignments.Assignments.Count;
                     assignmentIndex++)
                {
                    AgentAssignmentSnapshot assignment =
                        assignments.Assignments[assignmentIndex];
                    message.Append("  Assignment agent=")
                        .Append(assignment.MatchedAgentId ?? "<null>")
                        .Append(" objective=")
                        .Append(assignment.ObjectiveId ?? "<null>")
                        .Append(" runtime=")
                        .Append(assignment.RuntimeId.Value)
                        .Append(" node=")
                        .Append(assignment.NodeId ?? "<null>")
                        .Append(" fact=")
                        .Append(assignment.DesiredFactType ?? "<null>")
                        .Append(" status=")
                        .Append(assignment.StatusType)
                        .Append(" generation=")
                        .Append(assignment.Generation)
                        .Append(" message=")
                        .Append(assignment.Message ?? "<null>")
                        .AppendLine();
                }
            }
            else
            {
                message.AppendLine("Assignments=<missing board>");
            }

            List<ProductionSnapshot> productions = ProductionService.GetSnapshots(board.DomainId);
            ActivityParticipantBinding participant = board.Participants.Single();
            message.Append("ParticipantOwner=")
                .Append(participant.ParticipantEconomyOwner == null
                    ? "<null>"
                    : participant.ParticipantEconomyOwner.StableSimulationKey)
                .AppendLine();
            message.Append("Productions=").Append(productions.Count).AppendLine();
            for (int productionIndex = 0; productionIndex < productions.Count; productionIndex++)
            {
                ProductionSnapshot production = productions[productionIndex];
                message.Append("  Production entity=")
                    .Append(production.EntityId.Value)
                    .Append(" definition=")
                    .Append(production.Definition == null ? "<null>" : production.Definition.Id)
                    .Append(" catalog=")
                    .Append(production.Catalog == null ? "<null>" : production.Catalog.Id)
                    .Append(" owner=")
                    .Append(production.Owner == null
                        ? "<null>"
                        : production.Owner.StableSimulationKey)
                    .Append(" selfOwner=")
                    .Append(production.SelfEconomyOwner == null
                        ? "<null>"
                        : production.SelfEconomyOwner.StableSimulationKey)
                    .Append(" enabled=")
                    .Append(production.Enabled)
                    .Append(" accepts=")
                    .Append(production.AcceptsOrders)
                    .Append(" queued=")
                    .Append(production.QueueCount)
                    .Append(" active=")
                    .Append(production.ActivePipelineCount)
                    .AppendLine();

                bool controlAvailable = EntityControlAuthorityService.CanClaim(
                    production.EntityId,
                    EntityControlOwnerType.Orchestration,
                    "chainrush-test-production-probe",
                    out string controlFailure);
                bool agentAllocated =
                    typeof(AgentService)
                        .GetMethod(
                            "IsExecutorAllocated",
                            BindingFlags.Static | BindingFlags.NonPublic)
                        ?.Invoke(null, new object[] { production.EntityId }) is true;
                message.Append("    Control available=")
                    .Append(controlAvailable)
                    .Append(" failure=")
                    .Append(controlFailure ?? "<none>")
                    .Append(" agentAllocated=")
                    .Append(agentAllocated)
                    .AppendLine();

                if (CapabilityHostService.TryGet(
                        production.EntityId,
                        out CapabilityHostSnapshot host))
                {
                    message.Append("    Host definition=")
                        .Append(host.Definition == null ? "<null>" : host.Definition.Id)
                        .Append(" owner=")
                        .Append(host.Owner == null ? "<null>" : host.Owner.StableSimulationKey)
                        .Append(" activity=")
                        .Append(host.ActivityId.Value)
                        .AppendLine();
                }
            }

            AppendProductionModuleDiagnostic(message, board);
            AppendProcessDiagnostic(message, board.DomainId);

            return message.ToString();
        }

        static string BuildSelectedEntityDiagnostic(
            IReadOnlyList<Core.Entities.EntityId> selectedEntities)
        {
            var message = new StringBuilder("SelectedEntities=[");
            for (int i = 0; selectedEntities != null && i < selectedEntities.Count; i++)
            {
                if (i > 0)
                    message.Append(',');
                message.Append(selectedEntities[i].Value)
                    .Append(":exists=")
                    .Append(CapabilityHostService.Exists(selectedEntities[i]));
            }
            return message.AppendLine("]").ToString();
        }

        static void EnableOrchestrationTraceDiagnostics()
        {
            RuntimeDiagnosticsProfile profile = default;
            object boxedProfile = profile;
            typeof(RuntimeDiagnosticsProfile)
                .GetField("orchestrationTrace", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(boxedProfile, true);
            profile = (RuntimeDiagnosticsProfile)boxedProfile;
            typeof(GameRuntimeDiagnostics)
                .GetField("_profile", BindingFlags.Static | BindingFlags.NonPublic)
                ?.SetValue(null, profile);
        }

        static string BuildExecutorDiagnostic(Core.Entities.EntityId entityId)
        {
            bool canClaim = EntityControlAuthorityService.CanClaim(
                entityId,
                EntityControlOwnerType.Orchestration,
                "board-selection-playmode-probe",
                out string controlFailure);
            System.Type serviceType = typeof(AgentService);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo reservedMethod = serviceType.GetMethod("IsExecutorReserved", flags);
            MethodInfo issuedMethod = serviceType.GetMethod("IsExecutorIssuedToRequest", flags);
            bool reserved = reservedMethod != null
                && (bool)reservedMethod.Invoke(null, new object[] { entityId });
            bool issued = issuedMethod != null
                && (bool)issuedMethod.Invoke(null, new object[] { entityId });

            var reservationOwners = new StringBuilder();
            FieldInfo domainsField = serviceType.GetField("Domains", flags);
            if (domainsField?.GetValue(null) is IEnumerable domains)
            {
                foreach (object domainEntry in domains)
                {
                    object domain = domainEntry.GetType().GetProperty("Value")?.GetValue(domainEntry);
                    FieldInfo runtimesField = domain?.GetType().GetField(
                        "_runtimesById",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (!(runtimesField?.GetValue(domain) is IEnumerable runtimes))
                        continue;

                    foreach (object runtimeEntry in runtimes)
                    {
                        object runtime = runtimeEntry.GetType().GetProperty("Value")?.GetValue(runtimeEntry);
                        object state = runtime?.GetType().GetProperty("State")?.GetValue(runtime);
                        if (state == null)
                            continue;

                        object definition = state.GetType().GetProperty("Definition")?.GetValue(state);
                        string agentId = definition?.GetType().GetProperty("AgentId")?.GetValue(definition)
                            as string;
                        object reservations = state.GetType().GetProperty("ExecutorReservations")?.GetValue(state);
                        if (!(reservations is IEnumerable reservationItems))
                            continue;

                        foreach (object reservation in reservationItems)
                        {
                            object reservedEntity = reservation.GetType()
                                .GetProperty("ExecutorEntityId")
                                ?.GetValue(reservation);
                            if (!(reservedEntity is Core.Entities.EntityId reservedId)
                                || reservedId != entityId)
                            {
                                continue;
                            }

                            reservationOwners
                                .Append(" ownerRuntime=")
                                .Append(state.GetType().GetProperty("AgentRuntimeId")?.GetValue(state) ?? "<null>")
                                .Append(" agent=")
                                .Append(agentId ?? "<null>")
                                .Append(" assignment=")
                                .Append(reservation.GetType().GetProperty("AssignmentId")?.GetValue(reservation) ?? "<null>")
                                .Append(" policy=")
                                .Append(reservation.GetType().GetProperty("PolicyType")?.GetValue(reservation) ?? "<null>")
                                .Append(" reason=")
                                .Append(reservation.GetType().GetProperty("ReasonKey")?.GetValue(reservation) ?? "<null>");
                        }
                    }
                }
            }

            return string.Concat(
                "BoardHostExecutor entity=",
                entityId.Value.ToString(),
                " canClaim=",
                canClaim.ToString(),
                " controlFailure=",
                controlFailure ?? "<null>",
                " reserved=",
                reserved.ToString(),
                " issued=",
                issued.ToString(),
                reservationOwners.ToString(),
                "\n");
        }

        static void AppendAutobattleHostDiagnostic(
            StringBuilder message,
            ActivityRuntimeSnapshot autobattle,
            EconomyAssetData experience,
            HostValueData health,
            CapabilityHostData collectorDefinition,
            CapabilityHostData experienceDropDefinition)
        {
            List<CapabilityHostSnapshot> hosts = CapabilityHostService.GetAll()
                .Where(host => host.ActivityId == autobattle.Id)
                .OrderBy(host => host.EntityId.Value)
                .ToList();
            message.Append("AutobattleHosts=").Append(hosts.Count).AppendLine();
            for (int hostIndex = 0; hostIndex < hosts.Count; hostIndex++)
            {
                CapabilityHostSnapshot host = hosts[hostIndex];
                message.Append("  Host entity=")
                    .Append(host.EntityId.Value)
                    .Append(" definition=")
                    .Append(host.Definition == null ? "<null>" : host.Definition.Id)
                    .Append(" placement=")
                    .Append(host.PlacementType);

                if (CapabilityHostService.TryGetHostValue(
                        host.EntityId,
                        health,
                        out HostValueSnapshot healthSnapshot))
                {
                    message.Append(" health=").Append(healthSnapshot.CurrentValue);
                }

                if (SpatialService.TryGetWorldPosition(host.EntityId, out WorldPosition position))
                    message.Append(" position=").Append(position.ToString());
                else
                    message.Append(" position=<none>");

                if (host.SelfEconomyOwner != null)
                {
                    EconomySelectionQueryResult payload = EconomyService.Query(
                        new EconomySelectionQuery(
                            host.SelfEconomyOwner,
                            new List<TaxonomyTermData>(0),
                            EconomyFormType.Stack,
                            experience,
                            includeZeroBalance: true));
                    long amount = 0L;
                    for (int itemIndex = 0; itemIndex < payload.Items.Length; itemIndex++)
                        amount += payload.Items[itemIndex].Amount;
                    if (amount != 0L)
                        message.Append(" localExperience=").Append(amount);
                }

                if (AIBrainService.TryGetState(host.EntityId, out AIBrainRuntimeState aiState))
                {
                    AIBrainNodeRuntimeState node = aiState.ActiveNode;
                    message.Append(" ai=")
                        .Append(aiState.ActiveBrain == null ? "<null>" : aiState.ActiveBrain.Id)
                        .Append(" state=")
                        .Append(node == null || node.CurrentState == null
                            ? "<null>"
                            : node.CurrentState.Id)
                        .Append(" completed=")
                        .Append(node != null && node.CurrentStateIsCompleted)
                        .Append(" result=")
                        .Append(node == null ? "<none>" : node.CurrentStateResultType.ToString())
                        .Append(" message=")
                        .Append(node == null || string.IsNullOrWhiteSpace(node.CurrentStateResultMessage)
                            ? "<null>"
                            : node.CurrentStateResultMessage);

                    foreach (KeyValuePair<TaxonomyTermData, AIBrainTargetControlData> target
                             in aiState.TargetControls)
                    {
                        if (target.Key == null || !target.Value.SelectedTargetEntityId.IsValid)
                            continue;
                        message.Append(" target[")
                            .Append(target.Key.Id)
                            .Append("]=")
                            .Append(target.Value.SelectedTargetEntityId.Value);
                    }
                }

                message.AppendLine();
            }

            CapabilityHostSnapshot? collector = hosts
                .Where(host => host.Definition != null
                    && host.Definition.Matches(collectorDefinition))
                .Cast<CapabilityHostSnapshot?>()
                .FirstOrDefault();
            List<CapabilityHostSnapshot> drops = hosts
                .Where(host => host.Definition != null
                    && host.Definition.Matches(experienceDropDefinition))
                .ToList();
            message.Append("CollectorTargetEligibility collector=")
                .Append(collector.HasValue ? collector.Value.EntityId.Value.ToString() : "<missing>")
                .Append(" owner=")
                .Append(collector.HasValue && collector.Value.Owner != null
                    ? collector.Value.Owner.StableSimulationKey
                    : "<null>")
                .Append(" tags=");
            AppendTargetTags(message, collector.HasValue ? collector.Value.EntityId : default);
            message.AppendLine();

            for (int dropIndex = 0; dropIndex < drops.Count; dropIndex++)
            {
                CapabilityHostSnapshot drop = drops[dropIndex];
                message.Append("  Drop entity=")
                    .Append(drop.EntityId.Value)
                    .Append(" owner=")
                    .Append(drop.Owner == null ? "<null>" : drop.Owner.StableSimulationKey)
                    .Append(" entityExists=")
                    .Append(Core.Entities.EntityService.Exists(drop.EntityId))
                    .Append(" hostExists=")
                    .Append(CapabilityHostService.Exists(drop.EntityId))
                    .Append(" tags=");
                AppendTargetTags(message, drop.EntityId);
                message.Append(" relation=");

                if (collector.HasValue
                    && DiplomacyService.TryGetRelation(
                        autobattle.Id,
                        collector.Value.EntityId,
                        drop.EntityId,
                        DiplomacyChannelType.Military,
                        out DiplomacyRelationSnapshot relation))
                {
                    message.Append(relation.Disposition)
                        .Append(" score=")
                        .Append(relation.Score)
                        .Append(" from=")
                        .Append(relation.FromAffiliation.ToString())
                        .Append(" to=")
                        .Append(relation.ToAffiliation.ToString());
                }
                else
                {
                    message.Append("<unavailable>");
                }

                message.Append(" eligibility=");
                if (collector.HasValue
                    && TryEvaluateCollectorTarget(
                        autobattle.Id,
                        collector.Value.EntityId,
                        drop.EntityId,
                        out string eligibility))
                {
                    message.Append(eligibility);
                }
                else
                {
                    message.Append("<diagnostic-unavailable>");
                }

                message.AppendLine();
            }
        }

        static bool TryEvaluateCollectorTarget(
            ActivityId activityId,
            Core.Entities.EntityId collectorEntityId,
            Core.Entities.EntityId dropEntityId,
            out string result)
        {
            result = null;
            if (!AIBrainService.TryGetState(
                    collectorEntityId,
                    out AIBrainRuntimeState collectorState)
                || collectorState?.ActiveBrain == null)
            {
                return false;
            }

            SelectActivityTargetAIBrainActionData selector = collectorState.ActiveBrain.Nodes
                .SelectMany(node => node.States)
                .SelectMany(state => state.OnEnterActions)
                .OfType<SelectActivityTargetAIBrainActionData>()
                .FirstOrDefault();
            if (selector == null)
                return false;

            MethodInfo createPolicy = typeof(SelectActivityTargetAIBrainActionData).GetMethod(
                "CreateEligibilityPolicy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            System.Type utilityType = typeof(AIBrainService).Assembly.GetType(
                "Core.AI.AIBrainTargetEligibilityUtility");
            MethodInfo evaluate = utilityType?.GetMethod(
                "TryEvaluateTarget",
                BindingFlags.Static | BindingFlags.Public);
            if (createPolicy == null || evaluate == null)
                return false;

            var policy = createPolicy.Invoke(selector, null) as AIBrainTargetEligibilityPolicy;
            if (policy == null)
                return false;

            object[] arguments =
            {
                activityId,
                collectorEntityId,
                dropEntityId,
                policy,
                null,
            };
            try
            {
                bool eligible = (bool)evaluate.Invoke(null, arguments);
                result = eligible
                    ? "eligible"
                    : arguments[4] as string ?? "rejected without reason";
                return true;
            }
            catch (TargetInvocationException exception)
            {
                result = string.Concat(
                    "diagnostic threw ",
                    exception.InnerException?.GetType().Name ?? exception.GetType().Name,
                    ": ",
                    exception.InnerException?.Message ?? exception.Message);
                return true;
            }
        }

        static void AppendTargetTags(StringBuilder message, Core.Entities.EntityId entityId)
        {
            if (!entityId.IsValid
                || !CapabilityHostService.TryGetTargetTags(
                    entityId,
                    out List<TaxonomyTermData> tags))
            {
                message.Append("<unavailable>");
                return;
            }

            message.Append('[');
            for (int tagIndex = 0; tagIndex < tags.Count; tagIndex++)
            {
                if (tagIndex > 0)
                    message.Append(',');
                message.Append(tags[tagIndex] == null ? "<null>" : tags[tagIndex].Id);
            }
            message.Append(']');
        }

        static void AppendProcessDiagnostic(
            StringBuilder message,
            Core.Runtime.RuntimeDomainId domainId)
        {
            List<OrchestrationProcessSnapshot> processes =
                OrchestrationService.GetProcesses(domainId);
            message.Append("Processes=")
                .Append(processes.Count)
                .AppendLine();
            for (int i = 0; i < processes.Count; i++)
            {
                OrchestrationProcessSnapshot process = processes[i];
                message.Append("  Process id=")
                    .Append(process.ProcessId.ToString())
                    .Append(" objective=")
                    .Append(process.ObjectiveRuntimeId.Value)
                    .Append(" fact=")
                    .Append(process.DesiredFactStableKey ?? "<null>")
                    .Append(" status=")
                    .Append(process.StateType)
                    .Append(" revision=")
                    .Append(process.FactRevision)
                    .Append(" attempt=")
                    .Append(process.AttemptOrdinal)
                    .Append(" endpoint=")
                    .Append(process.SelectedEndpointKey.IsValid
                        ? process.SelectedEndpointKey.ToString()
                        : "<null>")
                    .Append(" message=")
                    .Append(process.Message ?? "<null>")
                    .AppendLine();
            }
        }

        static void AppendProductionModuleDiagnostic(
            StringBuilder message,
            ActivityRuntimeSnapshot board)
        {
            ActivityData definition = AssetDatabase.LoadAssetAtPath<ActivityData>(BoardActivityPath);
            List<OrchestrationParticipantSnapshot> participants =
                OrchestrationService.GetParticipants(board.DomainId);
            if (definition == null
                || board.Objectives.Count == 0
                || participants.Count == 0
                || board.Participants.Count == 0)
            {
                message.AppendLine("ProductionMethods=<diagnostic context unavailable>");
                return;
            }

            ActivityObjectiveRuntimeSnapshot objective = board.Objectives[0];
            ObjectiveRuntimeSnapshot objectiveRuntime = ObjectiveService.GetSnapshot(
                board.DomainId,
                objective.RuntimeId);
            var module = new ProductionStateOrchestrationModule();
            var registry = new OrchestrationModuleResultRegistry();
            module.Collect(
                new OrchestrationModuleExecutionContext(
                    board.Id,
                    board.DomainId,
                    definition,
                    objective,
                    objectiveRuntime,
                    participants[0],
                    board.Participants[0]),
                registry);

            var planningContext = default(OrchestrationPlanningRuntimeContext);
            if (!registry.TryGet(out IProductionStateModuleResult productionState)
                || !productionState.TryGetProductionMethods(
                    planningContext,
                    out List<OrchestrationProductionMethodSnapshot> methods))
            {
                message.AppendLine("ProductionMethods=<unavailable>");
                return;
            }

            message.Append("ProductionMethods=").Append(methods.Count).AppendLine();
            for (int methodIndex = 0; methodIndex < methods.Count; methodIndex++)
            {
                OrchestrationProductionMethodSnapshot method = methods[methodIndex];
                message.Append("  Method entity=")
                    .Append(method.ProductionEntityId.Value)
                    .Append(" host=")
                    .Append(method.ProducerHost == null ? "<null>" : method.ProducerHost.Id)
                    .Append(" production=")
                    .Append(method.Production == null ? "<null>" : method.Production.Id)
                    .Append(" catalog=")
                    .Append(method.Catalog == null ? "<null>" : method.Catalog.Id)
                    .Append(" recipe=")
                    .Append(method.Recipe == null ? "<null>" : method.Recipe.Id)
                    .Append(" outputIndex=")
                    .Append(method.OutputIndex)
                    .AppendLine();
            }
        }

        static bool TryFindRunningActivities(
            out ActivityRuntimeSnapshot autobattle,
            out ActivityRuntimeSnapshot board)
        {
            autobattle = default;
            board = default;
            bool hasAutobattle = false;
            bool hasBoard = false;
            List<ActivityRuntimeSnapshot> snapshots = ActivityService.GetAll();
            for (int i = 0; i < snapshots.Count; i++)
            {
                ActivityRuntimeSnapshot snapshot = snapshots[i];
                string activityTypeId = snapshot.ActivityType == null
                    ? null
                    : snapshot.ActivityType.Id;
                if (activityTypeId == AutobattleActivityTypeId)
                {
                    autobattle = snapshot;
                    hasAutobattle = snapshot.State == ActivityState.Running;
                }
                else if (activityTypeId == BoardActivityTypeId)
                {
                    board = snapshot;
                    hasBoard = snapshot.State == ActivityState.Running;
                }
            }

            return hasAutobattle && hasBoard;
        }

        static void AssertParticipant(
            IReadOnlyList<ActivityParticipantBinding> participants,
            int teamIndex,
            PlayerControlType controlType)
        {
            ActivityParticipantBinding participant =
                participants.Single(binding => binding.TeamIndex == teamIndex);
            Assert.AreEqual(controlType, participant.ControlType);
        }

        sealed class SelectionResultCapture : IEventListener<SelectionResultEvent>
        {
            readonly SelectionRequestId _requestId;

            public SelectionResultCapture(SelectionRequestId requestId)
            {
                _requestId = requestId;
            }

            public int Count { get; private set; }
            public SelectionResultEvent Result { get; private set; }

            public void OnEvent(SelectionResultEvent e)
            {
                if (e.RequestId != _requestId)
                    return;

                Count++;
                Result = e;
            }
        }

        sealed class ProductionOrderCapture :
            IEventListener<ProductionOrderStartedEvent>,
            IEventListener<ProductionOrderFinishedEvent>,
            IEventListener<OrchestrationProcessStateChangedEvent>,
            IEventListener<ObjectiveNodeStateChangedEvent>,
            IEventListener<ObjectiveRuntimeCompletedEvent>,
            IEventListener<ObjectiveRuntimeResetEvent>
        {
            readonly List<string> _recipeIds = new List<string>(4);
            readonly List<string> _finishedOrders = new List<string>(4);
            readonly List<string> _processTransitions = new List<string>(16);
            readonly List<string> _objectiveTransitions = new List<string>(8);
            readonly RuntimeDomainId _domainId;
            readonly Core.Entities.EntityId _productionEntityId;

            public ProductionOrderCapture(
                RuntimeDomainId domainId,
                Core.Entities.EntityId productionEntityId)
            {
                _domainId = domainId;
                _productionEntityId = productionEntityId;
            }

            public int StartedCount => _recipeIds.Count;

            public void OnEvent(ProductionOrderStartedEvent e)
            {
                if (e.ProductionEntityId == _productionEntityId)
                    _recipeIds.Add(e.RecipeId);
            }

            public void OnEvent(ProductionOrderFinishedEvent e)
            {
                if (e.ProductionEntityId != _productionEntityId)
                    return;

                _finishedOrders.Add(string.Concat(
                    e.OrderId.ToString(),
                    ":",
                    e.RecipeId,
                    ":",
                    e.FinalStatus.ToString(),
                    ":",
                    e.Failure.Code ?? "<none>",
                    ":",
                    e.Failure.Message ?? "<none>"));
            }

            public void OnEvent(OrchestrationProcessStateChangedEvent e)
            {
                OrchestrationProcessSnapshot snapshot = e.Snapshot;
                if (snapshot.DomainId != _domainId)
                    return;

                _processTransitions.Add(string.Concat(
                    snapshot.ProcessId.ToString(),
                    ":",
                    snapshot.ObjectiveRuntimeId.Value.ToString(),
                    ":",
                    snapshot.AttemptOrdinal.ToString(),
                    ":",
                    e.PreviousStateType.ToString(),
                    "->",
                    snapshot.StateType.ToString(),
                    ":",
                    snapshot.SelectedEndpointKey.IsValid
                        ? snapshot.SelectedEndpointKey.ToString()
                        : "<none>",
                    ":",
                    snapshot.Message ?? "<none>"));
                if (_processTransitions.Count > 64)
                    _processTransitions.RemoveAt(0);
            }

            public void OnEvent(ObjectiveNodeStateChangedEvent e)
            {
                if (e.DomainId != _domainId)
                    return;

                AddObjectiveTransition(string.Concat(
                    e.RuntimeId.Value.ToString(),
                    ":node:",
                    e.NodeId ?? "<none>",
                    ":",
                    e.OldState.ToString(),
                    "->",
                    e.NewState.ToString()));
            }

            public void OnEvent(ObjectiveRuntimeCompletedEvent e)
            {
                if (e.DomainId == _domainId)
                    AddObjectiveTransition(string.Concat(e.RuntimeId.Value.ToString(), ":completed"));
            }

            public void OnEvent(ObjectiveRuntimeResetEvent e)
            {
                if (e.DomainId == _domainId)
                    AddObjectiveTransition(string.Concat(e.RuntimeId.Value.ToString(), ":reset"));
            }

            void AddObjectiveTransition(string value)
            {
                _objectiveTransitions.Add(value);
                if (_objectiveTransitions.Count > 32)
                    _objectiveTransitions.RemoveAt(0);
            }

            public int CountRecipe(string recipeId)
            {
                int count = 0;
                for (int i = 0; i < _recipeIds.Count; i++)
                {
                    if (string.Equals(
                            _recipeIds[i],
                            recipeId,
                            System.StringComparison.Ordinal))
                    {
                        count++;
                    }
                }

                return count;
            }

            public string BuildDiagnostic()
            {
                return string.Concat(
                    "BoardProductionRecipes=[",
                    _recipeIds.Count == 0 ? "<none>" : string.Join(",", _recipeIds),
                    "]\nBoardProductionFinished=[",
                    _finishedOrders.Count == 0 ? "<none>" : string.Join(",", _finishedOrders),
                    "]\nBoardProcessTransitions=[",
                    _processTransitions.Count == 0 ? "<none>" : string.Join("\n", _processTransitions),
                    "]\nBoardObjectiveTransitions=[",
                    _objectiveTransitions.Count == 0 ? "<none>" : string.Join("\n", _objectiveTransitions),
                    "]\n");
            }
        }
    }
}
