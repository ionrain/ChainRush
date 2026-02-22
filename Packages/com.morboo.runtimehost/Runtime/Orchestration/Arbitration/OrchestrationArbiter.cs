using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arbitrates between combat and idle domains each tick. Domains extend
/// <see cref="DomainOrchestrator"/> and are polled via <see cref="DomainOrchestrator.Evaluate"/>.
/// The arbiter decides which domain is active (using hysteresis to prevent thrash)
/// and produces an <see cref="OrchestrationTickResult"/> — it does NOT execute commands.
/// <para>
/// IMPORTANT — The arbiter produces decisions only. The <see cref="OrchestrationLoop"/>
/// drives the tick lifecycle: arbiter.ProduceTick → router.Execute.
/// Domains must NOT dispatch directly; they only write proposals.
/// </para>
/// <para>
/// IMPORTANT — Per-tick world cache: The arbiter builds an
/// <see cref="OrchestrationWorldCache"/> once per tick from
/// <see cref="OrchestrationRegistry"/>. Domains and dispatch methods iterate
/// cached lists instead of re-querying the registry.
/// </para>
/// <para>
/// IMPORTANT — Policy maps are owned by domains. The arbiter pulls map references
/// each tick from cached domain registration bindings produced by domains.
/// Domains must not mutate their map references during Evaluate.
/// </para>
/// </summary>
public sealed class OrchestrationArbiter : MonoBehaviour, IArbiter, IDomainArbiterBindingApplyTarget
{
    readonly struct ArbiterBindingConsumerEntry
    {
        public readonly DomainArbiterBindingConsumerKey Key;
        public readonly ArbiterBindingConsumerApplyHandler Apply;

        public ArbiterBindingConsumerEntry(
            DomainArbiterBindingConsumerKey key,
            ArbiterBindingConsumerApplyHandler apply)
        {
            Key = key;
            Apply = apply;
        }
    }

    delegate bool ArbiterBindingConsumerApplyHandler(ScriptableObject asset);

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
    [Tooltip("Projection from 3D world-space to planar orchestration queries.")]
    [SerializeField] SpatialProjectionPlane projectionPlane = SpatialProjectionPlane.XY;

    // ──────────────────────────────────────────────────────────────────
    //  Serialized — Domains
    // ──────────────────────────────────────────────────────────────────

    [Header("Domains")]
    [Tooltip("LEGACY storage only (hidden). Domain composition source-of-truth is OrchestrationLoop (or a composition seam) which applies domains into the arbiter.")]
    [HideInInspector, SerializeField] DomainOrchestrator[] domainOrchestrators;

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

    DomainRegistration[] _cachedDomainRegistrations;
    int _domainCount;
    OrchestrationDomainId[] _stickyPrimaryDomainKeys;
    int _stickyPrimaryDomainKeyCount;
    bool _warnedInvalidDomains;
    bool _warnedDuplicateStickyPrimaryDomainKeys;
    bool _domainCompositionApplied;
    bool _warnedMissingDomainComposition;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — World cache (single reused instance)
    // ──────────────────────────────────────────────────────────────────

    readonly OrchestrationWorldCache _world = new OrchestrationWorldCache();

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Proposals (single reused instance)
    // ──────────────────────────────────────────────────────────────────

    // Reusable scratch for C03 compatibility adapter (per-domain legacy Evaluate -> collector import).
    readonly OrchestrationArbiterProposals _legacyProposalScratch = new OrchestrationArbiterProposals();
    readonly OrchestrationProposalCollector _proposalCollector = new OrchestrationProposalCollector();
    OrchestrationArbiterContext _ctx;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Policy maps (owned by domains, pulled via cached bindings)
    //  IMPORTANT: Not serialized. Arbiter reads from domains each tick
    //  via cached domain-registration binding contributors.
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

    OrchestrationDomainId _lastDomain;

