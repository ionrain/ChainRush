using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Core.Activities;
using Core.Activities.Events;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Events;
using Core.GameRuntime;
using Core.Objectives;
using Core.Orchestration;
using Core.Players;
using Core.Production;
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
        const string BoardMergeSkillPath =
            "Assets/Game/Activities/Board/Skills/BoardMergeSkill.asset";
        const string BoardTurnTokenPath =
            "Assets/Game/Activities/Shared/Economy/BoardTurnToken.asset";
        const string WaterUnitPath =
            "Assets/Game/Activities/Shared/Units/Water/WaterUnit.asset";
        const string SharedWalletTagPath =
            "Assets/Game/Activities/Shared/Economy/ActivityWalletTag.asset";
        const string AutobattleActivityTypeId = "chainrush.activity-type.autobattle";
        const string BoardActivityTypeId = "chainrush.activity-type.board";
        const float StartupTimeoutSeconds = 10f;

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

            Assert.IsFalse(autobattle.ParentActivityId.IsValid);
            Assert.AreEqual(2, autobattle.Participants.Count);
            AssertParticipant(autobattle.Participants, 0, PlayerControlType.LocalHuman);
            AssertParticipant(autobattle.Participants, 1, PlayerControlType.Bot);

            Assert.AreEqual(autobattle.Id, board.ParentActivityId);
            Assert.AreEqual(1, board.Participants.Count);
            AssertParticipant(board.Participants, 0, PlayerControlType.LocalHuman);
            Assert.AreEqual(1, board.ObjectiveRuntimeIds.Count);
            Assert.AreEqual(
                1,
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

            CapabilityHostData waterBase =
                AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardWaterBasePath);
            EconomyAssetData turnToken =
                AssetDatabase.LoadAssetAtPath<EconomyAssetData>(BoardTurnTokenPath);
            TaxonomyTermData sharedWalletTag =
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(SharedWalletTagPath);
            Assert.NotNull(waterBase);
            Assert.NotNull(turnToken);
            Assert.NotNull(sharedWalletTag);

            float populationDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (Time.realtimeSinceStartup < populationDeadline
                && (CountMaterializedBoardAssets(board, boardCellTag, waterBase) != 16
                    || QueryAmount(
                        board,
                        sharedWalletTag,
                        EconomyFormType.Stack,
                        turnToken) != 0L))
            {
                yield return null;
            }

            Assert.AreEqual(
                16,
                CountMaterializedBoardAssets(board, boardCellTag, waterBase),
                string.Concat(
                    "Population orchestration did not materialize one Water base on every Board marker.\n",
                    BuildPopulationDiagnostic(board, sharedWalletTag, turnToken)));
            Assert.AreEqual(
                0L,
                QueryAmount(board, sharedWalletTag, EconomyFormType.Stack, turnToken),
                "Board refresh production did not consume the seeded turn token.");

            CapabilityHostData boardHost =
                AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardHostPath);
            SkillData mergeSkill = AssetDatabase.LoadAssetAtPath<SkillData>(BoardMergeSkillPath);
            EconomyAssetData waterUnit =
                AssetDatabase.LoadAssetAtPath<EconomyAssetData>(WaterUnitPath);
            Assert.NotNull(boardHost);
            Assert.NotNull(mergeSkill);
            Assert.NotNull(waterUnit);
            Assert.IsTrue(
                TryFindBoardHost(board.Id, boardHost, out Core.Entities.EntityId boardHostEntityId),
                "The hidden Board host was not registered for the Board Activity.");
            List<Core.Entities.EntityId> selectedEntities = ResolveFirstMarkerTriple(
                board,
                boardCellTag,
                waterBase);
            Assert.AreEqual(3, selectedEntities.Count);

            SimulationControlIntentEvent mergeRequest = SimulationControlIntentEvent.ActivateSkillEntities(
                board.Id,
                boardHostEntityId,
                mergeSkill,
                selectedEntities);
            Assert.IsTrue(mergeRequest.RequestId.IsValid);
            EventBus.Trigger(mergeRequest);

            float mergeDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (Time.realtimeSinceStartup < mergeDeadline
                && (QueryAmount(board, sharedWalletTag, EconomyFormType.Token, waterUnit) != 1L
                    || CountMaterializedBoardAssets(board, boardCellTag, waterBase) != 13))
            {
                yield return null;
            }

            Assert.AreEqual(
                1L,
                QueryAmount(board, sharedWalletTag, EconomyFormType.Token, waterUnit),
                "Merge production did not issue exactly one Water unit token to the shared wallet.");
            Assert.AreEqual(
                13,
                CountMaterializedBoardAssets(board, boardCellTag, waterBase),
                "Merge production did not consume exactly the three selected Board tokens.");
            for (int selectedIndex = 0; selectedIndex < selectedEntities.Count; selectedIndex++)
            {
                Assert.IsFalse(
                    CapabilityHostService.Exists(selectedEntities[selectedIndex]),
                    "A selected Board token remained materialized after committed merge production.");
            }

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

        static List<Core.Entities.EntityId> ResolveFirstMarkerTriple(
            ActivityRuntimeSnapshot board,
            TaxonomyTermData markerTag,
            CapabilityHostData expectedAsset)
        {
            List<SpatialMarkerSnapshot> markers = SpatialMarkerService.GetMarkers(
                board.Id,
                board.ActivityRootEntityId,
                new List<TaxonomyTermData> { markerTag });
            List<SpatialMarkerSnapshot> selectedMarkers = markers
                .OrderBy(marker => marker.LocalIndex)
                .Take(3)
                .ToList();
            var selected = new List<Core.Entities.EntityId>(3);
            for (int markerIndex = 0; markerIndex < selectedMarkers.Count; markerIndex++)
            {
                Core.Entities.EntityId[] occupants =
                    SpatialService.GetOccupants(selectedMarkers[markerIndex].WorldPosition);
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
                    selected.Add(match);
            }

            return selected;
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

        static long QueryAmount(
            ActivityRuntimeSnapshot board,
            TaxonomyTermData walletTag,
            EconomyFormType formType,
            EconomyAssetData asset)
        {
            ActivityParticipantBinding participant = board.Participants.Single();
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
            ActivityRuntimeSnapshot board,
            TaxonomyTermData sharedWalletTag,
            EconomyAssetData turnToken)
        {
            var message = new StringBuilder(1024);
            message.Append("TurnToken=")
                .Append(QueryAmount(
                    board,
                    sharedWalletTag,
                    EconomyFormType.Stack,
                    turnToken))
                .AppendLine();

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
                if (!OrchestrationService.TryGetPlanTrace(
                        board.DomainId,
                        goal.ParticipantStableKey,
                        goal.SourceObjectiveRuntimeId,
                        out OrchestrationPlanTraceSnapshot trace))
                {
                    message.AppendLine("    PlanTrace=<missing>");
                    continue;
                }

                message.Append("    Plan facts=")
                    .Append(trace.PlanningFacts.Count)
                    .Append(" endpoints=")
                    .Append(trace.PlanningEndpoints.Count)
                    .Append(" diagnostics=")
                    .Append(trace.Diagnostics.Count)
                    .AppendLine();
                int diagnosticLimit = Mathf.Min(4, trace.Diagnostics.Count);
                for (int diagnosticIndex = 0;
                     diagnosticIndex < diagnosticLimit;
                     diagnosticIndex++)
                {
                    OrchestrationPlanDiagnosticSnapshot diagnostic =
                        trace.Diagnostics[diagnosticIndex];
                    message.Append("      ")
                        .Append(diagnostic.Code ?? "<null>")
                        .Append(": ")
                        .Append(diagnostic.Message ?? "<null>")
                        .AppendLine();
                }
                if (trace.Diagnostics.Count > diagnosticLimit)
                {
                    message.Append("      ... omitted diagnostics=")
                        .Append(trace.Diagnostics.Count - diagnosticLimit)
                        .AppendLine();
                }
            }

            if (ActivityAgentService.TryGetAssignmentBoardSnapshot(
                    board.DomainId,
                    out ActivityAgentAssignmentBoardSnapshot assignments))
            {
                message.Append("Assignments=")
                    .Append(assignments.Assignments.Count)
                    .AppendLine();
                for (int assignmentIndex = 0;
                     assignmentIndex < assignments.Assignments.Count;
                     assignmentIndex++)
                {
                    ActivityAgentAssignmentSnapshot assignment =
                        assignments.Assignments[assignmentIndex];
                    message.Append("  Assignment agent=")
                        .Append(assignment.MatchedAgentId ?? "<null>")
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
            AppendBranchDiagnostic(message, board.DomainId);

            return message.ToString();
        }

        static void AppendBranchDiagnostic(
            StringBuilder message,
            Core.Runtime.RuntimeDomainId domainId)
        {
            FieldInfo domainsField = typeof(OrchestrationService).GetField(
                "Domains",
                BindingFlags.Static | BindingFlags.NonPublic);
            object domains = domainsField == null ? null : domainsField.GetValue(null);
            object domainState = FindDictionaryValue(domains as IEnumerable, domainId);
            object plannerStates = GetPropertyValue(domainState, "PlannerStates");
            message.AppendLine("Branches:");
            foreach (object plannerEntry in plannerStates as IEnumerable ?? new object[0])
            {
                object plannerState = GetPropertyValue(plannerEntry, "Value");
                object branches = GetPropertyValue(plannerState, "Branches");
                foreach (object branchEntry in branches as IEnumerable ?? new object[0])
                {
                    object branchState = GetPropertyValue(branchEntry, "Value");
                    object planState = GetPropertyValue(branchState, "PlanState");
                    object awaitedFacts = GetPropertyValue(planState, "AwaitedFacts");
                    object contextValue = GetPropertyValue(planState, "Context");
                    OrchestrationPlanningRuntimeContext planningContext =
                        contextValue is OrchestrationPlanningRuntimeContext typedContext
                            ? typedContext
                            : default;
                    message.Append("  AwaitedFacts:").AppendLine();
                    foreach (object awaited in awaitedFacts as IEnumerable ?? new object[0])
                    {
                        if (!(awaited is AwaitedFactRecord record))
                            continue;
                        message.Append("    type=")
                            .Append(record.Fact == null ? "<null>" : record.Fact.FactType.ToString())
                            .Append(" data=")
                            .Append(record.Fact == null || record.Fact.Data == null
                                ? "<null>"
                                : record.Fact.Data.GetType().Name)
                            .Append(" key=")
                            .Append(record.FactKey.ToString())
                            .Append(" reason=")
                            .Append(record.Reason ?? "<null>")
                            .AppendLine();
                    }

                    message.Append("  InFlightOperationDeltas=")
                        .Append(planningContext.InFlightOperationDeltas.Count)
                        .AppendLine();
                    for (int deltaSetIndex = 0;
                         deltaSetIndex < planningContext.InFlightOperationDeltas.Count;
                         deltaSetIndex++)
                    {
                        OperationDeltaSet deltaSet =
                            planningContext.InFlightOperationDeltas[deltaSetIndex];
                        IReadOnlyList<EconomyOperationDelta> economyDeltas =
                            deltaSet.Get<EconomyOperationDelta>();
                        for (int deltaIndex = 0; deltaIndex < economyDeltas.Count; deltaIndex++)
                        {
                            EconomyOperationDelta delta = economyDeltas[deltaIndex];
                            message.Append("    Economy operation=")
                                .Append(delta.Operation)
                                .Append(" asset=")
                                .Append(delta.Asset == null ? "<null>" : delta.Asset.Id)
                                .Append(" amount=")
                                .Append(delta.Amount)
                                .Append(" owner=")
                                .Append(delta.OwnerStableKey ?? "<null>")
                                .Append(" source=")
                                .Append(delta.SourceStableKey ?? "<null>")
                                .AppendLine();
                        }
                    }

                    object rootFrames = GetPropertyValue(planState, "RootFrames");
                    foreach (object frame in rootFrames as IEnumerable ?? new object[0])
                    {
                        if (frame is OrchestrationBuildFrame buildFrame)
                            AppendFrameDiagnostic(message, buildFrame, planningContext, "  ");
                    }
                }
            }
        }

        static void AppendFrameDiagnostic(
            StringBuilder message,
            OrchestrationBuildFrame frame,
            in OrchestrationPlanningRuntimeContext planningContext,
            string indent)
        {
            message.Append(indent)
                .Append("Frame type=")
                .Append(frame.DesiredFact == null ? "<null>" : frame.DesiredFact.FactType.ToString())
                .Append(" data=")
                .Append(frame.DesiredFact == null || frame.DesiredFact.Data == null
                    ? "<null>"
                    : frame.DesiredFact.Data.GetType().Name)
                .Append(" status=")
                .Append(frame.Status)
                .Append(" raw=")
                .Append(frame.HasCurrentSnapshot
                    ? frame.CurrentSnapshot.RawValue.ToString()
                    : "<none>")
                .Append(" resolution=")
                .Append(frame.HasCurrentSnapshot
                    ? frame.CurrentSnapshot.ResolutionState.ToString()
                    : "<none>")
                .Append(" intent=")
                .Append(frame.ExecutionIntentReady)
                .Append(" endpoint=")
                .Append(frame.SelectedEndpoint == null
                    ? "<null>"
                    : frame.SelectedEndpoint.EndpointType.ToString())
                .Append(" failure=")
                .Append(frame.Failure ?? "<null>")
                .AppendLine();
            if (frame.DesiredFact.TryGetData(out EconomyOrchestrationQueryData economy)
                && planningContext.ModuleResults.TryGet(
                    out IEconomyStateModuleResult economyState))
            {
                message.Append(indent)
                    .Append("  EconomyModuleAmount=")
                    .Append(economyState.TryGetAmount(economy, out long moduleAmount)
                        ? moduleAmount.ToString()
                        : "<unresolved>")
                    .AppendLine();
            }
            for (int i = 0; i < frame.RequiredFrames.Count; i++)
            {
                AppendFrameDiagnostic(
                    message,
                    frame.RequiredFrames[i],
                    planningContext,
                    string.Concat(indent, "  "));
            }
        }

        static object FindDictionaryValue(IEnumerable dictionary, object expectedKey)
        {
            foreach (object entry in dictionary ?? new object[0])
            {
                object key = GetPropertyValue(entry, "Key");
                if (Equals(key, expectedKey))
                    return GetPropertyValue(entry, "Value");
            }

            return null;
        }

        static object GetPropertyValue(object source, string propertyName)
        {
            if (source == null)
                return null;
            PropertyInfo property = source.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property == null ? null : property.GetValue(source);
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
    }
}
