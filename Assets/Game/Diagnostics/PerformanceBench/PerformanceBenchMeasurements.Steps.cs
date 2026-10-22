#if UNITY_EDITOR
using System;
using System.IO;
using Core.EditorTools;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    [Serializable]
    public struct PerformanceBenchStep
    {
        public int index, simulationStep, unityFrame, phase;
        public bool completed;
        public double elapsedMs;
        public long allocatedBytes, aiTicks, aiActions, managedCommands, skillActions, searches, expanded;
        public long intents, candidates, distanceChecks, commits;
        public int movementCompleted, poseReachedTarget, patrolSucceeded;
    }

    internal sealed partial class PerformanceBenchMeasurements
    {
        [Serializable]
        struct PhaseDebtSnapshot
        {
            public int phase, unityFrame, stepAfterReady;
            public double accumulatedMs;
        }
        readonly PhaseDebtSnapshot[] _phaseDebt = new PhaseDebtSnapshot[64];
        int _phaseDebtCount;
        internal void PhaseDebt(int phase, int frame, int step, double accumulatedMs)
        {
            if (_phaseDebtCount == _phaseDebt.Length) { Overflow = true; return; }
            _phaseDebt[_phaseDebtCount++] = new PhaseDebtSnapshot { phase = phase, unityFrame = frame,
                stepAfterReady = step, accumulatedMs = accumulatedMs };
        }
        readonly PerformanceBenchStep[] _steps = new PerformanceBenchStep[32768];
        int _stepCount;
        internal void Step(in PerformanceBenchStep step)
        {
            if (_stepCount == _steps.Length) { Overflow = true; return; }
            _steps[_stepCount++] = step;
        }
        void SummarizeSteps(PerformanceBenchResult result)
        {
            double total = 0;
            int expected = result.settings.warmupSteps + 1;
            bool contiguous = true;
            for (int i = 0; i < _stepCount; i++)
            {
                var step = _steps[i];
                if (step.phase != (int)PerformanceBenchPhaseType.Measuring || !step.completed) continue;
                if (result.settings.intervalType == PerformanceBenchIntervalType.SimulationSteps)
                {
                    if (step.index < result.settings.warmupSteps + 1
                        || step.index > result.settings.warmupSteps + result.settings.measurementSteps) continue;
                    contiguous &= step.index == expected++;
                }
                result.measuredStepCount++; total += step.elapsedMs;
            }
            result.meanCompleteStepMs = result.measuredStepCount == 0 ? -1 : total / result.measuredStepCount;
            result.stepIntervalComplete = result.settings.intervalType == PerformanceBenchIntervalType.WallTime
                || contiguous && result.measuredStepCount == result.settings.measurementSteps;
        }
        void WriteSteps(string directory, ProfilerDumpStorageBudget budget)
        {
            using (var writer = budget.OpenWriter(Path.Combine(directory, "phase-debt.jsonl")))
                for (int i = 0; i < _phaseDebtCount; i++) writer.WriteLine(JsonUtility.ToJson(_phaseDebt[i]));
            using (var writer = budget.OpenWriter(Path.Combine(directory, "steps.jsonl")))
                for (int i = 0; i < _stepCount; i++) writer.WriteLine(JsonUtility.ToJson(_steps[i]));
            using (var writer = budget.OpenWriter(Path.Combine(directory, "boundary-frames.jsonl")))
                for (int i = 0; i < FrameCount; i++)
                    if (_frames[i].phaseBoundary) writer.WriteLine(JsonUtility.ToJson(_frames[i]));
        }
    }
}
#endif
