using UnityEngine;

/// <summary>
/// Command emitter for orchestration decisions.
/// Consumes <see cref="ArbiterDecision"/> and emits <see cref="DispatchCombatCommand"/>
/// / <see cref="DispatchIdleCommand"/> into the <see cref="InProcessCommandBus"/>.
/// <para>
/// IMPORTANT — The router does NOT call ApplyCombatCommand / ApplyIdleCommand.
/// Integration adapters (<see cref="CombatCommandAdapter"/>, <see cref="IdleCommandAdapter"/>)
/// subscribe to the bus, resolve EntityId → receiver, inject policies, and call Apply.
/// </para>
/// <para>
/// IMPORTANT — Iterates receiver identity snapshots from <see cref="OrchestrationWorldCache"/>
/// (EntityId + RoleId), not MonoBehaviour receiver lists. Zero direct receiver access.
/// </para>
/// </summary>
public sealed class ExecutionRouter
{
    readonly InProcessCommandBus _bus;

    // ──────────────────────────────────────────────────────────────────
    //  One-shot warning flags
    // ──────────────────────────────────────────────────────────────────

    bool _warnedMissingRoleIdle;
    bool _warnedNoIdleMap;

    // ──────────────────────────────────────────────────────────────────
    //  Constructor
    // ──────────────────────────────────────────────────────────────────

    public ExecutionRouter(InProcessCommandBus bus)
    {
        _bus = bus;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Emits dispatch commands into the bus based on the arbiter's decision.
    /// IMPORTANT: Does NOT call Apply on receivers. Bus subscribers handle that.
    /// </summary>
    public ExecutionResult Execute(
        ArbiterDecision decision,
        OrchestrationWorldCache world,
        ExecutionContext ctx)
    {
        switch (decision.DomainKey)
        {
            case OrchestrationDomainKeys.Combat:
                EmitCombat(ctx.CombatCommand, world);
                if (decision.ModeChanged)
                    EmitIdleHoldAll(world);
                break;

            case OrchestrationDomainKeys.Idle:
                EmitIdlePerUnit(world, ctx);
                if (decision.ModeChanged)
                    EmitCombatHoldAll(world);
                break;

            default: // None
                if (decision.ModeChanged)
                {
                    EmitCombatHoldAll(world);
                    EmitIdleHoldAll(world);
                }
                break;
        }

        if (ctx.DebugLog)
        {
            // Golden test debug output
            string firstRxIds = BuildFirstReceiverIds(world, 3);
            string firstCrowdIds = BuildFirstCrowdIds(world, 3);
            Debug.Log(string.Concat(
                "[Router] domain=", decision.DomainKey.ToString(),
                " combatRx=", world.CombatReceiverCount.ToString(),
                " idleRx=", world.IdleReceiverCount.ToString(),
                " firstRxIds=", firstRxIds,
                " firstCrowdIds=", firstCrowdIds));
        }

        return new ExecutionResult { EventCount = 0 };
    }

    // ──────────────────────────────────────────────────────────────────
    //  Combat emission — iterates receiver identity snapshots
    // ──────────────────────────────────────────────────────────────────

    void EmitCombat(CombatCommand cmd, OrchestrationWorldCache world)
    {
        int count = world.CombatReceiverCount;
        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetCombatReceiverEntityId(i);
            if (eid.IsNone) continue;

            _bus.PublishCombat(new DispatchCombatCommand
            {
                ReceiverEntityId = eid,
                Payload = cmd,
                ReceiverRoleId = world.GetCombatReceiverRoleId(i)
            });
        }
    }

