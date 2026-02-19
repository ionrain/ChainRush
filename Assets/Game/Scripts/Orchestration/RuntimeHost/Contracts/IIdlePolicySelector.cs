/// <summary>
/// Execution Contract: command bus → Integration adapter.
/// IMPORTANT: Sole authorized channel for injecting idle policy at runtime.
/// Implemented by game-specific selectors in Integration.Game; consumed by
/// <see cref="IdleCommandAdapter"/> during command dispatch.
/// </summary>
public interface IIdlePolicySelector
{
    void SetRuntimeDefaultPolicy(IdlePolicyAsset policy);
    IdlePolicyAsset ResolvePolicy();
}
