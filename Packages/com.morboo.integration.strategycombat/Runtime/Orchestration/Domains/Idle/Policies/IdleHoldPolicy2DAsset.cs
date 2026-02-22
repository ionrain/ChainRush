using UnityEngine;

/// <summary>
/// Simplest idle policy: always issues <see cref="IdleCommandType.Hold"/>.
/// Unit clears its target and stands in place.
/// </summary>
[CreateAssetMenu(fileName = "IdleHoldPolicy", menuName = "Game/Orchestration/Idle/Policies/Hold")]
public sealed class IdleHoldPolicy2DAsset : IdlePolicyAsset
{
    public override IdleCommand ChooseCommand(Transform self, Vector2 anchor, float now, out string debugInfo)
    {
        debugInfo = null;
        return IdleCommand.Hold();
    }
}
