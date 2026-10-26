using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChainRush.Board;
using Core.Activities.Selection;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Activities.Events;
using Core.EditorTools;
using Core.Events;
using EntityId = Core.Entities.EntityId;
using Core.Production.Authoring;
using Core.Production.Events;
using Core.Projection;
using Core.UI.Activities;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.EventSystems;

namespace ChainRush.Diagnostics.Editor
{
    // minimum seam: an explicitly armed Editor capture observes the real board lifecycle.
    [InitializeOnLoad]
    public static class ChainRushBoardSpawnCapture
    {
        static Session _session;
        static readonly ProfilerMarker ObserverMarker = new("Profiler.Morboo.BoardSpawn.Observer");
        static readonly ProfilerMarker<long, long, long> ActivationMarker =
            new("Profiler.Morboo.BoardSpawn.Activation", "ParentActivity", "RuntimeFrame", "PreRollComplete");
        static readonly ProfilerMarker<long, long, long> FullBoardMarker =
            new("Profiler.Morboo.BoardSpawn.FullBoard", "Activity", "ExpectedCells", "BoundCells");
        static readonly ProfilerMarker<long, long, long> ExperienceMarker =
            new("Profiler.Morboo.BoardSpawn.Experience", "ProductionEntity", "OldValue", "NewValue");
        static readonly ProfilerMarker<long, long, long> EarnedTurnMarker =
            new("Profiler.Morboo.BoardSpawn.EarnedTurn", "ProductionEntity", "YieldIndex", "BoardActivity");
        static readonly ProfilerMarker<long, long, long> SelectionMarker =
            new("Profiler.Morboo.BoardSpawn.Selection", "Activity", "Entity", "ResultType");
        static readonly ProfilerMarker<long, long, long> RefillMarker =
            new("Profiler.Morboo.BoardSpawn.Refill", "Activity", "OldRemaining", "NewBound");
        static readonly ProfilerMarker<long, long, long> RemovedMarker =
            new("Profiler.Morboo.BoardSpawn.OldRemoved", "Activity", "Entity", "RemovedCount");
        static readonly ProfilerMarker<long, long, long> FirstFullBoardMarker =
            new("Profiler.Morboo.BoardSpawn.FirstFullBoard", "Activity", "ExpectedCells", "BoundCells");

