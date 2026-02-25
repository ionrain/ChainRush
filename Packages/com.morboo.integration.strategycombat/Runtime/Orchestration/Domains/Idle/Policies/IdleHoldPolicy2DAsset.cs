using UnityEngine;

/// <summary>
/// Simplest idle policy: always issues orchestration <c>Cancel</c>.
/// Receiver returns to its local default behavior (stand/idle).
/// </summary>
[CreateAssetMenu(fileName = "IdleHoldPolicy", menuName = "Game/Orchestration/Idle/Policies/Hold")]
public sealed class IdleHoldPolicy2DAsset : IdlePolicyAsset
{
    public override OrchestrationCommand ChooseCommand(Transform self, Vector2 anchor, float now, out string debugInfo)
    {
        debugInfo = null;
        return OrchestrationCommand.Cancel();
    }
}
