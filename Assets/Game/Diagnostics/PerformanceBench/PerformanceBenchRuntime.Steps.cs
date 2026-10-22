#if UNITY_EDITOR
using System.Collections.Generic;
using Core.GameRuntime;
using Core.Scheduling.Events;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    internal sealed partial class PerformanceBenchRuntime
    {
        readonly Stack<StepStart> _stepStarts = new(4);
        struct StepStart
        {
            internal int Index, Phase, Frame, Run;
            internal int MovementCompleted, PoseReachedTarget, PatrolSucceeded;
            internal RuntimeMovementWorkSnapshot Movement;
            internal RuntimeAgentPipelineWorkSnapshot Agents;
        }
        void EnsureFramePhase(int frame)
        {
            int index = frame % 4;
            if (_phaseFrames[index] == frame) return;
            _phaseFrames[index] = frame; _framePhases[index] = _phase; _frameBoundaries[index] = false;
        }
        public void OnEvent(ScheduleStepStartedEvent e)
        {
            if (!ReferenceEquals(e.Source, _tickSource) || !_populationReady || _measurements == null
                || (_phase != PerformanceBenchPhaseType.Warmup && _phase != PerformanceBenchPhaseType.Measuring)) return;
            int frame = Time.frameCount;
            EnsureFramePhase(frame);
            if (_captureType == PerformanceBenchCaptureType.SkillsSummary && _phase == PerformanceBenchPhaseType.Measuring)
            {
                if (!GameRuntimeDiagnostics.MovementPopulationConfigured)
                {
                    foreach (var actor in _actors)
                        if (actor != null && actor.Entity.IsValid && !actor.EntityRemoved)
                            GameRuntimeDiagnostics.AddMovementEvidencePopulationMember(actor.Entity.Value, _activity.Value);
                    GameRuntimeDiagnostics.CompleteMovementEvidencePopulation();
                }
                GameRuntimeDiagnostics.BeginMovementEvidenceStep(e.Tick.TickIndex);
            }
            _stepStarts.Push(new StepStart { Index = _stepsAfterReady + 1, Phase = (int)_phase, Frame = frame, Run = _run,
                MovementCompleted = WorkFor(frame).MovementCompleted, PoseReachedTarget = WorkFor(frame).PoseReachedTarget,
                PatrolSucceeded = WorkFor(frame).PatrolSucceeded,
                Movement = GameRuntimeDiagnostics.ReadMovementWork(frame), Agents = GameRuntimeDiagnostics.ReadAgentPipelineWork(frame) });
        }
        public void OnEvent(ScheduleStepFinishedEvent e)
        {
            if (!ReferenceEquals(e.Source, _tickSource) || _stepStarts.Count == 0) return;
            var start = _stepStarts.Pop();
            if (start.Run != _run || _measurements == null) return;
            var movement = GameRuntimeDiagnostics.ReadMovementWork(start.Frame);
            var agents = GameRuntimeDiagnostics.ReadAgentPipelineWork(start.Frame);
            _measurements.Step(new PerformanceBenchStep {
                index = start.Index, simulationStep = e.Tick.TickIndex, unityFrame = start.Frame, phase = start.Phase,
                completed = e.Completed, elapsedMs = e.ElapsedMs, allocatedBytes = e.AllocatedBytes,
                aiTicks = agents.AITicks - start.Agents.AITicks, aiActions = agents.AIActions - start.Agents.AIActions,
                managedCommands = agents.AIManagedCommands - start.Agents.AIManagedCommands,
                skillActions = agents.SkillActions - start.Agents.SkillActions,
                searches = agents.AStarSearches - start.Agents.AStarSearches,
                expanded = agents.NavigationExpanded - start.Agents.NavigationExpanded,
                intents = movement.Intents - start.Movement.Intents,
                candidates = movement.Candidates - start.Movement.Candidates,
                distanceChecks = movement.DistanceChecks - start.Movement.DistanceChecks,
                commits = movement.AppliedResults - start.Movement.AppliedResults,
                movementCompleted = WorkFor(start.Frame).MovementCompleted - start.MovementCompleted,
                poseReachedTarget = WorkFor(start.Frame).PoseReachedTarget - start.PoseReachedTarget,
                patrolSucceeded = WorkFor(start.Frame).PatrolSucceeded - start.PatrolSucceeded,
            });
            if (_captureType == PerformanceBenchCaptureType.SkillsSummary && start.Phase == (int)PerformanceBenchPhaseType.Measuring)
                GameRuntimeDiagnostics.EndMovementEvidenceStep(e.Completed);
            if (!e.Completed) { Fail("Шаг симуляции завершился исключением; данные неполны."); return; }
            if (_settings.intervalType != PerformanceBenchIntervalType.SimulationSteps || _stop) return;
            _now = Time.realtimeSinceStartupAsDouble;
            if (_phase == PerformanceBenchPhaseType.Warmup && start.Index == _settings.warmupSteps)
            { SetPhase(PerformanceBenchPhaseType.Measuring); _nextCycle = _now; }
            else if (_phase == PerformanceBenchPhaseType.Measuring
                && start.Index == _settings.warmupSteps + _settings.measurementSteps)
                SetPhase(PerformanceBenchPhaseType.Draining);
        }
    }
}
#endif
