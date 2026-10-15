#if UNITY_EDITOR
using System;
using System.IO;
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
        readonly object _owner;
        bool _saved, _recording, _disposed, _windowStarted;
        bool _enabled, _binary, _deep, _editor, _allocationStacks;
        int _run, _first = -1, _last = -1;
        long _recordingWorkingSetBefore, _recordingWorkingSetDelta;
        PerformanceBenchCaptureType _type;
        string _directory, _context, _preparationFailure, _frameEvidence;
        bool _summaryStarted;
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
                _run = e.Run; _type = e.CaptureType; _directory = e.Directory; _budget = e.Budget;
                _first = _last = -1; _windowStarted = false; _preparation = null; _preparationFailure = null;
                _frameEvidence = null; _summaryStarted = false;
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
                if (_summaryStarted && _frameEvidence == null) _frameEvidence = GameRuntimeDiagnostics.FreezeDetailedFrameEvidence();
                StopRecording();
            }
            else if (e.Phase == PerformanceBenchPhaseType.Complete || e.Phase == PerformanceBenchPhaseType.Failed)
            {
                StopRecording();
                if (_type == PerformanceBenchCaptureType.Normal) { Finish(false, ""); return; }
                if (_type == PerformanceBenchCaptureType.SkillsSummary)
                {
                    try
                    {
                        if (!_summaryStarted) throw new InvalidOperationException(_preparationFailure ?? "Summary measurement did not start.");
                        MorbooProfilerCaptureDumpTool.WriteFrameEvidence(_directory, _frameEvidence, _budget);
                        Finish(false, "Bounded frame evidence exported; CPU recording remained disabled.");
                    }
                    catch (ProfilerDumpStorageLimitException exception) { Finish(!exception.DetailOnly, exception.Message); }
                    catch (Exception exception) { Finish(true, exception.Message); }
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
            if (_summaryStarted && _frameEvidence == null) _frameEvidence = GameRuntimeDiagnostics.FreezeDetailedFrameEvidence();
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
