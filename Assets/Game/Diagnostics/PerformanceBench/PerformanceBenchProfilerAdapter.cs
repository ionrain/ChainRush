#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Core.Scheduling;
using Core.Events;
using Core.GameRuntime;
using Core.EditorTools;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Profiling;

namespace ChainRush.Diagnostics
{
    // minimum seam: Editor profiling is a mediated run resource; it never owns gameplay cleanup.
    internal sealed class PerformanceBenchProfilerAdapter : IDisposable,
        IEventListener<PerformanceBenchRunPhaseEvent>
    {
        // minimum seam: read the authoritative accumulator only at phase
        // boundaries. Both compared packages expose this same existing storage;
        // no simulation code or baseline source needs diagnostic modifications.
        internal static double ReadScheduleDebt(ITickSource source)
        {
            if (source == null) return -1;
            const BindingFlags hiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
            const BindingFlags hiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            var coreField = typeof(SchedulingService).GetField("Core", hiddenStatic)
                ?? throw new MissingFieldException(typeof(SchedulingService).FullName, "Core");
            var core = coreField.GetValue(null);
            var registrationsField = core.GetType().GetField("_schedules", hiddenInstance)
                ?? throw new MissingFieldException(core.GetType().FullName, "_schedules");
            foreach (DictionaryEntry entry in (IDictionary)registrationsField.GetValue(core))
            {
                var runtime = entry.Value.GetType().GetField("Runtime", BindingFlags.Instance | BindingFlags.Public).GetValue(entry.Value);
                var tickSource = runtime.GetType().GetProperty("TickSource").GetValue(runtime);
                if (!ReferenceEquals(source, tickSource)) continue;
                var accumulated = runtime.GetType().GetField("_accumulated", hiddenInstance);
                return accumulated == null ? -1 : (float)accumulated.GetValue(runtime) * 1000d;
            }
            return -1;
        }

        readonly object _owner;
        bool _saved, _recording, _disposed, _windowStarted;
        bool _enabled, _binary, _deep, _editor, _allocationStacks;
        int _run, _first = -1, _last = -1;
        long _recordingWorkingSetBefore, _recordingWorkingSetDelta;
        PerformanceBenchCaptureType _type;
        string _directory, _context, _preparationFailure, _frameEvidence;
        int _expectedSummaryPopulation, _expectedSummarySteps;
        bool _summaryStarted, _summaryFinishPending, _movementSummaryFrozen;
        int _summaryEndFrame = -1;
        ProfilerDumpStorageBudget _budget;
        IProfilerWindowExport _export;
        PerformanceBenchGameViewScope _gameView;
        ProfilerCapturePreparation _preparation;

        internal PerformanceBenchProfilerAdapter(object owner)
        {
            _owner = owner;
            EventBus.Register<PerformanceBenchRunPhaseEvent>(this);
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += OnAssembliesRetiring;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public void OnEvent(PerformanceBenchRunPhaseEvent e)
        {
            if (!ReferenceEquals(e.Owner, _owner) || _disposed) return;
            if (e.Phase == PerformanceBenchPhaseType.Preparing)
            {
                if (_saved) throw new InvalidOperationException("The preceding profiler run has not retired.");
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (!string.Equals(project, e.ProjectPath, StringComparison.Ordinal))
                    throw new InvalidOperationException("Profiler adapter target project does not match this Editor.");
                _enabled = Profiler.enabled; _binary = Profiler.enableBinaryLog;
                _deep = ProfilerDriver.deepProfiling; _editor = ProfilerDriver.profileEditor;
                _allocationStacks = Profiler.enableAllocationCallstacks; _saved = true;
                _context = e.Context;
                var captureSettings = JObject.Parse(e.Context)["settings"];
                _expectedSummaryPopulation = captureSettings?["population"]?.Value<int>() ?? 0;
                _expectedSummarySteps = captureSettings?["measurementSteps"]?.Value<int>() ?? 0;
                _run = e.Run; _type = e.CaptureType; _directory = e.Directory; _budget = e.Budget;
                _first = _last = -1; _windowStarted = false; _preparation = null; _preparationFailure = null;
                _frameEvidence = null; _summaryStarted = false; _summaryFinishPending = false; _movementSummaryFrozen = false; _summaryEndFrame = -1;
                Profiler.enabled = false; Profiler.enableBinaryLog = false;
                ProfilerDriver.deepProfiling = false; ProfilerDriver.profileEditor = false;
                Profiler.enableAllocationCallstacks = false;
                _gameView = new PerformanceBenchGameViewScope();
            }
            else if (e.Phase == PerformanceBenchPhaseType.Measuring && _type == PerformanceBenchCaptureType.SkillsSummary)
            {
                _summaryStarted = GameRuntimeDiagnostics.BeginDetailedFrameEvidence();
                if (!_summaryStarted)
                {
                    _preparationFailure = "Detailed frame evidence source is unavailable.";
                    EventBus.Trigger(new PerformanceBenchWindowCapturedEvent(_owner, _run));
                }
            }
            else if (e.Phase == PerformanceBenchPhaseType.Measuring && _type != PerformanceBenchCaptureType.Normal)
            {
                _context = e.Context;
                try
                {
                    // Synchronous phase boundary: preparation finishes before CPU recording begins.
                    _preparation = MorbooProfilerCaptureDumpTool.PrepareCurrentCaptureContext();
                    _budget.WriteText(Path.Combine(_directory, "capture-preparation.json"), JsonUtility.ToJson(_preparation, true));
                    if (_preparation.status != "recorded") throw new InvalidOperationException(_preparation.reason);
                    ProfilerDriver.ClearAllFrames();
                    using (var process = System.Diagnostics.Process.GetCurrentProcess()) _recordingWorkingSetBefore = process.WorkingSet64;
                    Profiler.enabled = true; _recording = true; _windowStarted = true;
                }
                catch (Exception exception)
                {
                    _preparationFailure = exception.Message;
                    StopRecording();
                    EventBus.Trigger(new PerformanceBenchWindowCapturedEvent(_owner, _run));
                }
            }
            else if (e.Phase == PerformanceBenchPhaseType.Draining || e.Phase == PerformanceBenchPhaseType.Cleanup)
            {
                if (_summaryStarted && _frameEvidence == null)
                {
                    _summaryEndFrame = Time.frameCount;
                    _frameEvidence = GameRuntimeDiagnostics.FreezeDetailedSkillEvidence();
                }
                StopRecording();
            }
            else if (e.Phase == PerformanceBenchPhaseType.Complete || e.Phase == PerformanceBenchPhaseType.Failed)
            {
                StopRecording();
                if (_type == PerformanceBenchCaptureType.Normal) { Finish(false, ""); return; }
                if (_type == PerformanceBenchCaptureType.SkillsSummary)
                {
                    _summaryFinishPending = true;
                    TryFinishSummary();
                    return;
                }
                if (!_windowStarted) { Finish(true, "CPU window unavailable: " + (_preparationFailure ?? "measurement did not start.")); return; }
                int first = ProfilerDriver.firstFrameIndex + 1;
                int last = Math.Min(ProfilerDriver.lastFrameIndex, first + 2);
                if (first < 0 || last < first)
                { Finish(true, "CPU window unavailable: no complete frames."); return; }
                _first = first; _last = last;
                try { _export = MorbooProfilerCaptureDumpTool.BeginCurrentWindowExport(_directory, first, last, _context, _budget, _recordingWorkingSetDelta, _preparation); }
                catch (Exception exception) { Finish(true, exception.Message); }
            }
        }

        void Update()
        {
            if (_disposed) return;
            if (_summaryStarted && !_movementSummaryFrozen && _summaryEndFrame >= 0 && Time.frameCount > _summaryEndFrame)
            { _frameEvidence = GameRuntimeDiagnostics.AppendMovementFrameEvidence(_frameEvidence); _movementSummaryFrozen = true; }
            TryFinishSummary();
            if (_recording && ProfilerDriver.firstFrameIndex >= 0
                && ProfilerDriver.lastFrameIndex >= ProfilerDriver.firstFrameIndex + 3)
            {
                StopRecording();
                EventBus.Trigger(new PerformanceBenchWindowCapturedEvent(_owner, _run));
            }
            if (_export == null) return;
            try
            {
                _export.Step(1);
                if (_export.IsComplete || _export.IsIncomplete)
                    Finish(_export.IsIncomplete || _last - _first + 1 != 3, _export.CompletionMessage);
            }
            catch (Exception exception) { Finish(true, exception.Message); }
        }

        void TryFinishSummary()
        {
            if (!_summaryFinishPending || (_summaryStarted && !_movementSummaryFrozen)) return;
            _summaryFinishPending = false;
            try
            {
                if (!_summaryStarted) throw new InvalidOperationException(_preparationFailure ?? "Summary measurement did not start.");
                MorbooProfilerCaptureDumpTool.WriteFrameEvidence(_directory, _frameEvidence, _budget);
                ValidateSummary(_frameEvidence, _expectedSummarySteps, _expectedSummaryPopulation);
                ValidateCostAccounting(_frameEvidence);
                Finish(false, "Bounded Skills and Movement evidence exported; CPU recording remained disabled.");
            }
            catch (ProfilerDumpStorageLimitException exception) { Finish(true, exception.Message); }
            catch (Exception exception) { Finish(true, exception.Message); }
        }

        // minimum seam: export success requires the bounded evidence contract,
        // including population coverage; writing a file alone is insufficient.
        internal static void ValidateSummary(string json, int expectedSteps, int expectedPopulation)
        {
            if (expectedSteps != 256 || expectedPopulation <= 0) throw new InvalidOperationException("Summary workload expectations are missing or unsupported.");
            var root = JObject.Parse(json ?? throw new InvalidOperationException("Frame evidence is missing."));
            if (!(root["movementEvidence"] is JObject movement)) throw new InvalidOperationException("Movement evidence is missing.");
            void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException("Incomplete Movement evidence: " + reason); }
            Require(movement["observedSteps"]?.Value<int>() == expectedSteps && movement["completedObservedSteps"]?.Value<int>() == expectedSteps, "observed steps");
            Require(movement["selectionSteps"]?.Value<int>() == 32 && movement["completedDetailSteps"]?.Value<int>() == 224, "selection/detail interval");
            Require(movement["intervalComplete"]?.Value<bool>() == true && movement["finalDisplayFrameComplete"]?.Value<bool>() == true, "interval/display boundary");
            Require(movement["truncated"]?.Value<bool>() == false && movement["cpuRecording"]?.Value<bool>() == false, "truncation/recording");
            var population = movement["population"] as JObject;
            Require(population != null && population["configured"]?.Value<bool>() == true && population["entities"]?.Value<int>() == expectedPopulation, "population");
            var agents = population["agents"] as JArray; var columns = population["entityColumns"] as JArray;
            Require(agents != null && agents.Count == expectedPopulation && columns != null, "population rows");
            int entity = -1, observed = -1;
            for (int i = 0; i < columns.Count; i++) { if (columns[i].Value<string>() == "entity") entity = i; if (columns[i].Value<string>() == "observedSteps") observed = i; }
            Require(entity >= 0 && observed >= 0, "population columns");
            var identities = new System.Collections.Generic.HashSet<int>();
            foreach (var row in agents)
                Require(row is JArray values && values.Count == columns.Count && values[observed].Value<int>() == expectedSteps
                    && identities.Add(values[entity].Value<int>()), "Entity step coverage");
            var groups = population["steps"] as JArray;
            Require(groups != null && groups.Count == expectedSteps * 4, "population step groups");
            for (int step = 0; step < expectedSteps; step++)
            {
                int entities = 0; long ordinal = groups[step * 4][0].Value<long>();
                for (int group = 0; group < 4; group++)
                { var row = groups[step * 4 + group]; Require(row[0].Value<long>() == ordinal && row[1].Value<int>() == group, "step group order"); entities += row[2].Value<int>(); }
                Require(entities == expectedPopulation, "step population coverage");
            }
            var types = movement["types"] as JArray; var counts = movement["counts"] as JArray;
            int finished = -1;
            if (types != null) for (int i = 0; i < types.Count; i++) if (types[i].Value<string>() == "StepFinished") finished = i;
            Require(finished >= 0 && counts != null && finished < counts.Count && counts[finished].Value<int>() == expectedSteps, "StepFinished count");
            Require(movement["stages"] is JArray stages && stages.Count > 0 && movement["preparationWork"] is JObject, "stage/work accounting");
        }

        // minimum seam: this capture requires the current instrumentation. Older
        // files remain historical evidence; missing metrics are never invented.
        static void ValidateCostAccounting(string json)
        {
            var movement = (JObject)JObject.Parse(json)["movementEvidence"];
            void Require(bool condition, string reason)
            { if (!condition) throw new InvalidOperationException("Incomplete step cost accounting: " + reason); }
            Require(movement["costAccountingVersion"]?.Value<int>() == 6, "instrumentation version");
            var preparation = movement["preparationWork"] as JObject;
            Require(preparation != null, "preparation work");
            foreach (string field in new[] { "EntityPureReads", "EntityExistsReads", "EntityReadInvalidations", "EntityAccessReopens", "EntityStampCaptures", "EntityStampChecks", "PreparedBypassIntents", "UsedPreparedBypasses", "UnusedPreparedBypasses", "LateBypasses" })
                Require(preparation[field] != null && preparation[field].Value<long>() >= 0, "missing " + field);
            Require(preparation["PreparedBypassIntents"].Value<long>() == preparation["UsedPreparedBypasses"].Value<long>() + preparation["UnusedPreparedBypasses"].Value<long>(), "prepared bypass balance");
            var topology = movement["topologyWork"] as JObject;
            Require(topology != null && topology["complete"]?.Value<bool>() == true, "complete Topology accounting");
            var origins = topology["origins"] as JArray;
            Require(origins != null && origins.Count == 4, "Topology origins");
            long inserted = 0; var unique = new System.Collections.Generic.HashSet<string>();
            foreach (JObject origin in origins)
            {
                Require(unique.Add(origin["origin"].Value<string>()), "duplicate Topology origin");
                foreach (string field in new[] { "requests", "existing", "inserted", "failed", "batches", "growths", "lookups", "hits", "misses" })
                    Require(origin[field] != null && origin[field].Value<long>() >= 0, "Topology " + field);
                Require(origin["requests"].Value<long>() == origin["existing"].Value<long>() + origin["inserted"].Value<long>() + origin["failed"].Value<long>(), "Topology registration balance");
                Require(origin["lookups"].Value<long>() == origin["hits"].Value<long>() + origin["misses"].Value<long>(), "Topology lookup balance");
                inserted += origin["inserted"].Value<long>();
            }
            foreach (string origin in new[] { "General", "MovementKinematics", "NavigationBypass", "MovementPhysical" })
                Require(unique.Contains(origin), "missing Topology " + origin);
            Require(topology["coordinateDelta"] != null && inserted == topology["coordinateDelta"].Value<long>(), "Topology index size delta");
            Require(topology["retiredCoordinates"] != null && topology["retiredCoordinates"].Value<long>() >= 0, "Topology retirement");
            Require(topology["initialCoordinates"] != null && topology["finalCoordinates"] != null
                && inserted == topology["finalCoordinates"].Value<long>() - topology["initialCoordinates"].Value<long>() + topology["retiredCoordinates"].Value<long>(), "Topology active index / retirement balance");
            var demand = movement["preparationByRequestOutcome"]?["groups"] as JArray;
            Require(demand != null && demand.Count == 5, "request preparation outcomes");
            var stages = movement["stages"] as JArray;
            var names = new System.Collections.Generic.Dictionary<string, JArray>(); long self = 0;
            foreach (var token in stages)
            {
                var row = token as JArray;
                Require(row != null && row.Count == 5, "stage columns");
                string name = row[0].Value<string>(); Require(!names.ContainsKey(name), "duplicate stage"); names.Add(name, row);
                long inclusive = row[2].Value<long>(), own = row[3].Value<long>(), inStep = row[4].Value<long>();
                Require(inclusive >= own && own >= inStep && inStep >= 0, "nested stage duration"); self += inStep;
            }
            foreach (string name in new[] { "HostStep", "AI", "SkillsPrepare", "MovementPump", "SkillsComplete", "SkillsClocks", "SkillsMerge", "SkillsResults", "ResolveAdmission", "ResolveCandidates", "ResolveCurrent", "ResolveResponse", "SpatialPrepare", "SpatialReservation", "SpatialCommit", "SpatialRelease", "SpatialKernel", "MovementPendingRange", "MovementAdmission", "MovementApplyResult", "MovementResponseWrite", "MovementCleanup", "NavigationInputs", "DeferredBypass" })
                Require(names.ContainsKey(name), "missing " + name);
            foreach (string name in new[] { "HostStep", "AI", "SkillsPrepare", "MovementPump", "SkillsComplete", "SkillsClocks", "SkillsMerge", "SkillsResults" })
                Require(names[name][1].Value<long>() > 0, "inactive " + name);
            long observed = movement["observedStepNanoseconds"].Value<long>(), accounted = movement["accountedRootNanoseconds"].Value<long>();
            Require(self <= accounted + stages.Count && accounted <= observed, "exclusive sum / unassigned remainder");
            foreach (string section in new[] { "accessOwners", "nativeAccessOwners", "workerAccessOwners" })
            {
                var owners = movement[section] as JArray; Require(owners != null && owners.Count > 0, section);
                var found = new System.Collections.Generic.HashSet<string>();
                foreach (JObject owner in owners)
                {
                    Require(found.Add(owner["owner"].Value<string>()), "duplicate " + section + " owner");
                    if (section == "workerAccessOwners") Require(owner["Rows"] != null && owner["Rows"].Value<long>() >= 0, "worker row coverage");
                    foreach (string operation in new[] { "Open", "Close", "Refresh", "Read", "Write", "HandleCheck", "PresenceLookup" })
                        Require(owner[operation] != null && owner[operation].Value<long>() >= 0, "access operation");
                    Require(owner["Open"].Value<long>() == owner["Close"].Value<long>(), "unclosed " + section + " interval");
                }
                foreach (string owner in new[] { "AI", "Skills", "MovementEffect", "Movement", "Navigation", "Spatial", "Topology" })
                    Require(found.Contains(owner), "missing " + section + " owner " + owner);
            }
        }

        void StopRecording()
        {
            if (!_saved) return;
            if (_recording) using (var process = System.Diagnostics.Process.GetCurrentProcess())
                _recordingWorkingSetDelta = process.WorkingSet64 - _recordingWorkingSetBefore;
            _recording = false; Profiler.enabled = false; Profiler.enableBinaryLog = false;
        }

        void Finish(bool incomplete, string message)
        {
            bool details = _export?.DetailsTruncated == true || _budget?.DetailsTruncated == true;
            try { _export?.Dispose(); }
            finally
            {
                _export = null;
                if (_windowStarted) ProfilerDriver.ClearAllFrames();
                _windowStarted = false;
                Restore();
            }
            _frameEvidence = null;
            EventBus.Trigger(new PerformanceBenchCaptureCompletedEvent(_owner, _run, incomplete, details, message));
        }

        void Restore()
        {
            if (!_saved) return;
            try { _gameView?.Dispose(); }
            finally
            {
                _gameView = null;
                Profiler.enabled = false;
                ProfilerDriver.deepProfiling = _deep; ProfilerDriver.profileEditor = _editor;
                Profiler.enableAllocationCallstacks = _allocationStacks;
                Profiler.enableBinaryLog = _binary; Profiler.enabled = _enabled;
                _saved = false;
            }
        }

        void OnCompilationStarted(object context) => OnAssembliesRetiring();
        void OnAssembliesRetiring()
        {
            if (_saved && _type != PerformanceBenchCaptureType.Normal)
            {
                StopRecording();
                EventBus.Trigger(new PerformanceBenchWindowCapturedEvent(_owner, _run));
            }
            Dispose();
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        { if (state == PlayModeStateChange.ExitingPlayMode) Dispose(); }

        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            bool unfinished = _saved && _type != PerformanceBenchCaptureType.Normal;
            if (_summaryStarted && !_movementSummaryFrozen)
            {
                _frameEvidence = GameRuntimeDiagnostics.AppendMovementFrameEvidence(_frameEvidence ?? GameRuntimeDiagnostics.FreezeDetailedSkillEvidence());
                _movementSummaryFrozen = true;
            }
            EventBus.Unregister<PerformanceBenchRunPhaseEvent>(this);
            EditorApplication.update -= Update;
            AssemblyReloadEvents.beforeAssemblyReload -= OnAssembliesRetiring;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            StopRecording();
            try
            {
                _export?.Cancel(); _export?.Dispose();
                if (unfinished && _type == PerformanceBenchCaptureType.SkillsSummary && _frameEvidence != null)
                    MorbooProfilerCaptureDumpTool.WriteFrameEvidence(_directory, _frameEvidence, _budget);
            }
            finally
            {
                _export = null;
                if (_windowStarted) ProfilerDriver.ClearAllFrames();
                _windowStarted = false; Restore();
                if (unfinished)
                    EventBus.Trigger(new PerformanceBenchCaptureCompletedEvent(_owner, _run, true, false,
                        "Diagnostic capture/export was cancelled by scene or Editor retirement."));
            }
        }
    }
}
#endif
