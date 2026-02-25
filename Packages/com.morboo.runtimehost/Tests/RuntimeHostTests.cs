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
            Assert.That(d.ProposalKey, Is.EqualTo(OrchestrationProposalKeys.DomainDefault));
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
            Assert.That(d.ProposalKey, Is.EqualTo(OrchestrationProposalKeys.DomainDefault));
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
        var router = CreateRouterWithBuiltInRoutes(bus);
        var world = new OrchestrationWorldCache();

        // Populate minimal operator snapshot data
        AddOperatorSnapshot(world, new EntityId(1), new RoleId(10));
        AddOperatorSnapshot(world, new EntityId(2), new RoleId(20));
        world.Freeze();

        var decision = new ArbiterDecision
        {
            DomainKey = OrchestrationDomainKeys.Combat,
            ProposalKey = OrchestrationProposalKeys.DomainDefault,
            ModeChanged = false
        };

        var ctx = new ExecutionContext { Command = OrchestrationCommand.Cancel("Test=Combat") };

        router.Execute(decision, world, ctx);

        // Collect dispatched commands
        var dispatched = new List<DispatchOrchestrationCommand>();
        bus.Subscribe<DispatchOrchestrationCommand>(c => dispatched.Add(c));
        bus.Flush();

        Assert.That(dispatched.Count, Is.EqualTo(2));
        Assert.That(dispatched[0].ReceiverEntityId, Is.EqualTo(new EntityId(1)));
        Assert.That(dispatched[0].Payload.Type, Is.EqualTo(OrchestrationCommandType.Cancel));
        Assert.That(dispatched[1].ReceiverEntityId, Is.EqualTo(new EntityId(2)));
    }

    [Test]
    public void CommandEmitter_IdleHoldAll_ProducesDispatchCommands()
    {
        var bus = new InProcessCommandBus();
        var router = CreateRouterWithBuiltInRoutes(bus);
        var world = new OrchestrationWorldCache();

        AddOperatorSnapshot(world, new EntityId(5), new RoleId(15));
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

        var idleDispatched = new List<DispatchOrchestrationCommand>();
        bus.Subscribe<DispatchOrchestrationCommand>(c => idleDispatched.Add(c));
        bus.Flush();

        Assert.That(idleDispatched.Count, Is.EqualTo(1));
        Assert.That(idleDispatched[0].ReceiverEntityId, Is.EqualTo(new EntityId(5)));
        Assert.That(idleDispatched[0].Payload.Type, Is.EqualTo(OrchestrationCommandType.Cancel));
    }

    [Test]
    public void StrategyCombatNoneRoute_RouteExecutionPolicy_CanDisableModeChangeHoldAll()
    {
        var world = new OrchestrationWorldCache();
        AddOperatorSnapshot(world, new EntityId(11), new RoleId(101));
        AddOperatorSnapshot(world, new EntityId(22), new RoleId(202));
        world.Freeze();

        var decision = new ArbiterDecision
        {
            DomainKey = OrchestrationDomainKeys.None,
            ProposalKey = OrchestrationProposalKeys.None,
            ModeChanged = true
        };

        var ctx = new ExecutionContext();

        // Baseline (null policy) emits unified cancel-all for all operators on mode change.
        var baselineBus = new InProcessCommandBus();
        var baselineRouter = new ExecutionRouter(baselineBus);
        baselineRouter.RegisterRoute(new ExecutionRouteRegistration(
            OrchestrationDomainId.None,
            new StrategyCombatNoneExecutionRoute().Execute));

        var baselineCommands = new List<DispatchOrchestrationCommand>();
        baselineBus.Subscribe<DispatchOrchestrationCommand>(c => baselineCommands.Add(c));

        baselineRouter.Execute(decision, world, ctx);
        baselineBus.Flush();

        Assert.That(baselineCommands.Count, Is.EqualTo(2),
            "Default StrategyCombat none-route behavior should emit cancel-all for all operators on mode change.");
        Assert.That(baselineCommands[0].Payload.Type, Is.EqualTo(OrchestrationCommandType.Cancel));

        // Policy override disables cancel-all emission for None route.
        StrategyCombatRouteExecutionPolicyAsset policy = ScriptableObject.CreateInstance<StrategyCombatRouteExecutionPolicyAsset>();
        try
        {
            ConfigureNoneRoutePolicy(policy, emitCombatHoldOnModeChange: false, emitIdleHoldOnModeChange: false);

            var policyBus = new InProcessCommandBus();
            var policyRouter = new ExecutionRouter(policyBus);
            policyRouter.RegisterRoute(new ExecutionRouteRegistration(
                OrchestrationDomainId.None,
                new StrategyCombatNoneExecutionRoute(policy).Execute));

            var policyCommands = new List<DispatchOrchestrationCommand>();
            policyBus.Subscribe<DispatchOrchestrationCommand>(c => policyCommands.Add(c));

            policyRouter.Execute(decision, world, ctx);
            policyBus.Flush();

            Assert.That(policyCommands.Count, Is.EqualTo(0),
                "None-route policy should be able to disable cancel-all emission on mode change.");
        }
        finally
        {
            Object.DestroyImmediate(policy);
        }
    }

    [Test]
    public void CommandBus_Flush_ClearsQueue()
    {
        var bus = new InProcessCommandBus();
        bus.Publish(new DispatchOrchestrationCommand
        {
            ReceiverEntityId = new EntityId(1),
            Payload = OrchestrationCommand.Cancel("Test")
        });

        int count = 0;
        bus.Subscribe<DispatchOrchestrationCommand>(_ => count++);
        bus.Flush();
        Assert.That(count, Is.EqualTo(1));

        // Second flush should be empty
        count = 0;
        bus.Flush();
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void OrchestrationLoop_BuildsTwoPipelines_AndTreatsPipelineComponentAsCompositionOwner()
    {
        var loopGo = new GameObject("TestOrchestrationLoop");
        var p1Go = new GameObject("Pipeline1");
        var p2Go = new GameObject("Pipeline2");

        try
        {
            var loop = loopGo.AddComponent<OrchestrationLoop>();

            var arbiter1 = p1Go.AddComponent<OrchestrationArbiter>();
            var arbiter2 = p2Go.AddComponent<OrchestrationArbiter>();
            var pipeline1 = p1Go.AddComponent<OrchestrationPipelineComponent>();
            var pipeline2 = p2Go.AddComponent<OrchestrationPipelineComponent>();

            // Composition owner should still be valid when the component is disabled;
            // only the GameObject active state is relevant for scene composition.
            pipeline2.enabled = false;

            SetField(pipeline1, "arbiter", arbiter1);
            SetField(pipeline1, "domainOrchestrators", System.Array.Empty<DomainOrchestrator>());
            SetField(pipeline2, "arbiter", arbiter2);
            SetField(pipeline2, "domainOrchestrators", System.Array.Empty<DomainOrchestrator>());

            SetField(loop, "pipelines", new[] { pipeline1, pipeline2 });
            SetField(loop, "sharedRelations", null);

            InvokePrivateVoid(loop, "BuildAndApplyConfiguredPipelines");

            Assert.That(loop.ConfiguredPipelines.Count, Is.EqualTo(2));
            Assert.That(GetField<int>(loop, "_runtimePipelineCount"), Is.EqualTo(2),
                "Loop host should build two runtime pipelines for C04B/B7 multi-pipeline path.");
            Assert.That(GetField<OrchestrationPipeline>(loop, "_primaryPipeline"), Is.Not.Null,
                "Loop must preserve compatibility primary pipeline (index 0) while adapters still read loop-level context properties.");
            var runtimePipelines = GetField<OrchestrationPipeline[]>(loop, "_runtimePipelines");
            Assert.That(runtimePipelines, Is.Not.Null);
            Assert.That(runtimePipelines.Length, Is.EqualTo(2));
            Assert.That(ReferenceEquals(runtimePipelines[0].CommandBus, loop.CommandBus), Is.True);
            Assert.That(ReferenceEquals(runtimePipelines[1].CommandBus, loop.CommandBus), Is.True,
                "B7 multi-pipeline path should use the shared loop command bus so existing adapters can consume commands from all pipelines.");
            Assert.That(GetField<bool>(loop, "_warnedInvalidConfiguredPipelines"), Is.False,
                "Disabled OrchestrationPipelineComponent must not be treated as invalid composition input.");
        }
        finally
        {
            Object.DestroyImmediate(loopGo);
            Object.DestroyImmediate(p1Go);
            Object.DestroyImmediate(p2Go);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────

    static ExecutionRouter CreateRouterWithBuiltInRoutes(InProcessCommandBus bus)
    {
        var router = new ExecutionRouter(bus);
        router.RegisterRoute(new ExecutionRouteRegistration(OrchestrationDomainId.Combat, TestCombatRoute));
        router.RegisterRoute(new ExecutionRouteRegistration(OrchestrationDomainId.Idle, TestIdleRoute));
        router.RegisterRoute(new ExecutionRouteRegistration(OrchestrationDomainId.None, TestNoneRoute));
        return router;
    }

    static void TestCombatRoute(IExecutionRouteHost host, ArbiterDecision decision, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        if (host == null)
            return;

        PublishOrchestrationCommandForAll(host, ctx.Command, world);
        if (decision.ModeChanged)
            PublishCancelForAll(host, world, "Test=CombatModeChanged");
    }

    static void TestIdleRoute(IExecutionRouteHost host, ArbiterDecision decision, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        if (host == null)
            return;

        PublishCancelForAll(host, world, "Test=Idle");
        if (decision.ModeChanged)
            PublishCancelForAll(host, world, "Test=IdleActive");
    }

    static void TestNoneRoute(IExecutionRouteHost host, ArbiterDecision decision, OrchestrationWorldCache world, ExecutionContext ctx)
    {
        if (host == null)
            return;

        if (decision.ModeChanged)
            PublishCancelForAll(host, world, "Test=None");
    }

    static void PublishOrchestrationCommandForAll(IExecutionRouteHost host, OrchestrationCommand cmd, OrchestrationWorldCache world)
    {
        int count = world.OperatorCount;
        for (int i = 0; i < count; i++)
        {
            EntityId eid = world.GetOperatorEntityId(i);
            if (eid.IsNone)
                continue;

            host.PublishCommand(new DispatchOrchestrationCommand
            {
                ReceiverEntityId = eid,
                Payload = cmd
            });
        }
    }

    static void PublishCancelForAll(IExecutionRouteHost host, OrchestrationWorldCache world, string debugLabel)
    {
        PublishOrchestrationCommandForAll(host, OrchestrationCommand.Cancel(debugLabel), world);
    }

    static void AddOperatorSnapshot(OrchestrationWorldCache world, EntityId entityId, RoleId roleId)
    {
        var eids = GetField<List<EntityId>>(world, "_operatorEntityIds");
        var rids = GetField<List<RoleId>>(world, "_operatorRoleIds");
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
                OrchestrationProposalKeys.DomainDefault,
                priority: 100,
                score: 1f),
            new Proposal(
                OrchestrationDomainKeys.Idle,
                OrchestrationProposalKeys.DomainDefault,
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

    static void ConfigureNoneRoutePolicy(
        StrategyCombatRouteExecutionPolicyAsset policy,
        bool emitCombatHoldOnModeChange,
        bool emitIdleHoldOnModeChange)
    {
        Assert.That(policy, Is.Not.Null);

        var section = new StrategyCombatRouteExecutionPolicyAsset.NoneRoutePolicySection();
        SetField(section, "emitCombatHoldOnModeChange", emitCombatHoldOnModeChange);
        SetField(section, "emitIdleHoldOnModeChange", emitIdleHoldOnModeChange);
        SetField(policy, "noneRoute", section);
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

    // ──────────────────────────────────────────────────────────────────
    //  C06 — ActorCapabilitySnapshot
    // ──────────────────────────────────────────────────────────────────

    static ActorCapability CreateCapability(string capName)
    {
        var cap = ScriptableObject.CreateInstance<ActorCapability>();
        cap.name = capName;
        return cap;
    }

    [Test]
    public void ActorCapabilitySnapshot_Has_ReturnsTrueForPresentCapability()
    {
        var melee = CreateCapability("Melee");
        try
        {
            var snap = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { melee } };
            Assert.That(snap.Has(melee), Is.True);
        }
        finally { Object.DestroyImmediate(melee); }
    }

    [Test]
    public void ActorCapabilitySnapshot_Has_ReturnsFalseForAbsentCapability()
    {
        var melee = CreateCapability("Melee");
        var ranged = CreateCapability("Ranged");
        try
        {
            var snap = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { melee } };
            Assert.That(snap.Has(ranged), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(ranged);
        }
    }

    [Test]
    public void ActorCapabilitySnapshot_HasAny_ReturnsTrueWhenOnePresent()
    {
        var melee = CreateCapability("Melee");
        var ranged = CreateCapability("Ranged");
        var fly = CreateCapability("Fly");
        try
        {
            var snap = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { melee } };
            var query = new List<ActorCapability> { ranged, melee, fly };
            Assert.That(snap.HasAny(query), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(ranged);
            Object.DestroyImmediate(fly);
        }
    }

    [Test]
    public void ActorCapabilitySnapshot_MergeFrom_UnionsCapabilities()
    {
        var melee = CreateCapability("Melee");
        var ranged = CreateCapability("Ranged");
        try
        {
            var a = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { melee } };
            var b = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { ranged } };
            a.MergeFrom(b);
            Assert.That(a.Has(melee), Is.True, "Original capability should be retained.");
            Assert.That(a.Has(ranged), Is.True, "Merged capability should be present.");
        }
        finally
        {
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(ranged);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  C06 — ActorCapabilityEngagementPolicy
    // ──────────────────────────────────────────────────────────────────

    [Test]
    public void EngagementPolicy_CanEngage_SourceHasRequired_ReturnsTrue()
    {
        var fly = CreateCapability("Fly");
        var ranged = CreateCapability("Ranged");
        var policy = ScriptableObject.CreateInstance<ActorCapabilityEngagementPolicy>();
        try
        {
            var rules = new List<ActorCapabilityEngagementPolicy.EngagementRule>
            {
                new ActorCapabilityEngagementPolicy.EngagementRule
                {
                    targetCapability = fly,
                    sourceRequiresAny = new List<ActorCapability> { ranged }
                }
            };
            SetField(policy, "rules", rules);

            var source = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { ranged } };
            var target = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { fly } };
            Assert.That(policy.CanEngage(source, target), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(fly);
            Object.DestroyImmediate(ranged);
            Object.DestroyImmediate(policy);
        }
    }

    [Test]
    public void EngagementPolicy_CanEngage_SourceLacksRequired_ReturnsFalse()
    {
        var fly = CreateCapability("Fly");
        var ranged = CreateCapability("Ranged");
        var melee = CreateCapability("Melee");
        var policy = ScriptableObject.CreateInstance<ActorCapabilityEngagementPolicy>();
        try
        {
            var rules = new List<ActorCapabilityEngagementPolicy.EngagementRule>
            {
                new ActorCapabilityEngagementPolicy.EngagementRule
                {
                    targetCapability = fly,
                    sourceRequiresAny = new List<ActorCapability> { ranged }
                }
            };
            SetField(policy, "rules", rules);

            var source = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { melee } };
            var target = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { fly } };
            Assert.That(policy.CanEngage(source, target), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(fly);
            Object.DestroyImmediate(ranged);
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(policy);
        }
    }

    [Test]
    public void EngagementPolicy_CanEngage_NoRulesForTarget_ReturnsTrue()
    {
        var fly = CreateCapability("Fly");
        var melee = CreateCapability("Melee");
        var ranged = CreateCapability("Ranged");
        var policy = ScriptableObject.CreateInstance<ActorCapabilityEngagementPolicy>();
        try
        {
            var rules = new List<ActorCapabilityEngagementPolicy.EngagementRule>
            {
                new ActorCapabilityEngagementPolicy.EngagementRule
                {
                    targetCapability = fly,
                    sourceRequiresAny = new List<ActorCapability> { ranged }
                }
            };
            SetField(policy, "rules", rules);

            var source = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { melee } };
            var target = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { melee } };
            Assert.That(policy.CanEngage(source, target), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(fly);
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(ranged);
            Object.DestroyImmediate(policy);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    //  C06 — ActorCapabilityPolicyGate
    // ──────────────────────────────────────────────────────────────────

    sealed class TestGatedPolicy : IActorCapabilityGatedPolicy
    {
        public List<ActorCapability> Caps = new List<ActorCapability>();
        public IReadOnlyList<ActorCapability> RequiredCapabilities => Caps;
    }

    [Test]
    public void PolicyGate_CanApply_ActorHasAllRequired_ReturnsTrue()
    {
        var melee = CreateCapability("Melee");
        var ranged = CreateCapability("Ranged");
        try
        {
            var gated = new TestGatedPolicy { Caps = new List<ActorCapability> { melee, ranged } };
            var snap = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { melee, ranged } };
            Assert.That(ActorCapabilityPolicyGate.CanApply(gated, snap), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(ranged);
        }
    }

    [Test]
    public void PolicyGate_CanApply_ActorLacksRequired_ReturnsFalse()
    {
        var melee = CreateCapability("Melee");
        var ranged = CreateCapability("Ranged");
        try
        {
            var gated = new TestGatedPolicy { Caps = new List<ActorCapability> { melee, ranged } };
            var snap = new ActorCapabilitySnapshot { Base = new List<ActorCapability> { melee } };
            Assert.That(ActorCapabilityPolicyGate.CanApply(gated, snap), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(melee);
            Object.DestroyImmediate(ranged);
        }
    }

    [Test]
    public void PolicyGate_CanApply_EmptyRequirements_ReturnsTrue()
    {
        var gated = new TestGatedPolicy();
        var snap = new ActorCapabilitySnapshot();
        Assert.That(ActorCapabilityPolicyGate.CanApply(gated, snap), Is.True);
    }
}
