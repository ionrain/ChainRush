using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single tick source and dispatcher that arbitrates between combat and idle domains.
/// Domains extend <see cref="DomainOrchestrator"/> and are polled each tick via
/// <see cref="DomainOrchestrator.Evaluate"/>. The arbiter decides which domain is active (using hysteresis
/// to prevent thrash) and dispatches commands to registry receivers with faction filtering.
/// <para>
/// IMPORTANT — Ownership: The arbiter owns the final dispatch to receivers.
/// Domains must NOT dispatch directly; they only write proposals.
/// </para>
/// <para>
/// IMPORTANT — Per-tick world cache: The arbiter builds an
/// <see cref="OrchestrationWorldCache"/> once per tick from
/// <see cref="OrchestrationRegistry"/>. Domains and dispatch methods iterate
/// cached lists instead of re-querying the registry.
/// </para>
/// <para>
/// IMPORTANT — Per-role idle dispatch: When idle is active, the arbiter looks up
/// the policy for each receiver's <see cref="IRoleContextProvider.GetRoleAsset"/>, then
/// computes a unique command per unit using <see cref="IEntityIdProvider.GetEntityId"/>.
/// This yields per-role policy assignment with per-unit command uniqueness (no clumping).
/// </para>
/// <para>
/// IMPORTANT — Policy maps are owned by domains. The arbiter pulls map references
/// each tick via <see cref="IIdleRolePolicyMapSource"/> / <see cref="ICombatRolePolicyMapSource"/>
/// from cached <see cref="DomainOrchestrator"/> instances. Domains must not mutate their map reference during Evaluate.
/// </para>
/// </summary>
public sealed class OrchestrationArbiter : MonoBehaviour, IArbiter
{
    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Faction
    // ──────────────────────────────────────────────────────────────────

    [Header("Faction")]
    [SerializeField] FactionAsset orchestratorFaction;
    [SerializeField] FactionRelationTableAsset typedRelations;

    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Anchor
    // ──────────────────────────────────────────────────────────────────

    [Header("Anchor")]
    [Tooltip("Idle anchor point (e.g. hero transform). Falls back to this.transform.")]
    [SerializeField] Transform anchorOverride;

    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Domains
    // ──────────────────────────────────────────────────────────────────

    [Header("Domains")]
    [Tooltip("Domain orchestrators polled each tick in array order.")]
    [SerializeField] DomainOrchestrator[] domainOrchestrators;

    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Timing
    // ──────────────────────────────────────────────────────────────────

    [Header("Timing")]
    [SerializeField] float tickInterval = 0.25f;

    [Header("Hysteresis")]
    [Tooltip("Minimum time combat stays active after threat first appears.")]
    [SerializeField] float combatMinActiveTime = 0.8f;
    [Tooltip("Memory window after last threat before switching back to idle.")]
    [SerializeField] float combatCooldownAfterThreat = 0.6f;

    [Header("Debug")]
    [SerializeField] bool debugLog;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Domain cache
    // ──────────────────────────────────────────────────────────────────

    DomainOrchestrator[] _cachedDomains;
    int _domainCount;
    bool _warnedInvalidDomains;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — World cache (single reused instance)
    // ──────────────────────────────────────────────────────────────────

    readonly OrchestrationWorldCache _world = new OrchestrationWorldCache();

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Proposals (single reused instance)
    // ──────────────────────────────────────────────────────────────────

    readonly OrchestrationArbiterProposals _proposals = new OrchestrationArbiterProposals();
    OrchestrationArbiterContext _ctx;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Policy maps (owned by domains, pulled via interfaces)
    //  IMPORTANT: Not serialized. Arbiter reads from domains each tick
    //  via IIdleRolePolicyMapSource / ICombatRolePolicyMapSource.
    // ──────────────────────────────────────────────────────────────────

    IdleRolePolicyMapAsset _idleRolePolicyMap;
    CombatRolePolicyMapAsset _combatRolePolicyMap;
    CombatRoleConstraintsMapAsset _combatRoleConstraintsMap;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Hysteresis
    // ──────────────────────────────────────────────────────────────────

