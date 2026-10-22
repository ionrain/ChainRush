#if UNITY_EDITOR
using Core.Events;
using ChainRush.Diagnostics;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace ChainRush.Editor
{
    // minimum seam: Editor ingress only. The existing runtime owns admission, frozen settings and cleanup.
    internal static class PerformanceBenchRunCommands
    {
        [MenuItem("ChainRush/Diagnostics/Run Skills Summary (1000, budget 64)")]
        static void RunSkillsSummary()
        {
            if (!CanRunSkillsSummary()) return;
            var settings = new PerformanceBenchSettings
            {
                scenario = PerformanceBenchScenarioType.AI,
                movementBudget = PerformanceBenchMovementBudgetType.Current64,
                pool = PerformanceBenchPoolType.Warm,
                population = 1000, visiblePopulation = 250, batchSize = 100,
                repetitions = 1, seed = 1337, speed = 2, density = 1,
                warmup = 30, duration = 180, trace = true,
            };
            EventBus.Trigger(new PerformanceBenchCommandEvent(PerformanceBenchCommandType.Start,
                settings, captureType: PerformanceBenchCaptureType.SkillsSummary));
        }

        [MenuItem("ChainRush/Diagnostics/Navigation 1000/Movement evidence (128 + 256)")]
        static void RunMovementEvidence()
        {
            if (!CanRunSkillsSummary()) return;
            var settings = new PerformanceBenchSettings {
                scenario = PerformanceBenchScenarioType.Navigation, movementBudget = PerformanceBenchMovementBudgetType.Population,
                pool = PerformanceBenchPoolType.Warm, population = 1000, visiblePopulation = 250, batchSize = 25,
                repetitions = 1, seed = 1337, speed = 2, density = 1, warmup = 30, duration = 60,
                intervalType = PerformanceBenchIntervalType.SimulationSteps, warmupSteps = 128, measurementSteps = 256, trace = true };
            EventBus.Trigger(new PerformanceBenchCommandEvent(PerformanceBenchCommandType.Start, settings,
                captureType: PerformanceBenchCaptureType.SkillsSummary));
        }
        [MenuItem("ChainRush/Diagnostics/Navigation 1000/Movement evidence (128 + 256)", true)]
        static bool CanRunMovementEvidence() => CanRunSkillsSummary();

        [MenuItem("ChainRush/Diagnostics/Navigation 1000/Fixed steps Normal (128 + 256)")]
        static void RunFixedNavigation() => RunNavigation(PerformanceBenchIntervalType.SimulationSteps, false);
        [MenuItem("ChainRush/Diagnostics/Navigation 1000/Fixed steps Trace (128 + 3 frames)")]
        static void RunFixedNavigationTrace() => RunNavigation(PerformanceBenchIntervalType.SimulationSteps, true);
        [MenuItem("ChainRush/Diagnostics/Navigation 1000/Timed Normal (30 + 60 seconds)")]
        static void RunTimedNavigation() => RunNavigation(PerformanceBenchIntervalType.WallTime, false);

        static void RunNavigation(PerformanceBenchIntervalType interval, bool trace)
        {
            if (!CanRunSkillsSummary()) return;
            var settings = new PerformanceBenchSettings {
                scenario = PerformanceBenchScenarioType.Navigation, movementBudget = PerformanceBenchMovementBudgetType.Population,
                pool = PerformanceBenchPoolType.Warm, population = 1000, visiblePopulation = 250, batchSize = 100,
                repetitions = 1, seed = 1337, speed = 2, density = 1, warmup = 30, duration = 60,
                intervalType = interval, warmupSteps = 128, measurementSteps = 256, trace = trace };
            EventBus.Trigger(new PerformanceBenchCommandEvent(PerformanceBenchCommandType.Start, settings,
                captureType: trace ? PerformanceBenchCaptureType.TraceWindow : PerformanceBenchCaptureType.Normal));
        }
        [MenuItem("ChainRush/Diagnostics/Navigation 1000/Fixed steps Normal (128 + 256)", true)]
        [MenuItem("ChainRush/Diagnostics/Navigation 1000/Fixed steps Trace (128 + 3 frames)", true)]
        [MenuItem("ChainRush/Diagnostics/Navigation 1000/Timed Normal (30 + 60 seconds)", true)]
        static bool CanRunNavigation() => CanRunSkillsSummary();

        [MenuItem("ChainRush/Diagnostics/Run Skills Summary (1000, budget 64)", true)]
        static bool CanRunSkillsSummary() => EditorApplication.isPlaying
            && !EditorApplication.isCompiling
            && SceneManager.GetActiveScene().path == PerformanceBenchAuthoring.ScenePath;
    }
}
#endif
