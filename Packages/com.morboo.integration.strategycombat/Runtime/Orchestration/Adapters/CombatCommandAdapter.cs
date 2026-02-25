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

    [Tooltip("OrchestrationLoop component. Typed dependency; no runtime GetComponent fallback.")]
    [SerializeField] OrchestrationLoop orchestrationLoopComponent;

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
        _loop = orchestrationLoopComponent;

        if (_loop != null)
        {
            _loop.CommandBus.Subscribe<DispatchCombatCommand>(HandleCommand);
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
            _loop.CommandBus.Unsubscribe<DispatchCombatCommand>();
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
        ctx.TryGetBinding(out CombatRolePolicyMapAsset combatRolePolicyMap);
        ctx.TryGetBinding(out CombatRoleConstraintsMapAsset combatRoleConstraintsMap);

        // ── Per-role targeting policy injection ──────────────────────
        if (combatRolePolicyMap != null && !cmd.ReceiverRoleId.IsNone)
        {
            CombatTargetingPolicyAsset policyAsset;
            if (combatRolePolicyMap.TryGet(cmd.ReceiverRoleId, out policyAsset))
            {
                // C06: Capability gate — skip policy injection if actor lacks required capabilities
                if (policyAsset is IActorCapabilityGatedPolicy gated)
                {
                    IActorCapabilityQuery capQuery = world as IActorCapabilityQuery;
                    if (capQuery != null)
                    {
                        ActorCapabilitySnapshot actorCaps;
                        capQuery.TryGetActorCapabilities(cmd.ReceiverEntityId, out actorCaps);
                        if (!ActorCapabilityPolicyGate.CanApply(gated, actorCaps))
                            policyAsset = null;
                    }
                }

                // PERF: GetComponentInParent — selector may be on parent GO
                if (policyAsset != null)
                {
                    ICombatTargetPolicySelector selector = t.GetComponentInParent<ICombatTargetPolicySelector>();
                    if (selector != null)
                        selector.SetRuntimeDefaultPolicy(policyAsset);
                }
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
            if (combatRoleConstraintsMap != null && !cmd.ReceiverRoleId.IsNone)
                combatRoleConstraintsMap.TryGet(cmd.ReceiverRoleId, out resolved);
            ccr.SetRuntimeContext(resolved, world);
        }

        r.ApplyCombatCommand(cmd.Payload);
    }
}