    float _combatLockedUntil;
    float _threatMemoryUntil;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Mode transition tracking
    //  RATIONALE: Only issue cross-domain Hold on mode change to avoid
    //  redundant SetTarget(null) every tick.
    // ──────────────────────────────────────────────────────────────────

    ActiveDomainKind _lastDomain;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Coroutine
    // ──────────────────────────────────────────────────────────────────

    Coroutine _loop;
    WaitForSeconds _wait;
    bool _warnedMissingSetup;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Execution router (single dispatch point)
    // ──────────────────────────────────────────────────────────────────

    readonly ExecutionRouter _router = new ExecutionRouter();

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — One-shot warning flags (separate per category)
    // ──────────────────────────────────────────────────────────────────

    bool _warnedDuplicateIdleSource;
    bool _warnedDuplicateCombatSource;
    bool _warnedDuplicateConstraintsSource;

    // ──────────────────────────────────────────────────────────────────
    //  Public — Domain state query
    // ──────────────────────────────────────────────────────────────────

    public bool IsCombatActive => _lastDomain == ActiveDomainKind.Combat;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _wait = new WaitForSeconds(tickInterval);
        _lastDomain = ActiveDomainKind.None;
        _combatLockedUntil = 0f;
        _threatMemoryUntil = 0f;

        CacheDomains();

        _loop = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;

