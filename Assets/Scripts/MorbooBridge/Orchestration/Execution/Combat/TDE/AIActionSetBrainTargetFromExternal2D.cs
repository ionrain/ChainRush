using MoreMountains.Tools;
using UnityEngine;

/// <summary>
/// TopDownEngine AIAction that sets <c>_brain.Target</c> from an active external
/// command stored in <see cref="EnemyCombatCommandExecutor2D"/>.
/// <para>
/// INTEGRATION: This action should live in an AIState that is entered when
/// <see cref="AIDecisionHasExternalCommand2D"/> returns true. That state should
/// then transition into the enemy's normal "move-to-target / attack" path
/// (e.g. a state using AIActionMoveTowardsTarget2D) so the engine handles
/// actual movement and combat.
/// </para>
/// <para>
/// IMPORTANT — This action only sets <c>_brain.Target</c> when an external
/// command is active. It never clears <c>_brain.Target</c>; existing enemy AI
/// manages targets normally when no external command is present.
/// </para>
/// <para>
/// Point commands use a lazily-created waypoint child transform positioned at
/// the commanded point. The waypoint is never destroyed (pooling-friendly).
/// </para>
/// </summary>
public class AIActionSetBrainTargetFromExternal2D : AIAction
{
    [SerializeField] EnemyCombatCommandExecutor2D executor;

    public override void Initialization()
    {
        if (!ShouldInitialize) return;
        base.Initialization();
        if (executor == null)
            executor = GetComponentInParent<EnemyCombatCommandExecutor2D>();
    }

    public override void PerformAction()
    {
        if (executor == null)
            return;

        if (executor.HasExternalTarget)
        {
            _brain.Target = executor.ExternalTarget;
        }
        else if (executor.HasExternalPoint)
        {
            _brain.Target = executor.GetOrCreatePointWaypoint();
        }
    }
}
