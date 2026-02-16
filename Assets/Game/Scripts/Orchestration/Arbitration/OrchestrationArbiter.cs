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
/// computes a unique command per unit using <see cref="IRoleContextProvider.GetEntitySeed"/>.
/// This yields per-role policy assignment with per-unit command uniqueness (no clumping).
/// </para>
/// <para>
/// IMPORTANT — Policy maps are owned by domains. The arbiter pulls map references
/// each tick via <see cref="IIdleRolePolicyMapSource"/> / <see cref="ICombatRolePolicyMapSource"/>
/// from cached <see cref="DomainOrchestrator"/> instances. Domains must not mutate their map reference during Evaluate.
/// </para>
/// </summary>
public sealed class OrchestrationArbiter : MonoBehaviour
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

    enum DispatchMode { None, Combat, Idle }
    DispatchMode _lastMode;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Coroutine
    // ──────────────────────────────────────────────────────────────────

    Coroutine _loop;
    WaitForSeconds _wait;
    bool _warnedMissingSetup;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — One-shot warning flags (separate per category)
    // ──────────────────────────────────────────────────────────────────

    bool _warnedMissingProviderIdle;
    bool _warnedMissingRoleIdle;
    bool _warnedMissingRoleCombat;
    bool _warnedNoIdleMap;
    bool _warnedDuplicateIdleSource;
    bool _warnedDuplicateCombatSource;

    // ──────────────────────────────────────────────────────────────────
    //  Public — Domain state query
    // ──────────────────────────────────────────────────────────────────

    public bool IsCombatActive => _lastMode == DispatchMode.Combat;

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _wait = new WaitForSeconds(tickInterval);
        _lastMode = DispatchMode.None;
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
    //  Obsolete — Legacy proposal submission (kept for compilation)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Legacy submission path. No longer used in the arbiter-driven flow.
    /// Domains now implement <see cref="IOrchestrationDomain"/> and are polled.
    /// </summary>
    [System.Obsolete("Use IOrchestrationDomain.Evaluate instead. Arbiter now polls domains directly.")]
    public void SubmitCombat(in CombatCommand cmd, bool threatPresent)
    {
        _proposals.SetCombat(cmd, threatPresent);
    }

    /// <summary>
    /// Legacy submission path. No longer used in the arbiter-driven flow.
    /// Domains now implement <see cref="IOrchestrationDomain"/> and are polled.
    /// </summary>
    [System.Obsolete("Use IOrchestrationDomain.Evaluate instead. Arbiter now polls domains directly.")]
    public void SubmitIdleRoles(IdleRolePolicySet policySet)
    {
        _proposals.SetIdle(policySet);
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
            if (_lastMode != DispatchMode.None)
            {
                DispatchCombatHoldAll();
                DispatchIdleHoldAll();
                _lastMode = DispatchMode.None;
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

        // ── Update hysteresis timers ────────────────────────────────
        if (_proposals.ThreatPresent)
        {
            _combatLockedUntil = Mathf.Max(_combatLockedUntil, now + combatMinActiveTime);
            _threatMemoryUntil = now + combatCooldownAfterThreat;
        }

        bool combatSticky = now <= _combatLockedUntil || now <= _threatMemoryUntil;
        bool combatActive = _proposals.HasCombat && (_proposals.ThreatPresent || combatSticky);

        // ── Dispatch based on active domain ─────────────────────────
        if (combatActive)
        {
            DispatchCombat(_proposals.CombatCommand);

            // One-time cross-domain Hold on mode transition
            if (_lastMode != DispatchMode.Combat)
                DispatchIdleHoldAll();

            _lastMode = DispatchMode.Combat;
        }
        else if (_proposals.HasIdle)
        {
            DispatchIdlePerUnit(_ctx.Anchor, now);

            // One-time cross-domain Hold on mode transition
            if (_lastMode != DispatchMode.Idle)
                DispatchCombatHoldAll();

            _lastMode = DispatchMode.Idle;
        }
        else
        {
            // No proposals — hold everything
            if (_lastMode != DispatchMode.None)
            {
                DispatchCombatHoldAll();
                DispatchIdleHoldAll();
            }
            _lastMode = DispatchMode.None;
        }

        if (debugLog)
        {
            Debug.Log(string.Concat(
                "[Arbiter] mode=", _lastMode.ToString(),
                " threat=", _proposals.ThreatPresent ? "1" : "0",
                " combatProp=", _proposals.HasCombat ? "1" : "0",
                " idleProp=", _proposals.HasIdle ? "1" : "0",
                " actors=", _world.Actors.Count.ToString(),
                " lock=", (_combatLockedUntil - now).ToString("F1"),
                "s mem=", (_threatMemoryUntil - now).ToString("F1"), "s"), this);
        }
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
        bool foundIdle = false;
        bool foundCombat = false;

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
        }

        // Null clears: if no source provides a map, stored field becomes null
        _idleRolePolicyMap = newIdle;
        _combatRolePolicyMap = newCombat;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Combat dispatch — iterates cached friendly receivers
    // ──────────────────────────────────────────────────────────────────

    void DispatchCombat(CombatCommand cmd)
    {
        List<ICombatCommandReceiver> receivers = _world.FriendlyCombatReceivers;
        for (int i = 0; i < receivers.Count; i++)
        {
            ICombatCommandReceiver r = receivers[i];
            // Unity-null guard: object can be destroyed mid-tick
            if (r is Object uo && uo == null) continue;

            // Per-role targeting policy injection via typed RoleAsset
            if (_combatRolePolicyMap != null && r is IRoleAssetProvider rap)
            {
                RoleAsset role = rap.GetRoleAsset();
                if (role != null)
                {
                    CombatTargetingPolicyAsset policyAsset;
                    if (_combatRolePolicyMap.TryGet(role, out policyAsset))
                    {
                        if (r is Component c)
                        {
                            // PERF: GetComponentInParent — selector may be on parent GO
                            UnitCombatTargetSelector selector = c.GetComponentInParent<UnitCombatTargetSelector>();
                            if (selector != null)
                                selector.SetRuntimeDefaultPolicy(policyAsset);
                        }
                    }
                }
                else if (!_warnedMissingRoleCombat)
                {
                    _warnedMissingRoleCombat = true;
                    Debug.LogWarning("[OrchestrationArbiter] Combat receiver missing RoleAsset; " +
                                     "combatRolePolicyMap will not inject policy.", this);
                }
            }

            r.ApplyCombatCommand(cmd);
        }
    }

    void DispatchCombatHoldAll()
    {
        CombatCommand hold = CombatCommand.Create(CombatCommandType.Hold,
            debugLabel: "Arbiter=IdleActive");
        DispatchCombat(hold);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Idle dispatch — Per-role policy, per-unit command
    //  IMPORTANT: Policy is looked up by RoleAsset from _idleRolePolicyMap
    //  (pulled from domain via IIdleRolePolicyMapSource each tick).
    //  Commands are computed per-unit using entitySeed so units of the
    //  same role don't clump. Iterates cached friendly idle receivers.
    // ──────────────────────────────────────────────────────────────────

    void DispatchIdlePerUnit(Vector2 anchor, float now)
    {
        List<IIdleCommandReceiver> receivers = _world.FriendlyIdleReceivers;

        // Guard: no idle map bound → Hold all with warning
        if (_idleRolePolicyMap == null)
        {
            if (!_warnedNoIdleMap)
            {
                _warnedNoIdleMap = true;
                Debug.LogWarning("[OrchestrationArbiter] Idle domain active but no idle role policy map bound. " +
                                 "All idlers will Hold.", this);
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
                    Debug.LogWarning("[OrchestrationArbiter] Idle receiver does not implement " +
                                     "IRoleContextProvider; idle will Hold.", this);
                }
                r.ApplyIdleCommand(IdleCommand.Hold());
                continue;
            }

            RoleAsset role = rcp.GetRoleAsset();
            int entitySeed = rcp.GetEntitySeed();

            IdlePolicyAsset policy;
            IdleCommand cmd;

            if (role != null && _idleRolePolicyMap.TryGet(role, out policy) && policy != null)
            {
                // Inject runtime default on selector; resolve effective policy
                // IMPORTANT: per-unit override on selector wins over role-map
                IdlePolicyAsset effectivePolicy = policy;
                if (r is Component comp)
                {
                    UnitIdlePolicySelector sel = comp.GetComponent<UnitIdlePolicySelector>();
                    if (sel != null)
                    {
                        sel.SetRuntimeDefaultPolicy(policy);
                        effectivePolicy = sel.ResolvePolicy() ?? policy;
                    }
                }

                // Stable role seed from asset instance ID
                int roleSeed = role.GetInstanceID();

                // Get receiver transform for the policy
                Transform self = (r is Component c) ? c.transform : transform;

                string dbg;
                cmd = effectivePolicy.ChooseCommand(self, anchor, now, roleSeed, entitySeed, _ctx, out dbg);

                // PERF: Only allocate DebugLabel string when logging is on
                if (debugLog)
                {
                    cmd.DebugLabel = string.Concat("Idle=", effectivePolicy.Id, ":", role.Id);
                    if (dbg != null)
                        Debug.Log(string.Concat("[Arbiter] role=", role.Id, " policy=", effectivePolicy.Id, " dbg=", dbg), this);
                }
            }
            else
            {
                // Strict: no role match → Hold. Do NOT fall back to selector defaults.
                cmd = IdleCommand.Hold();
                if (debugLog)
                    cmd.DebugLabel = "Arbiter=NoRoleMatch";

                if (role == null && !_warnedMissingRoleIdle)
                {
                    _warnedMissingRoleIdle = true;
                    Debug.LogWarning("[OrchestrationArbiter] Unit missing RoleAsset; idle will Hold.", this);
                }

                if (debugLog)
                    Debug.Log(string.Concat("[Arbiter] No role match for '",
                        role != null ? role.Id : "null", "'"), this);
            }

            r.ApplyIdleCommand(cmd);
        }
    }

    void DispatchIdleHoldAll()
    {
        IdleCommand hold = IdleCommand.Hold();
        List<IIdleCommandReceiver> receivers = _world.FriendlyIdleReceivers;
        for (int i = 0; i < receivers.Count; i++)
        {
            IIdleCommandReceiver r = receivers[i];
            if (r is Object obj && obj == null) continue;
            r.ApplyIdleCommand(hold);
        }
    }
}
