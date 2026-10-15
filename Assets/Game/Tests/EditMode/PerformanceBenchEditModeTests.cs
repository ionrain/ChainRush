#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Tests.EditMode
{
    public sealed class PerformanceBenchEditModeTests
    {
        static Type BenchType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("ChainRush.Diagnostics." + name)).First(type => type != null);
        static object Settings() => Activator.CreateInstance(BenchType("PerformanceBenchSettings"));
        static void Set(object value, string field, object data) => value.GetType().GetField(field).SetValue(value, data);

        [Test]
        public void Settings_RejectImpossibleVisiblePopulation_WithoutChangingInput()
        {
            var settings = Settings(); Set(settings, "population", 200); Set(settings, "visiblePopulation", 250);
            object[] arguments = { null };
            Assert.IsFalse((bool)settings.GetType().GetMethod("TryValidate").Invoke(settings, arguments));
            Assert.AreEqual(250, settings.GetType().GetField("visiblePopulation").GetValue(settings));
            Assert.IsNotEmpty((string)arguments[0]);
        }
        [Test]
        public void Layout_IsRepeatable_AndExternalRoomsStayOutsideVisibleRoom()
        {
            var settings = Settings(); var layout = BenchType("PerformanceBenchLayout");
            var position = layout.GetMethod("Position");
            var first = (Vector3)position.Invoke(null, new object[] { 123, settings });
            var repeated = (Vector3)position.Invoke(null, new object[] { 123, settings });
            var external = (Vector3)position.Invoke(null, new object[] { 250, settings });
            Assert.AreEqual(first, repeated); Assert.Greater(external.x, 50);
            Assert.AreNotEqual((Vector3)position.Invoke(null, new object[] { 124, settings }), first);
        }
        [Test]
        public void ReplacementPlacement_IsRepeatable_AndStaysInItsRoom()
        {
            var settings = Settings(); var layout = BenchType("PerformanceBenchLayout");
            var replacement = layout.GetMethod("ReplacementPosition");
            var origin = (Vector3)layout.GetMethod("RoomOrigin").Invoke(null, new object[] { 2, 1f });
            for (int attempt = 0; attempt < 64; attempt++)
            {
                object[] arguments = { 567, 3, attempt, settings };
                var position = (Vector3)replacement.Invoke(null, arguments);
                Assert.AreEqual(position, replacement.Invoke(null, arguments));
                Assert.LessOrEqual(Math.Abs(position.x - origin.x), 19);
                Assert.LessOrEqual(Math.Abs(position.y - origin.y), 14);
                if (attempt == 0) Assert.AreEqual(layout.GetMethod("Position").Invoke(null, new object[] { 567, settings }), position);
            }
            Assert.AreNotEqual(replacement.Invoke(null, new object[] { 567, 3, 1, settings }),
                replacement.Invoke(null, new object[] { 567, 4, 1, settings }));
        }
        [Test]
        public void Series_FreezesSelectedConfigurations_AndRequiresSelection()
        {
            var entryType = BenchType("PerformanceBenchSeriesEntry");
            var entries = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(entryType));
            var entry = Activator.CreateInstance(entryType); var settings = Settings();
            Set(entry, "settings", settings); Set(entry, "selected", true); entries.Add(entry);
            var freeze = BenchType("PerformanceBenchSeries").GetMethod("Freeze");
            var frozen = (IList)freeze.Invoke(null, new object[] { entries });
            Assert.AreEqual(3, frozen.Count);
            Set(settings, "population", 5000);
            Assert.AreEqual(1000, frozen[0].GetType().GetField("population").GetValue(frozen[0]));
            Set(entry, "selected", false);
            var error = Assert.Throws<TargetInvocationException>(() => freeze.Invoke(null, new object[] { entries }));
            Assert.IsInstanceOf<ArgumentException>(error.InnerException);
        }
        [Test]
        public void Series_RejectsMixedPopulationsBeforeFreezing()
        {
            var entryType = BenchType("PerformanceBenchSeriesEntry");
            var entries = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(entryType));
            foreach (int population in new[] { 1000, 5000 })
            {
                var entry = Activator.CreateInstance(entryType); var settings = Settings();
                Set(settings, "population", population); Set(entry, "settings", settings); Set(entry, "selected", true); entries.Add(entry);
            }
            var error = Assert.Throws<TargetInvocationException>(() => BenchType("PerformanceBenchSeries").GetMethod("Freeze").Invoke(null, new object[] { entries }));
            Assert.IsInstanceOf<ArgumentException>(error.InnerException);
        }
        [Test]
        public void RunLimits_RequireConsecutiveFullMeasuringFrames_AndBoundPreparation()
        {
            var type = BenchType("PerformanceBenchRunLimits"); var limits = Activator.CreateInstance(type, true);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var observe = type.GetMethod("ObserveFrame", flags);
            bool Frame(double duration, string phase = "Measuring", bool boundary = false)
            {
                var row = Activator.CreateInstance(BenchType("PerformanceBenchFrame"));
                Set(row, "wallMs", duration); Set(row, "phaseBoundary", boundary);
                Set(row, "phase", (int)Enum.Parse(BenchType("PerformanceBenchPhaseType"), phase));
                return (bool)observe.Invoke(limits, new[] { row });
            }
            Assert.IsFalse(Frame(1100)); Assert.IsFalse(Frame(1200)); Assert.IsFalse(Frame(1000));
            Assert.IsFalse(Frame(1100)); Assert.IsFalse(Frame(1100, boundary: true));
            Assert.IsFalse(Frame(1100)); Assert.IsFalse(Frame(1100)); Assert.IsTrue(Frame(1100));
            Assert.IsFalse(Frame(1100, "Warmup")); Assert.IsFalse(Frame(1100));
            var deadline = type.GetMethod("PreparationExpired", flags);
            Assert.IsFalse((bool)deadline.Invoke(limits, new object[] { 179.999 }));
            Assert.IsTrue((bool)deadline.Invoke(limits, new object[] { 180d }));
        }
        [Test]
        public void Measurements_EnableTailPercentilesOnlyAtOneThousandFullFrames()
        {
            var type = BenchType("PerformanceBenchMeasurements"); var measurements = (IDisposable)Activator.CreateInstance(type, true);
            try
            {
                for (int i = 0; i < 1000; i++)
                {
                    var row = Activator.CreateInstance(BenchType("PerformanceBenchFrame"));
                    Set(row, "phase", (int)Enum.Parse(BenchType("PerformanceBenchPhaseType"), "Measuring")); Set(row, "wallMs", 5d);
                    type.GetMethod("Frame").Invoke(measurements, new[] { row });
                    if (i == 998)
                    {
                        var partial = type.GetMethod("Summarize").Invoke(measurements, new object[] { 1, Settings(), false, null });
                        Assert.IsFalse((bool)partial.GetType().GetField("statisticsSufficient").GetValue(partial));
                    }
                }
                var result = type.GetMethod("Summarize").Invoke(measurements, new object[] { 1, Settings(), false, null });
                Assert.IsTrue((bool)result.GetType().GetField("statisticsSufficient").GetValue(result));
                Assert.AreEqual(5d, result.GetType().GetField("p99Ms").GetValue(result));
            }
            finally { measurements.Dispose(); }
        }
        [Test]
        public void AuthoredScene_IsSeparate_AndActivityRequiresManualCompletion()
        {
            const string scene = "Assets/Game/Scenes/Diagnostics/PerformanceBench.unity";
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene));
            Assert.IsFalse(EditorBuildSettings.scenes.Any(value => value.enabled && value.path == scene));
            var assets = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Diagnostics/PerformanceBench/Content/BenchAssets.asset");
            Assert.NotNull(assets);
            foreach (string field in new[] { "straightActivity", "navigationActivity" })
            {
                var activity = (Core.Activities.ActivityData)assets.GetType().GetField(field).GetValue(assets);
                Assert.AreEqual(Core.Activities.ActivityEndMode.Manual, activity.Result.EndMode);
                Assert.AreEqual(1, activity.Teams.Count); Assert.IsEmpty(activity.Teams[0].Objectives);
            }
        }
        [Test]
        public void Measurements_KeepAllPhases_ButSummarizeOnlyMeasurementFrames()
        {
            var type = BenchType("PerformanceBenchMeasurements");
            var measurements = (IDisposable)Activator.CreateInstance(type, true);
            string output = Path.Combine(Path.GetTempPath(), "chainrush-bench-" + Guid.NewGuid().ToString("N"));
            try
            {
                var frameType = BenchType("PerformanceBenchFrame");
                void Frame(int number, string phase, double duration, int steps, bool boundary = false)
                {
                    var row = Activator.CreateInstance(frameType);
                    Set(row, "frame", number); Set(row, "wallMs", duration); Set(row, "steps", steps);
                    Set(row, "phaseBoundary", boundary);
                    Set(row, "phase", (int)Enum.Parse(BenchType("PerformanceBenchPhaseType"), phase));
                    type.GetMethod("Frame").Invoke(measurements, new[] { row });
                }
                Frame(1, "Warmup", 100, 1); Frame(2, "Measuring", 4, 0);
                Frame(3, "Measuring", 12, 1); Frame(4, "Cleanup", 500, 0);
                Frame(5, "Measuring", 87000, 0, true);
                var result = type.GetMethod("Summarize").Invoke(measurements, new object[] { 1, Settings(), false, null });
                object Read(string field) => result.GetType().GetField(field).GetValue(result);
                Assert.AreEqual(5, Read("frameCount")); Assert.AreEqual(2, Read("measuredFrames"));
                Assert.AreEqual(8d, Read("meanMs")); Assert.AreEqual(12d, Read("meanStepFrameMs"));
                Assert.AreEqual(8d, Read("medianMs"));
                Assert.IsFalse((bool)Read("statisticsSufficient"));
                Assert.AreEqual(-1d, Read("p95Ms")); Assert.AreEqual(-1d, Read("p99Ms"));
                Assert.AreEqual(4d, Read("meanNoStepFrameMs"));
                type.GetMethod("Write").Invoke(measurements, new object[] { output, new Core.EditorTools.ProfilerDumpStorageBudget(output) });
                Assert.AreEqual(6, File.ReadAllLines(Path.Combine(output, "frames.csv")).Length);
                Assert.That(File.ReadAllLines(Path.Combine(output, "counter-availability.txt")), Is.Not.Empty);
            }
            finally { measurements.Dispose(); if (Directory.Exists(output)) Directory.Delete(output, true); }
        }
        [Test]
        public void MovementBudgets_AndPoolModes_AreIndependentFrozenInputs()
        {
            foreach (string pool in new[] { "Warm", "Expansion" })
            foreach (string budget in new[] { "Current64", "Population" })
            {
                var settings = Settings();
                Set(settings, "pool", Enum.Parse(BenchType("PerformanceBenchPoolType"), pool));
                Set(settings, "movementBudget", Enum.Parse(BenchType("PerformanceBenchMovementBudgetType"), budget));
                object[] args = { null };
                Assert.IsTrue((bool)settings.GetType().GetMethod("TryValidate").Invoke(settings, args));
                var copy = settings.GetType().GetMethod("Copy").Invoke(settings, null);
                Assert.AreEqual(settings.GetType().GetField("pool").GetValue(settings), copy.GetType().GetField("pool").GetValue(copy));
                Assert.AreEqual(settings.GetType().GetField("movementBudget").GetValue(settings), copy.GetType().GetField("movementBudget").GetValue(copy));
            }
        }
    }
}
#endif
