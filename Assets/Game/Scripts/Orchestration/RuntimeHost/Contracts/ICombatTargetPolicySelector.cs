/// <summary>
/// Execution Contract: command bus → Integration adapter.
/// IMPORTANT: Sole authorized channel for injecting combat targeting policy at runtime.
/// Implemented by game-specific selectors in Integration.Game; consumed by
/// <see cref="CombatCommandAdapter"/> during command dispatch.
/// </summary>
public interface ICombatTargetPolicySelector
{
    void SetRuntimeDefaultPolicy(CombatTargetingPolicyAsset policy);
}
