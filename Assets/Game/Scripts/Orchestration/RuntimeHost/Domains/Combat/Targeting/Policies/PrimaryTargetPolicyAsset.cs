using UnityEngine;

/// <summary>
/// Returns the orchestrator-chosen primary target as-is. This is the default behavior
/// when no targeting policy override is needed.
/// </summary>
[CreateAssetMenu(fileName = "PrimaryTargetPolicy", menuName = "Game/Orchestration/Combat/Targeting Policies/Primary Target")]
public sealed class PrimaryTargetPolicyAsset : CombatTargetingPolicyAsset
{
    public override EntityId ChooseTarget(
        EntityId selfEntityId,
        Float2 selfPosition,
        EntityId primaryTarget,
        CombatTargetSet targetSet,
        float now,
        IWorldQueryBase world,
        out string debugInfo)
    {
        debugInfo = "PrimaryTarget";
        return primaryTarget;
    }
}