    bool _warnedMissingSetup;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — One-shot warning flags (separate per category)
    // ──────────────────────────────────────────────────────────────────

    bool _warnedDuplicateIdleSource;
    bool _warnedDuplicateCombatSource;
    bool _warnedDuplicateConstraintsSource;
    bool _warnedUnknownArbiterBindingKey;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Arbiter binding registry (key -> binding applier)
    //  Transitional C04A step: generic binding-key payload + local application registry.
    // ──────────────────────────────────────────────────────────────────

    readonly DomainArbiterBindingTargetEntry[] _arbiterBindingRegistry = new DomainArbiterBindingTargetEntry[3];
    int _arbiterBindingRegistryCount;

    // ──────────────────────────────────────────────────────────────────
    //  Runtime — Arbiter binding consumer registry (consumer key -> local handler)
    //  Transitional C04A step: remove consumer-key switch from apply-target seam.
    // ──────────────────────────────────────────────────────────────────

    readonly ArbiterBindingConsumerEntry[] _arbiterBindingConsumerRegistry = new ArbiterBindingConsumerEntry[3];
    int _arbiterBindingConsumerRegistryCount;

    // ──────────────────────────────────────────────────────────────────
    //  Public — Domain state query
    // ──────────────────────────────────────────────────────────────────

    public bool IsCombatActive => _lastDomain == OrchestrationDomainId.Combat;

    /// <summary>
    /// C04A composition seam for bridge/integration modules.
    /// Allows data-driven domain ordering/selection to be applied without editing
    /// RuntimeHost entrypoints. Applies the provided array and rebuilds cached domain
    /// registrations immediately.
    /// </summary>
    public void SetDomainOrchestratorsForComposition(DomainOrchestrator[] domains)
    {
        _domainCompositionApplied = true;
        domainOrchestrators = domains;
        CacheDomains();
    }

    // ──────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _lastDomain = OrchestrationDomainId.None;
        _combatLockedUntil = 0f;
        _threatMemoryUntil = 0f;

