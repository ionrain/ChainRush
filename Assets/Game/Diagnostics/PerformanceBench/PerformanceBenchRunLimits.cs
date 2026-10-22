#if UNITY_EDITOR
namespace ChainRush.Diagnostics
{
    // working model: stop admission at safety limits; the runtime still drains accepted work.
    internal sealed class PerformanceBenchRunLimits
    {
        int _slowFrames;
        internal void Reset() => _slowFrames = 0;
        internal bool PreparationExpired(double elapsed) => elapsed >= 180;
        internal PerformanceBenchStopReasonType ObservePhase(PerformanceBenchSettings settings, PerformanceBenchPhaseType phase, double elapsed)
        {
            if (settings.intervalType != PerformanceBenchIntervalType.SimulationSteps || elapsed < 180)
                return PerformanceBenchStopReasonType.None;
            if (phase == PerformanceBenchPhaseType.Warmup) return PerformanceBenchStopReasonType.WarmupDeadline;
            if (phase == PerformanceBenchPhaseType.Measuring) return PerformanceBenchStopReasonType.MeasurementDeadline;
            return PerformanceBenchStopReasonType.None;
        }
        internal bool ObserveFrame(PerformanceBenchFrame frame)
        {
            if (frame.phase != (int)PerformanceBenchPhaseType.Measuring || frame.phaseBoundary)
            { _slowFrames = 0; return false; }
            _slowFrames = frame.wallMs > 1000 ? _slowFrames + 1 : 0;
            return _slowFrames >= 3;
        }
    }
}
#endif
