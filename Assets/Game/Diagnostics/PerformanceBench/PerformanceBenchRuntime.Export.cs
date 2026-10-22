#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Core.GameRuntime;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace ChainRush.Diagnostics
{
    internal sealed partial class PerformanceBenchRuntime
    {
        [Serializable] sealed class PhaseInterval
        {
            public int phase, firstUnityFrame, lastUnityFrame;
            public double startedAt, finishedAt;
        }
        [Serializable] sealed class WorkloadContext
        {
            public int run, phase, activity, firstUnityFrame;
            public PerformanceBenchCaptureType captureType;
            public PerformanceBenchSettings settings;
        }
        [Serializable] sealed class RepositoryInfo { public string path, revision, status, reason; public bool dirty; }
        [Serializable] sealed class AssemblyInfo { public string name, mvid; }
        [Serializable] sealed class EnvironmentInfo
        {
            public string projectPath, unityVersion, capturedAtUtc, operatingSystem, processor, graphics;
            public string stepAccounting = "steps.jsonl spans complete synchronous tick dispatch including public step consumers; step observations and profiler-context preparation are outside its stopwatch. Fixed interval is warmupSteps+1 through warmupSteps+measurementSteps. Only complete render frames within the phase enter frame summaries; boundary frames are separate. Counts describe performed work, not equal trajectories.";
            public string frameAccounting = "wallMs includes Editor; phaseBoundary frames are exported but excluded from measurement summaries because they can contain work outside the measured phase. Recorder durations are inclusive and must not be summed. SkillsSummary exports bounded main-thread stage self times separately; worker CPU is unavailable without CPU recording.";
            public string operationAccounting = "Observed CPU/allocation cover admission, budgeted journal work and synchronous materialization. Deferred projection CPU/allocation and shared scheduling/queue costs are unavailable per object; use the profiler hierarchy. Placement marker preparation/release is reported separately as helper work. Request summaries include accepted measurement requests completed during draining; frame throughput uses only measurement completions. Inclusive counters must not be summed.";
            public string observationAccounting = "Occupancy checks and visible-count scans run at 2 Hz under PerformanceBench.Observe/Work; occupancy violations mean the native CanOccupyPosition check rejects the current occupant pose. Helpers are route targets, obstacles and transient placement markers, separate from the load population.";
            public int processorCount, screenWidth, screenHeight, targetFrameRate, vSyncCount, managedFrameCapacity = 240000, operationCapacity = 500000;
            public int registeredResourceCount;
            public bool deepProfile, profileEditor, profilerEnabled, allocationCallstacks;
            public bool binaryLogging, cpuWindowRequested;
            public bool burstCompilationEnabled, burstSafetyChecks, jobsDebuggerEnabled;
            public string jobFloatMode = "Strict";
            public PerformanceBenchCaptureType captureType;
            public string profilerAccounting = "Profiler settings are observed at preparation. Normal and SkillsSummary keep CPU recording off; CPU/trace windows enable it only for the first three complete frames after warmup. Job worker CPU values are inclusive and do not add to wall time. NativeCapacityBytes measures owned persistent input/output capacities; NativeStream allocator overhead and Unity native-only profiler memory are unavailable. Recorder durations are inclusive; window category percentages use only the exported main-thread frame time.";
            public List<AssemblyInfo> assemblies = new();
            public List<RepositoryInfo> repositories = new();
            public List<string> editorWindows = new();
        }
        [Serializable] sealed class PhaseReport { public List<PhaseInterval> phases; }
        [Serializable] sealed class SeriesReport { public List<PerformanceBenchResult> runs; }
        readonly List<PhaseInterval> _phaseIntervals = new();
        EnvironmentInfo _environment;
        string OutputRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProfilerCaptures", "PerformanceBench");

        void UpdateWorkloadContext()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (_phaseIntervals.Count > 0)
            {
                var previous = _phaseIntervals[_phaseIntervals.Count - 1];
                previous.finishedAt = now; previous.lastUnityFrame = Time.frameCount;
            }
            _phaseIntervals.Add(new PhaseInterval { phase = (int)_phase, firstUnityFrame = Time.frameCount + 1,
                lastUnityFrame = -1, startedAt = now, finishedAt = -1 });
            RefreshWorkloadPayload();
        }
        void RefreshWorkloadPayload()
        {
            if (_settings == null) return;
            var data = new WorkloadContext { run = _run, phase = (int)_phase, activity = _activity.Value,
                firstUnityFrame = Time.frameCount + 1, captureType = _captureType, settings = _settings.Copy() };
            GameRuntimeProfilerWorkloadContext.Set(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)));
        }
        void CaptureEnvironment()
        {
            _environment = new EnvironmentInfo
            {
                projectPath = Directory.GetParent(Application.dataPath).FullName, unityVersion = Application.unityVersion,
                capturedAtUtc = DateTime.UtcNow.ToString("O"), operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType, graphics = SystemInfo.graphicsDeviceName,
                processorCount = SystemInfo.processorCount, screenWidth = Screen.width, screenHeight = Screen.height,
                targetFrameRate = Application.targetFrameRate, vSyncCount = QualitySettings.vSyncCount,
                deepProfile = ProfilerDriver.deepProfiling, profileEditor = ProfilerDriver.profileEditor,
                profilerEnabled = Profiler.enabled, allocationCallstacks = Profiler.enableAllocationCallstacks,
                binaryLogging = Profiler.enableBinaryLog, cpuWindowRequested = _captureType == PerformanceBenchCaptureType.CPUWindow || _captureType == PerformanceBenchCaptureType.TraceWindow,
                captureType = _captureType,
                burstCompilationEnabled = Unity.Burst.BurstCompiler.Options.EnableBurstCompilation,
                burstSafetyChecks = Unity.Burst.BurstCompiler.Options.EnableBurstSafetyChecks,
                jobsDebuggerEnabled = Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled,
                registeredResourceCount = Core.Economy.EconomyService.GetRegisteredAssetsSorted().Length,
            };
            foreach (var assembly in new[] { typeof(PerformanceBenchRuntime).Assembly, typeof(GameRuntimeDiagnostics).Assembly, typeof(UnityEditor.Editor).Assembly, typeof(Unity.Burst.BurstCompiler).Assembly, typeof(Unity.Collections.NativeStream).Assembly, typeof(Unity.Mathematics.math).Assembly })
                _environment.assemblies.Add(new AssemblyInfo { name = assembly.GetName().Name, mvid = assembly.ManifestModule.ModuleVersionId.ToString() });
            _environment.repositories.Add(ReadRepository(_environment.projectPath));
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(GameRuntimeDiagnostics).Assembly);
            if (package != null) _environment.repositories.Add(ReadRepository(package.resolvedPath));
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                _environment.editorWindows.Add(window.GetType().FullName + ": maximized=" + window.maximized);
        }
        static RepositoryInfo ReadRepository(string path)
        {
            var result = new RepositoryInfo { path = path };
            try { result.revision = Git(path, "rev-parse HEAD"); result.dirty = Git(path, "status --porcelain").Length > 0; result.status = "recorded"; }
            catch (Exception exception) { result.status = "unavailable"; result.reason = exception.Message; }
            return result;
        }
        static string Git(string path, string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo("git", "-C \"" + path.Replace("\"", "\\\"") + "\" " + arguments)
                    { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                process.Start(); var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(5000)) { process.Kill(); throw new IOException("Git metadata unavailable: timeout."); }
                if (process.ExitCode != 0) throw new IOException(error.GetAwaiter().GetResult());
                return output.GetAwaiter().GetResult().Trim();
            }
        }
        void PrepareExportDirectory()
        {
            string name = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture)
                + "_" + _captureType + "_run-" + _run;
            _exportDirectory = Path.Combine(OutputRoot, name);
            _exportBudget = new Core.EditorTools.ProfilerDumpStorageBudget(_exportDirectory);
        }

        void WriteResult(PerformanceBenchResult result)
        {
            if (_phase == PerformanceBenchPhaseType.Measuring) throw new InvalidOperationException("Экспорт внутри измеряемого интервала запрещён.");
            result.outputDirectory = _exportDirectory;
            result.captureType = _captureType;
            if (_phaseIntervals.Count > 0)
            {
                var phase = _phaseIntervals[_phaseIntervals.Count - 1];
                phase.lastUnityFrame = Time.frameCount; phase.finishedAt = Time.realtimeSinceStartupAsDouble;
            }
            _measurements.Write(result.outputDirectory, _exportBudget);
            // Diagnostic availability is revision-specific; both compared
            // packages use the same bench without inventing baseline balances.
            var resourceReader = typeof(GameRuntimeDiagnostics).GetMethod("ReadResources", new[] { typeof(GameRuntimeContext) });
            _exportBudget.WriteText(Path.Combine(result.outputDirectory, "resources-after-cleanup.json"), resourceReader == null
                ? "{\"available\":false,\"reason\":\"This package revision does not expose runtime resource balances.\"}"
                : JsonUtility.ToJson(resourceReader.Invoke(null, new object[] { _context }), true));
            _exportBudget.WriteText(Path.Combine(result.outputDirectory, "summary.json"), JsonUtility.ToJson(result, true));
            _exportBudget.WriteText(Path.Combine(result.outputDirectory, "environment.json"), JsonUtility.ToJson(_environment, true));
            _exportBudget.WriteText(Path.Combine(result.outputDirectory, "phases.json"), JsonUtility.ToJson(new PhaseReport { phases = _phaseIntervals }, true));
            var closures = _phaseIntervals.FindAll(phase => phase.phase == (int)PerformanceBenchPhaseType.Cleanup);
            _exportBudget.WriteText(Path.Combine(result.outputDirectory, "activity-close.json"), JsonUtility.ToJson(new PhaseReport { phases = closures }, true));
            _phaseIntervals.Clear();
        }
        void WriteSeriesSummary()
        {
            if (_results.Count == 0) return;
            Directory.CreateDirectory(OutputRoot);
            File.WriteAllText(Path.Combine(OutputRoot, "latest-series.json"), JsonUtility.ToJson(new SeriesReport { runs = _results }, true));
        }
    }
}
#endif
