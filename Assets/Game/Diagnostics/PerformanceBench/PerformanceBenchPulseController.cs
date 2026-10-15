#if UNITY_EDITOR
using Core.Events;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    // Consumer boundary: the scene supplies a pulse; the installed runtime owns its work.
    [DefaultExecutionOrder(10000)]
    public sealed class PerformanceBenchPulseController : MonoBehaviour
    {
        void Update() => EventBus.Trigger(new PerformanceBenchPulseEvent(Time.realtimeSinceStartupAsDouble, Time.frameCount));
    }
}
#endif