    void EmitCombatHoldAll(OrchestrationWorldCache world)
    {
        CombatCommand hold = CombatCommand.Create(CombatCommandType.Hold,
            debugLabel: "Router=IdleActive");
        EmitCombat(hold, world);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Idle emission — Per-role policy, per-unit command
    //  IMPORTANT: Policy is looked up by RoleId from ctx.IdleRolePolicyMap.
    //  Commands are computed per-unit using entitySeed so units of the
    //  same role don't clump. Selector override is handled by Integration adapter.
    // ──────────────────────────────────────────────────────────────────

    void EmitIdlePerUnit(OrchestrationWorldCache world, ExecutionContext ctx)
    {
        int count = world.IdleReceiverCount;

        // Guard: no idle map bound → Hold all with warning
        if (ctx.IdleRolePolicyMap == null)
        {
            if (!_warnedNoIdleMap)
            {
                _warnedNoIdleMap = true;
                Debug.LogWarning("[ExecutionRouter] Idle domain active but no idle role policy map bound. " +
                                 "All idlers will Hold.");
            }

            IdleCommand holdCmd = IdleCommand.Hold();
            for (int i = 0; i < count; i++)
            {
                EntityId eid = world.GetIdleReceiverEntityId(i);
                if (eid.IsNone) continue;

                _bus.PublishIdle(new DispatchIdleCommand
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
            if (eid.IsNone) continue;

            RoleId roleId = world.GetIdleReceiverRoleId(i);
            int entitySeed = eid.ToStableInt();

            IdlePolicyAsset policy;
            IdleCommand cmd;

            if (!roleId.IsNone && ctx.IdleRolePolicyMap.TryGet(roleId, out policy) && policy != null)
            {
                // Compute command from role-map policy.
                // IMPORTANT: Selector override is handled by IdleCommandAdapter (Integration).
                int roleSeed = roleId.ToStableInt();

                Float2 selfPos;
                if (!world.TryGetActorPosition(eid, out selfPos))
                    selfPos = ctx.Anchor;

                string dbg;
                cmd = policy.ChooseCommand(selfPos, eid, ctx.Anchor, ctx.Now, roleSeed, entitySeed, world, out dbg);

                // PERF: Only allocate DebugLabel string when logging is on
                if (ctx.DebugLog)
                {
                    cmd.DebugLabel = string.Concat("Idle=", policy.Id, ":", roleId.ToString());
                    if (dbg != null)
                        Debug.Log(string.Concat("[Router] role=", roleId.ToString(), " policy=", policy.Id, " dbg=", dbg));
                }
            }
            else
            {
                // Strict: no role match → Hold. Do NOT fall back to selector defaults.
                cmd = IdleCommand.Hold();
                if (ctx.DebugLog)
                    cmd.DebugLabel = "Router=NoRoleMatch";

                if (roleId.IsNone && !_warnedMissingRoleIdle)
                {
                    _warnedMissingRoleIdle = true;
                    Debug.LogWarning("[ExecutionRouter] Unit missing RoleId; idle will Hold.");
                }

                if (ctx.DebugLog)
                    Debug.Log(string.Concat("[Router] No role match for '", roleId.ToString(), "'"));
            }

            _bus.PublishIdle(new DispatchIdleCommand
            {
                ReceiverEntityId = eid,
                Payload = cmd,
                ReceiverRoleId = roleId
            });
        }
    }

    void EmitIdleHoldAll(OrchestrationWorldCache world)
    {
        IdleCommand hold = IdleCommand.Hold();
        int count = world.IdleReceiverCount;
        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetIdleReceiverEntityId(i);
            if (eid.IsNone) continue;

            _bus.PublishIdle(new DispatchIdleCommand
            {
                ReceiverEntityId = eid,
                Payload = hold,
                ReceiverRoleId = world.GetIdleReceiverRoleId(i)
            });
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Debug helpers — golden test output
    // ──────────────────────────────────────────────────────────────────

    static string BuildFirstReceiverIds(OrchestrationWorldCache world, int max)
    {
        int total = world.CombatReceiverCount;
        if (total == 0) return "-";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(32);
        int count = total < max ? total : max;
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(world.GetCombatReceiverEntityId(i).ToStableInt().ToString());
        }
        return sb.ToString();
    }

    static string BuildFirstCrowdIds(OrchestrationWorldCache world, int max)
    {
        int crowdCount = world.CrowdCount;
        if (crowdCount == 0) return "-";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(32);
        int count = crowdCount < max ? crowdCount : max;
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(world.GetCrowdEntityId(i).ToStableInt().ToString());
        }
        return sb.ToString();
    }
}
