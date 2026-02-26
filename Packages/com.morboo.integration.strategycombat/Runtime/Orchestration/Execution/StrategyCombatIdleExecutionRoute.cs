using UnityEngine;

/// <summary>
/// StrategyCombat idle route executor.
/// Builds per-operator idle intent and emits unified <see cref="DispatchOrchestrationCommand"/>.
/// </summary>
public sealed class StrategyCombatIdleExecutionRoute
{
    readonly IDomainTargetProvider _idleTargetProvider;
    readonly StrategyCombatRouteExecutionProfile _profile;
    bool _warnedMissingRoleIdle;
    bool _warnedNoIdleMap;
    DomainTargetProviderValidationWarningState _targetProviderValidationWarnings;

    public StrategyCombatIdleExecutionRoute(IDomainTargetProvider idleTargetProvider, StrategyCombatRouteExecutionPolicyAsset policy = null)
    {
        _idleTargetProvider = idleTargetProvider;
        _profile = new StrategyCombatRouteExecutionProfile(policy);
    }

    public void Execute(IExecutionRouteHost host, ArbiterDecision decision, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        if (host == null)
            return;

        IDomainOperatorPositionProvider operatorPositionProvider;
        DomainTargetProviderValidationFailure targetProviderValidation =
            DomainTargetProviderValidation.Validate<IDomainOperatorPositionProvider>(
                _idleTargetProvider,
                OrchestrationDomainId.Idle,
                out operatorPositionProvider);
        if (targetProviderValidation != DomainTargetProviderValidationFailure.None)
        {
            PublishCancelForAll(host, world, "Router=IdleMissingTargetProvider");
            DomainTargetProviderValidation.LogFailureOnce(
                ref _targetProviderValidationWarnings,
                targetProviderValidation,
                _idleTargetProvider,
                OrchestrationDomainId.Idle,
                "StrategyCombatIdleExecutionRoute",
                "[StrategyCombatIdleExecutionRoute] Missing IdleTargetProvider. " +
                "Legacy self-position fallback path was removed. Assign IdleTargetProvider explicitly on IdleDomainComponent.",
                null,
                requiredCapabilityLabel: nameof(IDomainOperatorPositionProvider));
            return;
        }

        EmitIdlePerOperator(
            host,
            world,
            ctx,
            operatorPositionProvider,
            emitCancelForNoRoleMatch: decision.ModeChanged && _profile.Idle.EmitCombatHoldOnModeChange,
            noRoleMatchCancelLabel: ResolveDebugLabel(
                _profile.Idle.CombatHoldDebugLabelOverride,
                "Router=IdleActive"));
    }

    static string ResolveDebugLabel(string overrideLabel, string fallback)
    {
        return string.IsNullOrEmpty(overrideLabel) ? fallback : overrideLabel;
    }

    static void PublishCancelForAll(IExecutionRouteHost host, OrchestrationWorldCache world, string debugLabel)
    {
        OrchestrationCommand cancel = OrchestrationCommand.Cancel(debugLabel);
        int count = world.OperatorCount;
        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetOperatorEntityId(i);
            if (eid.IsNone)
                continue;

            host.PublishCommand(new DispatchOrchestrationCommand
            {
                ReceiverEntityId = eid,
                Payload = cancel
            });
        }
    }

    void EmitIdlePerOperator(
        IExecutionRouteHost host,
        OrchestrationWorldCache world,
        ExecutionContext ctx,
        IDomainOperatorPositionProvider operatorPositionProvider,
        bool emitCancelForNoRoleMatch,
        string noRoleMatchCancelLabel)
    {
        int count = world.OperatorCount;
        ctx.TryGetBinding(out IdleRolePolicyMapAsset idleRolePolicyMap);

        if (idleRolePolicyMap == null)
        {
            if (!_warnedNoIdleMap && _profile.Idle.WarnWhenRolePolicyMapMissing)
            {
                _warnedNoIdleMap = true;
                Debug.LogWarning("[ExecutionRouter] Idle domain active but no idle role policy map bound. Operators will Cancel.");
            }

            PublishCancelForAll(host, world, "Router=IdleNoMap");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetOperatorEntityId(i);
            if (eid.IsNone)
                continue;

            RoleId roleId = world.GetOperatorRoleId(i);
            int entitySeed = eid.ToStableInt();

            OrchestrationCommand orchCmd;

            IdlePolicyAsset policy;
            if (!roleId.IsNone && idleRolePolicyMap.TryGet(roleId, out policy) && policy != null)
            {
                orchCmd = BuildIdleCommandForPolicy(policy, roleId, eid, entitySeed, world, ctx, operatorPositionProvider);
            }
            else
            {
                if (roleId.IsNone && !_warnedMissingRoleIdle && _profile.Idle.WarnWhenRoleMissing)
                {
                    _warnedMissingRoleIdle = true;
                    Debug.LogWarning("[ExecutionRouter] Unit missing RoleId; idle command cannot be resolved.");
                }

                if (ctx.DebugLog && _profile.Idle.LogNoRoleMatchTraceWhenDebugLog)
                    Debug.Log(string.Concat("[Router] No idle role match for '", roleId.ToString(), "'"));

                orchCmd = emitCancelForNoRoleMatch
                    ? OrchestrationCommand.Cancel(noRoleMatchCancelLabel)
                    : OrchestrationCommand.None;
            }

            if (orchCmd.IsNone)
                continue;

            host.PublishCommand(new DispatchOrchestrationCommand
            {
                ReceiverEntityId = eid,
                Payload = orchCmd
            });
        }
    }

    OrchestrationCommand BuildIdleCommandForPolicy(
        IdlePolicyAsset policy,
        RoleId roleId,
        EntityId eid,
        int entitySeed,
        OrchestrationWorldCache world,
        ExecutionContext ctx,
        IDomainOperatorPositionProvider operatorPositionProvider)
    {
        IActorCapabilityQuery capQuery = world as IActorCapabilityQuery;
        if (policy is IActorCapabilityGatedPolicy gated && capQuery != null)
        {
            ActorCapabilitySnapshot actorCaps;
            capQuery.TryGetActorCapabilities(eid, out actorCaps);
            if (!ActorCapabilityPolicyGate.CanApply(gated, actorCaps))
            {
                OrchestrationCommand gatedCmd = OrchestrationCommand.Cancel();
                if (ctx.DebugLog)
                    gatedCmd.DebugLabel = "Router=CapabilityGated";
                return gatedCmd;
            }
        }

        int roleSeed = roleId.ToStableInt();
        Float3 selfPos;
        if (!operatorPositionProvider.TryResolveOperatorPosition(eid, world, ctx, out selfPos))
        {
            OrchestrationCommand failedResolveCmd = OrchestrationCommand.Cancel();
            if (ctx.DebugLog)
                failedResolveCmd.DebugLabel = "Router=IdleTargetResolveFailed";
            return failedResolveCmd;
        }

        string dbg;
        OrchestrationCommand cmd = policy.ChooseCommand(selfPos, eid, ctx.Anchor, ctx.Now, roleSeed, entitySeed, world, out dbg);

        if (ctx.DebugLog)
        {
            cmd.DebugLabel = string.Concat("Idle=", policy.Id, ":", roleId.ToString());
            if (dbg != null && _profile.Idle.LogPolicyDecisionTraceWhenDebugLog)
                Debug.Log(string.Concat("[Router] role=", roleId.ToString(), " policy=", policy.Id, " dbg=", dbg));
        }

        return cmd;
    }
}
