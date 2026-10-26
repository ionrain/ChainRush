using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Core.Activities;
using Core.Activities.Selection;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Determinism;
using Core.Economy;
using EntityId = Core.Entities.EntityId;
using Core.Events;
using Core.GameRuntime;
using Core.GameRuntime.Installers;
using Core.Players;
using Core.Taxonomy;
using Core.UI.Production;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ChainRush.Tests.PlayMode
{
    public sealed partial class ChainRushActivityCompositionPlayModeTests
    {
        // minimum seam: a complete real-game comparison, with the building selection as its only gameplay input difference.
        [UnityTest, Timeout(180000)]
        public IEnumerator Buildings_OnOffCompareOrdinaryPlacementAndReleaseFrames()
            => DisposeBuildingComparisonOnFailure(RunBuildingComparison());

        // minimum seam: Unity's nested IEnumerator runner records failures without disposing its parent.
        // Own the nested stack so a failed child still executes every enclosing cleanup/export finally.
        static IEnumerator DisposeBuildingComparisonOnFailure(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(16);
            stack.Push(body);
            try
            {
                while (stack.Count != 0)
                {
                    var current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        stack.Pop();
                        (current as IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested) stack.Push(nested);
                    else yield return current.Current;
                }
            }
            finally
            {
                while (stack.Count != 0) (stack.Pop() as IDisposable)?.Dispose();
            }
        }

        IEnumerator RunBuildingComparison()
        {
            double duration = ComparisonSeconds("CHAINRUSH_BUILDINGS_MEASURE_SECONDS", 30, 5, 60);
            double warmup = ComparisonSeconds("CHAINRUSH_BUILDINGS_WARMUP_SECONDS", 5, 2, 15);
            string output = Environment.GetEnvironmentVariable("CHAINRUSH_BUILDINGS_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) output = Path.Combine(Application.temporaryCachePath,
                "building-comparison-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ"));
            Assert.IsFalse(Directory.Exists(output), "Existing measurements cannot be overwritten.");
            Directory.CreateDirectory(output);
            bool profilerEnabled = Profiler.enabled, deep = ProfilerDriver.deepProfiling;
            bool oldRunInBackground = Application.runInBackground;
            int previousTargetFrameRate = Application.targetFrameRate, previousVSync = QualitySettings.vSyncCount;
            var results = new List<BuildingCaptureResult>(2);
            var storage = new BuildingComparisonStorage();
            _buildingComparisonStorage = storage;
            _buildingComparisonActive = true;
            IDisposable view = null;
            bool pairValidated = false;
            try
            {
                // No Profiler CPU recording or allocation capture is enabled. GC is observed only by timing recorders.
                Profiler.enabled = false;
                ProfilerDriver.deepProfiling = false;
                Application.runInBackground = true;
                var viewType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("ChainRush.Diagnostics.PerformanceBenchGameViewScope"))
                    .First(type => type != null);
                view = (IDisposable)Activator.CreateInstance(viewType,
                    BindingFlags.Instance | BindingFlags.NonPublic, null, null, null);
                // The Editor's original scene can already have a persistent host when Play starts.
                // Retire it through the fixture's existing lifecycle before installing either measured variant.
                yield return TearDownRuntime();
                foreach (bool enabled in new[] { false, true })
                {
                    storage.BeginVariant();
                    var buildingDefinitions = new HashSet<CapabilityHostBaseData>();
                    foreach (string name in new[] { "Infantry", "Ranged", "Heavy" })
                        buildingDefinitions.Add(AssetDatabase.LoadAssetAtPath<CapabilityHostData>(
                            "Assets/Game/Activities/Buildings/Economy/" + name + "Building.asset"));
                    Assert.IsFalse(buildingDefinitions.Contains(null));
                    var buildingCards = new HashSet<CapabilityHostBaseData>();
                    foreach (string name in new[] { "Infantry", "Ranged", "Heavy" })
                        buildingCards.Add(AssetDatabase.LoadAssetAtPath<CapabilityHostData>(
                            "Assets/Game/Activities/Buildings/Economy/" + name + "BoardCard.asset"));
                    Assert.IsFalse(buildingCards.Contains(null));
                    var cell = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(
                        "Assets/Game/Activities/Board/Taxonomy/BoardContent.asset");
                    GameRuntimeProfileData profileClone = null;
                    GameDeterminismConfigData determinismClone = null;
                    BuildingComparisonStorageInstaller installer = null;
                    BuildingFrameCapture capture = null;
                    BuildingCaptureResult result = null;
                    var selectedBuildings = new List<string>();
                    GameRuntimeHost configuredHost = null;
                    try
                    {
                        yield return LaunchPlayableActivities(beforeLevelStart: host =>
                        {
                            Assert.AreSame(configuredHost, host, "The comparison must use its configured runtime host.");
                            Assert.AreEqual(1, Object.FindObjectsByType<GameRuntimeHost>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
                            Assert.AreSame(storage, typeof(Core.Storage.StorageService).GetField("_runtime", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
                            Application.targetFrameRate = 300;
                            QualitySettings.vSyncCount = 0;
                            Assert.IsTrue(PlayerService.TryGet(host.RuntimeContext.PrimaryOwner, out var player));
                            var wallet = AssetDatabase.LoadAssetAtPath<EconomyWalletData>(
                                "Assets/Game/Activities/Buildings/Economy/BuildingCatalog.asset");
                            var selected = AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(
                                "Assets/Game/FrameworkUI/Data/Taxonomy/Selected.asset");
                            foreach (var definition in buildingDefinitions.Cast<CapabilityHostData>())
                            {
                                var query = EconomyService.Query(new EconomySelectionQuery(player.EconomyOwner,
                                    wallet.Tags, EconomyFormType.Stack, definition, EconomyAggregationType.Detailed));
                                if (query.Items.Length == 0) continue; // Never grant a building absent from the frozen save/profile.
                                Assert.AreEqual(1, query.Items.Length);
                                ChangeSelectionTags(player.EconomyOwner, wallet, definition, enabled,
                                    new List<TaxonomyTermData> { selected });
                                if (enabled) selectedBuildings.Add(definition.Id);
                            }
                            capture = new BuildingFrameCapture(enabled, buildingDefinitions, buildingCards, cell);
                            host.StartCoroutine(capture.Capture());
                        }, configureHost: host =>
                        {
                            configuredHost = host;
                            var data = new SerializedObject(host);
                            // The integration fixture prefab caps at 60; both comparison variants use the same 300 FPS ceiling.
                            data.FindProperty("unlockFrameRate").boolValue = true;
                            data.FindProperty("unlockedTargetFrameRate").intValue = 300;
                            var sourceProfile = (GameRuntimeProfileData)data.FindProperty("profile").objectReferenceValue;
                            profileClone = Object.Instantiate(sourceProfile);
                            _buildingComparisonObjects.Add(profileClone);
                            var profileData = new SerializedObject(profileClone);
                            var entries = profileData.FindProperty("installers");
                            installer = ScriptableObject.CreateInstance<BuildingComparisonStorageInstaller>();
                            _buildingComparisonObjects.Add(installer);
                            installer.Runtime = storage;
                            // This profile uses StorageService's default bootstrap, not a Storage installer.
                            // Append the test override at the end of the existing Infrastructure phase, before any owner loads.
                            Assert.IsFalse(sourceProfile.Installers.Any(item => item is StorageRuntimeInstallerData),
                                "The comparison's verified Storage ownership changed.");
                            int appendedIndex = entries.arraySize;
                            entries.arraySize++;
                            entries.GetArrayElementAtIndex(appendedIndex).objectReferenceValue = installer;
                            profileData.ApplyModifiedPropertiesWithoutUndo();
                            data.FindProperty("profile").objectReferenceValue = profileClone;
                            var sourceDeterminism = (GameDeterminismConfigData)data.FindProperty("determinism").objectReferenceValue;
                            determinismClone = Object.Instantiate(sourceDeterminism);
                            _buildingComparisonObjects.Add(determinismClone);
                            var deterministic = new SerializedObject(determinismClone);
                            deterministic.FindProperty("seedSource").enumValueIndex = (int)GameSeedSourceType.FixedFromConfig;
                            deterministic.FindProperty("fixedSeed").intValue = 12345;
                            deterministic.ApplyModifiedPropertiesWithoutUndo();
                            data.FindProperty("determinism").objectReferenceValue = determinismClone;
                            data.ApplyModifiedPropertiesWithoutUndo();
                        });
                        Assert.NotNull(capture);
                        Assert.IsTrue(TryFindRunningActivities(out var battle, out var board));
                        capture.SetActivities(battle.Id, board.Id);
                        // A real single-cell choice on each complete board keeps ordinary gameplay advancing.
                        yield return ChooseComparisonCell(capture, board);
                        double warmUntil = Time.realtimeSinceStartupAsDouble + warmup;
                        while (Time.realtimeSinceStartupAsDouble < warmUntil)
                        {
                            if (capture.TryGetBoardChoice(out _)) yield return ChooseComparisonCell(capture, board);
                            else yield return null;
                        }
                        Application.targetFrameRate = 300;
                        QualitySettings.vSyncCount = 0;
                        capture.MeasureFromFrame = Time.frameCount;
                        double started = Time.realtimeSinceStartupAsDouble;
                        while (Time.realtimeSinceStartupAsDouble - started < duration)
                        {
                            Assert.IsNull(capture.Failure, capture.Failure);
                            if (capture.TryGetBoardChoice(out _)) yield return ChooseComparisonCell(capture, board);
                            else yield return null;
                        }
                        // The last measured frame must complete before its recorder sample is read.
                        yield return null;
                        result = capture.Finish();
                        result.measuredSeconds = Time.realtimeSinceStartupAsDouble - started;
                        capture.CaptureProducerState();
                        result.warmupSeconds = warmup;
                        result.seed = DeterminismService.GetDebugSnapshot().Seed;
                        result.frozenStorageKeys = storage.BaselineKeys;
                        result.selectedBuildings = selectedBuildings;
                        result.targetFrameRate = Application.targetFrameRate;
                        result.vSyncCount = QualitySettings.vSyncCount;
                        result.fixedDeltaTime = Time.fixedDeltaTime;
                        result.engineVersion = Application.unityVersion;
                        Assert.IsTrue(result.completed, result.failure);
                        Assert.AreEqual(12345, result.seed);
                        Assert.Greater(result.frames.Count(frame => frame.measured), 100);
                        if (enabled) {
                            Assert.Greater(selectedBuildings.Count, 0, "The authored profile has no owned buildings.");
                            Assert.Greater(result.buildingsCreated, 0, "The ON case did not exercise building placement.");
                            Assert.Greater(result.buildingYields, 0, "The ON case did not exercise building production.");
                        }
                        else Assert.AreEqual(0, result.buildingsCreated);
                        Assert.IsTrue(result.markerAvailable[0], "Main Thread timing is required.");
                        Assert.IsTrue(result.markerAvailable[1], "GC timing is required.");
                        Assert.IsTrue(result.samplesMs.Where((value, index) => index % result.markers.Count == 0).Any(value => value > 0),
                            "Main Thread recorder returned no completed-frame timings.");
                        result.validated = true;
                    }
                    finally
                    {
                        if (capture != null)
                        {
                            result ??= capture.Finish();
                            capture.Dispose();
                            results.Add(result);
                        }
                    }
                    yield return TearDownRuntime();
                    ReleaseBuildingComparisonObjects();
                }
                Assert.AreEqual(2, results.Count);
                Assert.AreEqual(results[0].targetFrameRate, results[1].targetFrameRate);
                Assert.AreEqual(results[0].vSyncCount, results[1].vSyncCount);
                Assert.AreEqual(results[0].fixedDeltaTime, results[1].fixedDeltaTime);
                pairValidated = true;
                TestContext.Out.WriteLine("Building comparison saved: " + output);
            }
            finally
            {
                _buildingComparisonActive = false;
                view?.Dispose();
                Application.runInBackground = oldRunInBackground;
                Application.targetFrameRate = previousTargetFrameRate;
                QualitySettings.vSyncCount = previousVSync;
                ProfilerDriver.deepProfiling = deep;
                Profiler.enabled = profilerEnabled;
                // Export after both timing windows, so OFF export garbage cannot become ON's measured GC.
                foreach (var result in results) WriteComparisonResult(output, result);
                if (pairValidated && results.Count == 2 && results.All(result => result.validated))
                {
                File.WriteAllText(Path.Combine(output, "complete.txt"),
                    "OFF and ON complete. Fresh authored profile, seed 12345, 300 FPS ceiling. Frame and stage times are in milliseconds; marker times are inclusive.\n"
                    + "No allocation counts/bytes/stacks or heap capture. GC markers are separate and must not be double-counted.\n"
                    + "Natural population/production differences are workload changes, not isolated idle building overhead.\n");
                }

            }
        }
        BuildingComparisonStorage _buildingComparisonStorage;
        bool _buildingComparisonActive;
        readonly List<Object> _buildingComparisonObjects = new();
        void RestoreBuildingComparison()
        {
            if (_buildingComparisonActive) return;
            _buildingComparisonStorage?.Dispose();
            _buildingComparisonStorage = null;
            ReleaseBuildingComparisonObjects();
        }
        void ReleaseBuildingComparisonObjects()
        {
            foreach (var item in _buildingComparisonObjects) if (item != null) Object.Destroy(item);
            _buildingComparisonObjects.Clear();
        }
        static IEnumerator ChooseComparisonCell(BuildingFrameCapture capture, ActivityRuntimeSnapshot board)
        {
            double readyDeadline = Time.realtimeSinceStartupAsDouble + StartupTimeoutSeconds;
            while (!capture.TryGetBoardChoice(out _) && Time.realtimeSinceStartupAsDouble < readyDeadline) yield return null;
            Assert.IsTrue(capture.TryGetBoardChoice(out var target), "The next complete board was not available.");
            Assert.IsTrue(TryFindBoardHost(board.Id,
                AssetDatabase.LoadAssetAtPath<CapabilityHostData>(BoardHostPath), out var boardHost));
            var targets = new List<EntityId> { target };
            if (capture.IsBuildingCard(target)
                && CapabilityHostService.TryGet(target, out var selectedHost))
            {
                // Use an existing legal chain, never replace board contents or grant upgrade currency.
                var selectionAgent = AssetDatabase.LoadAssetAtPath<Core.Orchestration.AgentDefinitionData>(
                    "Assets/Game/Activities/Board/Agents/BoardSelectionAgent.asset");
                var segment = selectionAgent.TargetSelectionCriteria.Select(entry => entry.Criterion)
                    .OfType<Core.Orchestration.SegmentLengthCriterionData>().Single();
                var chain = ResolveConnectedMarkerSelection(board,
                    AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardCellTagPath),
                    (CapabilityHostData)selectedHost.Definition, 3, segment.MaximumDistance);
                if (chain.Count == 0)
                    chain = ResolveConnectedMarkerSelection(board,
                        AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardCellTagPath),
                        (CapabilityHostData)selectedHost.Definition, 2, segment.MaximumDistance);
                if (chain.Count != 0) targets = chain;
            }
            var request = SelectionIntentEvent.Begin(board.Id,
                AssetDatabase.LoadAssetAtPath<TaxonomyTermData>(BoardMergeSelectionPath), EntityId.Invalid, boardHost);
            var selection = new SelectionResultCapture(request.RequestId);
            EventBus.Register<SelectionResultEvent>(selection);
            try
            {
                foreach (var selectedTarget in targets) capture.MarkChoice(selectedTarget);
                EventBus.Trigger(request);
                yield return SubmitSelectionTargetsAcrossSteps(request, targets, selection);
                capture.MarkInput(); EventBus.Trigger(SelectionIntentEvent.Complete(request));
                double deadline = Time.realtimeSinceStartupAsDouble + StartupTimeoutSeconds;
                while (selection.Count == 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.AreEqual(1, selection.Count);
                Assert.AreEqual(SelectionResultType.Committed, selection.Result.Type, selection.Result.Message);
                deadline = Time.realtimeSinceStartupAsDouble + StartupTimeoutSeconds;
                while (targets.Any(CapabilityHostService.Exists) && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.IsFalse(targets.Any(CapabilityHostService.Exists),
                    "A committed board choice did not consume its selected cells.");
            }
            finally { EventBus.Unregister<SelectionResultEvent>(selection); }
            yield return BuyAvailableComparisonBuildingUpgrade(capture);
        }
        static IEnumerator BuyAvailableComparisonBuildingUpgrade(BuildingFrameCapture capture)
        {
            Assert.IsTrue(TryFindRunningActivities(out var battle, out _));
            var owner = battle.Participants.Single(participant => participant.ControlType == Core.Players.PlayerControlType.LocalHuman)
                .ParticipantEconomyOwner;
            foreach (string name in new[] { "Infantry", "Ranged", "Heavy" })
            {
                var action = AssetDatabase.LoadAssetAtPath<ProductionUIActionData>(
                    "Assets/Game/Activities/Buildings/UI/" + name + "RunUpgradeAction.asset");
                var price = action.Recipe.Inputs[0];
                Assert.IsTrue(price.TryResolveAmount(0, out long minimumCost, out var priceFailure), priceFailure);
                var funds = EconomyService.Query(new EconomySelectionQuery(owner, price.WalletTags,
                    price.FormType, price.Asset));
                if (funds.Items.Sum(item => item.Amount) < minimumCost) continue;
                var entries = EconomyService.Query(new EconomySelectionQuery(owner,
                    action.RecipientSelection.WalletTags, EconomyFormType.Stack,
                    action.RecipientSelection.ExactAsset, EconomyAggregationType.Detailed));
                if (entries.Items.Length == 0) continue;
                Assert.AreEqual(1, entries.Items.Length);
                var requestId = Guid.NewGuid();
                var response = new BuildingPurchaseCapture(requestId);
                EventBus.Register<ProductionUIResponse>(response);
                try
                {
                    EventBus.Trigger(new ProductionUIRequest(requestId, default, ProductionUIRequestType.Evaluate,
                        action, entries.Items[0].Handle, owner: owner));
                    Assert.IsTrue(response.HasEvaluation, "The existing purchase adapter did not answer evaluation.");
                    Assert.IsTrue(string.IsNullOrEmpty(response.Evaluation.Failure), response.Evaluation.Failure);
                    if (!response.Evaluation.Evaluation.Available) continue;
                    capture.MarkInput();
                    EventBus.Trigger(new ProductionUIRequest(requestId, default, ProductionUIRequestType.Order,
                        action, entries.Items[0].Handle, owner: owner));
                    double deadline = Time.realtimeSinceStartupAsDouble + StartupTimeoutSeconds;
                    while (!response.HasOrder && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.IsTrue(response.HasOrder, "The ordinary building upgrade purchase did not return an order.");
                    Assert.IsTrue(string.IsNullOrEmpty(response.Order.Failure), response.Order.Failure);
                    Assert.IsTrue(response.Order.Order.Id.IsValid);
                }
                finally
                {
                    EventBus.Trigger(new ProductionUIRequest(requestId, default, ProductionUIRequestType.Unobserve));
                    EventBus.Unregister<ProductionUIResponse>(response);
                }
            }
        }
        sealed class BuildingPurchaseCapture : IEventListener<ProductionUIResponse>
        {
            readonly Guid _request;
            internal bool HasEvaluation, HasOrder;
            internal ProductionUIResponse Evaluation, Order;
            internal BuildingPurchaseCapture(Guid request) => _request = request;
            public void OnEvent(ProductionUIResponse response)
            {
                if (response.RequestId != _request) return;
                if (response.Type == ProductionUIRequestType.Evaluate) { Evaluation = response; HasEvaluation = true; }
                if (response.Type == ProductionUIRequestType.Order) { Order = response; HasOrder = true; }
            }
        }
        static double ComparisonSeconds(string variable, double fallback, double minimum, double maximum)
        {
            string raw = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            Assert.IsTrue(double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double value)
                && value >= minimum && value <= maximum, "Invalid comparison duration: " + variable);
            return value;
        }
        static void WriteComparisonResult(string output, BuildingCaptureResult result)
        {
            string json = JsonUtility.ToJson(result);
            Assert.Less(System.Text.Encoding.UTF8.GetByteCount(json), 100 * 1024 * 1024, "Export budget exceeded.");
            File.WriteAllText(Path.Combine(output, result.buildingsEnabled ? "on.json" : "off.json"), json);
        }
    }
}
