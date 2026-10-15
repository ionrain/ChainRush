#if UNITY_EDITOR
using System.Collections.Generic;
using Core.Events;
using Core.EditorTools;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    public readonly struct PerformanceBenchCommandEvent : IEvent
    {
        public readonly PerformanceBenchCommandType Command;
        public readonly PerformanceBenchSettings Settings;
        public readonly PerformanceBenchCaptureType? CaptureType;
        public readonly IReadOnlyList<PerformanceBenchSettings> Series;
        public PerformanceBenchCommandEvent(PerformanceBenchCommandType command,
            PerformanceBenchSettings settings = null, IEnumerable<PerformanceBenchSettings> series = null, PerformanceBenchCaptureType? captureType = null)
        {
            Command = command; CaptureType = captureType;
            Settings = settings?.Copy();
            var snapshot = new List<PerformanceBenchSettings>();
            if (series != null) foreach (var entry in series) snapshot.Add(entry.Copy());
            Series = snapshot.AsReadOnly();
        }
    }
    public readonly struct PerformanceBenchRunPhaseEvent : IEvent
    {
        public readonly object Owner;
        public readonly int Run;
        public readonly PerformanceBenchPhaseType Phase;
        public readonly PerformanceBenchCaptureType CaptureType;
        public readonly string Directory, Context, ProjectPath;
        public readonly ProfilerDumpStorageBudget Budget;
        public PerformanceBenchRunPhaseEvent(object owner, int run, PerformanceBenchPhaseType phase,
            PerformanceBenchCaptureType captureType, string directory, string context, string projectPath, ProfilerDumpStorageBudget budget)
        { Owner = owner; Run = run; Phase = phase; CaptureType = captureType; Directory = directory;
            Context = context; ProjectPath = projectPath; Budget = budget; }
    }
    public readonly struct PerformanceBenchCaptureCompletedEvent : IEvent
    {
        public readonly object Owner;
        public readonly int Run;
        public readonly bool Incomplete, DetailsTruncated;
        public readonly string Message;
        public PerformanceBenchCaptureCompletedEvent(object owner, int run, bool incomplete, bool detailsTruncated, string message)
        { Owner = owner; Run = run; Incomplete = incomplete; DetailsTruncated = detailsTruncated; Message = message; }
    }
    public readonly struct PerformanceBenchWindowCapturedEvent : IEvent
    {
        public readonly object Owner;
        public readonly int Run;
        public PerformanceBenchWindowCapturedEvent(object owner, int run) { Owner = owner; Run = run; }
    }

    public readonly struct PerformanceBenchPulseEvent : IEvent
    {
        public readonly double Time;
        public readonly int Frame;
        public PerformanceBenchPulseEvent(double time, int frame) { Time = time; Frame = frame; }
    }
    public readonly struct PerformanceBenchCameraSettingsEvent : IEvent
    {
        public readonly float Density;
        public PerformanceBenchCameraSettingsEvent(float density) { Density = density; }
    }
    public readonly struct PerformanceBenchViewEvent : IEvent
    {
        public readonly Rect Bounds;
        public PerformanceBenchViewEvent(Rect bounds) { Bounds = bounds; }
    }
    public readonly struct PerformanceBenchStateEvent : IEvent
    {
        public readonly PerformanceBenchPhaseType Phase;
        public readonly int RequestedPopulation;
        public readonly PerformanceBenchStopReasonType StopReason;
        public readonly int Run, Population, Bound, Visible, Pending, CompletedRuns;
        public readonly int Moved, Waiting, RoutesBuilt, RoutesFailed, OccupancyViolations, MovementBudget;
        public readonly double Elapsed, FrameMs;
        public readonly string Message;
        public PerformanceBenchStateEvent(PerformanceBenchPhaseType phase, int run, int population,
            int bound, int visible, int pending, int completedRuns, double elapsed, double frameMs, string message,
            int moved = 0, int waiting = 0, int routesBuilt = 0, int routesFailed = 0, int occupancyViolations = 0, int movementBudget = 0, int requestedPopulation = 0, PerformanceBenchStopReasonType stopReason = PerformanceBenchStopReasonType.None)
        {
            RequestedPopulation = requestedPopulation; StopReason = stopReason;
            Phase = phase; Run = run; Population = population; Bound = bound; Visible = visible;
            Pending = pending; CompletedRuns = completedRuns; Elapsed = elapsed; FrameMs = frameMs; Message = message;
            Moved = moved; Waiting = waiting; RoutesBuilt = routesBuilt; RoutesFailed = routesFailed;
            OccupancyViolations = occupancyViolations; MovementBudget = movementBudget;
        }
    }
}
#endif
