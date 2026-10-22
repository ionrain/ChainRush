#if UNITY_EDITOR
using System;
using Core.Scheduling.Events;
using Core.GameRuntime;
using Core.World;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    internal sealed partial class PerformanceBenchRuntime
    {
        int _lastPoolTotal;
        public void OnEvent(PerformanceBenchViewEvent e) => _viewBounds = e.Bounds;
        void CheckReadinessDeadline()
        {
            if (_actors == null || _hasFailed || _creatingCount == 0) return;
            foreach (var actor in _actors)
                if (actor.Creating && _now - actor.Operation.admittedAt > 60)
                { Fail("Создание не завершилось за 60 секунд; cause not established. Смотрите дамп и очередь операций."); break; }
        }
        void AbandonUnreadyCreations()
        {
            if (_actors == null) return;
            foreach (var actor in _actors)
            {
                if (!actor.Creating || actor.Pending.IsValid) continue;
                SetCreating(actor, false); actor.Operation.success = false; _measurements?.Operation(actor.Operation);
                ReleaseActorPlacement(actor);
            }
        }
        ref FrameWork WorkFor(int frame)
        {
            ref var work = ref _frameWork[frame % 4];
            if (work.Frame != frame) work = new FrameWork { Frame = frame };
            return ref work;
        }
        void RecordPreviousFrame(int current)
        {
            if (_measurements == null || current < 1) return;
            int previous = current - 1;
            if (_phaseFrames[previous % 4] != previous) return;
            ref var work = ref WorkFor(previous);
            var jobs = GameRuntimeDiagnostics.ReadMovementWork(previous);
            var agents = GameRuntimeDiagnostics.ReadAgentPipelineWork(previous);
            var row = new PerformanceBenchFrame
            {
                aiNativeCapacityBytes = agents.AINativeCapacityBytes, skillNativeCapacityBytes = agents.SkillNativeCapacityBytes,
                navigationNativeCapacityBytes = agents.NavigationNativeCapacityBytes, kinematicsNativeCapacityBytes = agents.KinematicsNativeCapacityBytes,
                aiBatches = agents.AIBatches,
                aiPrograms = agents.AIPrograms,
                skillBatches = agents.SkillBatches,
                skillExecutions = agents.SkillExecutions,
                navigationAdvances = agents.NavigationAdvances,
                aStarSearches = agents.AStarSearches,
                aiTicks = agents.AITicks,
                aiActions = agents.AIActions,
                aiManagedCommands = agents.AIManagedCommands,
                aiInvalidations = agents.AIInvalidations,
                skillActions = agents.SkillActions,
                navigationExpanded = agents.NavigationExpanded,
                navigationGeometryChecks = agents.NavigationGeometryChecks,
                navigationOccupancyChecks = agents.NavigationOccupancyChecks,
                bypassCandidates = agents.BypassCandidates,
                bypassSnapshotClear = agents.BypassSnapshotClear,
                frame = previous, phase = (int)_framePhases[previous % 4], steps = work.Steps,
                screenWidth = work.ScreenWidth, screenHeight = work.ScreenHeight,
                firstStepAfterReady = work.FirstStepAfterReady, lastStepAfterReady = work.LastStepAfterReady,
                jobPipelines = jobs.Pipelines, jobIntents = jobs.Intents, jobCandidates = jobs.Candidates, jobDistanceChecks = jobs.DistanceChecks,
                jobPreparedPoints = jobs.PreparedPoints, jobNeighbors = jobs.Neighbors, appliedMovementResults = jobs.AppliedResults, nativeCapacityBytes = jobs.NativeCapacityBytes,
                phaseBoundary = _frameBoundaries[previous % 4] || _framePhases[previous % 4] != _phase,
                entities = work.Entities, bound = work.Bound, visible = work.Visible, pending = work.Pending, helpers = work.Helpers,
                wallMs = Time.unscaledDeltaTime * 1000d, moved = work.Moved, waiting = work.Waiting,
                movementCompleted = work.MovementCompleted, poseReachedTarget = work.PoseReachedTarget, patrolSucceeded = work.PatrolSucceeded,
                traffic = work.Traffic, routesBuilt = work.Built, routesFailed = work.Failed,
                created = work.Created, deleted = work.Deleted, rented = work.Rented, expanded = work.Expanded,
                occupancyChecks = work.OccupancyChecks, occupancyViolations = work.OccupancyViolations, blocked = work.Blocked,
            };
            _measurements.Frame(row);
            if (_limits.ObserveFrame(row))
                StopForLimit(PerformanceBenchStopReasonType.SlowFrames, "Три последовательных полных кадра измерения дольше 1 секунды. Принятая работа завершается, затем штатная очистка.");
            if (_pool != null && _pool.TryGetSnapshot(_poolKey, out var pool))
            {
                ref var currentWork = ref WorkFor(current);
                currentWork.Expanded += Math.Max(0, pool.TotalCount - _lastPoolTotal);
                _lastPoolTotal = pool.TotalCount;
            }
        }
        public void OnEvent(ScheduleStepDispatchedEvent e)
        {
            using var observation = ObserverMarker.Auto();
            if (IsActive && ReferenceEquals(e.TickSource, _tickSource))
            {
                ref var work = ref WorkFor(Time.frameCount); work.Steps++;
                if (_populationReady)
                {
                    int step = ++_stepsAfterReady;
                    if (work.FirstStepAfterReady == 0) work.FirstStepAfterReady = step;
                    work.LastStepAfterReady = step;
                }
            }
        }
        public void OnEvent(MovementAgentStepEvent e)
        {
            using var observation = ObserverMarker.Auto();
            if (e.ActivityId != _activity || !_byEntity.TryGetValue(e.EntityId.Value, out var actor)) return;
            int frame = Time.frameCount;
            ref var work = ref WorkFor(Time.frameCount);
            if (e.ResultType == MovementStepResultType.Completed) work.MovementCompleted++;
            if (e.ResultType == MovementStepResultType.Advanced && actor.LastMovedFrame != frame)
            { work.Moved++; actor.LastMovedFrame = frame; }
            else if (e.ResultType == MovementStepResultType.Waiting && actor.LastWaitingFrame != frame)
            { work.Waiting++; actor.LastWaitingFrame = frame; }
        }
        public void OnEvent(MovementAgentPoseChangedEvent e)
        {
            using var observation = ObserverMarker.Auto();
            if (e.ActivityId != _activity || !_byEntity.ContainsKey(e.EntityId.Value) || !e.ReachedTarget) return;
            WorkFor(Time.frameCount).PoseReachedTarget++;
        }
        public void OnEvent(MovementTrafficResolvedEvent e)
        {
            using var observation = ObserverMarker.Auto();
            if (e.ActivityId != _activity || !_byEntity.TryGetValue(e.EntityId.Value, out var actor)) return;
            ref var work = ref WorkFor(Time.frameCount); work.Traffic++;
            if (e.ResultType >= MovementTrafficResultType.Yielding && actor.LastWaitingFrame != Time.frameCount)
            { work.Waiting++; actor.LastWaitingFrame = Time.frameCount; }
            if (e.ResultType >= MovementTrafficResultType.WaitingBlocked) work.Blocked++;
        }
        public void OnEvent(NavigationRouteBuiltEvent e)
        {
            using var observation = ObserverMarker.Auto();
            if (e.Route == null || e.Route.ActivityId != _activity || !_byEntity.ContainsKey(e.Route.MoverEntityId.Value)) return;
            ref var work = ref WorkFor(Time.frameCount); work.Built++;
        }
        public void OnEvent(NavigationRouteFailedEvent e)
        {
            using var observation = ObserverMarker.Auto();
            if (e.ActivityId != _activity || !_byEntity.ContainsKey(e.MoverEntityId.Value)) return;
            ref var work = ref WorkFor(Time.frameCount); work.Failed++;
        }
        void UpdateVisible()
        {
            _visible = 0;
            if (_actors == null) return;
            foreach (var actor in _actors)
            {
                if (!actor.Ready || actor.Renderer == null || !actor.Renderer.enabled) continue;
                var bounds = actor.Renderer.bounds;
                if (_viewBounds.Overlaps(Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y))) _visible++;
            }
        }
        void CheckOccupancy()
        {
            if (_settings == null || _settings.scenario != PerformanceBenchScenarioType.Navigation || _actors == null) return;
            using var observation = ObserverMarker.Auto();
            ref var work = ref WorkFor(Time.frameCount);
            foreach (var actor in _actors)
            {
                if (!actor.Ready || actor.Deleting || actor.EntityRemoved
                    || !SpatialService.TryGetOccupantSnapshot(actor.Entity, out var occupant)) continue;
                work.OccupancyChecks++;
                if (!SpatialService.CanOccupyPosition(actor.Entity, occupant.Position, occupant.Footprint)) work.OccupancyViolations++;
            }
        }
        void PublishState()
        {
            ref var work = ref WorkFor(Time.frameCount);
            Core.Events.EventBus.Trigger(new PerformanceBenchStateEvent(_phase, _run, _population,
                _bound, _visible, CountWorking(), _results.Count,
                _now - _phaseStarted, Time.unscaledDeltaTime * 1000d, _message ?? _failure,
                work.Moved, work.Waiting, work.Built, work.Failed, work.OccupancyViolations,
                _settings == null ? 0 : _settings.movementBudget == PerformanceBenchMovementBudgetType.Current64 ? 64 : _settings.population,
                _settings?.population ?? 0, _stopReason));
        }
    }
}
#endif
