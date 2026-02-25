using UnityEngine;

/// <summary>
/// Transitional StrategyCombat idle route executor.
/// Owns the idle per-unit policy-driven dispatch body during C04A.
/// </summary>
public sealed class StrategyCombatIdleExecutionRoute
{
    readonly IdleTargetProvider _idleTargetProvider;
    readonly StrategyCombatRouteExecutionProfile _profile;
    bool _warnedMissingRoleIdle;
    bool _warnedNoIdleMap;
    DomainTargetProviderValidationWarningState _targetProviderValidationWarnings;

    public StrategyCombatIdleExecutionRoute(IdleTargetProvider idleTargetProvider, StrategyCombatRouteExecutionPolicyAsset policy = null)
    {
        _idleTargetProvider = idleTargetProvider;
        _profile = new StrategyCombatRouteExecutionProfile(policy);
    }

    public void Execute(IExecutionRouteHost host, ArbiterDecision decision, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        if (host == null)
            return;

        DomainTargetProviderValidationFailure targetProviderValidation =
            DomainTargetProviderValidation.Validate(_idleTargetProvider, OrchestrationDomainId.Idle);
        if (targetProviderValidation != DomainTargetProviderValidationFailure.None)
        {
            EmitIdleHoldAllWithMissingTargetProvider(host, world);
            DomainTargetProviderValidation.LogFailureOnce(
                ref _targetProviderValidationWarnings,
                targetProviderValidation,
                _idleTargetProvider,
                OrchestrationDomainId.Idle,
                "StrategyCombatIdleExecutionRoute",
                "[StrategyCombatIdleExecutionRoute] Missing IdleTargetProvider. " +
                "Legacy self-position fallback path was removed. Assign IdleTargetProvider explicitly on IdleDomainComponent.",
                null);

            if (decision.ModeChanged && _profile.Idle.EmitCombatHoldOnModeChange)
                PublishCombatCommandForAll(host, CombatCommand.Create(
                    CombatCommandType.Hold,
                    debugLabel: ResolveDebugLabel(
                        _profile.Idle.CombatHoldDebugLabelOverride,
                        "Router=IdleActive")), world);
            return;
        }

        EmitIdlePerUnit(host, world, ctx);
        if (decision.ModeChanged && _profile.Idle.EmitCombatHoldOnModeChange)
            PublishCombatCommandForAll(host, CombatCommand.Create(
                CombatCommandType.Hold,
                debugLabel: ResolveDebugLabel(
                    _profile.Idle.CombatHoldDebugLabelOverride,
                    "Router=IdleActive")), world);
    }

    static string ResolveDebugLabel(string overrideLabel, string fallback)
    {
        return string.IsNullOrEmpty(overrideLabel) ? fallback : overrideLabel;
    }

    void PublishCombatCommandForAll(IExecutionRouteHost host, CombatCommand cmd, OrchestrationWorldCache world)
    {
        int count = world.CombatReceiverCount;
        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetCombatReceiverEntityId(i);
            if (eid.IsNone)
                continue;

            host.PublishCommand(new DispatchCombatCommand
            {
                ReceiverEntityId = eid,
                Payload = cmd,
                ReceiverRoleId = world.GetCombatReceiverRoleId(i)
            });
        }
    }

    void EmitIdlePerUnit(IExecutionRouteHost host, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        int count = world.IdleReceiverCount;
        ctx.TryGetBinding<IdleRolePolicyMapAsset>(out IdleRolePolicyMapAsset idleRolePolicyMap);

        if (idleRolePolicyMap == null)
        {
            if (!_warnedNoIdleMap && _profile.Idle.WarnWhenRolePolicyMapMissing)
            {
                _warnedNoIdleMap = true;
                Debug.LogWarning("[ExecutionRouter] Idle domain active but no idle role policy map bound. All idlers will Hold.");
            }

            IdleCommand holdCmd = IdleCommand.Hold();
            for (int i = 0; i < count; i++)
            {
                EntityId eid = world.GetIdleReceiverEntityId(i);
                if (eid.IsNone)
                    continue;

                host.PublishCommand(new DispatchIdleCommand
                {
                    ReceiverEntityId = eid,
                    Payload = holdCmd,
                    ReceiverRoleId = world.GetIdleReceiverRoleId(i)
                });
            }
            return;
        }

        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetIdleReceiverEntityId(i);
            if (eid.IsNone)
                continue;

            RoleId roleId = world.GetIdleReceiverRoleId(i);
            int entitySeed = eid.ToStableInt();

            IdlePolicyAsset policy;
            IdleCommand cmd;

            if (!roleId.IsNone && idleRolePolicyMap.TryGet(roleId, out policy) && policy != null)
            {
                // C06: Capability gate — skip policy if actor lacks required capabilities
                IActorCapabilityQuery capQuery = world as IActorCapabilityQuery;
                if (policy is IActorCapabilityGatedPolicy gated && capQuery != null)
                {
                    ActorCapabilitySnapshot actorCaps;
                    capQuery.TryGetActorCapabilities(eid, out actorCaps);
                    if (!ActorCapabilityPolicyGate.CanApply(gated, actorCaps))
                    {
                        cmd = IdleCommand.Hold();
                        if (ctx.DebugLog)
                            cmd.DebugLabel = "Router=CapabilityGated";
                        goto publishIdle;
                    }
                }

                int roleSeed = roleId.ToStableInt();

                Float2 selfPos = _idleTargetProvider.ResolveSelfPosition(eid, world, ctx);

                string dbg;
                cmd = policy.ChooseCommand(selfPos, eid, ctx.Anchor, ctx.Now, roleSeed, entitySeed, world, out dbg);

                if (ctx.DebugLog)
                {
                    cmd.DebugLabel = string.Concat("Idle=", policy.Id, ":", roleId.ToString());
                    if (dbg != null && _profile.Idle.LogPolicyDecisionTraceWhenDebugLog)
                        Debug.Log(string.Concat("[Router] role=", roleId.ToString(), " policy=", policy.Id, " dbg=", dbg));
                }
            }
            else
            {
                cmd = IdleCommand.Hold();
                if (ctx.DebugLog)
                    cmd.DebugLabel = ResolveDebugLabel(
                        _profile.Idle.NoRoleMatchDebugLabelOverride,
                        "Router=NoRoleMatch");

                if (roleId.IsNone && !_warnedMissingRoleIdle && _profile.Idle.WarnWhenRoleMissing)
                {
                    _warnedMissingRoleIdle = true;
                    Debug.LogWarning("[ExecutionRouter] Unit missing RoleId; idle will Hold.");
                }

                if (ctx.DebugLog && _profile.Idle.LogNoRoleMatchTraceWhenDebugLog)
                    Debug.Log(string.Concat("[Router] No role match for '", roleId.ToString(), "'"));
            }

            publishIdle:
            host.PublishCommand(new DispatchIdleCommand
            {
                ReceiverEntityId = eid,
                Payload = cmd,
                ReceiverRoleId = roleId
            });
        }
    }

    void EmitIdleHoldAllWithMissingTargetProvider(IExecutionRouteHost host, OrchestrationWorldCache world)
    {
        IdleCommand holdCmd = IdleCommand.Hold();
        int count = world.IdleReceiverCount;
        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetIdleReceiverEntityId(i);
            if (eid.IsNone)
                continue;

            host.PublishCommand(new DispatchIdleCommand
            {
                ReceiverEntityId = eid,
                Payload = holdCmd,
                ReceiverRoleId = world.GetIdleReceiverRoleId(i)
            });
        }
    }
}