        static ChainRushBoardSpawnCapture()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += () => _session?.Abort("assembly reload");
            EditorApplication.quitting += () => _session?.Abort("Editor quitting");
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) _session?.Abort("Play Mode stopped");
            };
        }

        public static object Status => _session?.Evidence;

        public static void Arm(string directory, double beforeSeconds, double afterSeconds,
            double maximumSeconds, int maximumFrames, int historyFrames, Core.GameFlow.GameFlowEventTrigger startLevel = null,
            bool allocationCallstacks = true, bool captureRefill = false, bool captureBothFills = false,
            bool allocationProbe = false, double probeMaximumSeconds = 10, int probeMaximumFrames = 128,
            bool freezeCpuWindow = false)
        {
            if (Application.dataPath != "/Users/ionrain/ChainRush/Assets")
                throw new InvalidOperationException("Wrong project for board capture.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Arm the capture in the running, compiled ChainRush scene.");
            if (_session != null && !_session.Evidence.finished)
                throw new InvalidOperationException("A board capture is already active.");
            if (beforeSeconds < 0 || afterSeconds < 0 || maximumSeconds <= beforeSeconds + afterSeconds
                || maximumFrames <= 0 || historyFrames <= maximumFrames || historyFrames > 2000)
                throw new ArgumentOutOfRangeException(nameof(maximumFrames));
            if (Directory.Exists(directory)) throw new IOException("Capture directory already exists.");
            if (captureBothFills && (!captureRefill || allocationCallstacks || allocationProbe)
                || allocationProbe && (!captureRefill || !allocationCallstacks || captureBothFills
                    || probeMaximumSeconds <= 0 || probeMaximumFrames <= 0 || probeMaximumFrames >= historyFrames))
                throw new ArgumentException("CPU capture and the bounded allocation probe are separate modes.");
            foreach (var board in Resources.FindObjectsOfTypeAll<BoardUIController>())
                if (board.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("The board is already open; initial spawning would be missed.");
            _session = new Session(directory, beforeSeconds, afterSeconds, maximumSeconds, maximumFrames, historyFrames, startLevel,
                allocationCallstacks, captureRefill, captureBothFills, allocationProbe, probeMaximumSeconds, probeMaximumFrames, freezeCpuWindow);
        }

        public static void Cancel() => _session?.Abort("cancelled by user");

        static void Update()
        {
            if (_session == null || _session.Evidence.finished) return;
            try { _session.Update(); }
            catch (Exception exception) { _session.Abort(exception.ToString()); }
        }

        [Serializable]
        public sealed class CaptureEvidence
        {
            public string project, startedAtUtc, directory, status, failure, exportMessage, allocationCallstackStatus;
            public bool finished, captureComplete, settingsRestored, detailsTruncated, exportComplete;
            public bool allocationCallstacks, deepProfile, binaryLogging, textTrace, analysisDataComplete;
            public long allocationSamples, allocationSamplesWithCallstacks, allocationSamplesMissingBytes, orchestrationSamples;
            public double beforeSeconds, afterSeconds, maximumSeconds, elapsedSeconds;
            public int maximumFrames, historyFrames, firstFrame = -1, lastFrame = -1;
            public int activationFrame = -1, fullBoardFrame = -1, firstDisplayedAfterFullBoard = -1;
            public int parentActivity, boardActivity, expectedCells, boundCells, screenWidth, screenHeight;
            public bool levelStartTriggered;
            public bool preRollComplete, postRollComplete, overwritten;
            public bool captureRefill, refillWindowPinned, selectionCommitted, requiresRecordedFrameExtraction;
            public bool captureBothFills, allocationProbe, allocationProbeComplete;
            public double probeMaximumSeconds;
            public int probeMaximumFrames, allocationWindowFirstFrame = -1, allocationWindowLastFrame = -1;
            public int firstBoardFullFrame = -1, firstBoardDisplayedFrame = -1, selectionFrame = -1, selectionCommittedFrame = -1;
            public int experienceCreditFrame = -1, experienceSpentFrame = -1, earnedTurnFrame = -1, refillTriggerFrame = -1;
            public int firstOldRemovalFrame = -1, firstNewBindingFrame = -1, selectedEntity, selectedGridX, selectedGridY;
            public int earnedTurns, oldRemoved, oldRemaining, newBound;
            public long experienceCredited, experienceSpent;
            public string selectedView;
            public List<int> firstBoardEntities = new();
            public List<int> secondBoardEntities = new();
            // minimum seam: explain collection pressure using the actual heap and
            // Editor lifecycle, without requesting a collection or changing GC mode.
            public long managedUsedBefore, managedUsedAfter, managedReservedBefore, managedReservedAfter;
            public long unityAllocatedBefore, unityAllocatedAfter;
            public int collections0Before, collections0After, collections1Before, collections1After, collections2Before, collections2After;
            public bool incrementalGC, enterPlayModeOptionsEnabled;
            public ulong incrementalSliceNanoseconds;
            public string gcMode, enterPlayModeOptions;
            public string frameAccounting = "Completed CPU frames; startup/ordinary/spawn/post-spawn remain distinct. Worker CPU is not added to main-thread wall time.";
            public List<LifecycleObservation> observations = new();
            public WalletTopologyEvidence walletTopology;
        }

        // minimum seam: read only after CPU recording stops, identical for both versions.
        [Serializable]
        public sealed class WalletTopologyEvidence
        {
            public string timing = "After the CPU window, before Play cleanup";
            public string activityAttribution = "Participant owner identity in parent/board snapshots; other owners remain unassigned";
            public int owners, registeredAddresses, closingAddresses, addressesWithMultipleRetainers;
            public List<WalletOwnerEvidence> distribution = new();
        }

        [Serializable]
        public sealed class WalletOwnerEvidence
        {
            public string ownerType, stableKey;
            public int reference, walletCount;
            public List<int> participantActivities = new();
            public List<WalletAddressEvidence> addresses = new();
        }

        [Serializable]
        public sealed class WalletAddressEvidence
        {
            public string walletKey;
            public int reference, retainers;
            public bool closing;
        }

        static WalletTopologyEvidence ReadWalletTopology(int parentActivity, int boardActivity)
        {
            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            object core = typeof(EconomyService).GetField("Core", flags).GetValue(null);
            var registrations = (System.Collections.IDictionary)core.GetType().GetField("_walletRegistrations", flags).GetValue(core);
            var owners = new List<IEconomyAssetOwner>();
            var result = new WalletTopologyEvidence();
            foreach (System.Collections.DictionaryEntry pair in registrations)
            {
                var address = (EconomyAddress)pair.Key;
                int index = owners.FindIndex(owner => ReferenceEquals(owner, address.Owner));
                if (index < 0)
                {
                    index = owners.Count; owners.Add(address.Owner);
                    result.distribution.Add(new WalletOwnerEvidence { ownerType = address.Owner.GetType().FullName,
                        stableKey = address.Owner.StableSimulationKey,
                        reference = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(address.Owner) });
                }
                var state = pair.Value;
                int retainers = (int)state.GetType().GetField("References", flags).GetValue(state);
                bool closing = (bool)state.GetType().GetField("Closing", flags).GetValue(state);
                var row = result.distribution[index]; row.walletCount++;
                row.addresses.Add(new WalletAddressEvidence { walletKey = address.Container.StableSimulationKey,
                    reference = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(address.Container),
                    retainers = retainers, closing = closing });
                result.registeredAddresses++;
                if (closing) result.closingAddresses++;
                if (retainers > 1) result.addressesWithMultipleRetainers++;
            }
            foreach (int activity in new[] { parentActivity, boardActivity })
                if (Core.Activities.ActivityService.TryGetSnapshot(new Core.Activities.ActivityId(activity), out var snapshot))
                    foreach (var participant in snapshot.Participants)
                    {
                        int index = owners.FindIndex(owner => ReferenceEquals(owner, participant.ParticipantEconomyOwner));
                        if (index >= 0 && !result.distribution[index].participantActivities.Contains(activity))
                            result.distribution[index].participantActivities.Add(activity);
                    }
            result.owners = owners.Count;
            return result;
        }

        [Serializable]
        public sealed class LifecycleObservation
        {
            public string stage;
            public int frame, activity, expected, bound;
            public int entity;
            public long oldValue, newValue;
            public double elapsedSeconds;
        }

        sealed class Session : IDisposable, IEventListener<ActivityChildActivationEvent>, IEventListener<ActivityReadyEvent>,
            IEventListener<CapabilityHostRegisteredEvent>, IEventListener<EconomyResourceChangedEvent>,
            IEventListener<ProductionOrderYieldedEvent>, IEventListener<SelectionResultEvent>, IEventListener<ProjectionLifecycleEvent>
        {
            static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            static readonly FieldInfo ContextField = typeof(BoardUIController).GetField("_context", Hidden);
            static readonly FieldInfo CellsField = typeof(BoardUIController).GetField("_cells", Hidden);
            static readonly FieldInfo ItemField = typeof(BoardCellView).GetField("_itemView", Hidden);
            static readonly FieldInfo SelectionLockedField = typeof(BoardUIController).GetField("_selectionLocked", Hidden);
            static readonly FieldInfo PendingSelectionField = typeof(BoardUIController).GetField("_pendingBeginIntent", Hidden);
            static readonly PropertyInfo HistorySetting = FindHistorySetting();

            static PropertyInfo FindHistorySetting()
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType("UnityEditor.Profiling.ProfilerUserSettings");
                    var property = type?.GetProperty("frameCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property != null && property.CanRead && property.CanWrite) return property;
                }
                throw new MissingMemberException("Unity Profiler frame history setting is unavailable.");
            }
            readonly bool _enabled, _binary, _deep, _editor, _stacks, _cpu, _memory;
            readonly int _history;
            readonly bool _background;
            readonly EditorWindow _previousFocus;
            EditorWindow _createdGameView;
            Core.GameFlow.GameFlowEventTrigger _startLevel;
            readonly double _armed;
            readonly ulong _previousFrameStart;
            readonly ProfilerDumpStorageBudget _budget;
            readonly ProfilerCapturePreparation _preparation;
            IDisposable _numericCapture;
            IProfilerWindowExport _export;
            BoardUIController _board;
            ActivityUIContext _context;
            List<BoardCellView> _cells;
            CapabilityHostBaseData _playerSpawnerDefinition;
            EconomyAssetData _experience;
            ProductionRecipeData _turnTokenRecipe;
            EntityId _producerEntity;
            string _playerOwnerKey;
            SelectionRequestId _selectionRequest;
            readonly HashSet<int> _firstEntities = new();
            readonly HashSet<int> _removedOldEntities = new();
            readonly HashSet<int> _currentNewEntities = new();
            double _firstFullTime = -1;
            bool _selectionIssued;
            double _allocationProbeStart = -1;
            double _activationTime = -1, _fullTime = -1, _fullDisplayedTime = -1;
            int _lastObserved = -1, _lastBound = -1;
            bool _recording, _disposed;
            string _pendingError;
            public readonly CaptureEvidence Evidence;

            internal Session(string directory, double before, double after, double maximum, int frames, int history,
                Core.GameFlow.GameFlowEventTrigger startLevel, bool allocationCallstacks, bool captureRefill,
                bool captureBothFills, bool allocationProbe, double probeMaximumSeconds, int probeMaximumFrames, bool freezeCpuWindow)
            {
                if (ContextField == null || CellsField == null || ItemField == null)
                    throw new MissingFieldException("Board readiness observation fields changed.");
                if (captureRefill)
                {
                    if (SelectionLockedField == null || PendingSelectionField == null)
                        throw new MissingFieldException("Board selection observation fields changed.");
                    _playerSpawnerDefinition = AssetDatabase.LoadAssetAtPath<CapabilityHostBaseData>("Assets/Game/Activities/Autobattle/Economy/PlayerSpawner.asset");
                    _experience = AssetDatabase.LoadAssetAtPath<EconomyAssetData>("Assets/Game/Activities/Shared/Economy/Experience.asset");
                    _turnTokenRecipe = AssetDatabase.LoadAssetAtPath<ProductionRecipeData>("Assets/Game/Activities/Autobattle/Production/ExperienceToTurnTokenRecipe.asset");
                    if (_playerSpawnerDefinition == null || _experience == null || _turnTokenRecipe == null)
                        throw new InvalidOperationException("Existing experience/turn production assets are missing.");
                }
                _enabled = Profiler.enabled; _binary = Profiler.enableBinaryLog;
                _deep = ProfilerDriver.deepProfiling; _editor = ProfilerDriver.profileEditor;
                _stacks = Profiler.enableAllocationCallstacks;
                _cpu = Profiler.GetAreaEnabled(ProfilerArea.CPU); _memory = Profiler.GetAreaEnabled(ProfilerArea.Memory);
                _history = (int)HistorySetting.GetValue(null);
                _background = Application.runInBackground;
                _previousFocus = EditorWindow.focusedWindow;
                _startLevel = startLevel;
                if (ProfilerDriver.lastFrameIndex >= 0)
                    using (var last = ProfilerDriver.GetRawFrameDataView(ProfilerDriver.lastFrameIndex, 0))
                        _previousFrameStart = last.valid ? last.frameStartTimeNs : 0;
                Evidence = new CaptureEvidence
                {
                    project = Application.dataPath, directory = Path.GetFullPath(directory),
                    startedAtUtc = DateTime.UtcNow.ToString("O"), status = "preparing",
                    beforeSeconds = before, afterSeconds = after, maximumSeconds = maximum,
                    maximumFrames = frames, historyFrames = history,
                    screenWidth = Screen.width, screenHeight = Screen.height,
                    allocationCallstacks = allocationCallstacks, deepProfile = false, binaryLogging = false,
                    allocationCallstackStatus = allocationCallstacks ? "requested; awaiting export" : "disabled by request",
                    captureRefill = captureRefill, requiresRecordedFrameExtraction = captureRefill || freezeCpuWindow,
                    captureBothFills = captureBothFills, allocationProbe = allocationProbe,
                    probeMaximumSeconds = probeMaximumSeconds, probeMaximumFrames = probeMaximumFrames,
                    textTrace = Core.GameRuntime.GameRuntimeDiagnostics.OrchestrationTraceEnabled,
                };
                Evidence.managedUsedBefore = Profiler.GetMonoUsedSizeLong();
                Evidence.managedReservedBefore = Profiler.GetMonoHeapSizeLong();
                Evidence.unityAllocatedBefore = Profiler.GetTotalAllocatedMemoryLong();
                Evidence.collections0Before = GC.CollectionCount(0);
                Evidence.collections1Before = GC.CollectionCount(1);
                Evidence.collections2Before = GC.CollectionCount(2);
                Evidence.incrementalGC = UnityEngine.Scripting.GarbageCollector.isIncremental;
                Evidence.incrementalSliceNanoseconds = UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds;
                Evidence.gcMode = UnityEngine.Scripting.GarbageCollector.GCMode.ToString();
                Evidence.enterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
                Evidence.enterPlayModeOptions = EditorSettings.enterPlayModeOptions.ToString();
                if (Evidence.textTrace)
                    throw new InvalidOperationException("Disable authored orchestration text tracing before this capture.");
                _budget = new ProfilerDumpStorageBudget(Evidence.directory);
                try
                {
                    Profiler.enabled = false; Profiler.enableBinaryLog = false;
                    ProfilerDriver.deepProfiling = false; ProfilerDriver.profileEditor = false;
                    // The separate probe starts stacks only at the real experience-earned turn.
                    Profiler.enableAllocationCallstacks = allocationCallstacks && !allocationProbe;
                    if (Profiler.enableAllocationCallstacks != (allocationCallstacks && !allocationProbe))
                        throw new InvalidOperationException("Profiler allocation callstack setting does not match the requested capture mode.");
                    Profiler.SetAreaEnabled(ProfilerArea.CPU, true); Profiler.SetAreaEnabled(ProfilerArea.Memory, true);
                    HistorySetting.SetValue(null, history);
                    Application.runInBackground = true;
                    FocusGameView();
                    _numericCapture = MorbooProfilerCaptureDumpTool.BeginOrchestrationProfilerCapture();
                    _preparation = MorbooProfilerCaptureDumpTool.PrepareCurrentCaptureContext();
                    EventBus.Register<ActivityChildActivationEvent>(this);
                    EventBus.Register<ActivityReadyEvent>(this);
                    if (captureRefill)
                    {
                        EventBus.Register<CapabilityHostRegisteredEvent>(this);
                        EventBus.Register<EconomyResourceChangedEvent>(this);
                        EventBus.Register<ProductionOrderYieldedEvent>(this);
                        EventBus.Register<SelectionResultEvent>(this);
                        EventBus.Register<ProjectionLifecycleEvent>(this);
                    }
                    Application.logMessageReceived += OnLog;
                    _armed = EditorApplication.timeSinceStartup;
                    _recording = true;
                    Evidence.status = "recording before board activation";
                    Save();
                    Profiler.enabled = true;
                }
                catch (Exception exception)
                {
                    Evidence.failure = exception.ToString(); Evidence.status = "incomplete"; Evidence.finished = true;
                    Dispose(); Save(); throw;
                }
            }

            public void OnEvent(ActivityChildActivationEvent e)
            {
                if (!_recording || _activationTime >= 0) return;
                using (ObserverMarker.Auto())
                {
                    _activationTime = EditorApplication.timeSinceStartup;
                    Evidence.parentActivity = e.ParentActivityId.Value;
                    Evidence.activationFrame = ProfilerDriver.lastFrameIndex;
                    Evidence.preRollComplete = _activationTime - _armed >= Evidence.beforeSeconds;
                    Evidence.status = "recording board creation";
                    using (ActivationMarker.Auto(e.ParentActivityId.Value, Time.frameCount, Evidence.preRollComplete ? 1 : 0)) { }
                    Observe("child activation", e.ParentActivityId.Value);
                }
            }

            public void OnEvent(ActivityReadyEvent e)
            {
                if (_recording && _activationTime >= 0)
                    using (ObserverMarker.Auto()) Observe("ActivityReady; not board completion", e.ActivityId.Value);
            }

            public void OnEvent(CapabilityHostRegisteredEvent e)
            {
                if (!_recording || !Evidence.captureRefill || e.Snapshot.Definition == null
                    || !e.Snapshot.Definition.Matches(_playerSpawnerDefinition)
                    || e.Snapshot.OwnerLevel != Core.Economy.EconomyOwnerLevelType.Instance
                    || e.Snapshot.Owner == null || !e.Snapshot.EntityId.IsValid
                    || Evidence.parentActivity > 0 && e.Snapshot.ActivityId.Value != Evidence.parentActivity)
                    return;
                if (_producerEntity.IsValid && _producerEntity.Value <= e.Snapshot.EntityId.Value) return;
                _producerEntity = e.Snapshot.EntityId;
                _playerOwnerKey = e.Snapshot.Owner.StableSimulationKey;
            }

            public void OnEvent(EconomyResourceChangedEvent e)
            {
                if (!_recording || !Evidence.captureRefill || e.Asset == null || !e.Asset.Matches(_experience)
                    || e.Owner == null || string.IsNullOrEmpty(_playerOwnerKey)
                    || !string.Equals(e.Owner.StableSimulationKey, _playerOwnerKey, StringComparison.Ordinal)) return;
                using (ExperienceMarker.Auto(_producerEntity.Value, e.OldValue, e.NewValue)) { }
                if (e.Delta > 0)
                {
                    Evidence.experienceCredited += e.Delta;
                    Evidence.experienceCreditFrame = ProfilerDriver.lastFrameIndex;
                }
                else if (e.Delta < 0)
                {
                    Evidence.experienceSpent -= e.Delta;
                    Evidence.experienceSpentFrame = ProfilerDriver.lastFrameIndex;
                }
                Observe("player experience changed", Evidence.parentActivity, _producerEntity.Value, e.OldValue, e.NewValue);
                PinRefillWindow();
            }

            public void OnEvent(ProductionOrderYieldedEvent e)
            {
                if (!_recording || !Evidence.captureRefill || e.ProductionEntityId != _producerEntity
                    || !string.Equals(e.RecipeId, _turnTokenRecipe.Id, StringComparison.Ordinal)) return;
                Evidence.earnedTurns++;
                if (Evidence.earnedTurnFrame < 0) Evidence.earnedTurnFrame = ProfilerDriver.lastFrameIndex;
                using (EarnedTurnMarker.Auto(e.ProductionEntityId.Value, e.YieldIndex, Evidence.boardActivity)) { }
                Observe("turn produced by experience recipe", Evidence.parentActivity, e.ProductionEntityId.Value);
                PinRefillWindow();
            }

            public void OnEvent(SelectionResultEvent e)
            {
                if (!_recording || !Evidence.captureRefill || !_selectionIssued || !_selectionRequest.IsValid
                    || e.RequestId != _selectionRequest || e.ActivityId.Value != Evidence.boardActivity) return;
                using (SelectionMarker.Auto(Evidence.boardActivity, Evidence.selectedEntity, (long)e.Type)) { }
                Observe("ordinary board selection result", Evidence.boardActivity, Evidence.selectedEntity, 0, (long)e.Type);
                if (e.Type != SelectionResultType.Committed)
                { _pendingError = "The single ordinary board selection was not committed: " + e.Type + "; " + e.Message; return; }
                Evidence.selectionCommitted = true;
                Evidence.selectionCommittedFrame = ProfilerDriver.lastFrameIndex;
                PinRefillWindow();
            }

            public void OnEvent(ProjectionLifecycleEvent e)
            {
                if (!_recording || !Evidence.captureRefill || !_selectionIssued
                    || e.EventType != ProjectionLifecycleEventType.Removed
                    || e.Handle.ActivityId.Value != Evidence.boardActivity || !_firstEntities.Contains(e.Handle.EntityId.Value)) return;
                if (!_removedOldEntities.Add(e.Handle.EntityId.Value)) return;
                Evidence.oldRemoved = _removedOldEntities.Count;
                using (RemovedMarker.Auto(Evidence.boardActivity, e.Handle.EntityId.Value, Evidence.oldRemoved)) { }
                if (Evidence.firstOldRemovalFrame < 0) Evidence.firstOldRemovalFrame = ProfilerDriver.lastFrameIndex;
                Observe("first-board projection removed", Evidence.boardActivity, e.Handle.EntityId.Value);
            }

            void OnLog(string message, string stack, LogType type)
            {
                if (_recording && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    _pendingError = message + "\n" + stack;
            }

            void FocusGameView()
            {
                Type type = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                { type = assembly.GetType("UnityEditor.GameView"); if (type != null) break; }
                if (type == null) throw new TypeLoadException("Unity Game View is unavailable.");
                EditorWindow gameView = null;
                foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if (type.IsInstanceOfType(window)) { gameView = window; break; }
                if (gameView == null) gameView = _createdGameView = EditorWindow.GetWindow(type);
                gameView.Focus();
            }

            internal void Update()
            {
                if (_recording && _startLevel != null && EditorApplication.timeSinceStartup - _armed >= Evidence.beforeSeconds)
                {
                    var trigger = _startLevel; _startLevel = null;
                    Evidence.levelStartTriggered = trigger.isActiveAndEnabled && trigger.TryTrigger();
                    if (!Evidence.levelStartTriggered) { Stop(false, "level start trigger rejected activation"); return; }
                }
                // Real UI dispatch stays outside the observer scope: its downstream work is gameplay CPU time.
                if (_recording && Evidence.captureRefill) TrySubmitOrdinarySelection();
                using (ObserverMarker.Auto()) UpdateObservation();
            }

            void TrySubmitOrdinarySelection()
            {
                if (_selectionIssued || Evidence.firstBoardDisplayedFrame < 0 || _board == null || _cells == null
                    || EditorApplication.timeSinceStartup - _armed >= Evidence.maximumSeconds
                    || EditorApplication.timeSinceStartup - _firstFullTime < Evidence.beforeSeconds
                    || (bool)SelectionLockedField.GetValue(_board)) return;
                BoardCellView selected = null;
                foreach (var cell in _cells)
                    if (cell != null && cell.EntityId.IsValid && (selected == null
                        || cell.GridCoordinates.y < selected.GridCoordinates.y
                        || cell.GridCoordinates.y == selected.GridCoordinates.y && cell.GridCoordinates.x < selected.GridCoordinates.x))
                        selected = cell;
                if (selected == null || EventSystem.current == null)
                    throw new InvalidOperationException("An existing board cell and EventSystem are required for ordinary input.");
                _selectionIssued = true;
                Evidence.selectionFrame = ProfilerDriver.lastFrameIndex;
                Evidence.selectedEntity = selected.EntityId.Value;
                Evidence.selectedGridX = selected.GridCoordinates.x;
                Evidence.selectedGridY = selected.GridCoordinates.y;
                Evidence.selectedView = (ItemField.GetValue(selected) as BoardItemView)?.name;
                Observe("one ordinary pointer selection submitted", Evidence.boardActivity, Evidence.selectedEntity);
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                selected.OnPointerDown(pointer);
                _selectionRequest = ((SelectionIntentEvent)PendingSelectionField.GetValue(_board)).RequestId;
                if (!_selectionRequest.IsValid) throw new InvalidOperationException("The ordinary pointer input did not create a selection request.");
                selected.OnPointerUp(pointer);
            }

            // minimum seam: before the real refill trigger only the bounded pre-roll is retained.
            // The first fill is compared with its already saved capture, never relabelled as this window.
            void TrimRefillPreRoll(int last)
            {
                if (last < 0) return;
                ulong lastStart;
                using (var view = ProfilerDriver.GetRawFrameDataView(last, 0))
                { if (!view.valid) return; lastStart = view.frameStartTimeNs; }
                ulong span = (ulong)(Evidence.beforeSeconds * 1000000000d);
                ulong cutoff = lastStart > span ? lastStart - span : 0;
                int first = Math.Max(ProfilerDriver.firstFrameIndex, Evidence.firstFrame < 0 ? last : Evidence.firstFrame);
                while (first < last)
                {
                    using var next = ProfilerDriver.GetRawFrameDataView(first + 1, 0);
                    if (!next.valid || next.frameStartTimeNs > cutoff) break;
                    first++;
                }
                Evidence.firstFrame = first;
                using (var view = ProfilerDriver.GetRawFrameDataView(first, 0))
                    Evidence.preRollComplete = view.valid && lastStart >= view.frameStartTimeNs
                        && lastStart - view.frameStartTimeNs >= span;
            }

            void PinRefillWindow()
            {
                if (Evidence.refillWindowPinned || Evidence.earnedTurns <= 0
                    || Evidence.experienceCredited <= 0 || Evidence.experienceSpent <= 0) return;
                using (ObserverMarker.Auto())
                {
                    if (!Evidence.captureBothFills && !Evidence.allocationProbe)
                        TrimRefillPreRoll(ProfilerDriver.lastFrameIndex);
                    if (Evidence.allocationProbe)
                    {
                        Profiler.enableAllocationCallstacks = true;
                        if (!Profiler.enableAllocationCallstacks)
                            throw new InvalidOperationException("Allocation probe stacks could not be enabled.");
                        // Exclude the mixed current frame. CPU times in this probe are never benchmark times.
                        Evidence.firstFrame = Evidence.allocationWindowFirstFrame = ProfilerDriver.lastFrameIndex + 2;
                        _allocationProbeStart = EditorApplication.timeSinceStartup;
                        Evidence.allocationCallstackStatus = "bounded probe; awaiting frozen-frame extraction";
                    }
                    Evidence.refillWindowPinned = true;
                    Evidence.refillTriggerFrame = ProfilerDriver.lastFrameIndex;
                    Evidence.status = "recording the experience-earned turn and second board cycle";
                    Observe("refill CPU window pinned", Evidence.boardActivity, Evidence.selectedEntity);
                }
            }

            void UpdateObservation()
            {
                if (!_recording)
                {
                    if (_export == null) return;
                    if (Profiler.enabled) { Abort("Profiler recording restarted during export"); return; }
                    _export.Step(1);
                    if (!_export.IsComplete && !_export.IsIncomplete) return;
                    Evidence.exportComplete = _export.IsComplete;
                    Evidence.detailsTruncated = _export.DetailsTruncated;
                    Evidence.exportMessage = _export.CompletionMessage;
                    Finish();
                    return;
                }
                Evidence.elapsedSeconds = EditorApplication.timeSinceStartup - _armed;
                if (_pendingError != null) { Stop(false, _pendingError); return; }
                int last = ProfilerDriver.lastFrameIndex;
                if (last < 0)
                {
                    if (Evidence.elapsedSeconds >= Evidence.maximumSeconds) Stop(false, "capture wall-time limit reached");
                    return;
                }
                bool fresh;
                using (var view = ProfilerDriver.GetRawFrameDataView(last, 0))
                    fresh = view.valid && view.frameStartTimeNs > _previousFrameStart;
                if (!fresh)
                {
                    if (Evidence.elapsedSeconds >= Evidence.maximumSeconds) Stop(false, "capture wall-time limit reached");
                    return;
                }
                if (Evidence.firstFrame < 0)
                {
                    Evidence.firstFrame = last;
                    for (int index = ProfilerDriver.firstFrameIndex; index <= last; index++)
                        using (var view = ProfilerDriver.GetRawFrameDataView(index, 0))
                            if (view.valid && view.frameStartTimeNs > _previousFrameStart)
                            { Evidence.firstFrame = index; break; }
                }
                Evidence.lastFrame = last;
                if (Evidence.captureRefill && !Evidence.refillWindowPinned && !Evidence.captureBothFills)
                    TrimRefillPreRoll(last);
                if ((!Evidence.captureRefill || Evidence.captureBothFills || Evidence.refillWindowPinned)
                    && ProfilerDriver.firstFrameIndex > Evidence.firstFrame)
                { Evidence.overwritten = true; Stop(false, "capture beginning was overwritten"); return; }
                if (last != _lastObserved)
                {
                    _lastObserved = last;
                    ObserveBoard();
                }
                if (Evidence.allocationProbe && Evidence.refillWindowPinned)
                {
                    Evidence.allocationWindowLastFrame = last;
                    // Capture cleanup, planning/Analytics and the first real replacement materialization.
                    // The main CPU mode separately proves all sixteen replacements and their display.
                    if (Evidence.selectionCommitted && Evidence.oldRemoved == Evidence.expectedCells
                        && Evidence.firstNewBindingFrame >= Evidence.allocationWindowFirstFrame
                        && last >= Evidence.firstNewBindingFrame + 2)
                    {
                        Evidence.allocationProbeComplete = true;
                        Stop(true, null);
                        return;
                    }
                    if (EditorApplication.timeSinceStartup - _allocationProbeStart >= Evidence.probeMaximumSeconds
                        || last - Evidence.allocationWindowFirstFrame + 1 >= Evidence.probeMaximumFrames)
                    {
                        Stop(false, "allocation probe limit reached before cleanup and replacement evidence");
                        return;
                    }
                }
                if (Evidence.captureRefill && Evidence.firstBoardFullFrame >= 0 && Evidence.firstBoardDisplayedFrame < 0
                    && last > Evidence.firstBoardFullFrame && HasCompletedRendering(last))
                {
                    Evidence.firstBoardDisplayedFrame = last;
                    Observe("first board displayed; waiting to submit one ordinary selection", Evidence.boardActivity);
                }
                if (_fullTime >= 0 && last > Evidence.fullBoardFrame && Evidence.firstDisplayedAfterFullBoard < 0)
                    if (HasCompletedRendering(last))
                    {
                        Evidence.firstDisplayedAfterFullBoard = last;
                        _fullDisplayedTime = EditorApplication.timeSinceStartup;
                        if (Evidence.captureRefill) Observe("second board completed a displayed frame", Evidence.boardActivity);
                    }
                double postRollStart = Evidence.captureRefill ? _fullDisplayedTime : _fullTime;
                if (postRollStart >= 0 && EditorApplication.timeSinceStartup - postRollStart >= Evidence.afterSeconds
                    && Evidence.firstDisplayedAfterFullBoard >= 0)
                { Evidence.postRollComplete = true; Stop(Evidence.preRollComplete, Evidence.preRollComplete ? null : "pre-board interval incomplete"); return; }
                if ((!Evidence.captureRefill || Evidence.captureBothFills || Evidence.refillWindowPinned) && last - Evidence.firstFrame + 1 >= Evidence.maximumFrames
                    || Evidence.elapsedSeconds >= Evidence.maximumSeconds)
                    Stop(false, "capture limit reached before complete board/post-board interval");
            }

            void ObserveBoard()
            {
                if (_activationTime < 0 || _fullTime >= 0) return;
                if (_board == null)
                    foreach (var board in Resources.FindObjectsOfTypeAll<BoardUIController>())
                        if (board.gameObject.activeInHierarchy)
                        {
                            _board = board;
                            _context = (ActivityUIContext)ContextField.GetValue(board);
                            _cells = (List<BoardCellView>)CellsField.GetValue(board);
                            break;
                        }
                if (_board != null && _context == null) _context = (ActivityUIContext)ContextField.GetValue(_board);
                if (_board == null || _context == null || _cells == null) return;
                Evidence.boardActivity = _context.ActivityId.Value;
                Evidence.expectedCells = _context.Cells.Count;
                int bound = 0;
                int oldRemaining = 0;
                if (Evidence.captureRefill) _currentNewEntities.Clear();
                foreach (var cell in _cells)
                    if (cell != null && cell.EntityId.IsValid && ItemField.GetValue(cell) is BoardItemView item
                        && item != null && item.gameObject.activeInHierarchy)
                    {
                        bound++;
                        if (Evidence.captureRefill)
                        {
                            if (_firstEntities.Contains(cell.EntityId.Value)) oldRemaining++;
                            else _currentNewEntities.Add(cell.EntityId.Value);
                        }
                    }
                Evidence.boundCells = bound;
                if (bound != _lastBound)
                { _lastBound = bound; Observe("bound board cells", Evidence.boardActivity); }
                if (Evidence.captureRefill && Evidence.firstBoardFullFrame >= 0)
                {
                    Evidence.oldRemaining = oldRemaining;
                    Evidence.newBound = _currentNewEntities.Count;
                    if (_selectionIssued && Evidence.newBound > 0 && Evidence.firstNewBindingFrame < 0)
                    {
                        Evidence.firstNewBindingFrame = Evidence.lastFrame;
                        using (RefillMarker.Auto(Evidence.boardActivity, oldRemaining, Evidence.newBound)) { }
                        Observe("new board generation binding started", Evidence.boardActivity);
                    }
                }
                if (Evidence.expectedCells <= 0 || _cells.Count != Evidence.expectedCells || bound != Evidence.expectedCells) return;
                if (Evidence.captureRefill)
                {
                    if (Evidence.firstBoardFullFrame < 0)
                    {
                        foreach (var cell in _cells)
                        {
                            if (!_firstEntities.Add(cell.EntityId.Value))
                                throw new InvalidOperationException("The first board contains duplicate Entity bindings.");
                            Evidence.firstBoardEntities.Add(cell.EntityId.Value);
                        }
                        _firstFullTime = EditorApplication.timeSinceStartup;
                        Evidence.firstBoardFullFrame = Evidence.lastFrame;
                        Evidence.status = "first board full; waiting for displayed frame and ordinary selection";
                        using (FirstFullBoardMarker.Auto(Evidence.boardActivity, Evidence.expectedCells, bound)) { }
                        Observe("first board has complete bound projections", Evidence.boardActivity);
                        return;
                    }
                    if (!Evidence.refillWindowPinned || !Evidence.selectionCommitted || oldRemaining != 0 || Evidence.newBound != Evidence.expectedCells
                        || Evidence.oldRemoved != Evidence.expectedCells) return;
                    foreach (var cell in _cells) Evidence.secondBoardEntities.Add(cell.EntityId.Value);
                }
                _fullTime = EditorApplication.timeSinceStartup;
                Evidence.fullBoardFrame = Evidence.lastFrame;
                Evidence.status = "recording after full board binding";
                using (FullBoardMarker.Auto(Evidence.boardActivity, Evidence.expectedCells, bound)) { }
                Observe(Evidence.captureRefill ? "second board has entirely new active bound projections" : "all board cells have active bound projections", Evidence.boardActivity);
            }

            static bool HasCompletedRendering(int frame)
            {
                using var view = ProfilerDriver.GetRawFrameDataView(frame, 0);
                if (!view.valid) return false;
                int marker = view.GetMarkerId("RenderPlayModeViewCameras");
                if (marker < 0) return false;
                for (int i = 0; i < view.sampleCount; i++)
                    if (view.GetSampleMarkerId(i) == marker && view.GetSampleTimeMs(i) > 0) return true;
                return false;
            }

            [Serializable]
            sealed class ExportStatus
            {
                public long allocationStackSamples, allocationSamplesWithCallstacks, allocationStackSamplesMissingBytes, orchestrationEvidenceSamples;
                public string allocationCallstackStatus;
            }

            void Observe(string stage, int activity, int entity = 0, long oldValue = 0, long newValue = 0) => Evidence.observations.Add(new LifecycleObservation
            {
                stage = stage, activity = activity, frame = ProfilerDriver.lastFrameIndex,
                elapsedSeconds = EditorApplication.timeSinceStartup - _armed,
                expected = Evidence.expectedCells, bound = Evidence.boundCells,
                entity = entity, oldValue = oldValue, newValue = newValue,
            });

            void Stop(bool complete, string failure)
            {
                _recording = false;
                Profiler.enabled = false;
                Evidence.managedUsedAfter = Profiler.GetMonoUsedSizeLong();
                Evidence.managedReservedAfter = Profiler.GetMonoHeapSizeLong();
                Evidence.unityAllocatedAfter = Profiler.GetTotalAllocatedMemoryLong();
                Evidence.collections0After = GC.CollectionCount(0);
                Evidence.collections1After = GC.CollectionCount(1);
                Evidence.collections2After = GC.CollectionCount(2);
                Evidence.walletTopology = ReadWalletTopology(Evidence.parentActivity, Evidence.boardActivity);
                EventBus.Unregister<ActivityChildActivationEvent>(this);
                EventBus.Unregister<ActivityReadyEvent>(this);
                UnregisterRefillEvents();
                Application.logMessageReceived -= OnLog;
                _numericCapture?.Dispose(); _numericCapture = null;
                Evidence.captureComplete = complete;
                Evidence.failure = failure;
                if (Evidence.requiresRecordedFrameExtraction)
                {
                    // Frozen frames are read in bounded per-frame batches by the existing read-only control.
                    // Recording success is kept distinct from export/analysis success.
                    Evidence.status = complete ? "recorded for frame extraction" : "incomplete";
                    Evidence.finished = true;
                    Dispose(); Save(); return;
                }
                Evidence.status = "exporting frozen frame interval";
                Save();
                if (Evidence.firstFrame < 0) { Finish(); return; }
                _export = MorbooProfilerCaptureDumpTool.BeginCurrentWindowExport(Evidence.directory,
                    Evidence.firstFrame, Evidence.lastFrame, JsonUtility.ToJson(Evidence), _budget,
                    preparation: _preparation, orchestrationEvidence: true);
            }

            void Finish()
            {
                string manifest = Path.Combine(Evidence.directory, "cpu-window.morboo-profiler-aggregated", "manifest.json");
                if (Evidence.exportComplete && File.Exists(manifest))
                {
                    var exported = JsonUtility.FromJson<ExportStatus>(File.ReadAllText(manifest));
                    Evidence.allocationSamples = exported.allocationStackSamples;
                    Evidence.allocationSamplesWithCallstacks = exported.allocationSamplesWithCallstacks;
                    Evidence.allocationSamplesMissingBytes = exported.allocationStackSamplesMissingBytes;
                    Evidence.orchestrationSamples = exported.orchestrationEvidenceSamples;
                    bool stacksMatch = Evidence.allocationCallstacks
                        ? Evidence.allocationSamplesWithCallstacks > 0
                        : Evidence.allocationSamplesWithCallstacks == 0;
                    Evidence.allocationCallstackStatus = Evidence.allocationCallstacks
                        ? exported.allocationCallstackStatus
                        : stacksMatch ? "disabled by request" : "unexpected callstacks in CPU-only interval";
                    Evidence.analysisDataComplete = stacksMatch && Evidence.allocationSamples > 0
                        && Evidence.allocationSamplesMissingBytes == 0 && Evidence.orchestrationSamples > 0;
                    if (!Evidence.analysisDataComplete && Evidence.failure == null)
                        Evidence.failure = "allocation evidence, requested callstack mode or orchestration correlation incomplete";
                }
                Evidence.status = Evidence.captureComplete && Evidence.exportComplete && !Evidence.detailsTruncated && Evidence.analysisDataComplete
                    ? "complete" : "incomplete";
                Evidence.finished = true;
                Dispose();
                Save();
            }

            internal void Abort(string reason)
            {
                if (Evidence.finished) return;
                Evidence.failure = string.IsNullOrEmpty(Evidence.failure) ? reason : Evidence.failure + "\n" + reason;
                if (_recording) Evidence.captureComplete = false;
                Evidence.status = "incomplete"; Evidence.finished = true;
                try { _export?.Cancel(); }
                finally { Dispose(); Save(); }
            }

            // minimum seam: the routing table uses the normal export budget;
            // terminal control evidence stays within the reserved manifest space.
            void Save()
            {
                var topology = Evidence.walletTopology;
                if (topology != null)
                    _budget.WriteText(Path.Combine(Evidence.directory, "wallet-topology.json"), JsonUtility.ToJson(topology, true));
                Evidence.walletTopology = null;
                try
                {
                    _budget.WriteLimitManifest(Path.Combine(Evidence.directory, "board-capture.json"), JsonUtility.ToJson(Evidence, true));
                }
                finally { Evidence.walletTopology = topology; }
            }

            void UnregisterRefillEvents()
            {
                EventBus.Unregister<CapabilityHostRegisteredEvent>(this);
                EventBus.Unregister<EconomyResourceChangedEvent>(this);
                EventBus.Unregister<ProductionOrderYieldedEvent>(this);
                EventBus.Unregister<SelectionResultEvent>(this);
                EventBus.Unregister<ProjectionLifecycleEvent>(this);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true; _recording = false;
                try { _export?.Dispose(); }
                finally
                {
                    _export = null;
                    _board = null; _context = null; _cells = null; _startLevel = null;
                    _playerSpawnerDefinition = null; _experience = null; _turnTokenRecipe = null; _playerOwnerKey = null;
                    _numericCapture?.Dispose(); _numericCapture = null;
                    EventBus.Unregister<ActivityChildActivationEvent>(this);
                    EventBus.Unregister<ActivityReadyEvent>(this);
                    UnregisterRefillEvents();
                    Application.logMessageReceived -= OnLog;
                    Profiler.enabled = false;
                    Profiler.enableBinaryLog = _binary;
                    ProfilerDriver.deepProfiling = _deep; ProfilerDriver.profileEditor = _editor;
                    Profiler.enableAllocationCallstacks = _stacks;
                    Profiler.SetAreaEnabled(ProfilerArea.CPU, _cpu); Profiler.SetAreaEnabled(ProfilerArea.Memory, _memory);
                    HistorySetting.SetValue(null, _history);
                    Application.runInBackground = _background;
                    if (_createdGameView != null) { _createdGameView.Close(); _createdGameView = null; }
                    if (_previousFocus != null) _previousFocus.Focus();
                    Profiler.enabled = _enabled;
                    Evidence.settingsRestored = Profiler.enabled == _enabled && Profiler.enableBinaryLog == _binary
                        && ProfilerDriver.deepProfiling == _deep && ProfilerDriver.profileEditor == _editor
                        && Profiler.enableAllocationCallstacks == _stacks
                        && Profiler.GetAreaEnabled(ProfilerArea.CPU) == _cpu && Profiler.GetAreaEnabled(ProfilerArea.Memory) == _memory
                        && (int)HistorySetting.GetValue(null) == _history && Application.runInBackground == _background;
                    if (!Evidence.settingsRestored)
                    {
                        Evidence.status = "incomplete";
                        Evidence.failure = (Evidence.failure == null ? "" : Evidence.failure + "\n") + "Profiler settings were not restored";
                    }
                }
            }
        }
    }
}
