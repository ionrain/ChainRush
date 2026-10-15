#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Globalization;
using Core;
using Core.Activities;
using Core.CapabilityHosts;
using Core.CapabilityHosts.Runtime;
using Core.Events;
using Core.GameRuntime;
using Core.Projection;
using Core.Runtime;
using Core.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChainRush.Tests.PlayMode
{
    public sealed class PerformanceBenchPlayModeTests
    {
        static Type BenchType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("ChainRush.Diagnostics." + name)).First(type => type != null);
        static object Settings(int population, string scenario)
        {
            var type = BenchType("PerformanceBenchSettings"); var settings = Activator.CreateInstance(type);
            void Set(string name, object value) => type.GetField(name).SetValue(settings, value);
            Set("population", population); Set("visiblePopulation", population); Set("batchSize", population);
            Set("initialPoolCapacity", 0); Set("warmup", 0f); Set("duration", 1f); Set("interval", .025f); Set("lifetime", .025f);
            Set("scenario", Enum.Parse(BenchType("PerformanceBenchScenarioType"), scenario)); return settings;
        }
        static void Command(string action, object settings = null)
        {
            var type = BenchType("PerformanceBenchCommandEvent");
            var command = Activator.CreateInstance(type, Enum.Parse(BenchType("PerformanceBenchCommandType"), action), settings, null, null);
            typeof(EventBus).GetMethod("Trigger", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(type).Invoke(null, new[] { command });
        }
        static IEnumerator Await(Func<bool> condition, string failure)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(condition(), failure);
        }
        [UnitySetUp]
        public IEnumerator OpenBench()
        {
            yield return RetireExistingHosts();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Game/Scenes/Diagnostics/PerformanceBench.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return Await(() => UnityEngine.Object.FindFirstObjectByType<GameRuntimeHost>()?.RuntimeContext?.IsInitialized == true,
                "Standalone bench runtime did not initialize.");
        }
        [UnityTearDown]
        public IEnumerator StopBench()
        {
            Command("Stop");
            yield return Await(() => !ActivityService.GetAll().Any(activity => activity.State != ActivityState.Closed),
                "Bench Activity did not finish native retirement.");
            yield return RetireExistingHosts();
        }
        static IEnumerator RetireExistingHosts()
        {
            // GameRuntimeHost intentionally survives scene loads. A new test owns a new runtime,
            // so retire the previous context before replacing the scene and its persistent root.
            foreach (var host in UnityEngine.Object.FindObjectsByType<GameRuntimeHost>(FindObjectsSortMode.None))
            {
                var context = host.RuntimeContext;
                if (context != null)
                {
                    var retirement = context.RetireAsync();
                    yield return Await(() => retirement.IsCompleted, "Previous runtime did not retire.");
                    retirement.GetAwaiter().GetResult(); context.Dispose();
                }
                UnityEngine.Object.Destroy(host.gameObject);
            }
            yield return null;
            Assert.IsEmpty(UnityEngine.Object.FindObjectsByType<GameRuntimeHost>(FindObjectsSortMode.None));
        }
        [UnityTest]
        public IEnumerator Camera_IncludesVisibleRoom_AndPatrolEndpoints()
        {
            var camera = Camera.allCameras.Single();
            foreach (float density in new[] { 1f, 2f })
            {
                var type = BenchType("PerformanceBenchCameraSettingsEvent");
                var message = Activator.CreateInstance(type, density);
                typeof(EventBus).GetMethod("Trigger", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(type).Invoke(null, new[] { message });
                yield return null;
                var layout = BenchType("PerformanceBenchLayout");
                foreach (bool right in new[] { false, true })
                {
                    var point = (Vector3)layout.GetMethod("Target").Invoke(null, new object[] { 0, right, density });
                    var viewport = camera.WorldToViewportPoint(point);
                    Assert.That(viewport.x, Is.InRange(0f, 1f)); Assert.That(viewport.y, Is.InRange(0f, 1f));
                }
            }
        }
        [UnityTest]
        public IEnumerator Lifecycle_ReusesView_WithNewEntity_AndIgnoresOldRemoval()
        {
            using (var capture = new BindingCapture(StaticDefinition()))
            {
                Command("Start", Settings(1, "Lifecycle"));
                yield return Await(() => capture.Ready.Count >= 2, "Lifecycle did not complete two creations.");
                var first = capture.Ready[0]; var next = capture.Ready[1];
                Assert.AreNotEqual(first.Handle.EntityId, next.Handle.EntityId);
                Assert.AreEqual(first.ViewTransform, next.ViewTransform);
                EventBus.Trigger(new ProjectionLifecycleEvent(ProjectionLifecycleEventType.Removed, first.Handle, next.ViewTransform));
                Assert.IsTrue(next.ViewTransform.GetComponent<ProjectionBindingController>().TryGetContext(out var current));
                Assert.AreEqual(next.Handle, current.Handle);
            }
        }
        [UnityTest]
        public IEnumerator Stop_DuringAdmission_RemovesMaterializedEntities_AndReturnsViews()
        {
            using (var capture = new BindingCapture(StaticDefinition()))
            {
                var settings = Settings(30, "Render");
                Command("Start", settings);
                yield return Await(() => capture.Ready.Count > 0, "No object became ready.");
                Command("Stop");
                yield return Await(() => capture.Ready.All(ready => !CapabilityHostService.Exists(ready.Handle.EntityId)), "Stopped run retained capability hosts.");
                foreach (var ready in capture.Ready)
                    Assert.IsTrue(ready.ViewTransform == null || !ready.ViewTransform.GetComponent<ProjectionBindingController>().TryGetContext(out _));
            }
        }
        [UnityTest]
        public IEnumerator Scenarios_UseBothMovementBudgets_AndKeepOffscreenActorsRegistered()
        {
            var runtime = RuntimeOwner();
            foreach (string scenario in new[] { "Render", "AI", "Navigation", "Churn" })
            foreach (string budget in new[] { "Current64", "Population" })
            {
                var settings = Settings(72, scenario); var type = settings.GetType();
                type.GetField("visiblePopulation").SetValue(settings, 12);
                type.GetField("duration").SetValue(settings, 2f);
                type.GetField("movementBudget").SetValue(settings, Enum.Parse(BenchType("PerformanceBenchMovementBudgetType"), budget));
                Command("Start", settings);
                yield return Await(() =>
                {
                    if (Phase(runtime) == "Preparing") Assert.LessOrEqual((int)Field(runtime, "_creatingCount"), 25);
                    return Phase(runtime) == "Measuring" || Phase(runtime) == "Failed";
                }, "Scenario did not reach measurement.");
                Assert.AreEqual("Measuring", Phase(runtime), Field(runtime, "_failure") as string);
                Assert.AreEqual(72, Field(runtime, "_population"));
                var installed = (MovementTrafficProfileData)typeof(MovementService).GetField("TrafficProfile", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.AreEqual(budget == "Current64" ? 64 : 72, installed.MaxSolvedAgentsPerStep);
                if (scenario != "Render")
                    yield return Await(() => HasOffscreenProgress(runtime, 12) || Phase(runtime) == "Failed",
                        "Offscreen actors never progressed through the native movement path.");
                if (scenario != "Render") Assert.IsTrue(HasOffscreenProgress(runtime, 12), Field(runtime, "_failure") as string);
                yield return Await(() => Phase(runtime) == "Complete" || Phase(runtime) == "Failed", "Scenario did not clean up.");
                Assert.AreEqual("Complete", Phase(runtime), Field(runtime, "_failure") as string);
                var results = (IList)Field(runtime, "_results"); var result = results[results.Count - 1];
                Assert.Greater((int)result.GetType().GetField("measuredFrames").GetValue(result), 0);
                Assert.Greater((int)result.GetType().GetField("simulationSteps").GetValue(result), 0);
                Assert.AreEqual(0, Field(runtime, "_population")); Assert.AreEqual(0, Field(runtime, "_bound"));
            }
        }
        [UnityTest]
        public IEnumerator WorkAfterMeasurement_IsExported_ButExcludedFromMeasuredDuration()
        {
            var runtime = RuntimeOwner(); bool injected = false;
            EditorApplication.CallbackFunction work = () =>
            {
                if (injected || Phase(runtime) != "Draining") return;
                injected = true;
                // Reproduce work performed after the last measurement pulse, such as SaveProfile.
                System.Threading.Thread.Sleep(250);
            };
            EditorApplication.update += work;
            try
            {
                var settings = Settings(1, "Render"); settings.GetType().GetField("duration").SetValue(settings, .2f);
                Command("Start", settings);
                yield return Await(() => Phase(runtime) == "Complete" || Phase(runtime) == "Failed", "Boundary workload did not complete.");
                Assert.AreEqual("Complete", Phase(runtime)); Assert.IsTrue(injected, "Post-measurement work was not exercised.");
                var results = (IList)Field(runtime, "_results"); var result = results[results.Count - 1];
                var resultType = result.GetType();
                Assert.Less((double)resultType.GetField("measuredSeconds").GetValue(result), .4d);
                string path = (string)resultType.GetField("outputDirectory").GetValue(result);
                var rows = File.ReadAllLines(Path.Combine(path, "frames.csv")); var header = rows[0].Split(',');
                int boundary = Array.IndexOf(header, "phaseBoundary"), phase = Array.IndexOf(header, "phase"), wall = Array.IndexOf(header, "wallMs");
                Assert.IsTrue(rows.Skip(1).Select(row => row.Split(',')).Any(row => row[phase] == "3"
                    && bool.Parse(row[boundary]) && double.Parse(row[wall], CultureInfo.InvariantCulture) >= 200));
            }
            finally { EditorApplication.update -= work; }
        }
        [UnityTest]
        public IEnumerator PoolExpansion_RecordsNewInstances_SeparatelyFromWarmPool()
        {
            var runtime = RuntimeOwner();
            foreach (string pool in new[] { "Warm", "Expansion" })
            {
                var settings = Settings(10, "Lifecycle"); var type = settings.GetType();
                type.GetField("pool").SetValue(settings, Enum.Parse(BenchType("PerformanceBenchPoolType"), pool));
                type.GetField("initialPoolCapacity").SetValue(settings, 1);
                type.GetField("duration").SetValue(settings, 2f);
                Command("Start", settings);
                yield return Await(() => Phase(runtime) == "Complete" || Phase(runtime) == "Failed", "Pool workload did not complete.");
                Assert.AreEqual("Complete", Phase(runtime), Field(runtime, "_failure") as string);
                var results = (IList)Field(runtime, "_results"); var result = results[results.Count - 1];
                int expanded = (int)result.GetType().GetField("poolExpansions").GetValue(result);
                if (pool == "Warm") Assert.AreEqual(0, expanded); else Assert.Greater(expanded, 0);
            }
        }
        static object RuntimeOwner()
        {
            var context = UnityEngine.Object.FindFirstObjectByType<GameRuntimeHost>().RuntimeContext;
            var resources = (IEnumerable)context.GetType().GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);
            foreach (var resource in resources) if (resource.GetType() == BenchType("PerformanceBenchRuntime")) return resource;
            throw new InvalidOperationException("Bench workload owner not installed.");
        }
        static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        static string Phase(object runtime) => Field(runtime, "_phase").ToString();
        static bool HasOffscreenProgress(object runtime, int visible)
        {
            var actors = Field(runtime, "_actors") as Array;
            if (actors == null) return false;
            for (int i = visible; i < actors.Length; i++)
                if ((int)actors.GetValue(i).GetType().GetField("LastMovedFrame").GetValue(actors.GetValue(i)) >= 0) return true;
            return false;
        }
        static CapabilityHostData StaticDefinition()
        {
            var assets = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Diagnostics/PerformanceBench/Content/BenchAssets.asset");
            return (CapabilityHostData)assets.GetType().GetField("staticObject").GetValue(assets);
        }
        sealed class BindingCapture : IEventListener<ProjectionLifecycleEvent>, IDisposable
        {
            readonly CapabilityHostData _definition;
            public readonly List<ProjectionLifecycleEvent> Ready = new();
            public BindingCapture(CapabilityHostData definition) { _definition = definition; EventBus.Register<ProjectionLifecycleEvent>(this); }
            public void OnEvent(ProjectionLifecycleEvent e)
            {
                if (e.EventType == ProjectionLifecycleEventType.BoundReady && CapabilityHostService.TryGet(e.Handle.EntityId, out var host)
                    && Core.Economy.EconomyAssetDataExtensions.Matches(host.Definition, _definition)) Ready.Add(e);
            }
            public void Dispose() => EventBus.Unregister<ProjectionLifecycleEvent>(this);
        }
    }
}
#endif
