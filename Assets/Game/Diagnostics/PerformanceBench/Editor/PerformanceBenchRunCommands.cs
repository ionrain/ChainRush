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

        [MenuItem("ChainRush/Diagnostics/Run Skills Summary (1000, budget 64)", true)]
        static bool CanRunSkillsSummary() => EditorApplication.isPlaying
            && !EditorApplication.isCompiling
            && SceneManager.GetActiveScene().path == PerformanceBenchAuthoring.ScenePath;
    }
}
#endif
