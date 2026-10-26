using System;
using System.Collections;
using System.Collections.Generic;
using Core.Activities;
using Core.Activities.Events;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using EntityId = Core.Entities.EntityId;
using Core.Events;
using Core.Production.Events;
using Core.Production;
using Core.Economy;
using Core.Taxonomy;
using Unity.Profiling;
using UnityEngine;

namespace ChainRush.Tests.PlayMode
{
    // minimum seam: completed-frame wall/CPU/GC timing only. No allocation counter or CPU recording.
    internal sealed class BuildingFrameCapture : IDisposable,
        IEventListener<CapabilityHostRegisteredEvent>, IEventListener<CapabilityHostUnregisteredEvent>,
        IEventListener<ProductionOrderYieldedEvent>, IEventListener<ActivityClosingEvent>, IEventListener<ActivityCompletedEvent>
    {
        const int Capacity = 32768;
        readonly List<Timing> _timings = new();
        readonly Dictionary<string, ProfilerCategory> _categories = new(StringComparer.Ordinal);
        readonly FrameFacts[] _facts = new FrameFacts[8];
        readonly Dictionary<EntityId, CapabilityHostSnapshot> _hosts = new(2048);
        readonly HashSet<string> _buildings = new(StringComparer.Ordinal);
        readonly HashSet<string> _buildingCards = new(StringComparer.Ordinal);
        readonly TaxonomyTermData _cellTag;
        readonly BuildingCaptureResult _result;
        bool _running = true;
        int _lastFrame = -1, _battleHosts, _buildingCount, _cells;
        int _completedHosts, _completedBuildings, _completedCells;
        ActivityId _battle, _board;
        int _lastEventFrame = -100;
        internal int MeasureFromFrame { get; set; } = int.MaxValue;
        internal string Failure { get; private set; }

        internal BuildingFrameCapture(bool enabled, HashSet<CapabilityHostBaseData> buildings, HashSet<CapabilityHostBaseData> buildingCards, TaxonomyTermData cellTag)
        {
            foreach (var building in buildings) _buildings.Add(building.Id);
            foreach (var card in buildingCards) _buildingCards.Add(card.Id);
            _cellTag = cellTag;
            _result = new BuildingCaptureResult { buildingsEnabled = enabled,
                frames = new List<BuildingFrame>(Capacity), samplesMs = new List<double>() };
            var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
            Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(handle);
                _categories[description.Name] = description.Category;
            }
            Add("Main Thread", ProfilerCategory.Internal, true);
            Add("GC.Collect", ProfilerCategory.Memory, true);
            Add("GC.Incremental.Collect", ProfilerCategory.Memory);
            foreach (string name in new[] {
                "GameRuntimeHost.PumpRuntime", "GameRuntimeHost.PumpRuntime.SimulationFlush",
                "GameRuntimeHost.PumpRuntime.SchedulingUpdate", "GameRuntimeHost.PumpRuntime.MovementPump",
                "GameRuntimeHost.PumpRuntime.SkillPump", "GameRuntimeHost.PumpRuntime.AIPump", "SchedulingServiceCore.Update",
                "OrchestrationService.ProcessPass", "OrchestrationService.ProcessPass.FactCollection",
                "OrchestrationService.ProcessPass.Reconciliation", "OrchestrationService.ProcessPass.Admission",
                "AgentService.Tick", "ProductionRuntime.ProcessProduction", "ProductionRuntime.TryApplyOutputs",
                "KnowledgeService.RebuildDomainRecords", "MovementService.PumpRuntime", "SkillService.PumpRuntime",
                "Gfx.WaitForPresentOnGfxThread", "WaitForTargetFPS" })
                Add(name, ProfilerCategory.Scripts);
            _result.samplesMs.Capacity = Capacity * _timings.Count;
            foreach (var timing in _timings) _result.markers.Add(timing.Name);
            EventBus.Register<CapabilityHostRegisteredEvent>(this);
            EventBus.Register<CapabilityHostUnregisteredEvent>(this);
            EventBus.Register<ProductionOrderYieldedEvent>(this);
            EventBus.Register<ActivityClosingEvent>(this);
            EventBus.Register<ActivityCompletedEvent>(this);
        }
        void Add(string name, ProfilerCategory category, bool required = false)
            => _timings.Add(new Timing(name, _categories.TryGetValue(name, out var installed) ? installed : category, required));
        internal void SetActivities(ActivityId battle, ActivityId board)
        {
            _battle = battle; _board = board;
            _battleHosts = _buildingCount = _cells = 0;
            foreach (var host in _hosts.Values)
            {
                Count(host, 1);
                if (host.ActivityId == _board && host.Definition != null && host.Definition.Tags.Contains(_cellTag))
                    _result.initialBoard.Add(host.Definition.Id);
            }
        }
        bool IsBuilding(CapabilityHostBaseData definition) => definition != null && _buildings.Contains(definition.Id);
        EntityId _lastChoice;
        int _boardChangedFrame;
        internal bool IsBuildingCard(EntityId entity) => _hosts.TryGetValue(entity, out var host)
            && host.Definition != null && _buildingCards.Contains(host.Definition.Id);
        internal bool TryGetBoardChoice(out EntityId target)
        {
            target = EntityId.Invalid;
            if (_cells != 16 || Time.frameCount - _boardChangedFrame < 2 || _hosts.ContainsKey(_lastChoice)) return false;
            bool preferred = false;
            foreach (var host in _hosts.Values)
            {
                if (host.ActivityId != _board || host.Definition == null || !host.Definition.Tags.Contains(_cellTag)) continue;
                bool candidatePreferred = _buildingCards.Contains(host.Definition.Id);
                if (!target.IsValid || candidatePreferred && !preferred
                    || candidatePreferred == preferred && host.EntityId.Value < target.Value)
                { target = host.EntityId; preferred = candidatePreferred; }
            }
            return target.IsValid;
        }
        internal void MarkChoice(EntityId target)
        {
            _lastChoice = target; MarkInput();
            _result.choices.Add(new BuildingChoice { frame = Time.frameCount,
                asset = _hosts.TryGetValue(target, out var host) ? host.Definition.Id : null });
        }
        void Count(in CapabilityHostSnapshot host, int delta)
        {
            if (host.ActivityId == _battle) _battleHosts += delta;
            if (host.ActivityId == _battle && IsBuilding(host.Definition)) _buildingCount += delta;
            if (host.ActivityId == _board && host.Definition != null && host.Definition.Tags.Contains(_cellTag))
            { _cells += delta; _boardChangedFrame = Time.frameCount; }
            ref var facts = ref CurrentFacts();
            facts.populationValid = true; facts.battleHosts = _battleHosts; facts.buildings = _buildingCount; facts.cells = _cells;
        }
        ref FrameFacts CurrentFacts()
        {
            int frame = Time.frameCount; int index = frame % _facts.Length;
            if (_facts[index].frame != frame) _facts[index] = new FrameFacts { frame = frame };
            return ref _facts[index];
        }
        public void OnEvent(CapabilityHostRegisteredEvent e)
        {
            _hosts[e.Snapshot.EntityId] = e.Snapshot; Count(e.Snapshot, 1);
            ref var fact = ref CurrentFacts(); fact.created++; _lastEventFrame = Time.frameCount;
            if (IsBuilding(e.Snapshot.Definition)) { fact.buildingsCreated++; _result.buildingsCreated++; }
        }
        public void OnEvent(CapabilityHostUnregisteredEvent e)
        {
            if (_hosts.Remove(e.EntityId)) Count(e.Snapshot, -1);
            ref var fact = ref CurrentFacts(); fact.removed++; _lastEventFrame = Time.frameCount;
        }
        public void OnEvent(ProductionOrderYieldedEvent e)
        {
            ref var fact = ref CurrentFacts(); fact.yields++; _lastEventFrame = Time.frameCount;
            _result.totalYields++;
            if (_result.yields.Count < 1024) _result.yields.Add(new BuildingYield {
                frame = Time.frameCount, producer = e.ProductionEntityId.Value, recipe = e.RecipeId,
                producerDefinition = _hosts.TryGetValue(e.ProductionEntityId, out var observed) ? observed.Definition.Id : null });
            if (_hosts.TryGetValue(e.ProductionEntityId, out var producer) && IsBuilding(producer.Definition))
            { fact.buildingYields++; _result.buildingYields++; }
        }
        public void OnEvent(ActivityClosingEvent e)
        { if (e.ActivityId == _battle) Failure = "Level closed before the measured interval completed."; }
        public void OnEvent(ActivityCompletedEvent e)
        { if (e.Notification.ActivityId == _battle) Failure = "Level completed before the measured interval completed."; }
        internal void MarkInput()
        { ref var fact = ref CurrentFacts(); fact.input = true; _lastEventFrame = Time.frameCount; }
        internal IEnumerator Capture()
        {
            // Sample at a consistent point in each following frame; LastValue is the completed frame.
            while (_running)
            {
                yield return null;
                if (!_running) break;
                int frame = Time.frameCount - 1;
                if (frame <= _lastFrame) continue;
                if (_lastFrame >= 0 && frame != _lastFrame + 1) Failure = "Frame sampler missed a frame.";
                _lastFrame = frame;
                if (_result.frames.Count == Capacity) { Failure = "Frame capacity exceeded."; _running = false; break; }
                var fact = _facts[frame % _facts.Length];
                if (fact.frame != frame) fact = default;
                if (fact.populationValid) { _completedHosts = fact.battleHosts; _completedBuildings = fact.buildings; _completedCells = fact.cells; }
                _result.frames.Add(new BuildingFrame {
                    frame = frame, seconds = Time.realtimeSinceStartupAsDouble,
                    wallMs = Time.unscaledDeltaTime * 1000.0, measured = frame >= MeasureFromFrame,
                    eventFrame = fact.created != 0 || fact.removed != 0 || fact.yields != 0 || fact.input,
                    adjacentEvent = frame - _lastEventFrame <= 1,
                    created = fact.created, removed = fact.removed, yields = fact.yields,
                    buildingsCreated = fact.buildingsCreated, buildingYields = fact.buildingYields,
                    battleHosts = _completedHosts, buildings = _completedBuildings, boardCells = _completedCells });
                foreach (var timing in _timings) _result.samplesMs.Add(timing.ReadMilliseconds());
            }
        }
        internal BuildingCaptureResult Finish()
        {
            _running = false;
            _result.completed = Failure == null;
            _result.failure = Failure;
            foreach (var timing in _timings)
                _result.markerAvailable.Add(timing.Available);
            return _result;
        }
        internal void CaptureProducerState()
        {
            // Outside the timing window: keep proof of missing admission/projection instead of guessing from a zero yield count.
            foreach (var host in _hosts.Values)
            {
                if (!IsBuilding(host.Definition)) continue;
                var row = new BuildingProducerState { entity = host.EntityId.Value, definition = host.Definition.Id };
                row.attached = ProductionService.TryGetSnapshot(host.EntityId, out var production);
                if (row.attached)
                {
                    row.production = production.Definition?.Id; row.catalog = production.Catalog?.Id;
                    row.enabled = production.Enabled; row.acceptsOrders = production.AcceptsOrders;
                    row.queued = production.QueueCount; row.active = production.ActivePipelineCount;
                }
                var balances = EconomyService.Query(new EconomySelectionQuery(host.SelfEconomyOwner,
                    new List<TaxonomyTermData>(0), EconomyFormType.Stack, null, EconomyAggregationType.Detailed));
                foreach (var item in balances.Items) row.balances.Add(new BuildingBalance { asset = item.Asset.Id, amount = item.Amount });
                _result.producers.Add(row);
            }
        }
        public void Dispose()
        {
            _running = false;
            EventBus.Unregister<CapabilityHostRegisteredEvent>(this);
            EventBus.Unregister<CapabilityHostUnregisteredEvent>(this);
            EventBus.Unregister<ProductionOrderYieldedEvent>(this);
            EventBus.Unregister<ActivityClosingEvent>(this);
            EventBus.Unregister<ActivityCompletedEvent>(this);
            foreach (var timing in _timings) timing.Dispose();
        }
        struct FrameFacts
        {
            internal int frame, created, removed, yields, buildingsCreated, buildingYields;
            internal bool input, populationValid;
            internal int battleHosts, buildings, cells;
        }
        sealed class Timing : IDisposable
        {
            internal string Name { get; }
            readonly ProfilerRecorder _recorder;
            readonly bool _required;
            internal bool Available => _recorder.Valid;
            internal Timing(string name, ProfilerCategory category, bool required)
            {
                Name = name; _required = required;
                _recorder = ProfilerRecorder.StartNew(category, name, 1,
                    ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached
                    | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            }
            internal double ReadMilliseconds()
            {
                if (!_recorder.Valid) {
                    if (_required) throw new InvalidOperationException("Required timing unavailable: " + Name);
                    return -1;
                }
                return _recorder.LastValue / 1000000.0;
            }
            public void Dispose() { if (_recorder.Valid) _recorder.Dispose(); }
        }
    }
    [Serializable]
    internal sealed class BuildingCaptureResult
    {
        public bool buildingsEnabled, completed, validated;
        public string failure;
        public int seed, frozenStorageKeys, buildingsCreated, totalYields, buildingYields, targetFrameRate, vSyncCount;
        public double fixedDeltaTime;
        public string engineVersion;
        public List<BuildingChoice> choices = new();
        public double warmupSeconds, measuredSeconds;
        public List<string> selectedBuildings = new();
        public List<string> initialBoard = new();
        public List<BuildingYield> yields = new(1024);
        public List<BuildingProducerState> producers = new();
        public List<string> markers = new();
        public List<bool> markerAvailable = new();
        public List<BuildingFrame> frames;
        // Row-major: frame index * markers.Count + marker index. -1 means unavailable, never zero.
        public List<double> samplesMs;
    }
    [Serializable]
    internal struct BuildingYield
    {
        public int frame, producer;
        public string recipe, producerDefinition;
    }
    [Serializable]
    internal sealed class BuildingProducerState
    {
        public int entity, queued, active;
        public string definition, production, catalog;
        public bool attached, enabled, acceptsOrders;
        public List<BuildingBalance> balances = new();
    }
    [Serializable]
    internal struct BuildingBalance
    {
        public string asset;
        public long amount;
    }
    [Serializable]
    internal struct BuildingChoice
    {
        public int frame;
        public string asset;
    }
    [Serializable]
    internal struct BuildingFrame
    {
        public int frame, created, removed, yields, buildingsCreated, buildingYields, battleHosts, buildings, boardCells;
        public double seconds, wallMs;
        public bool measured, eventFrame, adjacentEvent;
    }
}
