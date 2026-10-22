#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;

namespace ChainRush.Tests.EditMode
{
    public sealed partial class PerformanceBenchProfilerEditModeTests
    {
        [Test]
        public void FixedInterval_UsesSeparateWarmupAndMeasurementDeadlines_WithoutExtendingWallRuns()
        {
            var settings = Activator.CreateInstance(BenchType("PerformanceBenchSettings"));
            var limits = Activator.CreateInstance(BenchType("PerformanceBenchRunLimits"), true);
            var observe = limits.GetType().GetMethod("ObservePhase", BindingFlags.Instance | BindingFlags.NonPublic);
            object Phase(string name) => Enum.Parse(BenchType("PerformanceBenchPhaseType"), name);
            Assert.AreEqual("None", observe.Invoke(limits, new[] { settings, Phase("Warmup"), 181d }).ToString());
            settings.GetType().GetField("intervalType").SetValue(settings,
                Enum.Parse(BenchType("PerformanceBenchIntervalType"), "SimulationSteps"));
            Assert.AreEqual("None", observe.Invoke(limits, new[] { settings, Phase("Warmup"), 179.99d }).ToString());
            Assert.AreEqual("WarmupDeadline", observe.Invoke(limits, new[] { settings, Phase("Warmup"), 180d }).ToString());
            Assert.AreEqual("MeasurementDeadline", observe.Invoke(limits, new[] { settings, Phase("Measuring"), 180d }).ToString());
            Assert.AreEqual("None", observe.Invoke(limits, new[] { settings, Phase("Draining"), 180d }).ToString());
        }

        [Test]
        public void FixedInterval_CountsAll256StepsEvenWhenBoundaryFramesAreExcluded()
        {
            var settings = Activator.CreateInstance(BenchType("PerformanceBenchSettings"));
            settings.GetType().GetField("intervalType").SetValue(settings,
                Enum.Parse(BenchType("PerformanceBenchIntervalType"), "SimulationSteps"));
            var type = BenchType("PerformanceBenchMeasurements");
            using var measurements = (IDisposable)Activator.CreateInstance(type, true);
            var stepType = BenchType("PerformanceBenchStep");
            var addStep = type.GetMethod("Step", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int step = 1; step <= 384; step++)
            {
                var row = Activator.CreateInstance(stepType);
                stepType.GetField("index").SetValue(row, step);
                stepType.GetField("phase").SetValue(row, step <= 128 ? 2 : 3);
                stepType.GetField("completed").SetValue(row, true);
                stepType.GetField("elapsedMs").SetValue(row, 2d);
                addStep.Invoke(measurements, new[] { row });
            }
            var result = type.GetMethod("Summarize").Invoke(measurements, new[] { (object)1, settings, false, null });
            Assert.AreEqual(256, result.GetType().GetField("measuredStepCount").GetValue(result));
            Assert.AreEqual(2d, result.GetType().GetField("meanCompleteStepMs").GetValue(result));
            Assert.AreEqual(true, result.GetType().GetField("stepIntervalComplete").GetValue(result));
            Assert.AreEqual(0, result.GetType().GetField("measuredFrames").GetValue(result));
            Assert.AreEqual(-1d, result.GetType().GetField("p95Ms").GetValue(result));
        }

