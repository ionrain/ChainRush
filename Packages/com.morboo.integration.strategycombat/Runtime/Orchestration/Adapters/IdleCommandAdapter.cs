using UnityEngine;

/// <summary>
/// Integration adapter: subscribes to <see cref="InProcessCommandBus"/> for
/// <see cref="DispatchIdleCommand"/>, resolves EntityId → receiver, handles
/// per-unit selector overrides, and calls <see cref="IIdleCommandReceiver.ApplyIdleCommand"/>.
/// <para>
/// IMPORTANT — This is the ONLY place ApplyIdleCommand is called at runtime.
/// RuntimeHost emits commands via the bus; this adapter bridges to MonoBehaviours.
/// </para>
/// <para>
/// IMPORTANT — Selector override: The router computes the idle command from the
/// role-map policy. This adapter injects the runtime default on the selector, then
/// checks if the selector provides a different effective policy. If so, the command
/// is recomputed from the effective policy. Per-unit override wins over role-map.
/// </para>
/// </summary>
public sealed class IdleCommandAdapter : MonoBehaviour
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
            _loop.CommandBus.Subscribe<DispatchIdleCommand>(HandleCommand);
        }
        else
        {
            Debug.LogWarning("[IdleCommandAdapter] No OrchestrationLoop found. " +
                             "Adapter will not receive commands.", this);
        }
    }

    void OnDisable()
    {
        if (_loop != null)
            _loop.CommandBus.Unsubscribe<DispatchIdleCommand>();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Command handler — resolves EntityId → receiver, overrides, applies
    // ──────────────────────────────────────────────────────────────────

    void HandleCommand(DispatchIdleCommand cmd)
    {
        Transform t = EntityTransformResolver.Resolve(cmd.ReceiverEntityId);
        if (t == null) return;

        IIdleCommandReceiver r = t.GetComponent<IIdleCommandReceiver>();
        if (r == null) return;

        // Unity-null guard: object can be destroyed mid-tick
        if (r is Object uo && uo == null) return;

        ExecutionContext ctx = _loop.CurrentExecContext;
        OrchestrationWorldCache world = _loop.CurrentWorld;

        IdleCommand finalCmd = cmd.Payload;

        // ── Selector override: per-unit override wins over role-map ──
        // IMPORTANT: The router computed the command from role-map policy.
        // The selector can override to a different policy. If so, recompute.
        if (ctx.IdleRolePolicyMap != null && !cmd.ReceiverRoleId.IsNone)
        {
            IdlePolicyAsset rolePolicy;
            if (ctx.IdleRolePolicyMap.TryGet(cmd.ReceiverRoleId, out rolePolicy) && rolePolicy != null)
            {
                IIdlePolicySelector sel = t.GetComponent<IIdlePolicySelector>();
                if (sel != null)
                {
                    sel.SetRuntimeDefaultPolicy(rolePolicy);
                    IdlePolicyAsset effectivePolicy = sel.ResolvePolicy() ?? rolePolicy;

                    if (effectivePolicy != rolePolicy)
                    {
                        // Recompute command from the effective (overridden) policy
                        Float2 selfPos = ((Vector2)t.position).ToFloat2();
                        int roleSeed = cmd.ReceiverRoleId.ToStableInt();
                        int entitySeed = cmd.ReceiverEntityId.ToStableInt();
                        string dbg;
                        finalCmd = effectivePolicy.ChooseCommand(
                            selfPos, cmd.ReceiverEntityId,
                            ctx.Anchor, ctx.Now,
                            roleSeed, entitySeed,
                            world, out dbg);

                        if (ctx.DebugLog)
                            finalCmd.DebugLabel = string.Concat("Idle=", effectivePolicy.Id,
                                ":", cmd.ReceiverRoleId.ToString(), "(override)");
                    }
                }
            }
        }

        r.ApplyIdleCommand(finalCmd);
    }
}