        // Clear stored maps to avoid stale refs on scene reload / domain toggles
        _idleRolePolicyMap = null;
        _combatRolePolicyMap = null;
        _combatRoleConstraintsMap = null;
    }

    IEnumerator Loop()
    {
        while (true)
        {
            yield return _wait;
            Tick();
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Domain cache — built once on enable
    // ──────────────────────────────────────────────────────────────────

    void CacheDomains()
    {
        _domainCount = 0;

        if (domainOrchestrators == null || domainOrchestrators.Length == 0)
        {
            _cachedDomains = null;
            if (!_warnedInvalidDomains)
            {
                _warnedInvalidDomains = true;
                Debug.LogWarning("[OrchestrationArbiter] domainOrchestrators is empty. " +
                                 "No domains will be polled.", this);
            }
            return;
        }

        if (_cachedDomains == null || _cachedDomains.Length < domainOrchestrators.Length)
            _cachedDomains = new DomainOrchestrator[domainOrchestrators.Length];

        for (int i = 0; i < domainOrchestrators.Length; i++)
        {
            DomainOrchestrator d = domainOrchestrators[i];

            // Unity-null safe: skip destroyed pooled components
            if (d is Object uo && uo == null) { d = null; }
            if (d == null)
            {
                if (!_warnedInvalidDomains)
                {
                    _warnedInvalidDomains = true;
                    Debug.LogWarning(string.Concat(
                        "[OrchestrationArbiter] domainOrchestrators[", i.ToString(),
                        "] is null. Skipping."), this);
                }
                continue;
            }

            _cachedDomains[_domainCount++] = d;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  World cache — built once per tick before domain polling
    //  IMPORTANT: One scan of OrchestrationRegistry per tick. Domains
    //  and dispatch methods iterate cached lists instead.
    // ──────────────────────────────────────────────────────────────────

    void BuildWorldCache(OrchestrationArbiterContext ctx)
    {
        _world.Clear();
        _world.Anchor = ctx.Anchor;
        _world.Now = ctx.Now;

        // ── Actors: alive entities with typed faction ────────────────
        IReadOnlyList<IStateReporter> reporters = OrchestrationRegistry.StateReporters;
        for (int i = 0; i < reporters.Count; i++)
        {
            IStateReporter r = reporters[i];
            if (r == null) continue;
            if (r is Object uo && uo == null) continue;

            IOrchestrationActor actor = r as IOrchestrationActor;
            if (actor == null) continue;
            if (!actor.IsAlive()) continue;

            IFactionAssetProvider fap = actor as IFactionAssetProvider;
            if (fap == null) continue;
            if (fap.GetFactionAsset() == null) continue;

            Transform t = actor.GetTransform();
            if (t == null) continue;

            _world.Actors.Add(actor);
        }

        // ── Friendly combat receivers ────────────────────────────────
        IReadOnlyList<ICombatCommandReceiver> combat = OrchestrationRegistry.CombatReceivers;
        for (int i = 0; i < combat.Count; i++)
        {
            ICombatCommandReceiver c = combat[i];
            if (c == null) continue;
            if (c is Object cObj && cObj == null) continue;

            if (IsFriendlyReceiverTyped(c, ctx.OrchestratorFaction, ctx.Relations))
                _world.FriendlyCombatReceivers.Add(c);
        }

        // ── Friendly idle receivers ──────────────────────────────────
        IReadOnlyList<IIdleCommandReceiver> idle = OrchestrationRegistry.IdleReceivers;
        for (int i = 0; i < idle.Count; i++)
        {
            IIdleCommandReceiver ir = idle[i];
            if (ir == null) continue;
            if (ir is Object iObj && iObj == null) continue;

            if (IsFriendlyReceiverTyped(ir, ctx.OrchestratorFaction, ctx.Relations))
                _world.FriendlyIdleReceivers.Add(ir);
        }

        // ── Build crowd transforms from receivers ────────────────────
        // Crowd = any friendly that receives combat or idle commands (physically present units).
        // IMPORTANT: Replaces GetComponent<Unit>() — no game-type dependency.
        for (int i = 0; i < _world.FriendlyCombatReceivers.Count; i++)
        {
            Component c = _world.FriendlyCombatReceivers[i] as Component;
            if (c != null && _world.CrowdDedup.Add(c.transform))
                _world.FriendlyCrowdTransforms.Add(c.transform);
        }
        for (int i = 0; i < _world.FriendlyIdleReceivers.Count; i++)
        {
            Component c = _world.FriendlyIdleReceivers[i] as Component;
            if (c != null && _world.CrowdDedup.Add(c.transform))
                _world.FriendlyCrowdTransforms.Add(c.transform);
        }

        // ── Build role-by-transform lookup ──────────────────────────
        // IMPORTANT: Policies read role via ctx.World, never via GetComponent.
        for (int i = 0; i < _world.FriendlyIdleReceivers.Count; i++)
        {
            Component c = _world.FriendlyIdleReceivers[i] as Component;
            if (c == null) continue;
            IRoleAssetProvider rp = c.GetComponent<IRoleAssetProvider>();
            if (rp == null) continue;
            RoleAsset role = rp.GetRoleAsset();
            if (role != null)
                _world.RoleByTransform[c.transform] = role;
        }
        for (int i = 0; i < _world.FriendlyCombatReceivers.Count; i++)
        {
            Component c = _world.FriendlyCombatReceivers[i] as Component;
            if (c == null) continue;
            if (_world.RoleByTransform.ContainsKey(c.transform)) continue;
            IRoleAssetProvider rp = c.GetComponent<IRoleAssetProvider>();
            if (rp == null) continue;
            RoleAsset role = rp.GetRoleAsset();
            if (role != null)
                _world.RoleByTransform[c.transform] = role;
        }

        // ── Resolve idle bounds per role ────────────────────────────
        // IMPORTANT: Single point of registry access for bounds. Domains read ctx.World only.
        IdleBoundsRegistry.FillResolvedBounds(_world.ResolvedIdleBounds);

        // ── Resolve combat target set ───────────────────────────────
        // IMPORTANT: Single point of registry access for target set. Domain uses ctx.World only.
        CombatTargetSet resolvedTs;
        OrchestrationRegistry.TryGetCombatTargetSet(ctx.OrchestratorFaction, out resolvedTs);
        _world.ResolvedCombatTargetSet = resolvedTs;

        // ── Snapshot IWorldQuery data and freeze ─────────────────────
        _world.SnapshotActors();
        _world.SnapshotCrowd();
        _world.BuildRoleByEntityId();
        _world.Freeze();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Faction helper — static, typed-only
    // ──────────────────────────────────────────────────────────────────

    static bool IsFriendlyReceiverTyped(object receiver, FactionAsset orchestratorFaction, FactionRelationTableAsset relations)
    {
        if (orchestratorFaction == null || relations == null) return false;

        IFactionAssetProvider fap = receiver as IFactionAssetProvider;
        if (fap == null) return false;

        FactionAsset rf = fap.GetFactionAsset();
        if (rf == null) return false;

        return relations.GetRelation(orchestratorFaction, rf) == FactionRelation.Friendly;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Tick — Build cache + Poll domains + Arbitration + Dispatch
    // ──────────────────────────────────────────────────────────────────

    void Tick()
    {
        // ── Validate setup ──────────────────────────────────────────
        if (orchestratorFaction == null || typedRelations == null)
        {
            if (!_warnedMissingSetup)
            {
                _warnedMissingSetup = true;
                Debug.LogWarning("[OrchestrationArbiter] Missing orchestratorFaction or typedRelations.", this);
            }

            // Hold everything without polling domains
            if (_lastDomain != ActiveDomainKind.None)
            {
                ArbiterDecision holdAll = new ArbiterDecision
                {
                    ActiveDomain = ActiveDomainKind.None,
                    ModeChanged = true
                };
                _router.Execute(holdAll, _world, default);
                _lastDomain = ActiveDomainKind.None;
            }
            return;
        }

        float now = Time.time;

        // ── Fill context ────────────────────────────────────────────
        _ctx.OrchestratorFaction = orchestratorFaction;
        _ctx.Relations = typedRelations;
        _ctx.Anchor = anchorOverride != null
            ? (Vector2)anchorOverride.position
            : (Vector2)transform.position;
        _ctx.Now = now;
        _ctx.DebugLog = debugLog;
        _ctx.World = _world;

        // ── Build world cache (one registry scan per tick) ──────────
        BuildWorldCache(_ctx);

        // ── Clear and poll domains ──────────────────────────────────
        _proposals.Clear();

        for (int i = 0; i < _domainCount; i++)
            _cachedDomains[i].Evaluate(_ctx, _proposals);

        // ── Pull policy maps from domains (after Evaluate, before dispatch) ──
        RefreshPolicyMapsFromDomains();

        // ── Arbitrate: pure decision ─────────────────────────────────
        ArbiterDecision decision = Arbitrate(_proposals, now);

        // ── Build execution context ──────────────────────────────────
        ExecutionContext execCtx = new ExecutionContext
        {
            IdleRolePolicyMap = _idleRolePolicyMap,
            CombatRolePolicyMap = _combatRolePolicyMap,
            CombatRoleConstraintsMap = _combatRoleConstraintsMap,
            OrchestratorFaction = orchestratorFaction,
            Relations = typedRelations,
            Anchor = _ctx.Anchor,
            Now = now,
            DebugLog = debugLog
        };

        // ── Execute: router dispatches commands to receivers ─────────
        _router.Execute(decision, _world, execCtx);
        _lastDomain = decision.ActiveDomain;

        if (debugLog)
        {
            Debug.Log(string.Concat(
                "[Arbiter] mode=", decision.ActiveDomain.ToString(),
                " modeChanged=", decision.ModeChanged ? "1" : "0",
                " threat=", _proposals.ThreatPresent ? "1" : "0",
                " combatProp=", _proposals.HasCombat ? "1" : "0",
                " idleProp=", _proposals.HasIdle ? "1" : "0",
                " actors=", _world.ActorCount.ToString(),
                " lock=", (_combatLockedUntil - now).ToString("F1"),
                "s mem=", (_threatMemoryUntil - now).ToString("F1"), "s"), this);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  IArbiter — Pure arbitration: proposals + time → decision
    //  IMPORTANT: No side effects, no dispatch, no registry reads.
    //  Hysteresis timers are the only mutable state touched.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Selects the active domain based on proposals, hysteresis timers, and current time.
    /// Pure function aside from updating hysteresis timers.
    /// </summary>
    public ArbiterDecision Arbitrate(OrchestrationArbiterProposals proposals, float now)
    {
        // ── Update hysteresis timers ────────────────────────────────
        if (proposals.ThreatPresent)
        {
            _combatLockedUntil = Mathf.Max(_combatLockedUntil, now + combatMinActiveTime);
            _threatMemoryUntil = now + combatCooldownAfterThreat;
        }

        bool combatSticky = now <= _combatLockedUntil || now <= _threatMemoryUntil;
        bool combatActive = proposals.HasCombat && (proposals.ThreatPresent || combatSticky);

        // ── Select domain ────────────────────────────────────────────
        ActiveDomainKind selected;
        CombatCommand combatCmd = default;

        if (combatActive)
        {
            selected = ActiveDomainKind.Combat;
            combatCmd = proposals.CombatCommand;
        }
        else if (proposals.HasIdle)
        {
            selected = ActiveDomainKind.Idle;
        }
        else
        {
            selected = ActiveDomainKind.None;
        }

        bool modeChanged = selected != _lastDomain;

        return new ArbiterDecision
        {
            ActiveDomain = selected,
            CombatCommand = combatCmd,
            ModeChanged = modeChanged
        };
    }

    // ──────────────────────────────────────────────────────────────────
    //  Policy map refresh — pull from cached DomainOrchestrators
    //  IMPORTANT: Called once per tick after domain Evaluate, before dispatch.
    //  "Last non-null wins" if multiple domains provide the same map type.
    //  If no source provides a non-null map, the stored field becomes null.
    //  PERF: Two `is` casts per domain per tick (0.25s interval). Negligible.
    // ──────────────────────────────────────────────────────────────────

    void RefreshPolicyMapsFromDomains()
    {
        IdleRolePolicyMapAsset newIdle = null;
        CombatRolePolicyMapAsset newCombat = null;
        CombatRoleConstraintsMapAsset newConstraints = null;
        bool foundIdle = false;
        bool foundCombat = false;
        bool foundConstraints = false;

        for (int i = 0; i < _domainCount; i++)
        {
            DomainOrchestrator d = _cachedDomains[i];
            // Unity-null safe: skip destroyed pooled components
            if (d is Object uo && uo == null) continue;

            if (d is IIdleRolePolicyMapSource idleSrc)
            {
                IdleRolePolicyMapAsset m = idleSrc.GetIdleRolePolicyMap();
                if (m != null)
                {
                    if (foundIdle && !_warnedDuplicateIdleSource)
                    {
                        _warnedDuplicateIdleSource = true;
                        Debug.LogWarning("[OrchestrationArbiter] Multiple domains provide " +
                            "IIdleRolePolicyMapSource. Last non-null wins.", this);
                    }
                    newIdle = m;
                    foundIdle = true;
                }
            }

            if (d is ICombatRolePolicyMapSource combatSrc)
            {
                CombatRolePolicyMapAsset m = combatSrc.GetCombatRolePolicyMap();
                if (m != null)
                {
                    if (foundCombat && !_warnedDuplicateCombatSource)
                    {
                        _warnedDuplicateCombatSource = true;
                        Debug.LogWarning("[OrchestrationArbiter] Multiple domains provide " +
                            "ICombatRolePolicyMapSource. Last non-null wins.", this);
                    }
                    newCombat = m;
                    foundCombat = true;
                }
            }

            if (d is ICombatRoleConstraintsMapSource constraintsSrc)
            {
                CombatRoleConstraintsMapAsset cm = constraintsSrc.GetCombatRoleConstraintsMap();
                if (cm != null)
                {
                    if (foundConstraints && !_warnedDuplicateConstraintsSource)
                    {
                        _warnedDuplicateConstraintsSource = true;
                        Debug.LogWarning("[OrchestrationArbiter] Multiple domains provide " +
                            "ICombatRoleConstraintsMapSource. Last non-null wins.", this);
                    }
                    newConstraints = cm;
                    foundConstraints = true;
                }
            }
        }

        // Null clears: if no source provides a map, stored field becomes null
        _idleRolePolicyMap = newIdle;
        _combatRolePolicyMap = newCombat;
        _combatRoleConstraintsMap = newConstraints;
    }

}
