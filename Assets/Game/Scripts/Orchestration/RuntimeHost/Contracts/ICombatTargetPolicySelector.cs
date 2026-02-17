/// <summary>
/// Execution Contract: arbiter dispatch → integration.
/// IMPORTANT: Sole authorized channel for injecting combat targeting policy at runtime.
/// Implemented by game-specific selectors in Integration.Game; consumed by arbiter per-role dispatch.
/// TODO Phase 2: evaluate moving to Core/Contracts/Execution.
/// </summary>
public interface ICombatTargetPolicySelector
{
    void SetRuntimeDefaultPolicy(CombatTargetingPolicyAsset policy);
}
