using MoreMountains.Tools;
using UnityEngine;

/// <summary>
/// TopDownEngine AIDecision that returns true when an
/// <see cref="EnemyCombatCommandExecutor2D"/> has an active external command
/// (target transform or point) that has not expired.
/// <para>
/// INTEGRATION: Place this decision on a transition that leads to an AIState
/// containing <see cref="AIActionSetBrainTargetFromExternal2D"/> (and then into
/// the enemy's normal "move-to-target" state, e.g. one using AIActionMoveTowardsTarget2D).
/// If the enemy brain does not have a state that moves toward <c>_brain.Target</c>,
/// setting the target alone will have no effect — the enemy will continue its
/// default behavior (e.g. MoveInDirection2D).
/// </para>
/// <para>
/// When this decision returns false (no command or expired TTL), the brain
/// stays in / falls back to its default state, preserving normal enemy AI.
/// </para>
/// </summary>
public class AIDecisionHasExternalCommand : AIDecision
{
    [SerializeField] EnemyCombatCommandExecutor executor;

    public override void Initialization()
    {
        base.Initialization();
        if (executor == null)
            executor = GetComponentInParent<EnemyCombatCommandExecutor>();
    }

    public override bool Decide()
    {
        return executor != null && (executor.HasExternalTarget || executor.HasExternalPoint);
    }
}
