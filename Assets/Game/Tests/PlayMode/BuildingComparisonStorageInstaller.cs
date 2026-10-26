using Core.GameRuntime;
using Core.Storage;

namespace ChainRush.Tests.PlayMode
{
    // minimum seam: install isolated test storage in the existing Infrastructure phase of an unsaved profile clone.
    // The real startup sequence still owns loading, registration and Economy state binding.
    public sealed class BuildingComparisonStorageInstaller : GameRuntimeInstallerData
    {
        internal IStorageRuntime Runtime { get; set; }
        public override GameRuntimePhase Phase => GameRuntimePhase.Infrastructure;
        public override void Install(GameRuntimeContext context) => StorageService.InstallRuntime(Runtime);
    }
}
