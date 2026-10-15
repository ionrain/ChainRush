#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace ChainRush.Editor
{
    // Editor-only tool: reject unsupported execution rather than change project configuration silently.
    [InitializeOnLoad]
    public sealed class PerformanceBenchSceneGuard : IProcessSceneWithReport
    {
        static PerformanceBenchSceneGuard() => EditorApplication.playModeStateChanged += OnPlayModeChanged;
        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || SceneManager.GetActiveScene().path != PerformanceBenchAuthoring.ScenePath) return;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null && settings.ActivePlayModeDataBuilder is BuildScriptFastMode) return;
            EditorApplication.isPlaying = false;
            UnityEngine.Debug.LogError("PerformanceBench requires Addressables Play Mode Script: Use Asset Database (fastest). Project configuration was not changed.");
        }
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (BuildPipeline.isBuildingPlayer && scene.path == PerformanceBenchAuthoring.ScenePath)
                throw new BuildFailedException("PerformanceBench is an Editor-only diagnostic scene; exclude it from the Player build.");
        }
    }
}
#endif
