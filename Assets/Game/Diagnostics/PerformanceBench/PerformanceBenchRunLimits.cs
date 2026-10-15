#if UNITY_EDITOR
namespace ChainRush.Diagnostics
{
    // working model: stop admission at safety limits; the runtime still drains accepted work.
    internal sealed class PerformanceBenchRunLimits
    {
        int _slowFrames;
        internal void Reset() => _slowFrames = 0;
        internal bool PreparationExpired(double elapsed) => elapsed >= 180;
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
