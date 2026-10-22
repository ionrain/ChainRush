#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Core.Activities;
using Core.Activities.Events;
using Core.AI;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Economy;
using Core.Entities;
using Core.Events;
using Core.GameRuntime;
using Core.Pooling;
using Core.Players;
using Core.Projection;
using Core.Scheduling;
using Core.Scheduling.Events;
using Core.World;
using Unity.Profiling;
using UnityEditorInternal;
using UnityEngine;
using EntityId = Core.Entities.EntityId;
using Object = UnityEngine.Object;

namespace ChainRush.Diagnostics
{
    // working model: a workload owner, using the production lifecycle rather than duplicating it.
    internal sealed partial class PerformanceBenchRuntime : IDisposable,
        IEventListener<PerformanceBenchCommandEvent>, IEventListener<PerformanceBenchPulseEvent>,
        IEventListener<PerformanceBenchCaptureCompletedEvent>, IEventListener<PerformanceBenchWindowCapturedEvent>,
        IEventListener<ScheduleStepStartedEvent>, IEventListener<ScheduleStepFinishedEvent>,
        IEventListener<ScheduleStepDispatchedEvent>, IEventListener<ProjectionLifecycleEvent>,
        IEventListener<AIBrainStateCompletedEvent>, IEventListener<MovementAgentStepEvent>, IEventListener<MovementAgentPoseChangedEvent>,
        IEventListener<MovementTrafficResolvedEvent>, IEventListener<NavigationRouteBuiltEvent>,
        IEventListener<NavigationRouteFailedEvent>, IEventListener<ActivityPreparationFailedEvent>,
        IEventListener<CapabilityHostRegisteredEvent>, IEventListener<PerformanceBenchViewEvent>
    {
        static readonly ProfilerMarker WorkMarker = new ProfilerMarker("PerformanceBench.Work");
        static readonly ProfilerMarker ObserverMarker = new ProfilerMarker("PerformanceBench.Observe");
        static readonly ProfilerMarker<long, long, long> FrameMarker = new("PerformanceBench.Frame", "Run", "Phase", "UnityFrame");
        readonly GameRuntimeContext _context;
        readonly PerformanceBenchAssets _assets;
        PerformanceBenchProfilerAdapter _profilerAdapter;
        PerformanceBenchCaptureType? _captureOverride;
        PerformanceBenchCaptureType _captureType;
        Core.EditorTools.ProfilerDumpStorageBudget _exportBudget;
        string _exportDirectory;
        bool _awaitingProfilerExport;
        bool _diagnosticsRestorePending;
        readonly Queue<PerformanceBenchSettings> _series = new();
        readonly List<PerformanceBenchResult> _results = new();
        readonly List<SpatialTransientMarkerHandle> _markers = new();
        readonly List<EntityId> _obstacles = new();
        readonly List<Object> _owned = new();
        readonly Dictionary<int, Actor> _byEntity = new();
        readonly List<Actor> _pending = new();
        readonly Queue<ActorContinuation> _patrolOrders = new(10000);
        readonly Queue<ActorContinuation> _readyCreations = new(10000);
        readonly FrameWork[] _frameWork = new FrameWork[4];
        readonly PerformanceBenchPhaseType[] _framePhases = new PerformanceBenchPhaseType[4];
        readonly bool[] _frameBoundaries = new bool[4];
        readonly int[] _phaseFrames = { -1, -1, -1, -1 };
        Actor[] _actors;
        TargetPair[] _targets;
        PerformanceBenchSettings _settings;
        PerformanceBenchPhaseType _phase;
        PerformanceBenchMeasurements _measurements;
        ActivityId _activity;
        ActivityRuntimeSnapshot _activitySnapshot;
        ActivityWorldEconomyOwner _worldOwner;
        CapabilityHostData _definition;
        ITickSource _tickSource;
        IPoolContext _pool;
        PoolKey _poolKey;
        Rect _viewBounds;
        Actor _materializing;
        int _run, _nextOperation, _nextBatch, _population, _bound, _visible, _cursor, _creatingCount, _deletingCount;
        bool _populationReady;
        int _stepsAfterReady;
        double _now, _phaseStarted, _nextUI, _nextCycle;
        int _probeStage;
        enum CycleType { Ready, Creating, Holding, Deleting, Replacing }
        CycleType _cycle;
        bool _prepared, _stop, _interrupted, _disposed, _admitting, _hasFailed;
        string _failure, _message;
        PerformanceBenchStopReasonType _stopReason;
        readonly PerformanceBenchRunLimits _limits = new();
        Task _retirement;
        readonly MovementTrafficProfileData _originalTraffic;
        readonly RuntimeDiagnosticsProfile _originalDiagnostics;

        sealed class Actor
        {
            public int Slot, Life, Batch;
            public EntityId Entity;
            public EconomyEntryHandle Token;
            public EconomyOperationHandle Pending;
            public bool Creating, Deleting, Ready, Right, OrderQueued, EntityRemoved, ViewReleased;
            public int LastMovedFrame = -1, LastWaitingFrame = -1;
            public ProjectionInstanceHandle Binding;
            public Renderer Renderer;
            public SpatialTransientMarkerHandle PlacementMarker;
            public SpatialMarkerSelectionLease MarkerLease;
            public SpatialReservation Reservation;
            public PerformanceBenchOperation Operation;
        }
        struct TargetPair { public EntityId Left, Right; }
        readonly struct ActorContinuation
        {
            public readonly Actor Actor;
            public readonly int Life, Run;
            public readonly ActivityId Activity;
            public readonly ProjectionInstanceHandle Binding;
            public ActorContinuation(Actor actor, int run, ActivityId activity)
            { Actor = actor; Life = actor.Life; Run = run; Activity = activity; Binding = actor.Binding; }
        }
        struct FrameWork
        {
            public int ScreenWidth, ScreenHeight, FirstStepAfterReady, LastStepAfterReady;
            public int Frame, Steps, Moved, Waiting, Traffic, Built, Failed, Created, Deleted, Rented, Expanded;
            public int Entities, Bound, Visible, Pending, Helpers, OccupancyChecks, OccupancyViolations, Blocked;
            public int MovementCompleted, PoseReachedTarget, PatrolSucceeded;
        }

        public PerformanceBenchRuntime(GameRuntimeContext context, PerformanceBenchAssets assets)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _assets = assets != null ? assets : throw new ArgumentNullException(nameof(assets));
            _originalTraffic = assets.trafficProfile; _originalDiagnostics = context.Profile.Diagnostics;
            _profilerAdapter = new PerformanceBenchProfilerAdapter(_context);
            EventBus.Register<PerformanceBenchCaptureCompletedEvent>(this);
            EventBus.Register<PerformanceBenchWindowCapturedEvent>(this);
            EventBus.Register<PerformanceBenchCommandEvent>(this); EventBus.Register<PerformanceBenchPulseEvent>(this);
            EventBus.Register<ScheduleStepStartedEvent>(this); EventBus.Register<ScheduleStepFinishedEvent>(this);
            EventBus.Register<ScheduleStepDispatchedEvent>(this); EventBus.Register<ProjectionLifecycleEvent>(this);
            EventBus.Register<AIBrainStateCompletedEvent>(this); EventBus.Register<MovementAgentStepEvent>(this); EventBus.Register<MovementAgentPoseChangedEvent>(this);
            EventBus.Register<MovementTrafficResolvedEvent>(this); EventBus.Register<NavigationRouteBuiltEvent>(this);
            EventBus.Register<NavigationRouteFailedEvent>(this); EventBus.Register<ActivityPreparationFailedEvent>(this);
            EventBus.Register<CapabilityHostRegisteredEvent>(this);
            EventBus.Register<PerformanceBenchViewEvent>(this);
            EntityService.EntityRemoved += OnEntityRemoved;
            GameRuntimeProfilerWorkloadContext.Clear();
        }

