#if UNITY_EDITOR
using Core.GameRuntime;
using UnityEngine;

namespace ChainRush.Diagnostics
{
    public sealed class PerformanceBenchRuntimeInstallerData : GameRuntimeInstallerData
    {
        public PerformanceBenchAssets assets;
        public override GameRuntimePhase Phase => GameRuntimePhase.Features;
        public override void Install(GameRuntimeContext context)
        {
            var runtime = new PerformanceBenchRuntime(context, assets);
            context.RegisterResource(runtime);
            context.RegisterRetirement(runtime.RetireAsync);
        }
    }
}
#endif