        EnsureArbiterBindingConsumerRegistryInitialized();
        CacheDomains();
    }

    void OnDisable()
    {
        // Clear stored maps to avoid stale refs on scene reload / domain toggles
        _idleRolePolicyMap = null;
        _combatRolePolicyMap = null;
        _combatRoleConstraintsMap = null;
    }

    void RegisterArbiterBinding(in DomainArbiterBindingTargetEntry entry)
    {
        if (entry.Key.IsNone || entry.Apply == null)
            return;

        for (int i = 0; i < _arbiterBindingRegistryCount; i++)
        {
            if (_arbiterBindingRegistry[i].Key != entry.Key)
                continue;

            return;
        }

        if (_arbiterBindingRegistryCount >= _arbiterBindingRegistry.Length)
            return;

        _arbiterBindingRegistry[_arbiterBindingRegistryCount++] = entry;
    }

    bool TryResolveArbiterBindingApplier(DomainArbiterBindingKey key, out DomainArbiterBindingApplyHandler apply)
    {
        for (int i = 0; i < _arbiterBindingRegistryCount; i++)
        {
            DomainArbiterBindingTargetEntry entry = _arbiterBindingRegistry[i];
            if (entry.Key != key)
                continue;

            apply = entry.Apply;
            return true;
        }

        apply = null;
        return false;
    }

    void EnsureArbiterBindingConsumerRegistryInitialized()
    {
        if (_arbiterBindingConsumerRegistryCount > 0)
            return;

        RegisterArbiterBindingConsumer(
            RuntimeHostArbiterBindingConsumerKeys.IdleRolePolicyMap,
            TryApplyIdleRolePolicyMapBinding);
        RegisterArbiterBindingConsumer(
            RuntimeHostArbiterBindingConsumerKeys.CombatRolePolicyMap,
            TryApplyCombatRolePolicyMapBinding);
        RegisterArbiterBindingConsumer(
            RuntimeHostArbiterBindingConsumerKeys.CombatRoleConstraintsMap,
            TryApplyCombatRoleConstraintsMapBinding);
    }

    void RegisterArbiterBindingConsumer(
        DomainArbiterBindingConsumerKey consumerKey,
        ArbiterBindingConsumerApplyHandler apply)
    {
        if (consumerKey.IsNone || apply == null)
            return;

        for (int i = 0; i < _arbiterBindingConsumerRegistryCount; i++)
        {
            if (_arbiterBindingConsumerRegistry[i].Key != consumerKey)
                continue;

            return;
        }

        if (_arbiterBindingConsumerRegistryCount >= _arbiterBindingConsumerRegistry.Length)
            return;

        _arbiterBindingConsumerRegistry[_arbiterBindingConsumerRegistryCount++] =
            new ArbiterBindingConsumerEntry(consumerKey, apply);
    }

    bool TryResolveArbiterBindingConsumer(
        DomainArbiterBindingConsumerKey consumerKey,
        out ArbiterBindingConsumerApplyHandler apply)
    {
        for (int i = 0; i < _arbiterBindingConsumerRegistryCount; i++)
        {
            ArbiterBindingConsumerEntry entry = _arbiterBindingConsumerRegistry[i];
            if (entry.Key != consumerKey)
                continue;

            apply = entry.Apply;
            return true;
        }

        apply = null;
        return false;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Domain cache — built once on enable
    // ──────────────────────────────────────────────────────────────────

    void CacheDomains()
    {
        _domainCount = 0;
        _stickyPrimaryDomainKeyCount = 0;
        _arbiterBindingRegistryCount = 0;

        if (domainOrchestrators == null || domainOrchestrators.Length == 0)
        {
            _cachedDomainRegistrations = null;
            if (!_warnedInvalidDomains)
            {
                _warnedInvalidDomains = true;
                Debug.LogWarning("[OrchestrationArbiter] domainOrchestrators is empty. " +
                                 "No domains will be polled.", this);
            }
            return;
        }

        if (_cachedDomainRegistrations == null || _cachedDomainRegistrations.Length < domainOrchestrators.Length)
            _cachedDomainRegistrations = new DomainRegistration[domainOrchestrators.Length];
        if (_stickyPrimaryDomainKeys == null || _stickyPrimaryDomainKeys.Length < domainOrchestrators.Length)
            _stickyPrimaryDomainKeys = new OrchestrationDomainId[domainOrchestrators.Length];

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

            DomainRegistration registration = d.GetRegistration();
            _cachedDomainRegistrations[_domainCount] = registration;
            _domainCount++;
            CacheDomainArbitrationProfile(registration);
            CacheArbiterBindingTargetRegistry(registration);
        }
    }

    void CacheArbiterBindingTargetRegistry(in DomainRegistration registration)
    {
        IDomainArbiterBindingContributor bindingContributor = registration.ArbiterBindingContributor;
        if (bindingContributor == null)
            return;

        DomainArbiterBindingTargetContribution contribution = default;
        bindingContributor.ContributeArbiterBindingTargets(ref contribution);

        for (int i = 0; i < contribution.Count; i++)
            RegisterArbiterBinding(contribution.GetEntry(i));
    }

    void CacheDomainArbitrationProfile(in DomainRegistration registration)
    {
        DomainArbitrationProfile profile = registration.ArbitrationProfile;
        OrchestrationDomainId domainId = registration.Orchestrator == null
            ? OrchestrationDomainId.None
            : registration.Orchestrator.DomainId;

        if (domainId == OrchestrationDomainId.None)
            return;

        if (!profile.StickyPrimary)
            return;

        for (int i = 0; i < _stickyPrimaryDomainKeyCount; i++)
        {
            if (_stickyPrimaryDomainKeys[i] != domainId)
                continue;

            if (!_warnedDuplicateStickyPrimaryDomainKeys)
            {
                _warnedDuplicateStickyPrimaryDomainKeys = true;
                Debug.LogWarning(string.Concat(
                    "[OrchestrationArbiter] Duplicate sticky-primary arbitration profile for DomainKey=",
                    domainId.ToString(),
                    ". First registration wins."), this);
            }
            return;
        }

        _stickyPrimaryDomainKeys[_stickyPrimaryDomainKeyCount++] = domainId;
    }

    // ──────────────────────────────────────────────────────────────────
    //  World cache — built once per tick before domain polling
    //  IMPORTANT: One scan of OrchestrationRegistry per tick. Domains
    //  and dispatch methods iterate cached lists instead.
    // ──────────────────────────────────────────────────────────────────

    void BuildWorldCache(OrchestrationArbiterContext ctx)
    {
        _world.Clear();
        _world.ProjectionPlane = projectionPlane;
        _world.WorldAnchor = ctx.WorldAnchor;
        _world.Anchor = ctx.Anchor;
        _world.Now = ctx.Now;

        // ── Actors: active entities with typed faction ───────────────
        IReadOnlyList<IStateReporter> reporters = OrchestrationRegistry.StateReporters;
        for (int i = 0; i < reporters.Count; i++)
        {
            IStateReporter r = reporters[i];
            if (r == null) continue;
            if (r is Object uo && uo == null) continue;

            IOrchestrationActor actor = r as IOrchestrationActor;
            if (actor == null) continue;
            if (actor.GetLifecycleState() != EntityLifecycleState.Active) continue;

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
        // IMPORTANT: RoleAsset → RoleId conversion at this Integration boundary.
        for (int i = 0; i < _world.FriendlyIdleReceivers.Count; i++)
        {
            Component c = _world.FriendlyIdleReceivers[i] as Component;
            if (c == null) continue;
            IRoleAssetProvider rp = c.GetComponent<IRoleAssetProvider>();
            if (rp == null) continue;
            RoleAsset roleAsset = rp.GetRoleAsset();
            if (roleAsset != null && !roleAsset.RoleId.IsNone)
                _world.RoleByTransform[c.transform] = roleAsset.RoleId;
        }
        for (int i = 0; i < _world.FriendlyCombatReceivers.Count; i++)
        {
            Component c = _world.FriendlyCombatReceivers[i] as Component;
            if (c == null) continue;
            if (_world.RoleByTransform.ContainsKey(c.transform)) continue;
            IRoleAssetProvider rp = c.GetComponent<IRoleAssetProvider>();
            if (rp == null) continue;
            RoleAsset roleAsset = rp.GetRoleAsset();
            if (roleAsset != null && !roleAsset.RoleId.IsNone)
                _world.RoleByTransform[c.transform] = roleAsset.RoleId;
        }

        // ── Resolve idle bounds per role ────────────────────────────
        IdleBoundsRegistry.FillResolvedBounds(_world.ResolvedIdleBounds);

        // ── Resolve combat target set ───────────────────────────────
        CombatTargetSet resolvedTs;
        OrchestrationRegistry.TryGetCombatTargetSet(ctx.OrchestratorFaction, out resolvedTs);
        _world.ResolvedCombatTargetSet = resolvedTs;

        // ── Snapshot IWorldQuery data and freeze ─────────────────────
        _world.SnapshotActors(ctx.OrchestratorFaction, ctx.Relations);
        _world.SnapshotCrowd();
        _world.BuildRoleByEntityId();
        _world.SnapshotReceivers();
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
    //  ProduceTick — Build cache + Poll domains + Arbitration
    //  IMPORTANT: Does NOT execute commands. Returns result for
    //  OrchestrationLoop to pass to ExecutionRouter.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Produces a tick result: builds world cache, polls domains, arbitrates.
    /// Does NOT dispatch commands — the caller (<see cref="OrchestrationLoop"/>)
    /// passes the result to <see cref="ExecutionRouter.Execute"/>.
    /// </summary>
    public OrchestrationTickResult ProduceTick(float now)
    {
        if (!_domainCompositionApplied)
        {
            if (!_warnedMissingDomainComposition)
            {
                _warnedMissingDomainComposition = true;
                Debug.LogError(
                    "[OrchestrationArbiter] Domain composition was not applied. " +
                    "Configure ordered domains via OrchestrationLoop.domainOrchestrators " +
                    "(or apply them through a composition seam) and do not use Arbiter inspector domain fallback.",
                    this);
            }

            if (_lastDomain != OrchestrationDomainId.None)
            {
                ArbiterDecision holdAll = new ArbiterDecision
                {
                    DomainKey = OrchestrationDomainKeys.None,
                    ProposalKey = OrchestrationProposalKeys.None,
                    ModeChanged = true
                };
                _lastDomain = OrchestrationDomainId.None;
                return new OrchestrationTickResult
                {
                    Decision = holdAll,
                    World = _world,
                    ExecContext = default,
                    Skipped = false
                };
            }

            return new OrchestrationTickResult { Skipped = true };
        }

        // ── Validate setup ──────────────────────────────────────────
        if (orchestratorFaction == null || typedRelations == null)
        {
            if (!_warnedMissingSetup)
            {
                _warnedMissingSetup = true;
                Debug.LogWarning("[OrchestrationArbiter] Missing orchestratorFaction or typedRelations.", this);
            }

            // Signal hold-all to the loop
            if (_lastDomain != OrchestrationDomainId.None)
            {
                ArbiterDecision holdAll = new ArbiterDecision
                {
                    DomainKey = OrchestrationDomainKeys.None,
                    ProposalKey = OrchestrationProposalKeys.None,
                    ModeChanged = true
                };
                _lastDomain = OrchestrationDomainId.None;
                return new OrchestrationTickResult
                {
                    Decision = holdAll,
                    World = _world,
                    ExecContext = default,
                    Skipped = false
                };
            }

            return new OrchestrationTickResult { Skipped = true };
        }

        // ── Fill context ────────────────────────────────────────────
        _ctx.OrchestratorFaction = orchestratorFaction;
        _ctx.Relations = typedRelations;
        _ctx.WorldAnchor = anchorOverride != null
            ? anchorOverride.position.ToFloat3()
            : transform.position.ToFloat3();
        _ctx.Anchor = _ctx.WorldAnchor.ProjectToFloat2(projectionPlane);
        _ctx.Now = now;
        _ctx.DebugLog = debugLog;
        _ctx.World = _world;

        // ── Build world cache (one registry scan per tick) ──────────
        BuildWorldCache(_ctx);

        // ── Clear and poll domains ──────────────────────────────────
        _proposalCollector.Clear();
        for (int i = 0; i < _domainCount; i++)
            _cachedDomainRegistrations[i].Orchestrator.CollectProposals(_ctx, _proposalCollector, _legacyProposalScratch);

        // ── Pull policy maps from domains (after Evaluate, before result) ──
        RefreshPolicyMapsFromDomains();

        // ── Arbitrate: pure decision ─────────────────────────────────
        // C04 path: arbitrate from proposal collection metadata (not fixed legacy input flags).
        ArbiterDecision decision = Arbitrate(_proposalCollector.Entries, _proposalCollector.ThreatPresent, now);
        CombatCommand selectedCombatCommand = default;
        bool hasSelectedCombatCommand =
            (OrchestrationDomainId)decision.DomainKey == OrchestrationDomainId.Combat &&
            _proposalCollector.TryGetCombatCommand(decision.ProposalKey, out selectedCombatCommand);

        // ── Build execution context ──────────────────────────────────
        ExecutionContext execCtx = new ExecutionContext
        {
            IdleRolePolicyMap = _idleRolePolicyMap,
            CombatRolePolicyMap = _combatRolePolicyMap,
            CombatRoleConstraintsMap = _combatRoleConstraintsMap,
            CombatCommand = hasSelectedCombatCommand ? selectedCombatCommand : default,
            OrchestratorFaction = orchestratorFaction,
            Relations = typedRelations,
            WorldAnchor = _ctx.WorldAnchor,
            Anchor = _ctx.Anchor,
            Now = now,
            DebugLog = debugLog
        };

        // ── Update mode tracking ─────────────────────────────────────
        _lastDomain = (OrchestrationDomainId)decision.DomainKey;

        if (debugLog)
        {
            Debug.Log(string.Concat(
                "[Arbiter] mode=", ((OrchestrationDomainId)decision.DomainKey).ToString(),
                " modeChanged=", decision.ModeChanged ? "1" : "0",
                " threat=", _proposalCollector.ThreatPresent ? "1" : "0",
                " combatProp=", _proposalCollector.HasCombat ? "1" : "0",
                " idleProp=", _proposalCollector.HasIdle ? "1" : "0",
                " actors=", _world.ActorCount.ToString(),
                " lock=", (_combatLockedUntil - now).ToString("F1"),
                "s mem=", (_threatMemoryUntil - now).ToString("F1"), "s"), this);
        }

        return new OrchestrationTickResult
        {
            Decision = decision,
            ExecContext = execCtx,
            World = _world,
            Skipped = false
        };
    }

    // ──────────────────────────────────────────────────────────────────
    //  IArbiter — Pure arbitration: proposals + time → decision
    //  IMPORTANT: No side effects, no dispatch, no registry reads.
    //  Hysteresis timers are the only mutable state touched.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// RuntimeHost convenience overload used by local call-sites during migration.
    /// Canonical interface seam is <see cref="Arbitrate(IReadOnlyList{Proposal}, bool, float)"/>.
    /// </summary>
    public ArbiterDecision Arbitrate(OrchestrationProposalCollector collector, float now)
    {
        if (collector == null)
            return Arbitrate((IReadOnlyList<Proposal>)null, false, now);

        return Arbitrate(collector.Entries, collector.ThreatPresent, now);
    }

    /// <summary>
    /// Selects an active domain based on proposal-list metadata and current time.
    /// C04 canonical path: explicit proposal-list arbitration with tie-break policy.
    /// </summary>
    public ArbiterDecision Arbitrate(IReadOnlyList<Proposal> proposals, bool threatPresent, float now)
    {
        bool hasStickyPrimaryProposal = false;
        Proposal bestStickyPrimary = default;

        bool hasFallbackProposal = false;
        Proposal bestFallback = default;

        if (proposals != null)
        {
            for (int i = 0; i < proposals.Count; i++)
            {
                Proposal p = proposals[i];
                if ((OrchestrationDomainId)p.DomainKey == OrchestrationDomainId.None || p.ProposalKey == OrchestrationProposalKeys.None)
                    continue;

                if (IsStickyPrimaryProposal(p))
                {
                    if (!hasStickyPrimaryProposal || IsHigherPriorityProposal(p, bestStickyPrimary))
                    {
                        bestStickyPrimary = p;
                        hasStickyPrimaryProposal = true;
                    }
                }
                else
                {
                    if (!hasFallbackProposal || IsHigherPriorityProposal(p, bestFallback))
                    {
                        bestFallback = p;
                        hasFallbackProposal = true;
                    }
                }
            }
        }

        return ArbitrateNormalized(
            threatPresent,
            hasStickyPrimaryProposal,
            bestStickyPrimary,
            hasFallbackProposal,
            bestFallback,
            now);
    }

    /// <summary>
    /// Transitional C04 classifier seam.
    /// Current hysteresis semantics are still combat-centric, so proposal-list arbitration
    /// maps proposals into "sticky primary" vs "fallback" buckets here.
    /// C04A/C05+: replace hard-coded domain checks with policy/registration-driven metadata.
    /// </summary>
    bool IsStickyPrimaryProposal(in Proposal proposal)
    {
        return IsStickyPrimaryDomainKey((OrchestrationDomainId)proposal.DomainKey);
    }

    bool IsStickyPrimaryDomainKey(OrchestrationDomainId domainKey)
    {
        for (int i = 0; i < _stickyPrimaryDomainKeyCount; i++)
        {
            if (_stickyPrimaryDomainKeys[i] == domainKey)
                return true;
        }

        return false;
    }

    static bool IsHigherPriorityProposal(in Proposal candidate, in Proposal current)
    {
        if (candidate.Priority != current.Priority)
            return candidate.Priority > current.Priority;

        if (!Mathf.Approximately(candidate.Score, current.Score))
            return candidate.Score > current.Score;

        // Stable tie-break to avoid hidden dependence on registration order.
        if (candidate.DomainKey != current.DomainKey)
            return candidate.DomainKey < current.DomainKey;

        return candidate.ProposalKey < current.ProposalKey;
    }

    /// <summary>
    /// Legacy compatibility overload (IArbiter) used until proposal-list arbitration
    /// becomes the canonical public contract. Runtime path in C04 uses
    /// <see cref="Arbitrate(OrchestrationProposalCollector,float)"/>.
    /// Pure function aside from updating hysteresis timers.
    /// </summary>
    public ArbiterDecision Arbitrate(in ArbitrationInput input, float now)
    {
        Proposal legacyCombat = new Proposal(
            (int)OrchestrationDomainId.Combat,
            OrchestrationProposalKeys.CombatPrimary,
            priority: 100,
            score: input.ThreatPresent ? 1f : 0f);
        Proposal legacyIdle = new Proposal(
            (int)OrchestrationDomainId.Idle,
            OrchestrationProposalKeys.IdleDefault,
            priority: 10,
            score: 0f);

        return ArbitrateNormalized(
            input.ThreatPresent,
            input.HasPrimaryProposal,
            legacyCombat,
            input.HasSecondaryProposal,
            legacyIdle,
            now);
    }

    ArbiterDecision ArbitrateNormalized(
        bool threatPresent,
        bool hasStickyPrimaryProposal,
        in Proposal bestStickyPrimary,
        bool hasFallbackProposal,
        in Proposal bestFallback,
        float now)
    {
        // ── Update hysteresis timers ────────────────────────────────
        if (threatPresent)
        {
            _combatLockedUntil = Mathf.Max(_combatLockedUntil, now + combatMinActiveTime);
            _threatMemoryUntil = now + combatCooldownAfterThreat;
        }

        bool combatSticky = now <= _combatLockedUntil || now <= _threatMemoryUntil;
        bool stickyPrimaryActive = hasStickyPrimaryProposal && (threatPresent || combatSticky);

        OrchestrationDomainId selectedDomain;
        int selectedProposal;

        if (stickyPrimaryActive)
        {
            selectedDomain = (OrchestrationDomainId)bestStickyPrimary.DomainKey;
            selectedProposal = bestStickyPrimary.ProposalKey;
        }
        else if (hasFallbackProposal)
        {
            selectedDomain = (OrchestrationDomainId)bestFallback.DomainKey;
            selectedProposal = bestFallback.ProposalKey;
        }
        else
        {
            selectedDomain = OrchestrationDomainId.None;
            selectedProposal = OrchestrationProposalKeys.None;
        }

        bool modeChanged = selectedDomain != _lastDomain;

        return new ArbiterDecision
        {
            DomainKey = (int)selectedDomain,
            ProposalKey = selectedProposal,
            ModeChanged = modeChanged
        };
    }

    // ──────────────────────────────────────────────────────────────────
    //  Policy map refresh — pull from cached DomainRegistrations
    //  IMPORTANT: Called once per tick after domain Evaluate, before dispatch.
    //  "Last non-null wins" if multiple domains provide the same map type.
    //  If no source provides a non-null map, the stored field becomes null.
    //  PERF: Provider interface discovery is cached in DomainRegistration.
    // ──────────────────────────────────────────────────────────────────

    void RefreshPolicyMapsFromDomains()
    {
        // Null clears: if no source provides a map this tick, stored field becomes null.
        _idleRolePolicyMap = null;
        _combatRolePolicyMap = null;
        _combatRoleConstraintsMap = null;

        for (int i = 0; i < _domainCount; i++)
        {
            DomainRegistration registration = _cachedDomainRegistrations[i];
            DomainOrchestrator d = registration.Orchestrator;
            // Unity-null safe: skip destroyed pooled components
            if (d is Object uo && uo == null) continue;

            IDomainArbiterBindingContributor bindingContributor = registration.ArbiterBindingContributor;
            if (bindingContributor == null)
                continue;

            DomainArbiterBindingContribution contribution = default;
            bindingContributor.ContributeArbiterBindings(ref contribution);

            for (int j = 0; j < contribution.Count; j++)
            {
                DomainArbiterBindingEntry entry = contribution.GetEntry(j);
                if (!TryResolveArbiterBindingApplier(entry.Key, out DomainArbiterBindingApplyHandler apply))
                {
                    if (!_warnedUnknownArbiterBindingKey)
                    {
                        _warnedUnknownArbiterBindingKey = true;
                        Debug.LogWarning(string.Concat(
                            "[OrchestrationArbiter] Unknown arbiter binding key=",
                            entry.Key.Value.ToString(),
                            ". Entry ignored (transitional registry)."), this);
                    }
                    continue;
                }

                apply(this, entry.Asset);
            }
        }
    }

    private bool TryApplyIdleRolePolicyMapBinding(ScriptableObject asset)
    {
        IdleRolePolicyMapAsset idleMap = asset as IdleRolePolicyMapAsset;
        if (idleMap == null)
            return false;

        if (_idleRolePolicyMap != null && !_warnedDuplicateIdleSource)
        {
            _warnedDuplicateIdleSource = true;
            Debug.LogWarning("[OrchestrationArbiter] Multiple domains provide " +
                "idle role policy map bindings. Last non-null wins.", this);
        }

        _idleRolePolicyMap = idleMap;
        return true;
    }

    private bool TryApplyCombatRolePolicyMapBinding(ScriptableObject asset)
    {
        CombatRolePolicyMapAsset combatMap = asset as CombatRolePolicyMapAsset;
        if (combatMap == null)
            return false;

        if (_combatRolePolicyMap != null && !_warnedDuplicateCombatSource)
        {
            _warnedDuplicateCombatSource = true;
            Debug.LogWarning("[OrchestrationArbiter] Multiple domains provide " +
                "combat role policy map bindings. Last non-null wins.", this);
        }

        _combatRolePolicyMap = combatMap;
        return true;
    }

    private bool TryApplyCombatRoleConstraintsMapBinding(ScriptableObject asset)
    {
        CombatRoleConstraintsMapAsset constraintsMap = asset as CombatRoleConstraintsMapAsset;
        if (constraintsMap == null)
            return false;

        if (_combatRoleConstraintsMap != null && !_warnedDuplicateConstraintsSource)
        {
            _warnedDuplicateConstraintsSource = true;
            Debug.LogWarning("[OrchestrationArbiter] Multiple domains provide " +
                "combat role constraints map bindings. Last non-null wins.", this);
        }

        _combatRoleConstraintsMap = constraintsMap;
        return true;
    }

    bool IDomainArbiterBindingApplyTarget.TryApplyArbiterBindingConsumer(
        DomainArbiterBindingConsumerKey consumerKey,
        ScriptableObject asset)
    {
        EnsureArbiterBindingConsumerRegistryInitialized();
        if (!TryResolveArbiterBindingConsumer(consumerKey, out ArbiterBindingConsumerApplyHandler apply))
            return false;

        return apply(asset);
    }

}