        public void OnEvent(PerformanceBenchCommandEvent e)
        {
            if (_disposed || _retirement != null) return;
            bool started = false;
            try
            {
                switch (e.Command)
                {
                    case PerformanceBenchCommandType.Start:
                        RequireIdle(); RequireSettings(e.Settings); ValidateCaptureType(e.Settings, e.CaptureType); _captureOverride = e.CaptureType; _series.Clear(); _series.Enqueue(e.Settings.Copy()); started = true; BeginNextRun(); break;
                    case PerformanceBenchCommandType.RunSeries:
                        RequireIdle(); _captureOverride = e.CaptureType;
                        if (e.Series.Count == 0) throw new InvalidOperationException("Серия пуста.");
                        foreach (var item in e.Series) { RequireSettings(item); ValidateCaptureType(item, e.CaptureType); }
                        PerformanceBenchSeries.ValidatePopulation(e.Series);
                        _series.Clear();
                        foreach (var item in e.Series) _series.Enqueue(item.Copy());
                        started = true; BeginNextRun(); break;
                    case PerformanceBenchCommandType.Stop:
                    case PerformanceBenchCommandType.Clear:
                        _series.Clear(); _stop = true; _interrupted = true; _stopReason = PerformanceBenchStopReasonType.User;
                        if (IsActive) SetPhase(PerformanceBenchPhaseType.Draining); break;
                    case PerformanceBenchCommandType.CreateBatch:
                        RequireMeasurement(); AdmitCreate(_settings.batchSize); break;
                    case PerformanceBenchCommandType.DeleteBatch:
                        RequireMeasurement(); AdmitDelete(_settings.batchSize); break;
                    case PerformanceBenchCommandType.Export:
                        if (IsActive) throw new InvalidOperationException("Экспорт доступен после завершения и очистки.");
                        WriteSeriesSummary(); _message = "Результаты сохранены в ProfilerCaptures/PerformanceBench."; break;
                }
            }
            catch (Exception exception)
            {
                _message = exception.Message;
                if (started && IsActive) Fail(exception.Message);
            }
            PublishState();
        }
        bool IsActive => _phase >= PerformanceBenchPhaseType.Preparing && _phase <= PerformanceBenchPhaseType.Cleanup;
        void RequireIdle()
        {
            if (IsActive || _awaitingProfilerExport) throw new InvalidOperationException("Дождитесь очистки и завершения экспорта текущего запуска.");
            if (!_context.IsInitialized || _viewBounds.width <= 0) throw new InvalidOperationException("Runtime или камера стенда ещё не готовы.");
            if (ProfilerDriver.deepProfiling) throw new InvalidOperationException("Выключите Deep Profile перед запуском стенда.");
        }
        static void ValidateCaptureType(PerformanceBenchSettings settings, PerformanceBenchCaptureType? requested)
        {
            if (!requested.HasValue) return;
            if (!Enum.IsDefined(typeof(PerformanceBenchCaptureType), requested.Value)
                || (requested.Value == PerformanceBenchCaptureType.TraceWindow || requested.Value == PerformanceBenchCaptureType.SkillsSummary) != settings.trace)
                throw new ArgumentException("Capture type must agree with the diagnostic flag.");
        }
        static void RequireSettings(PerformanceBenchSettings settings)
        {
            if (settings == null) throw new ArgumentException("Не заданы параметры запуска.");
            if (!settings.TryValidate(out string failure)) throw new ArgumentException(failure);
        }
        void RequireMeasurement()
        {
            if (_phase != PerformanceBenchPhaseType.Measuring || _stop) throw new InvalidOperationException("Команда пакета доступна во время измерения.");
            if (_pending.Count > 0) throw new InvalidOperationException("Предыдущий пакет ещё выполняется.");
        }
        void BeginNextRun()
        {
            _settings = _series.Dequeue(); _run++; _nextOperation = 0; _nextBatch = 0;
            _populationReady = false; _stepsAfterReady = 0; _stepStarts.Clear();
            _limits.Reset(); _stopReason = PerformanceBenchStopReasonType.None;
            _failure = null; _message = null; _stop = false; _interrupted = false; _prepared = false; _hasFailed = false;
            _environment = null;
            _population = 0; _bound = 0; _visible = 0; _cursor = 0;
            _creatingCount = 0; _deletingCount = 0;
            _probeStage = 0;
            _cycle = CycleType.Ready;
            _actors = new Actor[_settings.population];
            for (int i = 0; i < _actors.Length; i++) _actors[i] = new Actor { Slot = i };
            _byEntity.EnsureCapacity(_settings.population); _pending.Capacity = _settings.population;
            _measurements = new PerformanceBenchMeasurements();
            _phaseIntervals.Clear(); _phaseFrames[0] = _phaseFrames[1] = _phaseFrames[2] = _phaseFrames[3] = -1;
            _captureType = _captureOverride ?? (_settings.trace ? PerformanceBenchCaptureType.TraceWindow : PerformanceBenchCaptureType.Normal);
            if (!Enum.IsDefined(typeof(PerformanceBenchCaptureType), _captureType)
                || (_captureType == PerformanceBenchCaptureType.TraceWindow || _captureType == PerformanceBenchCaptureType.SkillsSummary) != _settings.trace)
                throw new ArgumentException("Режим profiler window должен соответствовать флагу подробной диагностики.");
            PrepareExportDirectory();
            ConfigureDiagnostics();
            var boxed = (object)_originalTraffic;
            SetField(boxed, "maxSolvedAgentsPerStep", _settings.movementBudget == PerformanceBenchMovementBudgetType.Current64 ? 64 : _settings.population);
            MovementService.Install((MovementTrafficProfileData)boxed);
            _definition = PrepareDefinition();
            SetPhase(PerformanceBenchPhaseType.Preparing);
            CaptureEnvironment();
            var activity = _settings.scenario == PerformanceBenchScenarioType.Navigation ? _assets.navigationActivity : _assets.straightActivity;
            if (!PlayerService.TryGet(_context.PrimaryOwner, out var player))
                throw new InvalidOperationException("Не зарегистрирован участник стенда.");
            var roster = new List<ActivityParticipantBinding> { new(player.PlayerEntityId, player.EconomyOwner,
                0, player.ControlType, player.IsLocallyPresent, false, default) };
            var result = ActivityService.Start(new ActivityStartRequest(activity, roster, null, debugName: "PerformanceBench " + _run));
            if (!result.Success) { Fail(result.Message); return; }
            _activity = result.ActivityId;
            RefreshWorkloadPayload();
            EventBus.Trigger(new PerformanceBenchCameraSettingsEvent(_settings.density));
        }
        CapabilityHostData PrepareDefinition()
        {
            var source = _settings.Moves ? _assets.movingObject : _assets.staticObject;
            var clone = Object.Instantiate(source); clone.hideFlags = HideFlags.HideAndDontSave;
            var wallets = new List<WalletEntry>();
            foreach (var wallet in source.WalletEntries)
            {
                var seeds = new List<SeedEntry>();
                foreach (var seed in wallet.Seed)
                    seeds.Add(seed.Asset == _assets.speedValue
                        ? new SeedEntry(seed.Asset, Math.Max(1, (long)Math.Round(_settings.speed * 1000 * _assets.tickDelta)), seed.FormType, seed.RuntimeTags)
                        : new SeedEntry(seed));
                wallets.Add(new WalletEntry(wallet.Wallet, seeds));
            }
            ((IEconomyRuntimeAssetOverride)clone).ApplyRuntimeOverride("performance-bench:" + _run + ":definition", wallets);
            EconomyService.RegisterAssets(new List<EconomyAssetData> { clone });
            _owned.Add(clone); return clone;
        }
        void PrepareSpace()
        {
            if (!ActivityService.TryGetSnapshot(_activity, out _activitySnapshot)) return;
            if (_activitySnapshot.State == ActivityState.Closing || _activitySnapshot.State == ActivityState.Closed)
            { Fail("Activity завершилась во время подготовки."); return; }
            if (_activitySnapshot.State != ActivityState.Running) return;
            if (!ActivityWorldRuntimeRegistry.TryGetOwner(_activity, out _worldOwner))
                throw new InvalidOperationException("Не найден экономический владелец Activity.");
            SchedulingService.TryGetTickSource(_activitySnapshot.ScheduleId, out _tickSource);
            PrepareTargetsAndGeometry();
            _pool = PoolService.Current.OpenContext("projection:activity:" + _activity.Value);
            var prefab = _assets.projectionPrefab.GetComponent<ProjectionBindingController>();
            _poolKey = prefab.PoolKey;
            _pool.ClaimPool(prefab, _settings.pool == PerformanceBenchPoolType.Warm ? _settings.population : _settings.initialPoolCapacity,
                expandable: true, maxCapacity: 0);
            if (_pool.TryGetSnapshot(_poolKey, out var pool)) _lastPoolTotal = pool.TotalCount;
            _prepared = true;
        }
        public void OnEvent(PerformanceBenchPulseEvent e)
        {
            if (_disposed || _retirement != null || !_context.IsInitialized) return;
            _now = e.Time;
            using (WorkMarker.Auto())
            using (FrameMarker.Auto(_run, (long)_phase, e.Frame))
            {
                RecordPreviousFrame(e.Frame);
                EnsureFramePhase(e.Frame);
                try
                {
                    if (_phase == PerformanceBenchPhaseType.Preparing && _limits.PreparationExpired(_now - _phaseStarted))
                        StopForLimit(PerformanceBenchStopReasonType.PreparationDeadline, "Подготовка превысила 180 секунд. Принятая работа завершается, затем штатная очистка.");
                    if (_settings != null)
                    {
                        var deadline = _limits.ObservePhase(_settings, _phase, _now - _phaseStarted);
                        if (deadline != PerformanceBenchStopReasonType.None)
                            StopForLimit(deadline, "Этап фиксированного интервала превысил 180 секунд; результат неполный. Завершение принятой работы и штатная очистка.");
                    }
                    CompletePending(); CompleteReadyCreations(); DrainPatrolOrders();
                    if (_stop && IsActive && _phase < PerformanceBenchPhaseType.Draining) SetPhase(PerformanceBenchPhaseType.Draining);
                    if (IsActive && ProjectionService.TryGetDiagnosticScopeSnapshot(_activity, out var projection) && projection.HasFailed)
                        Fail(projection.Failure);
                    switch (_phase)
                    {
                        case PerformanceBenchPhaseType.Preparing:
                            if (!_prepared) PrepareSpace();
                            if (!_prepared || _stop) break;
                            if (_probeStage == 0) { AdmitCreate(1); _probeStage = 1; break; }
                            if (_probeStage == 1)
                            { if (_pending.Count == 0 && _bound == 1) { AdmitDelete(1); _probeStage = 2; } break; }
                            if (_probeStage == 2)
                            { if (_pending.Count == 0 && _population == 0) _probeStage = 3; else break; }
                            if (_settings.scenario != PerformanceBenchScenarioType.Lifecycle && _population < _settings.population
                                && CountWorking() == 0)
                                AdmitCreate(Math.Min(25, _settings.population - _population - CountCreating()));
                            if (_pending.Count == 0 && (_settings.scenario == PerformanceBenchScenarioType.Lifecycle || _bound == _settings.population))
                            {
                                _populationReady = true;
                                SetPhase(PerformanceBenchPhaseType.Warmup);
                                if (_settings.Moves) foreach (var actor in _actors)
                                { actor.Right = PerformanceBenchLayout.InitialDirection(actor.Slot, _settings.seed); QueuePatrol(actor); }
                            }
                            break;
                        case PerformanceBenchPhaseType.Warmup:
                            if (_settings.intervalType == PerformanceBenchIntervalType.WallTime && _now - _phaseStarted >= _settings.warmup)
                            { SetPhase(PerformanceBenchPhaseType.Measuring); _nextCycle = _now; }
                            break;
                        case PerformanceBenchPhaseType.Measuring:
                            if (_measurements.Overflow) { Fail("Буфер измерений заполнен; данные неполные."); break; }
                            if (_settings.intervalType == PerformanceBenchIntervalType.WallTime && _now - _phaseStarted >= _settings.duration) { SetPhase(PerformanceBenchPhaseType.Draining); break; }
                            AdvanceScenario(); break;
                        case PerformanceBenchPhaseType.Draining:
                            if (_pending.Count == 0 && _hasFailed) AbandonUnreadyCreations();
                            if (_pending.Count == 0 && (_hasFailed || CountWorking() == 0)) BeginCleanup(); break;
                        case PerformanceBenchPhaseType.Cleanup:
                            if (!ActivityService.TryGetSnapshot(_activity, out var snapshot) || snapshot.State == ActivityState.Closed)
                                FinishRun(); break;
                    }
                }
                catch (Exception exception) { Fail(exception.Message); }
                if (!_awaitingProfilerExport && _phase == PerformanceBenchPhaseType.Complete && _series.Count > 0 && _retirement == null)
                    BeginNextRun();
                if (_now >= _nextUI) { _nextUI = _now + .5; CheckReadinessDeadline(); UpdateVisible(); CheckOccupancy(); PublishState(); }
                // Wall duration includes work after this pulse. Phase transitions can contain
                // capture/export/cleanup work outside the measured interval.
                _frameBoundaries[e.Frame % 4] |= _framePhases[e.Frame % 4] != _phase;
                ref var frameWork = ref WorkFor(e.Frame);
                frameWork.ScreenWidth = Screen.width; frameWork.ScreenHeight = Screen.height;
                if (_phase == PerformanceBenchPhaseType.Measuring && (Screen.width != 923 || Screen.height != 1870))
                    Fail("Game View resolution differs from the fixed benchmark 923x1870.");
                frameWork.Entities = _population; frameWork.Bound = _bound; frameWork.Visible = _visible;
                frameWork.Pending = CountWorking(); frameWork.Helpers = _markers.Count + _obstacles.Count + _placementMarkers.Count;
            }
        }
        int CountCreating() => _creatingCount;
        int CountWorking() => _creatingCount + _deletingCount;
        void SetCreating(Actor actor, bool value)
        {
            if (actor.Creating == value) return;
            actor.Creating = value; _creatingCount += value ? 1 : -1;
        }
        void SetDeleting(Actor actor, bool value)
        {
            if (actor.Deleting == value) return;
            actor.Deleting = value; _deletingCount += value ? 1 : -1;
        }
        void AdvanceScenario()
        {
            if (CountWorking() > 0) return;
            if (_settings.scenario == PerformanceBenchScenarioType.Lifecycle)
            {
                if (_cycle == CycleType.Creating) { _cycle = CycleType.Holding; _nextCycle = _now + _settings.lifetime; }
                else if (_cycle == CycleType.Deleting) { _cycle = CycleType.Ready; _nextCycle = _now + _settings.interval; }
                if (_now < _nextCycle) return;
                if (_population == 0) { AdmitCreate(_settings.batchSize); _cycle = CycleType.Creating; }
                else { AdmitDelete(_population); _cycle = CycleType.Deleting; }
            }
            else if (_settings.scenario == PerformanceBenchScenarioType.Churn)
            {
                if (_cycle == CycleType.Replacing && _population == _settings.population)
                { _cycle = CycleType.Ready; _nextCycle = _now + _settings.interval; }
                if (_cycle == CycleType.Deleting)
                { AdmitCreate(_settings.population - _population); _cycle = CycleType.Replacing; return; }
                if (_now < _nextCycle) return;
                if (_population < _settings.population) { AdmitCreate(_settings.population - _population); _cycle = CycleType.Replacing; }
                else { AdmitDelete(Math.Max(1, (int)Math.Round(_settings.population * _settings.replacementPercent / 100))); _cycle = CycleType.Deleting; }
            }
        }
        void SetPhase(PerformanceBenchPhaseType phase)
        {
            if (_captureType == PerformanceBenchCaptureType.SkillsSummary && _phase == PerformanceBenchPhaseType.Measuring
                && phase != PerformanceBenchPhaseType.Measuring) GameRuntimeDiagnostics.CloseMovementEvidenceMeasurement();
            EnsureFramePhase(Time.frameCount);
            _frameBoundaries[Time.frameCount % 4] = true;
            _measurements?.PhaseDebt((int)phase, Time.frameCount, _stepsAfterReady,
                PerformanceBenchProfilerAdapter.ReadScheduleDebt(_tickSource));
            _phase = phase; _phaseStarted = _now;
            UpdateWorkloadContext();
            EventBus.Trigger(new PerformanceBenchRunPhaseEvent(_context, _run, phase, _captureType,
                _exportDirectory, JsonUtility.ToJson(new WorkloadContext { run = _run, phase = (int)phase,
                    activity = _activity.Value, captureType = _captureType, settings = _settings?.Copy() }),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")), _exportBudget));
        }
        public void OnEvent(PerformanceBenchWindowCapturedEvent e)
        {
            if (!ReferenceEquals(e.Owner, _context) || e.Run != _run || _phase != PerformanceBenchPhaseType.Measuring) return;
            _stop = true; _message = "Диагностическое окно из трёх кадров записано; завершение принятой работы и очистка.";
            SetPhase(PerformanceBenchPhaseType.Draining);
        }
        public void OnEvent(PerformanceBenchCaptureCompletedEvent e)
        {
            if (!ReferenceEquals(e.Owner, _context) || e.Run != _run) return;
            RestoreDiagnostics();
            _awaitingProfilerExport = false;
            if (_results.Count == 0 || _results[_results.Count - 1].run != _run) return;
            var result = _results[_results.Count - 1];
            result.incomplete |= e.Incomplete || _exportBudget.Incomplete;
            result.profilerDetailsTruncated = e.DetailsTruncated;
            result.profilerExportMessage = e.Message;
            if (result.incomplete) { _series.Clear(); _message = "Данные экспорта неполны: " + e.Message; }
            try { _exportBudget.WriteText(System.IO.Path.Combine(result.outputDirectory, "summary.json"), JsonUtility.ToJson(result, true)); }
            catch (Core.EditorTools.ProfilerDumpStorageLimitException)
            { _exportBudget.WriteLimitManifest(System.IO.Path.Combine(result.outputDirectory, "export-limit.json"), JsonUtility.ToJson(result, true)); }
        }

        void Fail(string failure)
        {
            if (!_hasFailed) _failure = failure ?? "Операция завершилась без успеха; cause not established.";
            _hasFailed = true;
            _stopReason = _measurements?.Overflow == true ? PerformanceBenchStopReasonType.BufferOverflow : PerformanceBenchStopReasonType.LifecycleFailure;
            _message = failure; _series.Clear(); _stop = true;
            if (IsActive && _phase < PerformanceBenchPhaseType.Draining) SetPhase(PerformanceBenchPhaseType.Draining);
        }
        void StopForLimit(PerformanceBenchStopReasonType reason, string message)
        {
            _stop = true; _interrupted = true; _stopReason = reason; _message = message;
            _series.Clear();
            if (IsActive && _phase < PerformanceBenchPhaseType.Draining) SetPhase(PerformanceBenchPhaseType.Draining);
        }
        void BeginCleanup()
        {
            SetPhase(PerformanceBenchPhaseType.Cleanup);
            ReleaseAuxiliaryEntities();
            if (_activity.IsValid) ActivityService.Close(_activity, ActivityCloseCauseType.Manual);
            else FinishRun();
        }
        void FinishRun()
        {
            var result = _measurements.Summarize(_run, _settings, _interrupted, _failure);
            result.stopReason = _stopReason;
            result.incomplete |= _stopReason == PerformanceBenchStopReasonType.WarmupDeadline
                || _stopReason == PerformanceBenchStopReasonType.MeasurementDeadline;
            result.captureType = _captureType;
            result.stepIntervalRequired = _settings.intervalType == PerformanceBenchIntervalType.SimulationSteps
                && (_captureType == PerformanceBenchCaptureType.Normal || _captureType == PerformanceBenchCaptureType.SkillsSummary);
            result.incomplete |= result.stepIntervalRequired && !result.stepIntervalComplete;
            result.stopMessage = _message;
            result.lifecycleSucceeded = !_hasFailed && _pending.Count == 0 && CountWorking() == 0 && _bound == 0;
            try { WriteResult(result); }
            catch (Exception exception)
            {
                _failure = "Экспорт недоступен: " + exception.Message;
                result.failure = _failure; result.incomplete = true; _series.Clear();
                _exportBudget.WriteLimitManifest(System.IO.Path.Combine(_exportDirectory, "export-limit.json"), JsonUtility.ToJson(result, true));
            }
            finally { _results.Add(result); _measurements.Dispose(); _measurements = null; }
            _pool?.Dispose(); _pool = null; _activity = default; _tickSource = null;
            _byEntity.Clear(); _pending.Clear(); _patrolOrders.Clear(); _readyCreations.Clear();
            EconomyService.RegisterAssets(new List<EconomyAssetData> { _assets.staticObject, _assets.movingObject });
            foreach (var owned in _owned) Object.Destroy(owned);
            _owned.Clear(); _actors = null; _definition = null; _worldOwner = null;
            _population = 0; _bound = 0; _visible = 0;
            _creatingCount = 0; _deletingCount = 0;
            MovementService.Install(_originalTraffic);
            // minimum seam: Summary owns its bounded evidence until the adapter has
            // included the final display frame and acknowledged export or cancellation.
            if (_captureType != PerformanceBenchCaptureType.SkillsSummary) RestoreDiagnostics();
            GameRuntimeProfilerWorkloadContext.Clear();
            _awaitingProfilerExport = true;
            SetPhase(_hasFailed ? PerformanceBenchPhaseType.Failed : PerformanceBenchPhaseType.Complete);
            _message = result.outputDirectory;
            if (result.interrupted) _message = "Запуск прерван: " + result.stopMessage + " " + _message;
            if (!result.statisticsSufficient) _message = "Недостаточная статистика: " + result.measuredFrames + "/1000 полных кадров; p95/p99 недоступны. " + _message;
            if (_series.Count > 0 && _retirement == null && !_awaitingProfilerExport) BeginNextRun();
            else
            {
                try { WriteSeriesSummary(); }
                catch (Exception exception) { _message = "Сводка серии недоступна: " + exception.Message; }
            }
        }
        public void OnEvent(ActivityPreparationFailedEvent e) { if (e.ActivityId == _activity) Fail(e.Message); }
        static void SetField(object value, string name, object fieldValue)
        {
            for (var type = value.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;
                field.SetValue(value, fieldValue); return;
            }
            throw new MissingFieldException(value.GetType().FullName, name);
        }
        void ConfigureDiagnostics()
        {
            object profile = default(RuntimeDiagnosticsProfile);
            SetField(profile, "orchestrationTrace", _settings.trace);
            SetField(profile, "economyTransactionTrace", _settings.trace);
            SetField(profile, "orchestrationMaxRows", 128);
            EconomyTransactionTraceService.SetViewerCaptureEnabled(false);
            _diagnosticsRestorePending = true;
            GameRuntimeDiagnostics.StartSession(_settings.seed, (RuntimeDiagnosticsProfile)profile);
        }
        void RestoreDiagnostics()
        {
            if (!_diagnosticsRestorePending) return;
            _diagnosticsRestorePending = false;
            GameRuntimeDiagnostics.StartSession(_context.SessionSeed ?? _settings.seed, _originalDiagnostics);
        }
        public Task RetireAsync()
        {
            if (_retirement != null) return _retirement;
            _stop = true; _interrupted = true; _series.Clear();
            return _retirement = RetireOwnedWorkAsync();
        }
        async Task RetireOwnedWorkAsync()
        {
            var pending = new List<Actor>(_pending);
            try
            {
                foreach (var actor in pending)
                {
                    if (!actor.Pending.IsValid) continue;
                    var result = await EconomyService.ExecuteOperationAsync(actor.Pending);
                    FinalizeOperation(actor, result, materialize: false);
                }
            }
            finally
            {
                _pending.Clear(); ReleaseAuxiliaryEntities();
                if (_activity.IsValid) ActivityService.Close(_activity, ActivityCloseCauseType.Manual);
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            EventBus.Unregister<PerformanceBenchWindowCapturedEvent>(this);
            EventBus.Unregister<PerformanceBenchCommandEvent>(this); EventBus.Unregister<PerformanceBenchPulseEvent>(this);
            EventBus.Unregister<ScheduleStepStartedEvent>(this); EventBus.Unregister<ScheduleStepFinishedEvent>(this);
            EventBus.Unregister<ScheduleStepDispatchedEvent>(this); EventBus.Unregister<ProjectionLifecycleEvent>(this);
            EventBus.Unregister<AIBrainStateCompletedEvent>(this); EventBus.Unregister<MovementAgentStepEvent>(this); EventBus.Unregister<MovementAgentPoseChangedEvent>(this);
            EventBus.Unregister<MovementTrafficResolvedEvent>(this); EventBus.Unregister<NavigationRouteBuiltEvent>(this);
            EventBus.Unregister<NavigationRouteFailedEvent>(this); EventBus.Unregister<ActivityPreparationFailedEvent>(this);
            EventBus.Unregister<CapabilityHostRegisteredEvent>(this);
            EventBus.Unregister<PerformanceBenchViewEvent>(this);
            EntityService.EntityRemoved -= OnEntityRemoved;
            if (_measurements != null && _settings != null)
            {
                try
                {
                    SetPhase(PerformanceBenchPhaseType.Cleanup);
                    var interrupted = _measurements.Summarize(_run, _settings, true, _failure);
                    interrupted.captureType = _captureType;
                    interrupted.incomplete = true;
                    interrupted.profilerDetailsTruncated = _exportBudget.DetailsTruncated;
                    interrupted.profilerExportMessage = "Run retired before measurements and diagnostic export completed.";
                    WriteResult(interrupted); _results.Add(interrupted); WriteSeriesSummary();
                }
                catch (Exception exception) { Debug.LogWarning("[PerformanceBench] Экспорт при закрытии недоступен: " + exception.Message); }
                finally { _measurements.Dispose(); _measurements = null; }
            }
            try { _profilerAdapter?.Dispose(); }
            finally
            {
                try { RestoreDiagnostics(); }
                finally
                {
                    EventBus.Unregister<PerformanceBenchCaptureCompletedEvent>(this);
                    _pool?.Dispose(); GameRuntimeProfilerWorkloadContext.Clear();
                    foreach (var owned in _owned) Object.Destroy(owned);
                    _owned.Clear();
                }
            }
        }
    }
}
#endif
