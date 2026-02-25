using System.Collections.Generic;

/// <summary>
/// Implemented by any policy asset that requires specific actor capabilities.
/// IMPORTANT: Capability gating is opt-in — policies without this interface pass through.
/// </summary>
public interface IActorCapabilityGatedPolicy
{
    IReadOnlyList<ActorCapability> RequiredCapabilities { get; }
}

/// <summary>
/// Static helper for capability-gated policy checks.
/// PERF: No allocations. Linear scan over required capabilities.
/// </summary>
public static class ActorCapabilityPolicyGate
{
    /// <summary>
    /// Returns true if the actor's capabilities satisfy the policy's requirements.
    /// If the policy has no requirements (null or empty), returns true.
    /// </summary>
    public static bool CanApply(IActorCapabilityGatedPolicy policy, in ActorCapabilitySnapshot actorCaps)
    {
        IReadOnlyList<ActorCapability> required = policy.RequiredCapabilities;
        if (required == null || required.Count == 0) return true;

        for (int i = 0; i < required.Count; i++)
        {
            if (required[i] != null && !actorCaps.Has(required[i]))
                return false;
        }
        return true;
    }
}
