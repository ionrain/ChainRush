using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using Core.AI;
using Core.Activities;
using Core.Activities.Events;
using Core.Activities.Selection;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Determinism;
using Core.Diplomacy;
using Core.Drops;
using Core.Economy;
using Core.Events;
using Core.GameRuntime;
using Core.GameFlow;
using Core.HostValues;
using Core.Objectives;
using Core.Objectives.Events;
using Core.Orchestration;
using Core.Players;
using Core.Pooling;
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
        const string BoardHostPath =
            "Assets/Game/Activities/Board/Economy/BoardHost.asset";
        const string BoardMergeSelectionPath =
            "Assets/Game/Activities/Board/Taxonomy/BoardMergeSelection.asset";
        const string BoardTurnTokenPath =
            "Assets/Game/Activities/Shared/Economy/BoardTurnToken.asset";
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

        static readonly List<int> BoardSelectionStarts = new List<int> { 1, 7, 13 };

        [UnityTearDown]
        public IEnumerator TearDownRuntime()
        {
            GameFlowService.ResetRuntime();
            ActivityLauncher.ResetRuntime();
            ActivityService.ResetRuntime();
            ProjectionService.ResetRuntime();
            _restoreBoardContent?.Invoke();
            _restoreBoardContent = null;
            _restoreBattleSeeds?.Invoke();
            _restoreBattleSeeds = null;

            foreach (GameRuntimeHost host in Object.FindObjectsByType<GameRuntimeHost>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                typeof(GameRuntimeHost).GetField(
                    "_persistentEconomySaved", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(host, true);
                Object.Destroy(host.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator BoardUI_RegionHolesKeepViewAndProjectionCoordinates()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Activities/Board/UI/BoardUI.prefab");
            Assert.NotNull(prefab);
            GameObject instance = Object.Instantiate(prefab);
            var presentation = instance.GetComponent<Core.UI.Flow.UIPresentationController>();
            var probe = new GameObject("projection-probe");
            try
            {
                var settings = new UIProjectionSettingsData();
                typeof(UIProjectionSettingsData).GetField("cellSize", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(settings, new Vector2(140, 140));
                typeof(UIProjectionSettingsData).GetField("spacing", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(settings, new Vector2(10, 10));
                var spaceId = new WorldSpaceId(731);
                var cells = new List<ActivityUICell>
                {
                    new ActivityUICell(0, new Vector2Int(0, 0), new WorldPosition(spaceId, 1), Vector3.zero),
                    new ActivityUICell(1, new Vector2Int(2, 0), new WorldPosition(spaceId, 2), new Vector3(2, 0, 0)),
                    new ActivityUICell(2, new Vector2Int(0, 2), new WorldPosition(spaceId, 3), new Vector3(0, 0, 2)),
                };
                var projection = new UIProjectionTarget(3);
                var context = new ActivityUIContext(new ActivityId(731), default, cells, settings, projection);
                Assert.IsTrue(presentation.TryApplyUIContext(context));
                Assert.IsTrue(projection.IsReady);
                var expected = new List<Vector2> { new Vector2(-150, 150), new Vector2(150, 150), new Vector2(-150, -150) };
                for (int i = 0; i < cells.Count; i++)
                {
                    Assert.IsTrue(projection.TryApplyPose(probe.transform, cells[i].WorldPosition,
                        cells[i].Coordinates, Quaternion.identity, Vector3.one));
                    var view = (RectTransform)probe.transform.parent;
                    Assert.AreEqual(expected[i], view.anchoredPosition);
                    Assert.AreEqual(settings.CellSize, view.sizeDelta);
                }
                Assert.IsFalse(projection.TryApplyPose(probe.transform, new WorldPosition(spaceId, 4),
                    Vector3.one, Quaternion.identity, Vector3.one));
                Assert.IsNull(instance.GetComponentInChildren<UnityEngine.UI.GridLayoutGroup>());
            }
            finally
            {
                Object.Destroy(probe);
                Object.Destroy(instance);
            }
            yield return null;
        }

        const string PerfumePath = "Assets/Game/Activities/Shared/Units/Perfume/Perfume.asset";
        System.Action _restoreBoardContent;

        [UnityTest]
        public IEnumerator RuntimeComposition_LaunchesBoardOnceAndParentCloseClosesIt()
        {
            var capture = new PlayableRuntimeCapture();
            capture.Register();
            try
            {
                yield return LaunchPlayableActivities();
                Assert.IsTrue(TryFindRunningActivities(out var battle, out var board));
                var hero = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(PerfumePath);
                float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                while (!TryFindActivityHost(battle.Id, hero, out _) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.IsTrue(TryFindActivityHost(battle.Id, hero, out var heroEntity), "Perfume was not materialized.");
                Assert.IsTrue(SpatialService.TryGetPose(heroEntity, out var pose));
                var heroMarkerTag = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>("Assets/Game/Activities/Autobattle/Taxonomy/HeroSpawn.asset");
                var heroMarker = SpatialMarkerService.GetMarkers(battle.Id, battle.ActivityRootEntityId,
                    new List<TaxonomyTermData> { heroMarkerTag }).Single();
                Assert.AreEqual(heroMarker.WorldPosition, pose.WorldPosition);
                deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline && !TryFindProjectionBinding(battle.Id, heroEntity, out _))
                    yield return null;
                Assert.IsTrue(TryFindProjectionBinding(battle.Id, heroEntity, out _), "Perfume has no projected view.");
                Assert.IsFalse(hero.SupportsCapability(CapabilityHostType.MovementOwner));
                Assert.IsFalse(CapabilityHostService.GetAll().Any(host => host.ActivityId == battle.Id
                    && host.Definition != null && (host.Definition.Id.StartsWith("chainrush.unit.water")
                        || host.Definition.Id.StartsWith("chainrush.unit.cola"))),
                    "Ordinary units must not exist before a Board selection.");

                deadline = Time.realtimeSinceStartup + CollectorCycleTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline
                    && (!capture.Hits.Any(hit => hit.OwnerEntityId == heroEntity) || capture.CompletedDrops == 0))
                {
                    yield return null;
                    Assert.IsTrue(SpatialService.TryGetPose(heroEntity, out var current));
                    Assert.AreEqual(pose.Coordinates, current.Coordinates, "Perfume moved while firing.");
                }
                Assert.IsTrue(capture.Hits.Any(hit => hit.OwnerEntityId == heroEntity),
                    "Perfume did not land a carried-skill hit.\n" + BuildExecutorDiagnostic(heroEntity));
                Assert.Greater(capture.CompletedDrops, 0, "Real combat did not complete an enemy defeat/drop.");
                Assert.IsNull(capture.Failure);
                var cellTag = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardCellTagPath);
                deadline = Time.realtimeSinceStartup + CollectorCycleTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline && CountMaterializedBoardAssets(board, cellTag, null) != 16)
                    yield return null;
                Assert.AreEqual(16, CountMaterializedBoardAssets(board, cellTag, null),
                    "Combat -> Experience -> turn payment -> Population did not fill the Board.\n" + BuildOrchestrationDiagnostic(board.DomainId));
                var activation = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardActivationTermPath);
                EventBus.Trigger(new ActivityChildActivationEvent(battle.Id, new List<TaxonomyTermData> { activation }));
                yield return null;
                Assert.AreEqual(1, ActivityService.GetChildActivityIds(battle.Id).Count);
                Assert.IsTrue(ActivityService.Close(battle.Id, ActivityCloseCauseType.Manual));
                Assert.IsTrue(ActivityService.TryGetSnapshot(board.Id, out board));
                Assert.AreEqual(ActivityState.Closed, board.State);
                Assert.AreEqual(ActivityCloseCauseType.ParentClosed, board.CloseCauseType);
                Assert.AreEqual(ActivityResultType.Cancelled, board.ResultType);
                yield return null;
                Assert.IsFalse(capture.Carriers.Any(Core.Entities.EntityService.Exists), "A carrier Entity survived Activity close.");
            }
            finally { capture.Unregister(); }
        }

        [UnityTest]
        public IEnumerator EnemyDefeat_ProjectionCapacityFailure_LogsReasonRemovesEnemyAndStartsNextWave(
            [Values(false, true)] bool occupyPool)
        {
            var capture = new DefeatPoolCapture { ExpectFailedDrops = occupyPool };
            var held = new List<IPoolObject>();
            IPoolContext pool = null;
            capture.Register();
            try
            {
                yield return LaunchPlayableActivities();
                Assert.IsTrue(TryFindRunningActivities(out var battle, out _));
                pool = PoolService.Current.OpenContext("projection:activity:" + battle.Id.Value);
                var key = new PoolKey("chainrush.autobattle.experience-drop");
                float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline && !pool.TryGetSnapshot(key, out _)) yield return null;
                Assert.IsTrue(pool.TryGetSnapshot(key, out var initial), "Experience projection pool was not prepared.");
                Assert.AreEqual(32, initial.MaxCapacity, "This reproduction uses the unchanged authored limit.");
                if (occupyPool)
                {
                    // Hold views only: no Entity, occupancy, token or enemy health is changed.
                    while (pool.TryRent(key, out var instance)) held.Add(instance);
                    Assert.IsTrue(pool.TryGetSnapshot(key, out var full));
                    Assert.AreEqual(full.MaxCapacity, full.ActiveCount);
                    Assert.AreEqual(0, full.FreeCount);
                    TestContext.WriteLine($"Pool occupied: total={full.TotalCount}, active={full.ActiveCount}, free={full.FreeCount}.");
                }

                deadline = Time.realtimeSinceStartup + CollectorCycleTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline
                    && (capture.Defeated.Count < 2 || capture.PreparedDrops < 2
                        || capture.Defeated.Take(2).Any(Core.Entities.EntityService.Exists)
                        || capture.WaveOrders < 2))
                {
                    if (occupyPool)
                        while (pool.TryRent(key, out var returned)) held.Add(returned);
                    yield return null;
                }
                Assert.GreaterOrEqual(capture.Defeated.Count, 2, "Natural combat did not defeat the initial enemies.");
                Assert.GreaterOrEqual(capture.PreparedDrops, 2, "Drop preparation did not finish.");
                var defeated = capture.Defeated.Take(2).ToList();
                if (occupyPool)
                {
                    while (pool.TryRent(key, out var returned)) held.Add(returned);
                    Assert.IsTrue(pool.TryGetSnapshot(key, out var occupied));
                    Assert.AreEqual(0, occupied.FreeCount, "Removal must not require projection capacity to return.");
                }

                string diagnostic = $"HeldPool={occupyPool}; WaveOrders={capture.WaveOrders}; "
                    + "Remaining=" + string.Join(",", defeated.Where(Core.Entities.EntityService.Exists).Select(entity => entity.Value))
                    + "; DropResults=" + string.Join(" | ", capture.Results.Select(result =>
                        result.SourceEntityId.Value + ":" + result.ResultType + ":" + result.Failure))
                    + "; BrainFailures=" + string.Join(" | ", capture.Failures.Select(failure =>
                        failure.OwnerEntityId.Value + ":" + failure.Message));
                TestContext.WriteLine(diagnostic);
                foreach (var entity in defeated)
                {
                    Assert.IsFalse(Core.Entities.EntityService.Exists(entity), diagnostic);
                    var result = capture.Results.Single(item => item.SourceEntityId == entity);
                    if (!occupyPool) Assert.AreEqual(DropResultType.Completed, result.ResultType, diagnostic);
                    if (result.ResultType != DropResultType.Completed)
                    {
                        Assert.IsNotEmpty(result.Failure, diagnostic);
                        Assert.IsTrue(capture.DropErrors.Any(error => error.Contains("Entity='" + entity.Value + "'")
                            && error.Contains(result.Failure)), diagnostic);
                    }
                    Assert.IsFalse(TryFindProjectionBinding(battle.Id, entity, out _), diagnostic);
                }
                if (occupyPool)
                    Assert.IsTrue(capture.Results.Any(result => defeated.Contains(result.SourceEntityId)
                        && result.ResultType != DropResultType.Completed), "The pool failure branch was not reached.");
                Assert.GreaterOrEqual(capture.WaveOrders, 2, diagnostic);
            }
            finally
            {
                foreach (var instance in held) pool.Return(instance);
                capture.Unregister();
            }
        }

        sealed class DefeatPoolCapture :
            IEventListener<HostValueChangedEvent>, IEventListener<DropResultEvent>,
            IEventListener<AIBrainDebugEvent>, IEventListener<ProductionOrderStartedEvent>,
            IEventListener<ProductionOrderFinishedEvent>
        {
            readonly CapabilityHostData _enemy = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(
                "Assets/Game/Activities/Autobattle/Economy/BugBrownSmall.asset");
            readonly HostValueData _health = AssetDatabase.LoadAssetAtPath<HostValueData>(
                "Assets/Game/Activities/Autobattle/HostValues/Health.asset");
            readonly string _waveRecipe = AssetDatabase.LoadAssetAtPath<ProductionRecipeData>(
                "Assets/Game/Activities/Autobattle/Production/EnemyWaveRecipe.asset").Id;
            readonly string _dropRecipe = AssetDatabase.LoadAssetAtPath<ProductionRecipeData>(
                "Assets/Game/Activities/Autobattle/Production/ExperienceDropRecipe.asset").Id;
            public readonly List<Core.Entities.EntityId> Defeated = new List<Core.Entities.EntityId>();
            public readonly List<DropResultEvent> Results = new List<DropResultEvent>();
            public readonly List<AIBrainDebugEvent> Failures = new List<AIBrainDebugEvent>();
            public readonly List<string> DropErrors = new List<string>();
            public bool ExpectFailedDrops;
            public int WaveOrders;
            public int PreparedDrops;

            public void Register()
            {
                EventBus.Register<HostValueChangedEvent>(this);
                EventBus.Register<DropResultEvent>(this);
                EventBus.Register<AIBrainDebugEvent>(this);
                EventBus.Register<ProductionOrderStartedEvent>(this);
                EventBus.Register<ProductionOrderFinishedEvent>(this);
                Application.logMessageReceived += OnLog;
            }

            public void Unregister()
            {
                EventBus.Unregister<HostValueChangedEvent>(this);
                EventBus.Unregister<DropResultEvent>(this);
                EventBus.Unregister<AIBrainDebugEvent>(this);
                EventBus.Unregister<ProductionOrderStartedEvent>(this);
                EventBus.Unregister<ProductionOrderFinishedEvent>(this);
                Application.logMessageReceived -= OnLog;
            }

            public void OnEvent(HostValueChangedEvent e)
            {
                if (e.Value == _health && e.PreviousRawValue > 0 && e.CurrentRawValue <= 0
                    && CapabilityHostService.TryGet(e.EntityId, out var host)
                    && host.Definition.Id == _enemy.Id) Defeated.Add(e.EntityId);
            }
            public void OnEvent(DropResultEvent e)
            {
                Results.Add(e);
                if (ExpectFailedDrops && e.ResultType != DropResultType.Completed)
                    LogAssert.Expect(LogType.Error, new Regex(@"\[DropAIBrainAction\] Drop failed\..*Entity='"
                        + e.SourceEntityId.Value + @"'.*Reason='" + Regex.Escape(e.Failure) + @"'"));
            }

            void OnLog(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Error && message.StartsWith("[DropAIBrainAction]")) DropErrors.Add(message);
            }
            public void OnEvent(AIBrainDebugEvent e)
            {
                if (e.Type == AIBrainDebugEventType.ActionFailed && Defeated.Contains(e.OwnerEntityId)) Failures.Add(e);
            }
            public void OnEvent(ProductionOrderStartedEvent e)
            {
                if (e.RecipeId == _waveRecipe) WaveOrders++;
            }
            public void OnEvent(ProductionOrderFinishedEvent e)
            {
                if (e.RecipeId == _dropRecipe && e.FinalStatus == ProductionOrderStatus.Completed) PreparedDrops++;
            }
        }

        [UnityTest]
        public IEnumerator BoardSelection_OneToSixteen_ProducesExactFormsAndMaterializes(
            [Values("Water", "Cola")] string content,
            [ValueSource(nameof(BoardSelectionStarts))] int firstSelection)
        {
            SelectOnlyBoardContent(content);
            var capture = new PlayableRuntimeCapture();
            capture.Register();
            try
            {
                yield return LaunchPlayableActivities();
                Assert.IsTrue(TryFindRunningActivities(out var battle, out var board));
                var player = battle.Participants.Single(participant => participant.TeamIndex == 0);
                var wallet = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(SharedWalletTagPath);
                var turn = AssetDatabase.LoadAssetAtPath<EconomyAssetData>(BoardTurnTokenPath);
                IssueTestTurns(player.ParticipantEconomyOwner, wallet, turn, 32);
                var cell = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(
                    "Assets/Game/Activities/Board/Economy/" + content + "BoardBase.asset");
                var cellTag = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardCellTagPath);
                var selection = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardMergeSelectionPath);
                var hostDefinition = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardHostPath);
                Assert.IsTrue(TryFindBoardHost(board.Id, hostDefinition, out var boardHost));
                var forms = new List<CapabilityHostData>();
                var recipes = new List<ProductionRecipeData>();
                for (int form = 1; form <= 4; form++)
                {
                    forms.Add(AssetDatabase.LoadAssetAtPath<CapabilityHostData>(
                        "Assets/Game/Activities/Shared/Units/" + content + "/" + content + "Unit" + (form == 1 ? "" : form.ToString()) + ".asset"));
                    recipes.Add(AssetDatabase.LoadAssetAtPath<ProductionRecipeData>(
                        "Assets/Game/Activities/Board/Production/" + (content == "Water" ? "BoardMergeRecipe" : "ColaMergeRecipe") + form + ".asset"));
                }
                var payments = new BoardPaymentCapture(player.ParticipantEconomyOwner, turn);
                EventBus.Register<EconomyOperationChangedEvent>(payments);
                try
                {
                    for (int size = firstSelection; size <= System.Math.Min(firstSelection + 5, 16); size++)
                    {
                        yield return AwaitCompletedPopulation(board, cellTag, cell);
                        int paidBefore = payments.Handles.Count;
                        int[] before = forms.Select(form => capture.Count(form)).ToArray();
                        for (int frame = 0; frame < 4; frame++) yield return null;
                        Assert.AreEqual(paidBefore, payments.Handles.Count, "A full Board was charged again.");
                        var expected = new List<string>();
                        for (int remaining = size; remaining > 0;)
                        {
                            int form = System.Math.Min(4, remaining);
                            expected.Add(recipes[form - 1].Id);
                            remaining -= form;
                        }
                        yield return AssertBoardMergeSequence(board, cellTag, cell, boardHost, selection, size, expected);
                        float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                        while (Time.realtimeSinceStartup < deadline
                            && forms.Select((form, index) => capture.Count(form) - before[index]).Sum() < expected.Count)
                            yield return null;
                        for (int form = 1; form <= 4; form++)
                        {
                            int expectedCount = form == 4 ? size / 4 : size % 4 == form ? 1 : 0;
                            Assert.AreEqual(expectedCount, capture.Count(forms[form - 1]) - before[form - 1],
                                content + " selection=" + size + " form=" + form + "\n" + BuildOrchestrationDiagnostic(battle.DomainId));
                            Assert.AreEqual(0, QueryAmount(player.ParticipantEconomyOwner, wallet, EconomyFormType.Stack, forms[form - 1]),
                                "Deployment left a unit Stack instead of a materialized Token.");
                            if (expectedCount > 0)
                                AssertMaterializedHostsHaveBackingTokens(battle.Id, forms[form - 1], player.ParticipantEconomyOwner, wallet);
                        }
                        Assert.IsNull(capture.Failure);
                        yield return AwaitCompletedPopulation(board, cellTag, cell);
                        Assert.AreEqual(paidBefore + 1, payments.Handles.Count, "Refresh must consume exactly one turn.");
                        Assert.IsNull(payments.Failure);
                        TestContext.WriteLine(content + " selection=" + size + ": real merge/deployment/materialization passed.");
                    }
                    Assert.IsTrue(ActivityService.Close(battle.Id, ActivityCloseCauseType.Manual));
                    foreach (var handle in payments.Handles)
                        Assert.IsFalse(EconomyService.TryGetOperation(handle, out _));
                }
                finally { EventBus.Unregister<EconomyOperationChangedEvent>(payments); }
            }
            finally { capture.Unregister(); }
        }

        System.Action _restoreBattleSeeds;

        [UnityTest]
        public IEnumerator SelectedUnit_ApproachesAndDamagesEnemy([Values("Water", "Cola")] string content)
        {
            SelectOnlyBoardContent(content);
            var activity = AssetDatabase.LoadAssetAtPath<ActivityData>("Assets/Game/Activities/Autobattle/Definition/AutobattleActivity.asset");
            var hero = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(PerfumePath);
            var seeds = activity.Teams[0].Wallets.Single(wallet => wallet.Seed.Any(entry => entry.Seed.Asset == hero)).Seed;
            var original = new List<ActivityWalletSeedEntryData>(seeds);
            seeds.RemoveAll(entry => entry.Seed.Asset == hero);
            _restoreBattleSeeds = () => { seeds.Clear(); seeds.AddRange(original); };
            var capture = new PlayableRuntimeCapture();
            capture.Register();
            try
            {
                yield return LaunchPlayableActivities();
                Assert.IsTrue(TryFindRunningActivities(out var battle, out var board));
                var player = battle.Participants.Single(participant => participant.TeamIndex == 0);
                IssueTestTurns(player.ParticipantEconomyOwner,
                    AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(SharedWalletTagPath),
                    AssetDatabase.LoadAssetAtPath<EconomyAssetData>(BoardTurnTokenPath), 1);
                var cell = AssetDatabase.LoadAssetAtPath<CapabilityHostData>("Assets/Game/Activities/Board/Economy/" + content + "BoardBase.asset");
                var cellTag = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardCellTagPath);
                yield return AwaitCompletedPopulation(board, cellTag, cell);
                Assert.IsTrue(TryFindBoardHost(board.Id, AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardHostPath), out var boardHost));
                var recipe = AssetDatabase.LoadAssetAtPath<ProductionRecipeData>("Assets/Game/Activities/Board/Production/"
                    + (content == "Water" ? "BoardMergeRecipe1" : "ColaMergeRecipe1") + ".asset");
                yield return AssertBoardMergeSequence(board, cellTag, cell, boardHost,
                    AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardMergeSelectionPath), 1, new List<string> { recipe.Id });
                var unit = AssetDatabase.LoadAssetAtPath<CapabilityHostData>("Assets/Game/Activities/Shared/Units/" + content + "/" + content + "Unit.asset");
                float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline && !TryFindActivityHost(battle.Id, unit, out _)) yield return null;
                Assert.IsTrue(TryFindActivityHost(battle.Id, unit, out var entity), "Selected unit was not materialized.");
                Assert.IsTrue(SpatialService.TryGetPose(entity, out var initialPose));
                deadline = Time.realtimeSinceStartup + CollectorCycleTimeoutSeconds;
                bool moved = false;
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    if (SpatialService.TryGetPose(entity, out var pose)) moved |= pose.Coordinates != initialPose.Coordinates;
                    bool hit = content == "Water"
                        ? capture.Damage.Any(change => change.MutationContext.SourceEntityId == entity)
                        : capture.Hits.Any(value => value.OwnerEntityId == entity
                            && capture.Damage.Any(change => change.EntityId == value.TargetEntityId));
                    if (hit) break;
                }
                if (content == "Water")
                {
                    Assert.IsTrue(moved, "Water never approached its enemy.");
                    Assert.IsTrue(capture.Damage.Any(change => change.MutationContext.SourceEntityId == entity),
                        "Water did not apply attack damage.\n" + BuildExecutorDiagnostic(entity));
                }
                else
                    Assert.IsTrue(capture.Hits.Any(hit => hit.OwnerEntityId == entity
                        && capture.Damage.Any(change => change.EntityId == hit.TargetEntityId)),
                        "Cola did not land a damaging carrier hit.\n" + BuildExecutorDiagnostic(entity));
                Assert.IsNull(capture.Failure);
            }
            finally { capture.Unregister(); }
        }

        [UnityTest]
        public IEnumerator BoardStubSelection_ConsumesOnlySelectedCellsAndRefills(
            [Values("LightningBolt", "Power", "Defense", "Health", "Speed", "SkillSpeed", "Gold")] string content)
        {
            SelectOnlyBoardContent(content);
            yield return LaunchPlayableActivities();
            Assert.IsTrue(TryFindRunningActivities(out var battle, out var board));
            var player = battle.Participants.Single(participant => participant.TeamIndex == 0);
            var wallet = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(SharedWalletTagPath);
            IssueTestTurns(player.ParticipantEconomyOwner, wallet, AssetDatabase.LoadAssetAtPath<EconomyAssetData>(BoardTurnTokenPath), 4);
            var cell = AssetDatabase.LoadAssetAtPath<CapabilityHostData>("Assets/Game/Activities/Board/Economy/" + content + "BoardBase.asset");
            var cellTag = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardCellTagPath);
            var selection = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardMergeSelectionPath);
            Assert.IsTrue(TryFindBoardHost(board.Id, AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardHostPath), out var host));
            yield return AwaitCompletedPopulation(board, cellTag, cell);
            var untouched = ResolveConnectedMarkerSelection(board, cellTag, cell, 16).Skip(3).ToList();
            yield return AssertBoardMergeSequence(board, cellTag, cell, host, selection, 3, new List<string>());
            Assert.IsTrue(untouched.All(CapabilityHostService.Exists), "Consume removed unselected tokens.");
            yield return AwaitCompletedPopulation(board, cellTag, cell);
            Assert.AreEqual(16, CountMaterializedBoardAssets(board, cellTag, cell));
            Assert.IsTrue(ActivityService.Close(battle.Id, ActivityCloseCauseType.Manual));
        }

        void SelectOnlyBoardContent(string content)
        {
            var definition = AssetDatabase.LoadAssetAtPath<AgentDefinitionData>(
                "Assets/Game/Activities/Board/Agents/BoardPopulationAgent.asset");
            var population = (PopulationAgentData)definition.Agent;
            var selected = AssetDatabase.LoadAssetAtPath<CapabilityHostData>(
                "Assets/Game/Activities/Board/Economy/" + content + "BoardBase.asset");
            Assert.NotNull(selected);
            var release = population.Releases.Single();
            var field = typeof(PopulationReleaseData).GetField("content", BindingFlags.Instance | BindingFlags.NonPublic);
            var original = field.GetValue(release);
            // The fixture selects content; grouped producers and actual orders remain unchanged.
            field.SetValue(release, new List<PopulationContentRuleData>
            { new PopulationContentRuleData(new PopulationAssetContentSourceData(selected), 1f) });
            _restoreBoardContent = () => field.SetValue(release, original);
        }

        static void IssueTestTurns(IEconomyAssetOwner owner, TaxonomyTermData wallet, EconomyAssetData turn, int count)
        {
            var request = new EconomyMutationOperationRequest(owner, EconomyOperation.Issue,
                new EconomyEntrySelectionData(turn, EconomyFormType.Stack, new List<TaxonomyTermData> { wallet }, null, null, null, null),
                count, EconomyTransactionTraceContext.None);
            Assert.IsTrue(EconomyService.TryRegisterOperation(request, out var handle, out var failure), failure.Message);
            Assert.IsTrue(EconomyService.TryExecuteOperation(handle, out _, out failure), failure.Message);
            Assert.IsTrue(EconomyService.TryCloseOperation(handle));
        }

        static IEnumerator LaunchPlayableActivities()
        {
            Scene scene = EditorSceneManager.LoadSceneInPlayMode(IntegrationScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && !scene.isLoaded) yield return null;
            yield return null;
            var host = Object.FindFirstObjectByType<GameRuntimeHost>(FindObjectsInactive.Include);
            Assert.NotNull(host);
            deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline
                && (host.RuntimeContext == null || !host.RuntimeContext.IsInitialized || !TryFindRunningActivities(out _, out _)))
                yield return null;
            Assert.IsTrue(TryFindRunningActivities(out var battle, out var board), "Activities did not start.");
            Assert.AreEqual(battle.Id, board.ParentActivityId);
            Assert.AreEqual(11, board.ObjectiveRuntimeIds.Count);
            Assert.AreEqual(16, CountBoardUICells());
            AssertBoardUIVisible(host);
            Assert.AreEqual(2, battle.Participants.Count);
            AssertParticipant(battle.Participants, 0, PlayerControlType.LocalHuman);
            AssertParticipant(battle.Participants, 1, PlayerControlType.Bot);
        }

        sealed class PlayableRuntimeCapture :
            IEventListener<SkillCarrierLifecycleEvent>,
            IEventListener<ProjectionLifecycleEvent>,
            IEventListener<HostValueChangedEvent>,
            IEventListener<DropResultEvent>
        {
            public readonly List<SkillCarrierLifecycleEvent> Hits = new List<SkillCarrierLifecycleEvent>();
            public readonly List<HostValueChangedEvent> Damage = new List<HostValueChangedEvent>();
            readonly HostValueData _health = AssetDatabase.LoadAssetAtPath<HostValueData>(
                "Assets/Game/Activities/Autobattle/HostValues/Health.asset");
            public readonly HashSet<Core.Entities.EntityId> Carriers = new HashSet<Core.Entities.EntityId>();
            readonly HashSet<Core.Entities.EntityId> _ready = new HashSet<Core.Entities.EntityId>();
            readonly Dictionary<string, HashSet<Core.Entities.EntityId>> _materialized =
                new Dictionary<string, HashSet<Core.Entities.EntityId>>();
            public int CompletedDrops;
            public string Failure;
            public int Count(CapabilityHostBaseData asset)
            {
                foreach (var entity in _ready)
                {
                    if (!CapabilityHostService.TryGet(entity, out var host) || host.Definition == null
                        || !ActivityService.TryGetMaterializedEntityTokenHandle(host.ActivityId, entity, out var token)
                        || !token.IsValid) continue;
                    if (!_materialized.TryGetValue(host.Definition.Id, out var entities))
                        _materialized.Add(host.Definition.Id, entities = new HashSet<Core.Entities.EntityId>());
                    entities.Add(entity);
                }
                return _materialized.TryGetValue(asset.Id, out var entries) ? entries.Count : 0;
            }
            public void Register()
            {
                EventBus.Register<SkillCarrierLifecycleEvent>(this);
                EventBus.Register<ProjectionLifecycleEvent>(this);
                EventBus.Register<HostValueChangedEvent>(this);
                EventBus.Register<DropResultEvent>(this);
            }
            public void Unregister()
            {
                EventBus.Unregister<SkillCarrierLifecycleEvent>(this);
                EventBus.Unregister<ProjectionLifecycleEvent>(this);
                EventBus.Unregister<HostValueChangedEvent>(this);
                EventBus.Unregister<DropResultEvent>(this);
            }
            public void OnEvent(SkillCarrierLifecycleEvent e)
            {
                if (e.EventType == SkillCarrierLifecycleEventType.Spawned) Carriers.Add(e.CarrierEntityId);
                if (e.EventType != SkillCarrierLifecycleEventType.Hit) return;
                Hits.Add(e);
                if (!DiplomacyService.TryGetRelation(ActivityIdFor(e.OwnerEntityId), e.OwnerEntityId, e.TargetEntityId,
                        DiplomacyChannelType.Military, out var relation) || relation.Disposition != DiplomacyDispositionType.Hostile)
                    Failure = "A combat carrier hit a non-hostile target.";
            }
            static ActivityId ActivityIdFor(Core.Entities.EntityId entity) =>
                CapabilityHostService.GetAll().Single(host => host.EntityId == entity).ActivityId;
            public void OnEvent(ProjectionLifecycleEvent e)
            {
                if (e.EventType == ProjectionLifecycleEventType.BoundReady) _ready.Add(e.Handle.EntityId);
            }
            public void OnEvent(DropResultEvent e)
            {
                if (e.ResultType == DropResultType.Completed) CompletedDrops++;
            }
            public void OnEvent(HostValueChangedEvent e)
            {
                if (e.Value == _health && e.DeltaRawValue < 0) Damage.Add(e);
            }
        }


        static IEnumerator AwaitCompletedPopulation(ActivityRuntimeSnapshot board,
            TaxonomyTermData markerTag, CapabilityHostData item)
        {
            ActivityObjectiveRuntimeSnapshot objective = board.Objectives.Single(
                value => value.RootNodeId == "chainrush-board-population");
            float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                ObjectiveRuntimeSnapshot snapshot = ObjectiveService.GetSnapshot(board.DomainId, objective.RuntimeId);
                if (CountMaterializedBoardAssets(board, markerTag, item) == 16
                    && snapshot.Nodes.Any(node => node.NodeId == objective.RootNodeId && node.State == ObjectiveState.Completed))
                    yield break;
                yield return null;
            }
            Assert.Fail("Board did not finish its paid population cycle.\n" + BuildOrchestrationDiagnostic(board.DomainId));
        }

        sealed class BoardPaymentCapture : IEventListener<EconomyOperationChangedEvent>
        {
            readonly IEconomyAssetOwner _owner;
            readonly EconomyAssetData _asset;
            public readonly List<EconomyOperationHandle> Handles = new List<EconomyOperationHandle>();
            public string Failure;
            public BoardPaymentCapture(IEconomyAssetOwner owner, EconomyAssetData asset)
            { _owner = owner; _asset = asset; }
            public void OnEvent(EconomyOperationChangedEvent e)
            {
                if (e.State != EconomyOperationStateType.Committed || e.Owner != _owner
                    || !EconomyService.TryGetOperation(e.Handle, out EconomyOperationSnapshot operation)
                    || operation.Request.Asset != _asset || operation.Request.Operation != EconomyOperation.Consume)
                    return;
                if (operation.Request.Amount != 1L || Handles.Contains(e.Handle))
                    Failure = "Board payment was repeated or consumed more than one turn.";
                Handles.Add(e.Handle);
            }
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
                yield return SubmitSelectionTargetsAcrossSteps(request, selectedEntities, selectionCapture);

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
            foreach (var expected in expectedRecipeIds.GroupBy(id => id))
            {
                Assert.AreEqual(
                    expected.Count(),
                    productionCapture.CountRecipe(expected.Key),
                    productionCapture.BuildDiagnostic());
            }
        }

        static IEnumerator SubmitSelectionTargetsAcrossSteps(
            SelectionIntentEvent request,
            IReadOnlyList<Core.Entities.EntityId> targets,
            SelectionResultCapture capture)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                EventBus.Trigger(SelectionIntentEvent.Target(request, targets[i]));
                int inputStep = DeterminismService.CurrentTick;
                float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                do
                {
                    yield return null;
                    Assert.AreEqual(0, capture.Count, string.Concat(
                        "Selection closed before pointer release after target ", i + 1, ": ",
                        capture.Result.Message));
                    Assert.Less(Time.realtimeSinceStartup, deadline,
                        "Simulation did not advance between selection inputs.");
                }
                while (DeterminismService.CurrentTick - inputStep < 2);
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
            var contentTag = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>("Assets/Game/Activities/Board/Taxonomy/BoardContent.asset");
            for (int markerIndex = 0; markerIndex < markers.Count; markerIndex++)
            {
                Core.Entities.EntityId[] occupants =
                    SpatialService.GetOccupants(markers[markerIndex].WorldPosition);
                for (int occupantIndex = 0; occupantIndex < occupants.Length; occupantIndex++)
                {
                    Core.Entities.EntityId entityId = occupants[occupantIndex];
                    if (!CapabilityHostService.TryGet(entityId, out CapabilityHostSnapshot host)
                        || host.Definition == null
                        || (expectedAsset != null ? !host.Definition.Matches(expectedAsset) : !host.Definition.Tags.Contains(contentTag)))
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
                    || !candidateBinding.isActiveAndEnabled
                    || !candidateBinding.gameObject.activeInHierarchy
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


        static void AssertMaterializedHostsHaveBackingTokens(
            ActivityId activityId,
            CapabilityHostData definition,
            IEconomyAssetOwner owner,
            TaxonomyTermData walletTag)
        {
            List<CapabilityHostSnapshot> hosts = CapabilityHostService.GetAll()
                .Where(host => host.ActivityId == activityId
                    && host.Definition != null
                    && host.Definition.Matches(definition)
                    && host.Owner != null
                    && owner != null
                    && string.Equals(
                        host.Owner.StableSimulationKey,
                        owner.StableSimulationKey,
                        System.StringComparison.Ordinal))
                .OrderBy(host => host.EntityId.Value)
                .ToList();
            Assert.IsNotEmpty(hosts, "No materialized CapabilityHost matched the expected asset and owner.");
            var linkedHandles = new HashSet<EconomyEntryHandle>();
            for (int i = 0; i < hosts.Count; i++)
            {
                Assert.IsTrue(
                    ActivityService.TryGetMaterializedEntityTokenHandle(
                        activityId,
                        hosts[i].EntityId,
                        out EconomyEntryHandle handle)
                    && handle.IsValid,
                    string.Concat(
                        "Materialized CapabilityHost entity ",
                        hosts[i].EntityId.Value.ToString(),
                        " has no backing Economy Token."));
                linkedHandles.Add(handle);
            }

            EconomySelectionQueryResult tokens = EconomyService.Query(new EconomySelectionQuery(
                owner,
                new List<TaxonomyTermData> { walletTag },
                EconomyFormType.Token,
                definition,
                EconomyAggregationType.Detailed,
                includeZeroBalance: false,
                availabilityType: EconomySelectionAvailabilityType.Committed));
            Assert.AreEqual(
                hosts.Count,
                tokens.Items.Length,
                "Materialized CapabilityHost count does not match committed backing Token count.");
            for (int i = 0; i < tokens.Items.Length; i++)
            {
                Assert.IsTrue(
                    linkedHandles.Contains(tokens.Items[i].Handle),
                    "A committed CapabilityHost Token is not linked to a materialized entity.");
            }
        }

        static long QueryAmount(
            IEconomyAssetOwner owner,
            TaxonomyTermData walletTag,
            EconomyFormType formType,
            EconomyAssetData asset)
        {
            EconomySelectionQueryResult result = EconomyService.Query(
                new EconomySelectionQuery(
                    owner,
                    new List<TaxonomyTermData> { walletTag },
                    formType,
                    asset,
                    includeZeroBalance: true));
            long amount = 0L;
            for (int itemIndex = 0; itemIndex < result.Items.Length; itemIndex++)
                amount += result.Items[itemIndex].Amount;

            return amount;
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
                    .Append(" objectives=");
                AppendRootDemandObjectives(message, process);
                message
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

        static string BuildOrchestrationDiagnostic(RuntimeDomainId domainId)
        {
            var message = new StringBuilder(512);
            List<OrchestrationParticipantSnapshot> participants =
                OrchestrationService.GetParticipants(domainId);
            message.Append("Participants=")
                .Append(participants.Count)
                .AppendLine();
            for (int i = 0; i < participants.Count; i++)
            {
                OrchestrationParticipantSnapshot participant = participants[i];
                message.Append("  Participant key=")
                    .Append(participant.ParticipantStableKey ?? "<null>")
                    .Append(" state=")
                    .Append(participant.State)
                    .Append(" goals=")
                    .Append(participant.GoalCount)
                    .AppendLine();
            }

            List<OrchestrationGoalSnapshot> goals = OrchestrationService.GetGoals(domainId);
            message.Append("Goals=")
                .Append(goals.Count)
                .AppendLine();
            for (int i = 0; i < goals.Count; i++)
            {
                OrchestrationGoalSnapshot goal = goals[i];
                message.Append("  Goal objective=")
                    .Append(goal.SourceObjectiveRuntimeId.Value)
                    .Append(" participant=")
                    .Append(goal.ParticipantStableKey ?? "<null>")
                    .Append(" state=")
                    .Append(goal.State)
                    .Append(" message=")
                    .Append(goal.Message ?? "<none>")
                    .AppendLine();
            }

            AppendProcessDiagnostic(message, domainId);
            return message.ToString();
        }

        static void AppendProductionModuleDiagnostic(
            StringBuilder message,
            ActivityRuntimeSnapshot board)
        {
            ActivityData definition = AssetDatabase.LoadAssetAtPath<ActivityData>(BoardActivityPath);
            List<OrchestrationParticipantSnapshot> participants =
                OrchestrationService.GetParticipants(board.DomainId);
            if (definition == null
                || participants.Count == 0
                || board.Participants.Count == 0)
            {
                message.AppendLine("ProductionMethods=<diagnostic context unavailable>");
                return;
            }

            var module = new ProductionStateOrchestrationModule();
            var registry = new OrchestrationModuleResultRegistry();
            module.Collect(
                new OrchestrationModuleExecutionContext(
                    board.Id,
                    board.DomainId,
                    definition,
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

        static bool HasRootDemand(
            in OrchestrationProcessSnapshot process,
            ObjectiveRuntimeId objectiveRuntimeId)
        {
            for (int i = 0; process.RootDemands != null && i < process.RootDemands.Count; i++)
            {
                if (process.RootDemands[i].Key.SourceObjectiveRuntimeId == objectiveRuntimeId)
                    return true;
            }

            return false;
        }

        static void AppendRootDemandObjectives(
            StringBuilder message,
            in OrchestrationProcessSnapshot process)
        {
            if (process.RootDemands == null || process.RootDemands.Count == 0)
            {
                message.Append("<none>");
                return;
            }

            message.Append('[');
            for (int i = 0; i < process.RootDemands.Count; i++)
            {
                if (i > 0)
                    message.Append(',');
                message.Append(process.RootDemands[i].Key.SourceObjectiveRuntimeId.Value);
            }
            message.Append(']');
        }

        static string BuildRootDemandObjectives(in OrchestrationProcessSnapshot process)
        {
            var message = new StringBuilder(32);
            AppendRootDemandObjectives(message, process);
            return message.ToString();
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
                    BuildRootDemandObjectives(snapshot),
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
