using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Unit tests for RuntimeHost: EntityIdAllocator, Arbiter arbitration, CommandEmitter (bus).
/// </summary>
public sealed class RuntimeHostTests
{
    // ──────────────────────────────────────────────────────────────────
    //  EntityIdAllocator
    // ──────────────────────────────────────────────────────────────────

    [Test]
    public void EntityIdAllocator_ProducesUniqueIds()
    {
        var ids = new HashSet<EntityId>();
        for (int i = 0; i < 1000; i++)
        {
            EntityId id = EntityIdAllocator.Create();
            Assert.That(ids.Add(id), Is.True, $"Duplicate EntityId at iteration {i}: {id}");
        }
        Assert.That(ids.Count, Is.EqualTo(1000));
    }

    [Test]
    public void EntityIdAllocator_NeverReturnsNone()
    {
        for (int i = 0; i < 100; i++)
        {
            EntityId id = EntityIdAllocator.Create();
            Assert.That(id.IsNone, Is.False, $"Got EntityId.None at iteration {i}");
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Arbiter — pure arbitration logic
    //  NOTE: OrchestrationArbiter is a MonoBehaviour, so we need a GO.
    //  The Arbitrate method is pure (aside from hysteresis timer updates).
    // ──────────────────────────────────────────────────────────────────

    OrchestrationArbiter CreateArbiter(float combatMinActiveTime = 0.8f, float combatCooldownAfterThreat = 0.6f)
    {
        var go = new GameObject("TestArbiter");
        var arbiter = go.AddComponent<OrchestrationArbiter>();
        var combatDomain = go.AddComponent<TestCombatStickyDomain>();
        var idleDomain = go.AddComponent<TestIdleDomain>();

        // Set serialized hysteresis fields via reflection
        SetField(arbiter, "combatMinActiveTime", combatMinActiveTime);
        SetField(arbiter, "combatCooldownAfterThreat", combatCooldownAfterThreat);
        SetField(arbiter, "domainOrchestrators", new DomainOrchestrator[] { combatDomain, idleDomain });
        InvokePrivateVoid(arbiter, "CacheDomains");

        // Reset internal state
        SetField(arbiter, "_lastDomain", OrchestrationDomainId.None);
        SetField(arbiter, "_combatLockedUntil", 0f);
        SetField(arbiter, "_threatMemoryUntil", 0f);

        return arbiter;
    }

    void DestroyArbiter(OrchestrationArbiter arbiter)
    {
        if (arbiter != null)
            Object.DestroyImmediate(arbiter.gameObject);
    }

    [Test]
    public void Arbiter_SameInputs_SameDecision()
    {
        var arbiter = CreateArbiter();
        try
        {
            List<Proposal> proposals = CreateCombatAndIdleProposals();
            ArbiterDecision d1 = arbiter.Arbitrate(proposals, true, 1f);
            // Reset timers so second call has identical state
            SetField(arbiter, "_combatLockedUntil", 0f);
            SetField(arbiter, "_threatMemoryUntil", 0f);
            SetField(arbiter, "_lastDomain", OrchestrationDomainId.None);
            ArbiterDecision d2 = arbiter.Arbitrate(proposals, true, 1f);

            Assert.That(d1.DomainKey, Is.EqualTo(d2.DomainKey));
            Assert.That(d1.ProposalKey, Is.EqualTo(d2.ProposalKey));
        }
        finally { DestroyArbiter(arbiter); }
    }

    [Test]
    public void Arbiter_ThreatPresent_SelectsCombat()
    {
        var arbiter = CreateArbiter();
        try
        {
            List<Proposal> proposals = CreateCombatAndIdleProposals();

            ArbiterDecision d = arbiter.Arbitrate(proposals, true, 1f);

            Assert.That(d.DomainKey, Is.EqualTo(OrchestrationDomainKeys.Combat));
            Assert.That(d.ProposalKey, Is.EqualTo(OrchestrationProposalKeys.CombatPrimary));
        }
        finally { DestroyArbiter(arbiter); }
    }

    [Test]
    public void Arbiter_NoThreat_NoCombatLock_SelectsIdle()
    {
        var arbiter = CreateArbiter();
        try
        {
            List<Proposal> proposals = CreateCombatAndIdleProposals();

            ArbiterDecision d = arbiter.Arbitrate(proposals, false, 100f); // Far future, no lock

            Assert.That(d.DomainKey, Is.EqualTo(OrchestrationDomainKeys.Idle));
            Assert.That(d.ProposalKey, Is.EqualTo(OrchestrationProposalKeys.IdleDefault));
        }
        finally { DestroyArbiter(arbiter); }
    }

    [Test]
    public void Arbiter_Hysteresis_SticksToCombat()
    {
        var arbiter = CreateArbiter(combatMinActiveTime: 1.0f, combatCooldownAfterThreat: 0.5f);
        try
        {
            List<Proposal> proposals = CreateCombatAndIdleProposals();
            // Tick 1: threat present → combat
            ArbiterDecision d1 = arbiter.Arbitrate(proposals, true, 1f);
            Assert.That(d1.DomainKey, Is.EqualTo(OrchestrationDomainKeys.Combat));

            // Update _lastDomain to reflect the decision
            SetField(arbiter, "_lastDomain", (OrchestrationDomainId)d1.DomainKey);

            // Tick 2: threat gone, but within hysteresis window → still combat
            ArbiterDecision d2 = arbiter.Arbitrate(proposals, false, 1.3f);
            Assert.That(d2.DomainKey, Is.EqualTo(OrchestrationDomainKeys.Combat),
                "Should stick to combat within hysteresis window");
            Assert.That(d2.ModeChanged, Is.False);

            SetField(arbiter, "_lastDomain", (OrchestrationDomainId)d2.DomainKey);

            // Tick 3: well past hysteresis window → idle
            ArbiterDecision d3 = arbiter.Arbitrate(proposals, false, 10f);
            Assert.That(d3.DomainKey, Is.EqualTo(OrchestrationDomainKeys.Idle));
            Assert.That(d3.ModeChanged, Is.True);
        }
        finally { DestroyArbiter(arbiter); }
    }

    [Test]
    public void Arbiter_NoProposals_SelectsNone()
    {
        var arbiter = CreateArbiter();
        try
        {
            var proposals = new List<Proposal>();
            ArbiterDecision d = arbiter.Arbitrate(proposals, false, 1f);

            Assert.That(d.DomainKey, Is.EqualTo(OrchestrationDomainKeys.None));
            Assert.That(d.ProposalKey, Is.EqualTo(OrchestrationProposalKeys.None));
        }
        finally { DestroyArbiter(arbiter); }
    }

    [Test]
    public void Arbiter_ModeChanged_OnFirstTick()
    {
        var arbiter = CreateArbiter();
        try
        {
            List<Proposal> proposals = CreateCombatAndIdleProposals();
            ArbiterDecision d = arbiter.Arbitrate(proposals, true, 1f);

            // First tick with domain != None → ModeChanged (from default None)
            Assert.That(d.ModeChanged, Is.True);
        }
        finally { DestroyArbiter(arbiter); }
    }

    [Test]
    public void Arbiter_LegacyArbitrationInput_CompatibilityMatchesProposalPath()
    {
        var arbiter = CreateArbiter();
        try
        {
            var legacyInput = new ArbitrationInput(
                hasPrimaryProposal: true,
                hasSecondaryProposal: true,
                threatPresent: true);
            List<Proposal> proposals = CreateCombatAndIdleProposals();

            ArbiterDecision legacy = arbiter.Arbitrate(legacyInput, 1f);

            // Reset timers so proposal-path run sees the same internal state.
            SetField(arbiter, "_combatLockedUntil", 0f);
            SetField(arbiter, "_threatMemoryUntil", 0f);
            SetField(arbiter, "_lastDomain", OrchestrationDomainId.None);

            ArbiterDecision canonical = arbiter.Arbitrate(proposals, true, 1f);

            Assert.That(legacy.DomainKey, Is.EqualTo(canonical.DomainKey));
            Assert.That(legacy.ProposalKey, Is.EqualTo(canonical.ProposalKey));
            Assert.That(legacy.ModeChanged, Is.EqualTo(canonical.ModeChanged));
        }
        finally { DestroyArbiter(arbiter); }
    }

    [Test]
    public void Arbiter_ProposalTieBreak_SamePriorityAndScore_SelectsLowerProposalKey()
    {
        var arbiter = CreateArbiter();
        try
        {
            var proposals = new List<Proposal>
            {
                new Proposal(
                    OrchestrationDomainKeys.Idle,
                    proposalKey: 42,
                    priority: 10,
                    score: 0f),
                new Proposal(
                    OrchestrationDomainKeys.Idle,
                    proposalKey: 7,
                    priority: 10,
                    score: 0f),
            };

            ArbiterDecision d = arbiter.Arbitrate(proposals, false, 1f);

            Assert.That(d.DomainKey, Is.EqualTo(OrchestrationDomainKeys.Idle));
            Assert.That(d.ProposalKey, Is.EqualTo(7),
                "Stable tie-break should pick lower proposal key when priority/score are equal.");
        }
        finally { DestroyArbiter(arbiter); }
    }

    // ──────────────────────────────────────────────────────────────────
    //  CommandEmitter — bus dispatching
    // ──────────────────────────────────────────────────────────────────

    [Test]
    public void CommandEmitter_CombatDecision_ProducesDispatchCommands()
    {
        var bus = new InProcessCommandBus();
        var router = new ExecutionRouter(bus);
        var world = new OrchestrationWorldCache();

        // Populate minimal receiver snapshot data
        // Add 2 combat receivers with EntityIds
        AddCombatReceiverSnapshot(world, new EntityId(1), new RoleId(10));
        AddCombatReceiverSnapshot(world, new EntityId(2), new RoleId(20));
        world.Freeze();

        var decision = new ArbiterDecision
        {
            DomainKey = OrchestrationDomainKeys.Combat,
            ProposalKey = OrchestrationProposalKeys.CombatPrimary,
            ModeChanged = false
        };

        CombatCommand cmd = CombatCommand.Create(CombatCommandType.Hold);
        var ctx = new ExecutionContext { CombatCommand = cmd };

        router.Execute(decision, world, ctx);

        // Collect dispatched commands
        var dispatched = new List<DispatchCombatCommand>();
        bus.Subscribe<DispatchCombatCommand>(c => dispatched.Add(c));
        bus.Flush();

        Assert.That(dispatched.Count, Is.EqualTo(2));
        Assert.That(dispatched[0].ReceiverEntityId, Is.EqualTo(new EntityId(1)));
        Assert.That(dispatched[0].ReceiverRoleId, Is.EqualTo(new RoleId(10)));
        Assert.That(dispatched[0].Payload.Type, Is.EqualTo(CombatCommandType.Hold));
        Assert.That(dispatched[1].ReceiverEntityId, Is.EqualTo(new EntityId(2)));
    }

    [Test]
    public void CommandEmitter_IdleHoldAll_ProducesDispatchCommands()
    {
        var bus = new InProcessCommandBus();
        var router = new ExecutionRouter(bus);
        var world = new OrchestrationWorldCache();

        // Add idle receiver
        AddIdleReceiverSnapshot(world, new EntityId(5), new RoleId(15));
        world.Freeze();

        // Mode change from combat → idle triggers DispatchCombatHoldAll (empty) + DispatchIdlePerUnit
        // Use None domain with ModeChanged to trigger hold-all on both
        var decision = new ArbiterDecision
        {
            DomainKey = OrchestrationDomainKeys.None,
            ProposalKey = OrchestrationProposalKeys.None,
            ModeChanged = true
        };

        var ctx = new ExecutionContext();

        router.Execute(decision, world, ctx);

        var idleDispatched = new List<DispatchIdleCommand>();
        bus.Subscribe<DispatchIdleCommand>(c => idleDispatched.Add(c));
        bus.Flush();

        Assert.That(idleDispatched.Count, Is.EqualTo(1));
        Assert.That(idleDispatched[0].ReceiverEntityId, Is.EqualTo(new EntityId(5)));
        Assert.That(idleDispatched[0].Payload.Type, Is.EqualTo(IdleCommandType.Hold));
    }

    [Test]
    public void CommandBus_Flush_ClearsQueue()
    {
        var bus = new InProcessCommandBus();
        bus.Publish(new DispatchCombatCommand
        {
            ReceiverEntityId = new EntityId(1),
            Payload = CombatCommand.Create(CombatCommandType.Hold)
        });

        int count = 0;
        bus.Subscribe<DispatchCombatCommand>(_ => count++);
        bus.Flush();
        Assert.That(count, Is.EqualTo(1));

        // Second flush should be empty
        count = 0;
        bus.Flush();
        Assert.That(count, Is.EqualTo(0));
    }

    // ──────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a combat receiver snapshot entry to the world cache via reflection.
    /// IMPORTANT: For tests only — bypasses normal SnapshotReceivers flow.
    /// </summary>
    static void AddCombatReceiverSnapshot(OrchestrationWorldCache world, EntityId entityId, RoleId roleId)
    {
        var eids = GetField<List<EntityId>>(world, "_combatReceiverEntityIds");
        var rids = GetField<List<RoleId>>(world, "_combatReceiverRoleIds");
        eids.Add(entityId);
        rids.Add(roleId);
    }

    static void AddIdleReceiverSnapshot(OrchestrationWorldCache world, EntityId entityId, RoleId roleId)
    {
        var eids = GetField<List<EntityId>>(world, "_idleReceiverEntityIds");
        var rids = GetField<List<RoleId>>(world, "_idleReceiverRoleIds");
        eids.Add(entityId);
        rids.Add(roleId);
    }

    sealed class TestCombatStickyDomain : DomainOrchestrator, IDomainArbitrationProfileSource
    {
        public override OrchestrationDomainId DomainId => OrchestrationDomainId.Combat;

        public override void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals)
        {
        }

        public DomainArbitrationProfile GetArbitrationProfile()
        {
            return new DomainArbitrationProfile(stickyPrimary: true);
        }
    }

    sealed class TestIdleDomain : DomainOrchestrator, IDomainArbitrationProfileSource
    {
        public override OrchestrationDomainId DomainId => OrchestrationDomainId.Idle;

        public override void Evaluate(OrchestrationArbiterContext ctx, OrchestrationArbiterProposals proposals)
        {
        }

        public DomainArbitrationProfile GetArbitrationProfile()
        {
            return new DomainArbitrationProfile(stickyPrimary: false);
        }
    }

    static List<Proposal> CreateCombatAndIdleProposals()
    {
        return new List<Proposal>
        {
            new Proposal(
                OrchestrationDomainKeys.Combat,
                OrchestrationProposalKeys.CombatPrimary,
                priority: 100,
                score: 1f),
            new Proposal(
                OrchestrationDomainKeys.Idle,
                OrchestrationProposalKeys.IdleDefault,
                priority: 10,
                score: 0f),
        };
    }

    static void SetField(object target, string fieldName, object value)
    {
        FieldInfo fi = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(fi, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType().Name}");
        fi.SetValue(target, value);
    }

    static void InvokePrivateVoid(object target, string methodName)
    {
        MethodInfo mi = target.GetType().GetMethod(methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(mi, Is.Not.Null, $"Method '{methodName}' not found on {target.GetType().Name}");
        mi.Invoke(target, null);
    }

    static T GetField<T>(object target, string fieldName)
    {
        FieldInfo fi = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(fi, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType().Name}");
        return (T)fi.GetValue(target);
    }
}
