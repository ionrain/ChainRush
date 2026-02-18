using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single dispatch point for orchestration commands.
/// Consumes <see cref="ArbiterDecision"/> and routes commands to receivers
/// via the concrete <see cref="OrchestrationWorldCache"/> (needs receiver lists).
/// <para>
/// IMPORTANT — Ownership: The router is the ONLY code that calls
/// <c>ApplyCombatCommand</c> / <c>ApplyIdleCommand</c> on receivers.
/// The arbiter produces decisions; the router executes them.
/// </para>
/// <para>
/// IMPORTANT — Phase 2A: Iterates receiver lists directly (no EntityId→receiver
/// dictionaries). Queries EntityId per receiver for policy/role lookups only.
/// </para>
/// </summary>
public sealed class ExecutionRouter
{
    // ──────────────────────────────────────────────────────────────────
    //  One-shot warning flags (separate per category)
    // ──────────────────────────────────────────────────────────────────

    bool _warnedMissingProviderIdle;
    bool _warnedMissingRoleIdle;
    bool _warnedMissingRoleCombat;
    bool _warnedNoIdleMap;

    // ──────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches commands to receivers based on the arbiter's decision.
    /// Returns <see cref="ExecutionResult"/> (Phase 2B seam — EventCount always 0 for now).
    /// </summary>
    public ExecutionResult Execute(
        ArbiterDecision decision,
        OrchestrationWorldCache world,
        ExecutionContext ctx)
    {
        switch (decision.DomainKey)
        {
            case OrchestrationDomainKeys.Combat:
                DispatchCombat(ctx.CombatCommand, world, ctx);
                if (decision.ModeChanged)
                    DispatchIdleHoldAll(world);
                break;

            case OrchestrationDomainKeys.Idle:
                DispatchIdlePerUnit(world, ctx);
                if (decision.ModeChanged)
                    DispatchCombatHoldAll(world, ctx);
                break;

            default: // None
                if (decision.ModeChanged)
                {
                    DispatchCombatHoldAll(world, ctx);
                    DispatchIdleHoldAll(world);
                }
                break;
        }

        if (ctx.DebugLog)
        {
            // Golden test debug output: detect accidental list ordering, filtering, or
            // crowd membership changes across commits.
            string firstRxIds = BuildFirstIds(world.FriendlyCombatReceivers, 3);
            string firstCrowdIds = BuildFirstCrowdIds(world, 3);
            Debug.Log(string.Concat(
                "[Router] domain=", decision.DomainKey.ToString(),
                " combatRx=", world.FriendlyCombatReceivers.Count.ToString(),
                " idleRx=", world.FriendlyIdleReceivers.Count.ToString(),
                " firstRxIds=", firstRxIds,
                " firstCrowdIds=", firstCrowdIds));
        }

        return new ExecutionResult { EventCount = 0 };
    }

    // ──────────────────────────────────────────────────────────────────
    //  Combat dispatch — iterates cached friendly receivers
    // ──────────────────────────────────────────────────────────────────

    void DispatchCombat(CombatCommand cmd, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        List<ICombatCommandReceiver> receivers = world.FriendlyCombatReceivers;
        for (int i = 0; i < receivers.Count; i++)
        {
            ICombatCommandReceiver r = receivers[i];
            // Unity-null guard: object can be destroyed mid-tick
            if (r is Object uo && uo == null) continue;

            // Per-role targeting policy injection via typed RoleAsset
            if (ctx.CombatRolePolicyMap != null && r is IRoleAssetProvider rap)
            {
                RoleAsset role = rap.GetRoleAsset();
                if (role != null)
                {
                    CombatTargetingPolicyAsset policyAsset;
                    if (ctx.CombatRolePolicyMap.TryGet(role, out policyAsset))
                    {
                        if (r is Component c)
                        {
                            // PERF: GetComponentInParent — selector may be on parent GO
                            ICombatTargetPolicySelector selector = c.GetComponentInParent<ICombatTargetPolicySelector>();
                            if (selector != null)
                                selector.SetRuntimeDefaultPolicy(policyAsset);
                        }
                    }
                }
                else if (!_warnedMissingRoleCombat)
                {
                    _warnedMissingRoleCombat = true;
                    Debug.LogWarning("[ExecutionRouter] Combat receiver missing RoleAsset; " +
                                     "combatRolePolicyMap will not inject policy.");
                }
            }

            // Per-role constraints injection (after policy injection, before ApplyCombatCommand).
            // IMPORTANT: Always call SetRuntimeContext when receiver implements
            // IConstrainedCombatReceiver — pass null constraints when no map/role match
            // so executor transitions cleanly to unconstrained mode.
            if (r is IConstrainedCombatReceiver ccr)
            {
                CombatMoveConstraintsAsset resolved = null;
                if (ctx.CombatRoleConstraintsMap != null && r is IRoleAssetProvider rap2)
                {
                    RoleAsset cRole = rap2.GetRoleAsset();
                    if (cRole != null)
                        ctx.CombatRoleConstraintsMap.TryGet(cRole, out resolved);
                }
                ccr.SetRuntimeContext(resolved, world);
            }

            r.ApplyCombatCommand(cmd);
        }
    }

