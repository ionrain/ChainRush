using UnityEngine;

/// <summary>
/// C06A unified orchestration command adapter.
/// <para>
/// Subscribes to
/// <see cref="DispatchOrchestrationCommand"/> and calls <see cref="IOrchestrationCommandReceiver.ApplyCommand"/>.
/// </para>
/// </summary>
public sealed class OrchestrationCommandAdapter : MonoBehaviour
{
    [Tooltip("OrchestrationLoop component. Typed dependency; no runtime GetComponent fallback.")]
    [SerializeField] OrchestrationLoop orchestrationLoopComponent;

    OrchestrationLoop _loop;
    bool _warnedMissingRole;

    void OnEnable()
    {
        _loop = orchestrationLoopComponent;

        if (_loop != null)
        {
            _loop.CommandBus.Subscribe<DispatchOrchestrationCommand>(HandleCommand);
        }
        else
        {
            Debug.LogWarning("[OrchestrationCommandAdapter] No OrchestrationLoop found. Adapter will not receive commands.", this);
        }
    }

    void OnDisable()
    {
        if (_loop != null)
            _loop.CommandBus.Unsubscribe<DispatchOrchestrationCommand>();
    }

    void HandleCommand(DispatchOrchestrationCommand cmd)
    {
        Transform t = EntityTransformResolver.Resolve(cmd.ReceiverEntityId);
        if (t == null) return;

        IOrchestrationCommandReceiver r = t.GetComponent<IOrchestrationCommandReceiver>();
        if (r == null) return;

        if (r is Object uo && uo == null) return;

        ExecutionContext ctx = _loop.CurrentExecContext;
        OrchestrationWorldCache world = _loop.CurrentWorld;

        RoleId roleId = ResolveRoleId(world, cmd.ReceiverEntityId);

        InjectCombatPolicyIfApplicable(t, cmd.ReceiverEntityId, roleId, cmd.Payload, ctx, world);
        InjectConstraintsIfApplicable(r, roleId, world, ctx);

        OrchestrationCommand finalCmd = RecomputeIdleCommandIfOverridden(t, cmd.ReceiverEntityId, roleId, cmd.Payload, ctx, world);
        if (finalCmd.IsNone)
            return;

        r.ApplyCommand(finalCmd);
    }

    RoleId ResolveRoleId(OrchestrationWorldCache world, EntityId receiverEntityId)
    {
        if (world != null && world.TryGetRoleId(receiverEntityId, out RoleId roleId))
            return roleId;

        if (!_warnedMissingRole)
        {
            _warnedMissingRole = true;
            Debug.LogWarning("[OrchestrationCommandAdapter] Receiver role could not be resolved from world cache.");
        }

        return RoleId.None;
    }

    static bool IsEntityEngage(in OrchestrationCommand cmd)
    {
        return cmd.Type == OrchestrationCommandType.Engage && cmd.Target.Kind == OrchestrationTargetKind.Entity;
    }

    static bool IsSpatialEngage(in OrchestrationCommand cmd)
    {
        if (cmd.Type != OrchestrationCommandType.Engage)
            return false;

        return cmd.Target.Kind == OrchestrationTargetKind.Point
            || cmd.Target.Kind == OrchestrationTargetKind.Area
            || cmd.Target.Kind == OrchestrationTargetKind.Route;
    }

    void InjectCombatPolicyIfApplicable(
        Transform t,
        EntityId receiverEntityId,
        RoleId roleId,
        in OrchestrationCommand cmd,
        in ExecutionContext ctx,
        OrchestrationWorldCache world)
    {
        if (!IsEntityEngage(cmd))
            return;

        if (roleId.IsNone)
            return;

        ctx.TryGetBinding(out CombatRolePolicyMapAsset combatRolePolicyMap);
        if (combatRolePolicyMap == null)
            return;

        CombatTargetingPolicyAsset policyAsset;
        if (!combatRolePolicyMap.TryGet(roleId, out policyAsset) || policyAsset == null)
            return;

        if (policyAsset is IActorCapabilityGatedPolicy gated)
        {
            IActorCapabilityQuery capQuery = world as IActorCapabilityQuery;
            if (capQuery != null)
            {
                ActorCapabilitySnapshot actorCaps;
                capQuery.TryGetActorCapabilities(receiverEntityId, out actorCaps);
                if (!ActorCapabilityPolicyGate.CanApply(gated, actorCaps))
                    policyAsset = null;
            }
        }

        if (policyAsset == null)
            return;

        ICombatTargetPolicySelector selector = t.GetComponentInParent<ICombatTargetPolicySelector>();
        if (selector != null)
            selector.SetRuntimeDefaultPolicy(policyAsset);
    }

    void InjectConstraintsIfApplicable(
        IOrchestrationCommandReceiver receiver,
        RoleId roleId,
        OrchestrationWorldCache world,
        in ExecutionContext ctx)
    {
        IConstrainedCombatReceiver ccr = receiver as IConstrainedCombatReceiver;
        if (ccr == null)
            return;

        ctx.TryGetBinding(out CombatRoleConstraintsMapAsset combatRoleConstraintsMap);

        CombatMoveConstraintsAsset resolved = null;
        if (combatRoleConstraintsMap != null && !roleId.IsNone)
            combatRoleConstraintsMap.TryGet(roleId, out resolved);

        ccr.SetRuntimeContext(resolved, world);
    }

    OrchestrationCommand RecomputeIdleCommandIfOverridden(
        Transform t,
        EntityId receiverEntityId,
        RoleId roleId,
        in OrchestrationCommand cmd,
        in ExecutionContext ctx,
        OrchestrationWorldCache world)
    {
        if (!IsSpatialEngage(cmd))
            return cmd;

        if (roleId.IsNone)
            return cmd;

        ctx.TryGetBinding(out IdleRolePolicyMapAsset idleRolePolicyMap);
        if (idleRolePolicyMap == null)
            return cmd;

        IdlePolicyAsset rolePolicy;
        if (!idleRolePolicyMap.TryGet(roleId, out rolePolicy) || rolePolicy == null)
            return cmd;

        IIdlePolicySelector sel = t.GetComponent<IIdlePolicySelector>();
        if (sel == null)
            return cmd;

        sel.SetRuntimeDefaultPolicy(rolePolicy);
        IdlePolicyAsset effectivePolicy = sel.ResolvePolicy() ?? rolePolicy;
        if (effectivePolicy == rolePolicy)
            return cmd;

        Float2 selfPos = ((Vector2)t.position).ToFloat2();
        int roleSeed = roleId.ToStableInt();
        int entitySeed = receiverEntityId.ToStableInt();
        string dbg;
        OrchestrationCommand idleCmd = effectivePolicy.ChooseCommand(
            selfPos,
            receiverEntityId,
            ctx.Anchor,
            ctx.Now,
            roleSeed,
            entitySeed,
            world,
            out dbg);

        if (ctx.DebugLog)
            idleCmd.DebugLabel = string.Concat("Idle=", effectivePolicy.Id, ":", roleId.ToString(), "(override)");

        return idleCmd;
    }
}