        [Test]
        public void FixedInterval_MissingOrRepeatedStepDoesNotSatisfyCompletion()
        {
            var settings = Activator.CreateInstance(BenchType("PerformanceBenchSettings"));
            settings.GetType().GetField("intervalType").SetValue(settings,
                Enum.Parse(BenchType("PerformanceBenchIntervalType"), "SimulationSteps"));
            var type = BenchType("PerformanceBenchMeasurements");
            using var measurements = (IDisposable)Activator.CreateInstance(type, true);
            var stepType = BenchType("PerformanceBenchStep");
            for (int step = 129; step <= 384; step++)
            {
                var row = Activator.CreateInstance(stepType);
                stepType.GetField("index").SetValue(row, step == 200 ? 199 : step);
                stepType.GetField("phase").SetValue(row, 3);
                stepType.GetField("completed").SetValue(row, true);
                type.GetMethod("Step", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(measurements, new[] { row });
            }
            var result = type.GetMethod("Summarize").Invoke(measurements, new[] { (object)1, settings, false, null });
            Assert.AreEqual(false, result.GetType().GetField("stepIntervalComplete").GetValue(result));
        }

        [Test]
        public void PhaseDebt_StoresBoundaryValuesAndMarksUnavailableWithoutSubstitutingZero()
        {
            var type = BenchType("PerformanceBenchMeasurements");
            using var measurements = (IDisposable)Activator.CreateInstance(type, true);
            var add = type.GetMethod("PhaseDebt", BindingFlags.Instance | BindingFlags.NonPublic);
            add.Invoke(measurements, new object[] { 2, 50, 0, -1d });
            add.Invoke(measurements, new object[] { 3, 60, 128, 273.25d });
            add.Invoke(measurements, new object[] { 4, 80, 384, 2.5d });
            var rows = (Array)type.GetField("_phaseDebt", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(measurements);
            Assert.AreEqual(3, type.GetField("_phaseDebtCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(measurements));
            var row = rows.GetValue(0); Assert.AreEqual(-1d, row.GetType().GetField("accumulatedMs").GetValue(row));
            row = rows.GetValue(1); Assert.AreEqual(273.25d, row.GetType().GetField("accumulatedMs").GetValue(row));
            Assert.AreEqual(128, row.GetType().GetField("stepAfterReady").GetValue(row));
            row = rows.GetValue(2); Assert.AreEqual(2.5d, row.GetType().GetField("accumulatedMs").GetValue(row));
            Assert.AreEqual(384, row.GetType().GetField("stepAfterReady").GetValue(row));
        }

        [Test]
        public void PhaseDebt_ReadsTheAuthoritativeRealtimeAccumulatorWithoutChangingCatchUp()
        {
            var lease = Core.Scheduling.SchedulingService.Acquire(Core.Scheduling.ScheduleRequest.ForRealtime(.25f,
                dedicated: true, catchUpPolicyType: Core.Scheduling.RealtimeCatchUpPolicyType.Budgeted, maxStepsPerFrame: 1));
            var other = Core.Scheduling.SchedulingService.Acquire(Core.Scheduling.ScheduleRequest.ForTurnBased(1));
            var read = BenchType("PerformanceBenchProfilerAdapter").GetMethod("ReadScheduleDebt", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                Assert.AreEqual(-1d, read.Invoke(null, new object[] { other.TickSource }));
                Assert.AreEqual(0d, read.Invoke(null, new object[] { lease.TickSource }));
                Core.Scheduling.SchedulingService.Update(.75f);
                Assert.AreEqual(500d, read.Invoke(null, new object[] { lease.TickSource }));
                Assert.IsTrue(Core.Scheduling.SchedulingService.TryGetSnapshot(lease.Id, out var first));
                Assert.AreEqual(1, first.TickIndex);
                Core.Scheduling.SchedulingService.Update(.25f);
                Assert.AreEqual(500d, read.Invoke(null, new object[] { lease.TickSource }));
                Assert.IsTrue(Core.Scheduling.SchedulingService.TryGetSnapshot(lease.Id, out var second));
                Assert.AreEqual(2, second.TickIndex);
            }
            finally { Core.Scheduling.SchedulingService.Release(lease); Core.Scheduling.SchedulingService.Release(other); }
            Assert.AreEqual(-1d, read.Invoke(null, new object[] { lease.TickSource }));
        }

        [Test]
        public void NormalFrames_KeepFramesWithoutSimulationInTheFullFrameMean()
        {
            var type = BenchType("PerformanceBenchMeasurements");
            using var measurements = (IDisposable)Activator.CreateInstance(type, true);
            var frameType = BenchType("PerformanceBenchFrame");
            var add = type.GetMethod("Frame");
            int frame = 0;
            foreach (var item in new[] { (4, 100d), (0, 2d), (0, 3d) })
            {
                var row = Activator.CreateInstance(frameType);
                frameType.GetField("frame").SetValue(row, ++frame);
                frameType.GetField("phase").SetValue(row, 3);
                frameType.GetField("steps").SetValue(row, item.Item1);
                frameType.GetField("wallMs").SetValue(row, item.Item2);
                add.Invoke(measurements, new[] { row });
            }
            var settings = Activator.CreateInstance(BenchType("PerformanceBenchSettings"));
            var result = type.GetMethod("Summarize").Invoke(measurements, new[] { (object)1, settings, false, null });
            Assert.AreEqual(35d, result.GetType().GetField("meanMs").GetValue(result));
            Assert.AreEqual(100d, result.GetType().GetField("meanStepFrameMs").GetValue(result));
            Assert.AreEqual(2.5d, result.GetType().GetField("meanNoStepFrameMs").GetValue(result));
            Assert.AreEqual(1, result.GetType().GetField("simulationFrames").GetValue(result));
            Assert.AreEqual(2, result.GetType().GetField("noSimulationFrames").GetValue(result));
            Assert.AreEqual(4, result.GetType().GetField("simulationSteps").GetValue(result));
        }
    }
}
#endif