    void DispatchCombatHoldAll(OrchestrationWorldCache world, ExecutionContext ctx)
    {
        CombatCommand hold = CombatCommand.Create(CombatCommandType.Hold,
            debugLabel: "Router=IdleActive");
        DispatchCombat(hold, world, ctx);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Idle dispatch — Per-role policy, per-unit command
    //  IMPORTANT: Policy is looked up by RoleAsset from ctx.IdleRolePolicyMap
    //  (pulled from domain via IIdleRolePolicyMapSource each tick).
    //  Commands are computed per-unit using entitySeed so units of the
    //  same role don't clump. Iterates cached friendly idle receivers.
    // ──────────────────────────────────────────────────────────────────

    void DispatchIdlePerUnit(OrchestrationWorldCache world, ExecutionContext ctx)
    {
        List<IIdleCommandReceiver> receivers = world.FriendlyIdleReceivers;

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
            for (int i = 0; i < receivers.Count; i++)
            {
                IIdleCommandReceiver r = receivers[i];
                if (r is Object obj && obj == null) continue;
                r.ApplyIdleCommand(holdCmd);
            }
            return;
        }

        for (int i = 0; i < receivers.Count; i++)
        {
            IIdleCommandReceiver r = receivers[i];
            // Unity-null guard: object can be destroyed mid-tick
            if (r is Object obj && obj == null) continue;

            // Resolve typed role and entity seed
            IRoleContextProvider rcp = r as IRoleContextProvider;
            if (rcp == null)
            {
                if (!_warnedMissingProviderIdle)
                {
                    _warnedMissingProviderIdle = true;
                    Debug.LogWarning("[ExecutionRouter] Idle receiver does not implement " +
                                     "IRoleContextProvider; idle will Hold.");
                }
                r.ApplyIdleCommand(IdleCommand.Hold());
                continue;
            }

            RoleAsset role = rcp.GetRoleAsset();
            int entitySeed = rcp.GetEntityId().ToStableInt();

            IdlePolicyAsset policy;
            IdleCommand cmd;

            if (role != null && ctx.IdleRolePolicyMap.TryGet(role, out policy) && policy != null)
            {
                // Inject runtime default on selector; resolve effective policy
                // IMPORTANT: per-unit override on selector wins over role-map
                IdlePolicyAsset effectivePolicy = policy;
                if (r is Component comp)
                {
                    IIdlePolicySelector sel = comp.GetComponent<IIdlePolicySelector>();
                    if (sel != null)
                    {
                        sel.SetRuntimeDefaultPolicy(policy);
                        effectivePolicy = sel.ResolvePolicy() ?? policy;
                    }
                }

                // Stable role seed from asset instance ID
                int roleSeed = role.GetInstanceID();

                // Get receiver position and EntityId for the IWorldQuery-based policy overload
                EntityId selfEntityId = rcp.GetEntityId();
                Vector2 selfPos = (r is Component c) ? (Vector2)c.transform.position : ctx.Anchor;

                string dbg;
                cmd = effectivePolicy.ChooseCommand(selfPos, selfEntityId, ctx.Anchor, ctx.Now, roleSeed, entitySeed, world, out dbg);

                // PERF: Only allocate DebugLabel string when logging is on
                if (ctx.DebugLog)
                {
                    cmd.DebugLabel = string.Concat("Idle=", effectivePolicy.Id, ":", role.Id);
                    if (dbg != null)
                        Debug.Log(string.Concat("[Router] role=", role.Id, " policy=", effectivePolicy.Id, " dbg=", dbg));
                }
            }
            else
            {
                // Strict: no role match → Hold. Do NOT fall back to selector defaults.
                cmd = IdleCommand.Hold();
                if (ctx.DebugLog)
                    cmd.DebugLabel = "Router=NoRoleMatch";

                if (role == null && !_warnedMissingRoleIdle)
                {
                    _warnedMissingRoleIdle = true;
                    Debug.LogWarning("[ExecutionRouter] Unit missing RoleAsset; idle will Hold.");
                }

                if (ctx.DebugLog)
                    Debug.Log(string.Concat("[Router] No role match for '",
                        role != null ? role.Id : "null", "'"));
            }

            r.ApplyIdleCommand(cmd);
        }
    }

    void DispatchIdleHoldAll(OrchestrationWorldCache world)
    {
        IdleCommand hold = IdleCommand.Hold();
        List<IIdleCommandReceiver> receivers = world.FriendlyIdleReceivers;
        for (int i = 0; i < receivers.Count; i++)
        {
            IIdleCommandReceiver r = receivers[i];
            if (r is Object obj && obj == null) continue;
            r.ApplyIdleCommand(hold);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Debug helpers — golden test output
    // ──────────────────────────────────────────────────────────────────

    static string BuildFirstIds(List<ICombatCommandReceiver> receivers, int max)
    {
        if (receivers.Count == 0) return "-";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(32);
        int count = receivers.Count < max ? receivers.Count : max;
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(',');
            IEntityIdProvider idp = receivers[i] as IEntityIdProvider;
            sb.Append(idp != null ? idp.GetEntityId().ToStableInt().ToString() : "?");
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
