#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Core.EditorTools;
using Core.GameRuntime;
using Core.Skills;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;

namespace ChainRush.Tests.EditMode
{
    public sealed partial class PerformanceBenchProfilerEditModeTests
    {
        [Test]
        public void SkillsSummary_MissingMovementStillExportsEvidenceAndRestoresSettings()
        {
            var profileField = typeof(GameRuntimeDiagnostics).GetField("_profile", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = (RuntimeDiagnosticsProfile)profileField.GetValue(null);
            object profile = new RuntimeDiagnosticsProfile();
            typeof(RuntimeDiagnosticsProfile).GetField("orchestrationTrace", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, true);
            GameRuntimeDiagnostics.StartSession(1337, (RuntimeDiagnosticsProfile)profile);
            var source = typeof(SkillService).Assembly.GetType("Core.Skills.SkillRuntimeDiagnostics", true);
            ((IDisposable)source.GetMethod("BeginStep", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 1L, 1L })).Dispose();
            string directory = Path.Combine(Path.GetTempPath(), "bench-skills-summary-" + Guid.NewGuid().ToString("N"));
            var owner = new object(); var type = BenchType("PerformanceBenchProfilerAdapter");
            var adapter = (IDisposable)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { owner }, null);
            var budget = new ProfilerDumpStorageBudget(directory);
            var capture = Enum.Parse(BenchType("PerformanceBenchCaptureType"), "SkillsSummary");
            void Phase(string name) => Publish(Activator.CreateInstance(BenchType("PerformanceBenchRunPhaseEvent"), owner, 1,
                Enum.Parse(BenchType("PerformanceBenchPhaseType"), name), capture, directory, "{}",
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")), budget));
            try
            {
                Phase("Preparing"); Phase("Measuring");
                Assert.IsFalse(Profiler.enabled); Assert.IsFalse(Profiler.enableBinaryLog);
                Assert.IsFalse((bool)type.GetField("_recording", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter));
                Assert.IsTrue((bool)type.GetField("_summaryStarted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter));
                Phase("Draining");
                string frozen = (string)type.GetField("_frameEvidence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter);
                Assert.IsNotNull(frozen);
                GameRuntimeDiagnostics.StartSession(0, previous);
                // A cancelled/frozen adapter still writes its evidence, while the
                // validator must reject the missing Movement payload.
                type.GetField("_movementSummaryFrozen", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(adapter, true);
                type.GetField("_expectedSummarySteps", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(adapter, 256);
                type.GetField("_expectedSummaryPopulation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(adapter, 1000);
                Phase("Complete");
                Assert.IsFalse((bool)type.GetField("_saved", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter));
                Assert.AreEqual(frozen, File.ReadAllText(Path.Combine(directory, "runtime-frame-evidence.json")));
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.raw", SearchOption.AllDirectories).Length);
                Assert.AreEqual(0, Directory.GetFiles(directory, "*.data", SearchOption.AllDirectories).Length);
                Assert.Less(budget.WrittenBytes, 8 * 1024 * 1024);
            }
            finally
            {
                adapter.Dispose(); GameRuntimeDiagnostics.StartSession(0, previous);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        static string CompleteMovementSummaryFixture()
        {
            var text = new System.Text.StringBuilder("{\"movementEvidence\":{\"observedSteps\":256,\"completedObservedSteps\":256,\"selectionSteps\":32,\"completedDetailSteps\":224,\"intervalComplete\":true,\"finalDisplayFrameComplete\":true,\"truncated\":false,\"cpuRecording\":false,\"types\":[\"StepFinished\"],\"counts\":[256],\"stages\":[[\"Inputs\",1,10,10]],\"preparationWork\":{},\"population\":{\"configured\":true,\"entities\":2,\"entityColumns\":[\"entity\",\"observedSteps\"],\"agents\":[[1,256],[2,256]],\"steps\":[");
            for (int step = 1; step <= 256; step++) for (int group = 0; group < 4; group++)
            {
                if (step != 1 || group != 0) text.Append(',');
                text.Append('[').Append(step).Append(',').Append(group).Append(',').Append(group == 0 ? 2 : 0).Append(']');
            }
            return text.Append("]}}}").ToString();
        }

        [Test]
        public void SkillsSummary_ValidatorAcceptsCompletePopulationAndStepCoverage()
        {
            var validate = BenchType("PerformanceBenchProfilerAdapter").GetMethod("ValidateSummary", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.DoesNotThrow(() => validate.Invoke(null, new object[] { CompleteMovementSummaryFixture(), 256, 2 }));
        }

        [TestCase("missing")] [TestCase("steps")] [TestCase("truncated")] [TestCase("display")]
        [TestCase("coverage")] [TestCase("duplicate")] [TestCase("groups")] [TestCase("detail")]
        public void SkillsSummary_ValidatorRejectsIncompleteOrInconsistentEvidence(string scenario)
        {
            string json = CompleteMovementSummaryFixture();
            switch (scenario)
            {
                case "missing": json = "{\"movementEvidence\":null}"; break;
                case "steps": json = json.Replace("\"completedObservedSteps\":256", "\"completedObservedSteps\":255"); break;
                case "truncated": json = json.Replace("\"truncated\":false", "\"truncated\":true"); break;
                case "display": json = json.Replace("\"finalDisplayFrameComplete\":true", "\"finalDisplayFrameComplete\":false"); break;
                case "coverage": json = json.Replace("[2,256]", "[2,255]"); break;
                case "duplicate": json = json.Replace("[2,256]", "[1,256]"); break;
                case "groups": json = json.Replace("[1,0,2]", "[1,0,1]"); break;
                case "detail": json = json.Replace("\"completedDetailSteps\":224", "\"completedDetailSteps\":223"); break;
            }
            var validate = BenchType("PerformanceBenchProfilerAdapter").GetMethod("ValidateSummary", BindingFlags.Static | BindingFlags.NonPublic);
            var error = Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, new object[] { json, 256, 2 }));
            Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
        }

        [Test]
        public void GameViewScope_DisposeRemovesOnlyItsTemporaryResolution()
        {
            var assembly = typeof(UnityEditor.EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
            var group = sizesType.GetProperty("currentGroup", flags).GetValue(sizes);
            int CustomCount() => (int)group.GetType().GetMethod("GetCustomCount", flags).Invoke(group, null);
            int before = CustomCount();
            var type = BenchType("PerformanceBenchGameViewScope");
            var scope = (IDisposable)Activator.CreateInstance(type, flags, null, null, null);
            try { Assert.That(CustomCount(), Is.InRange(before, before + 1)); }
            finally { scope.Dispose(); scope.Dispose(); }
            Assert.AreEqual(before, CustomCount());
        }

        [Test]
        public void SkillsSummary_RequiresDetailedFlagWhileNormalRejectsIt()
        {
            var settings = Activator.CreateInstance(BenchType("PerformanceBenchSettings"));
            var validate = BenchType("PerformanceBenchRuntime").GetMethod("ValidateCaptureType", BindingFlags.Static | BindingFlags.NonPublic);
            var summary = Enum.Parse(BenchType("PerformanceBenchCaptureType"), "SkillsSummary");
            Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, new[] { settings, summary }));
            settings.GetType().GetField("trace").SetValue(settings, true);
            Assert.DoesNotThrow(() => validate.Invoke(null, new[] { settings, summary }));
            var normal = Enum.Parse(BenchType("PerformanceBenchCaptureType"), "Normal");
            Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, new[] { settings, normal }));
        }
    }
}
#endif
