using UnityEngine;

/// <summary>
/// Integration adapter: subscribes to <see cref="InProcessCommandBus"/> for
/// <see cref="DispatchCombatCommand"/>, resolves EntityId → receiver, injects
/// per-role policies + constraints, and calls <see cref="ICombatCommandReceiver.ApplyCombatCommand"/>.
/// <para>
/// IMPORTANT — This is the ONLY place ApplyCombatCommand is called at runtime.
/// RuntimeHost emits commands via the bus; this adapter bridges to MonoBehaviours.
/// </para>
/// </summary>
public sealed class CombatCommandAdapter : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────
    //  Serialized
    // ──────────────────────────────────────────────────────────────────

    [Tooltip("OrchestrationLoop component (MonoBehaviour). Falls back to GetComponent on this GameObject.")]
    [SerializeField] MonoBehaviour orchestrationLoopComponent;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime
    // ──────────────────────────────────────────────────────────────────

    OrchestrationLoop _loop;
    bool _warnedMissingRoleCombat;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _loop = orchestrationLoopComponent as OrchestrationLoop;
        if (_loop == null)
            _loop = GetComponent<OrchestrationLoop>();

        if (_loop != null)
        {
            _loop.CommandBus.SubscribeCombat(HandleCommand);
        }
        else
        {
            Debug.LogWarning("[CombatCommandAdapter] No OrchestrationLoop found. " +
                             "Adapter will not receive commands.", this);
        }
    }

    void OnDisable()
    {
        if (_loop != null)
            _loop.CommandBus.SubscribeCombat(null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Command handler — resolves EntityId → receiver, injects, applies
    // ──────────────────────────────────────────────────────────────────

    void HandleCommand(DispatchCombatCommand cmd)
    {
        Transform t = EntityTransformResolver.Resolve(cmd.ReceiverEntityId);
        if (t == null) return;

        ICombatCommandReceiver r = t.GetComponent<ICombatCommandReceiver>();
        if (r == null) return;

        // Unity-null guard: object can be destroyed mid-tick
        if (r is Object uo && uo == null) return;

        ExecutionContext ctx = _loop.CurrentExecContext;
        OrchestrationWorldCache world = _loop.CurrentWorld;

        // ── Per-role targeting policy injection ──────────────────────
        if (ctx.CombatRolePolicyMap != null && !cmd.ReceiverRoleId.IsNone)
        {
            CombatTargetingPolicyAsset policyAsset;
            if (ctx.CombatRolePolicyMap.TryGet(cmd.ReceiverRoleId, out policyAsset))
            {
                // PERF: GetComponentInParent — selector may be on parent GO
                ICombatTargetPolicySelector selector = t.GetComponentInParent<ICombatTargetPolicySelector>();
                if (selector != null)
                    selector.SetRuntimeDefaultPolicy(policyAsset);
            }
        }
        else if (cmd.ReceiverRoleId.IsNone && !_warnedMissingRoleCombat)
        {
            _warnedMissingRoleCombat = true;
            Debug.LogWarning("[CombatCommandAdapter] Combat receiver missing RoleId; " +
                             "combatRolePolicyMap will not inject policy.");
        }

        // ── Per-role constraints injection ───────────────────────────
        // IMPORTANT: Always call SetRuntimeContext when receiver implements
        // IConstrainedCombatReceiver — pass null when no map/role match
        // so executor transitions cleanly to unconstrained mode.
        IConstrainedCombatReceiver ccr = r as IConstrainedCombatReceiver;
        if (ccr != null)
        {
            CombatMoveConstraintsAsset resolved = null;
            if (ctx.CombatRoleConstraintsMap != null && !cmd.ReceiverRoleId.IsNone)
                ctx.CombatRoleConstraintsMap.TryGet(cmd.ReceiverRoleId, out resolved);
            ccr.SetRuntimeContext(resolved, world);
        }

        r.ApplyCombatCommand(cmd.Payload);
    }
}
