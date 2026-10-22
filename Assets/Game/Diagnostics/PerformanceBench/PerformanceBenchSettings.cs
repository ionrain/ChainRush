#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace ChainRush.Diagnostics
{
    public enum PerformanceBenchIntervalType { WallTime, SimulationSteps }
    public enum PerformanceBenchCaptureType { Normal, CPUWindow, TraceWindow, SkillsSummary }
    public enum PerformanceBenchScenarioType { Lifecycle, AI, Navigation, Render, Churn }
    public enum PerformanceBenchPoolType { Warm, Expansion }
    public enum PerformanceBenchMovementBudgetType { Current64, Population }
    public enum PerformanceBenchPhaseType { Idle, Preparing, Warmup, Measuring, Draining, Cleanup, Complete, Failed }
    public enum PerformanceBenchStopReasonType { None, User, PreparationDeadline, SlowFrames, BufferOverflow, LifecycleFailure, WarmupDeadline, MeasurementDeadline }
    public enum PerformanceBenchCommandType { Start, Stop, CreateBatch, DeleteBatch, Clear, RunSeries, Export }

    // working model: an explicit, frozen configuration for one diagnostic workload.
    [Serializable]
    public sealed class PerformanceBenchSettings
    {
        public PerformanceBenchScenarioType scenario = PerformanceBenchScenarioType.AI;
        public PerformanceBenchPoolType pool = PerformanceBenchPoolType.Warm;
        public PerformanceBenchMovementBudgetType movementBudget = PerformanceBenchMovementBudgetType.Population;
        public int population = 1000;
        public int visiblePopulation = 250;
        public int batchSize = 10;
        public float interval = 1;
        public float lifetime = 1;
        public float speed = 2;
        public float density = 1;
        public float replacementPercent = 1;
        public PerformanceBenchIntervalType intervalType;
        public int warmupSteps = 128;
        public int measurementSteps = 256;
        public float warmup = 30;
        public float duration = 180;
        public int repetitions = 3;
        public int seed = 1337;
        public int initialPoolCapacity = 8;
        public bool trace;

        public bool Moves => scenario == PerformanceBenchScenarioType.AI
            || scenario == PerformanceBenchScenarioType.Navigation || scenario == PerformanceBenchScenarioType.Churn;
        public PerformanceBenchSettings Copy() => (PerformanceBenchSettings)MemberwiseClone();
        public bool TryValidate(out string failure)
        {
            failure = null;
            if (!Enum.IsDefined(typeof(PerformanceBenchIntervalType), intervalType)
                || !Enum.IsDefined(typeof(PerformanceBenchScenarioType), scenario)
                || !Enum.IsDefined(typeof(PerformanceBenchPoolType), pool)
                || !Enum.IsDefined(typeof(PerformanceBenchMovementBudgetType), movementBudget))
                failure = "Неизвестный режим стенда.";
            else if (warmupSteps < 1 || measurementSteps < 1 || warmupSteps > 16384 || measurementSteps > 16384)
                failure = "Число шагов этапа должно быть от 1 до 16384.";
            else if (population < 1 || population > 10000 || visiblePopulation < 1
                || visiblePopulation > Math.Min(300, population))
                failure = "Объекты: 1–10000; видимые: 1–300 и не больше всей популяции.";
            else if (batchSize < 1 || batchSize > population || initialPoolCapacity < 0 || initialPoolCapacity > population)
                failure = "Пакет и начальная ёмкость пула должны помещаться в популяцию.";
            else if (pool == PerformanceBenchPoolType.Expansion && initialPoolCapacity < 1)
                failure = "Для подготовки prefab в режиме расширения нужна начальная ёмкость не меньше 1.";
            else if (!Positive(interval) || !Positive(lifetime) || !Positive(speed) || !Positive(density)
                || !Positive(duration) || warmup < 0 || float.IsNaN(warmup) || float.IsInfinity(warmup)
                || replacementPercent <= 0 || replacementPercent > 100 || float.IsNaN(replacementPercent)
                || repetitions < 1 || repetitions > 100)
                failure = "Интервалы, скорость и плотность должны быть положительными; замена: 0–100%; повторы: 1–100.";
            return failure == null;
        }
        static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class PerformanceBenchSeriesEntry
    {
        public bool selected;
        public PerformanceBenchSettings settings = new PerformanceBenchSettings();
    }

    public static class PerformanceBenchSeries
    {
        public static List<PerformanceBenchSettings> Freeze(IEnumerable<PerformanceBenchSeriesEntry> entries)
        {
            var result = new List<PerformanceBenchSettings>();
            foreach (var entry in entries)
            {
                if (entry == null || !entry.selected) continue;
                if (entry.settings == null || !entry.settings.TryValidate(out _))
                    throw new ArgumentException("В серии есть некорректная выбранная конфигурация.");
                for (int i = 0; i < entry.settings.repetitions; i++) result.Add(entry.settings.Copy());
            }
            ValidatePopulation(result);
            if (result.Count == 0) throw new ArgumentException("Выберите хотя бы одну конфигурацию серии.");
            return result;
        }
        internal static void ValidatePopulation(IReadOnlyList<PerformanceBenchSettings> runs)
        {
            if (runs.Count == 0) return;
            int population = runs[0].population;
            foreach (var run in runs)
                if (run.population != population)
                    throw new ArgumentException("Автоматическая серия должна содержать одну популяцию. После отчёта 1000 → отдельное решение о 5000; после 5000 → отдельное решение о 10000.");
        }
        public static double EstimateSeconds(IEnumerable<PerformanceBenchSettings> runs)
        {
            double seconds = 0;
            foreach (var run in runs) seconds += run.intervalType == PerformanceBenchIntervalType.SimulationSteps ? 360 : run.warmup + run.duration;
            return seconds;
        }
    }
}
#endif
