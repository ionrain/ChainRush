using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

public sealed class OrchestrationImplementationFitnessTests
{
    const string StrategyCombatOrchestrationRoot = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration";
    const string StrategyCombatDomainsRoot = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains";
    const string StrategyCombatArbiterPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs";
    const string StrategyCombatRouterPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouter.cs";
    const string StrategyCombatLoopPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/OrchestrationLoop.cs";
    const string RuntimeHostArbiterPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs";
    const string RuntimeHostBindingContributorPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/DomainArbiterBindingContributor.cs";
    const string RuntimeHostDomainOrchestratorPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/DomainOrchestrator.cs";
    const string RuntimeHostRouterPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouter.cs";
    const string RuntimeHostRouteContributorPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/DomainExecutionRouteContributor.cs";
    const string RuntimeHostExecutionContextPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionContext.cs";
    const string RuntimeHostExecutionRouteRegistrationPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouteRegistration.cs";
    const string StrategyCombatCombatExecutionRoutePath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatCombatExecutionRoute.cs";
    const string StrategyCombatIdleExecutionRoutePath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatIdleExecutionRoute.cs";
    const string CombatDomainComponentPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Combat/CombatDomainComponent.cs";
    const string IdleDomainComponentPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Idle/IdleDomainComponent.cs";
    const string StrategyCombatCombatTargetProviderPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Combat/Targeting/CombatTargetProvider.cs";
    const string StrategyCombatIdleTargetProviderPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains/Idle/Targeting/IdleTargetProvider.cs";
    const string RuntimeHostDomainTargetProviderPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Targeting/DomainTargetProvider.cs";
    const string RuntimeHostDomainComponentPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Components/DomainComponent.cs";
    const string RuntimeHostDomainOrchestratorComponentPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Components/DomainOrchestratorComponent.cs";
    const string RuntimeHostDomainOrchestratorCompositionPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Components/DomainOrchestratorComposition.cs";
    const string RuntimeHostDomainRouteExecutionPolicyPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/DomainRouteExecutionPolicy.cs";
    const string RuntimeHostDomainRouteExecutionPolicyConsumerPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/IDomainRouteExecutionPolicyConsumer.cs";
    const string StrategyCombatNoneExecutionRoutePath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatNoneExecutionRoute.cs";
    const string StrategyCombatUnknownRouteFallbackExecutionRoutePath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/StrategyCombatUnknownRouteFallbackExecutionRoute.cs";
    const string RuntimeHostDomainRouteExecutionPolicyProviderPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/DomainRouteExecutionPolicyProvider.cs";
    const string DomainRouteExecutionPolicyBridgePath = "Assets/Scripts/MorbooBridge/Orchestration/Composition/DomainRouteExecutionPolicyBridge.cs";
    const string RuntimeHostLoopPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/OrchestrationLoop.cs";
    const string RuntimeHostPipelinePath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/OrchestrationPipeline.cs";
    const string RuntimeHostPipelineComponentPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/OrchestrationPipelineComponent.cs";
    const string StrategyCombatArbiterBindingAppliersPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Arbitration/StrategyCombatArbiterBindingAppliers.cs";
    const string StrategyCombatCombatCommandAdapterPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Adapters/CombatCommandAdapter.cs";
    const string StrategyCombatIdleCommandAdapterPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Adapters/IdleCommandAdapter.cs";
    const string FrameworkArbiterContractPath = "Packages/com.morboo.framework/Runtime/Decision/IArbiter.cs";

    static readonly Regex PublishCallRegex = new Regex(@"\bPublish\s*\(", RegexOptions.Compiled);
    static readonly Regex EntityTransformResolverRegex = new Regex(@"\bEntityTransformResolver\b", RegexOptions.Compiled);
    static readonly Regex ApplyCallRegex = new Regex(@"\.\s*(ApplyCombatCommand|ApplyIdleCommand)\s*\(", RegexOptions.Compiled);
    static readonly Regex ITickSourceRegex = new Regex(@"\bITickSource\b", RegexOptions.Compiled);
    static readonly Regex RealtimeSchedulerRegex = new Regex(@"\bRealtimeScheduler\b", RegexOptions.Compiled);
    static readonly Regex WorldCacheDowncastRegex = new Regex(@"\bas\s+OrchestrationWorldCache\b", RegexOptions.Compiled);
    static readonly Regex SerializedUntypedDependencyHolderRegex =
        new Regex(@"\[SerializeField\]\s*(?:\[[^\]]+\]\s*)*(?:private|public|protected|internal)?\s*(GameObject|MonoBehaviour|Component)\b",
            RegexOptions.Compiled);
    static readonly Regex DomainOnboardingDescriptorRegex =
        new Regex(@"\b(IDomainModule|DomainModule|IDomainRegistration|DomainRegistration|DomainDescriptor|DomainOnboardingDescriptor)\b",
            RegexOptions.Compiled);
    static readonly Regex HardcodedDomainBranchingRegex =
        new Regex(@"\b(OrchestrationDomainKeys\.(Combat|Idle)|DispatchCombatCommand|DispatchIdleCommand|CombatCommand|IdleCommand|ICombatRolePolicyMapSource|IIdleRolePolicyMapSource|ICombatRoleConstraintsMapSource)\b",
            RegexOptions.Compiled);
    static readonly Regex ProposalCollectorTypeRegex =
        new Regex(@"\bOrchestrationProposalCollector\b", RegexOptions.Compiled);
    static readonly Regex ProposalCollectorLegacyArbitrationInputCallRegex =
        new Regex(@"\b_proposalCollector\s*\.\s*ToArbitrationInput\s*\(", RegexOptions.Compiled);
    static readonly Regex LegacyProposalsArbitrationInputCallRegex =
        new Regex(@"\b_proposals\s*\.\s*ToArbitrationInput\s*\(", RegexOptions.Compiled);
    static readonly Regex ArbiterCollectProposalsCallRegex =
        new Regex(@"\b(?:_cachedDomains\s*\[[^\]]+\]|_cachedDomainRegistrations\s*\[[^\]]+\]\s*\.\s*Orchestrator)\s*\.\s*CollectProposals\s*\(",
            RegexOptions.Compiled);
    static readonly Regex ArbiterDirectEvaluateCallRegex =
        new Regex(@"\b_cachedDomains\s*\[[^\]]+\]\s*\.\s*Evaluate\s*\(", RegexOptions.Compiled);
    static readonly Regex DomainOrchestratorImportLegacyCallRegex =
        new Regex(@"\bcollector\s*\.\s*ImportLegacy\s*\(", RegexOptions.Compiled);
    static readonly Regex ArbiterCollectorArbitrateCallRegex =
        new Regex(@"\bArbitrate\s*\(\s*_proposalCollector\s*\.\s*Entries\s*,\s*_proposalCollector\s*\.\s*ThreatPresent\s*,\s*now\s*\)", RegexOptions.Compiled);
    static readonly Regex ArbiterCollectorProposalIterationRegex =
        new Regex(@"\bproposals\s*\.\s*Count\b|\bproposals\s*\[[^\]]+\]", RegexOptions.Compiled);
    static readonly Regex IArbiterProposalListOverloadRegex =
        new Regex(@"\bArbiterDecision\s+Arbitrate\s*\(\s*IReadOnlyList\s*<\s*Proposal\s*>\s+\w+\s*,\s*bool\s+\w+\s*,\s*float\s+\w+\s*\)", RegexOptions.Compiled);
    static readonly Regex DomainRegistrationTypeRegex =
        new Regex(@"\bDomainRegistration\b", RegexOptions.Compiled);
    static readonly Regex DomainGetRegistrationCallRegex =
        new Regex(@"\.\s*GetRegistration\s*\(", RegexOptions.Compiled);
    static readonly Regex DomainGetRegistrationMethodRegex =
        new Regex(@"\bDomainRegistration\s+GetRegistration\s*\(", RegexOptions.Compiled);
    static readonly Regex CachedDomainRegistrationPolicyProviderRegex =
        new Regex(@"\bregistration\s*\.\s*(IdleRolePolicyMapSource|CombatRolePolicyMapSource|CombatRoleConstraintsMapSource|ArbiterBindingContributor)\b", RegexOptions.Compiled);
    static readonly Regex ExecutionRouteRegistrationTypeRegex =
        new Regex(@"\bExecutionRouteRegistration\b", RegexOptions.Compiled);
    static readonly Regex RegisterRouteCallRegex =
        new Regex(@"\bRegisterRoute\s*\(", RegexOptions.Compiled);
    static readonly Regex HiddenSerializedDomainOrchestratorsFieldRegex =
        new Regex(@"\[(?:HideInInspector\s*,\s*SerializeField|SerializeField\s*,\s*HideInInspector)\]\s*DomainOrchestrator\s*\[\s*\]\s*domainOrchestrators\s*;",
            RegexOptions.Compiled);
    static readonly Regex DomainKeyCombatIdleTokenRegex =
        new Regex(@"\b(?:OrchestrationDomainKeys|OrchestrationDomainId)\.(Combat|Idle)\b", RegexOptions.Compiled);
    static readonly Regex DomainNameBranchingConstructRegex =
        new Regex(@"\b(if|else\s+if)\s*\([^)]*\b(?:OrchestrationDomainKeys|OrchestrationDomainId)\.(Combat|Idle)\b[^)]*\)|\bcase\s+(?:OrchestrationDomainKeys|OrchestrationDomainId)\.(Combat|Idle)\b",
            RegexOptions.Compiled);
    static readonly Regex ArbiterBindingTargetKindRegex =
        new Regex(@"\bDomainArbiterBindingTargetKind\b", RegexOptions.Compiled);
    static readonly Regex RuntimeHostBindingTargetAppliersTypeRegex =
        new Regex(@"\bclass\s+DomainArbiterBindingTargetAppliers\b", RegexOptions.Compiled);
    static readonly Regex StrategyCombatBindingTargetAppliersTypeRegex =
        new Regex(@"\bclass\s+StrategyCombatArbiterBindingAppliers\b", RegexOptions.Compiled);
    static readonly Regex RuntimeHostConsumerKeysTypeRegex =
        new Regex(@"\bRuntimeHostArbiterBindingConsumerKeys\b", RegexOptions.Compiled);
    static readonly Regex OrchestrationLoopGetComponentFallbackRegex =
        new Regex(@"\bGetComponent\s*<\s*OrchestrationLoop\s*>\s*\(", RegexOptions.Compiled);
    static readonly Regex OrchestrationLoopUntypedCastRegex =
        new Regex(@"\bas\s+OrchestrationLoop\b", RegexOptions.Compiled);

    [Test]
    public void StrategyCombat_Domains_DoNotPublishCommandsDirectly()
    {
        Assert.That(Directory.Exists(StrategyCombatDomainsRoot), Is.True,
            $"Missing directory: {StrategyCombatDomainsRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(StrategyCombatDomainsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = PublishCallRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value.Trim()}'");
        }

        Assert.That(violations, Is.Empty,
            "Domains must not publish commands directly:\n" + string.Join("\n", violations));
    }

    [Test]
    public void StrategyCombat_Domains_DoNotResolveEntityTransforms()
    {
        Assert.That(Directory.Exists(StrategyCombatDomainsRoot), Is.True,
            $"Missing directory: {StrategyCombatDomainsRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(StrategyCombatDomainsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = EntityTransformResolverRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "Domains must not resolve EntityId -> Transform directly:\n" + string.Join("\n", violations));
    }

    [Test]
    public void StrategyCombat_ExecutionRouter_DoesNotApplyCommandsDirectly()
    {
        Assert.That(File.Exists(StrategyCombatRouterPath), Is.True,
            $"Missing file: {StrategyCombatRouterPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatRouterPath));
        Match m = ApplyCallRegex.Match(stripped);

        Assert.That(m.Success, Is.False,
            $"ExecutionRouter must not call Apply*Command directly: {m.Value}");
    }

    [Test]
    public void StrategyCombat_Arbiter_DoesNotPublishCommands()
    {
        Assert.That(File.Exists(StrategyCombatArbiterPath), Is.True,
            $"Missing file: {StrategyCombatArbiterPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatArbiterPath));

        // IMPORTANT: _eventBus.Publish is allowed (mode-changed events).
        // Only command-bus publishing (PublishCommand, _commandBus.Publish) is prohibited.
        Match cmdPublish = Regex.Match(stripped, @"\b(PublishCommand|_commandBus\s*\.\s*Publish)\s*\(");

        Assert.That(cmdPublish.Success, Is.False,
            $"OrchestrationArbiter must not publish commands directly: {cmdPublish.Value}");
    }

    [Test]
    public void StrategyCombat_OrchestrationLoop_UsesTickSourceAbstraction()
    {
        Assert.That(File.Exists(StrategyCombatLoopPath), Is.True,
            $"Missing file: {StrategyCombatLoopPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatLoopPath));

        Assert.That(ITickSourceRegex.IsMatch(stripped), Is.True,
            "OrchestrationLoop must depend on ITickSource abstraction.");

        Assert.That(RealtimeSchedulerRegex.IsMatch(stripped), Is.False,
            "OrchestrationLoop must not hard-reference RealtimeScheduler type.");
    }

    [Test]
    public void StrategyCombat_ExecutionRoutes_UseDispatchContracts()
    {
        Assert.That(File.Exists(StrategyCombatCombatExecutionRoutePath), Is.True,
            $"Missing file: {StrategyCombatCombatExecutionRoutePath}");
        Assert.That(File.Exists(StrategyCombatIdleExecutionRoutePath), Is.True,
            $"Missing file: {StrategyCombatIdleExecutionRoutePath}");
        Assert.That(File.Exists(StrategyCombatNoneExecutionRoutePath), Is.True,
            $"Missing file: {StrategyCombatNoneExecutionRoutePath}");
        Assert.That(File.Exists(StrategyCombatUnknownRouteFallbackExecutionRoutePath), Is.True,
            $"Missing file: {StrategyCombatUnknownRouteFallbackExecutionRoutePath}");

        string stripped = string.Concat(
            StripCommentsAndStrings(File.ReadAllText(StrategyCombatCombatExecutionRoutePath)), "\n",
            StripCommentsAndStrings(File.ReadAllText(StrategyCombatIdleExecutionRoutePath)), "\n",
            StripCommentsAndStrings(File.ReadAllText(StrategyCombatNoneExecutionRoutePath)), "\n",
            StripCommentsAndStrings(File.ReadAllText(StrategyCombatUnknownRouteFallbackExecutionRoutePath)));

        Assert.That(Regex.IsMatch(stripped, @"\bDispatchCombatCommand\b"), Is.True,
            "StrategyCombat route executors must emit DispatchCombatCommand.");

        Assert.That(Regex.IsMatch(stripped, @"\bDispatchIdleCommand\b"), Is.True,
            "StrategyCombat route executors must emit DispatchIdleCommand.");
    }

    [Test]
    public void RuntimeHost_Arbiter_UsesProposalCollectorSeam()
    {
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");
        Assert.That(File.Exists(RuntimeHostDomainOrchestratorPath), Is.True,
            $"Missing file: {RuntimeHostDomainOrchestratorPath}");
        Assert.That(File.Exists(FrameworkArbiterContractPath), Is.True,
            $"Missing file: {FrameworkArbiterContractPath}");

        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));
        string domainOrchestratorStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostDomainOrchestratorPath));
        string arbiterContractStripped = StripCommentsAndStrings(File.ReadAllText(FrameworkArbiterContractPath));

        Assert.That(ProposalCollectorTypeRegex.IsMatch(arbiterStripped), Is.True,
            "OrchestrationArbiter must declare/use OrchestrationProposalCollector in C03 collector seam.");

        Assert.That(ArbiterCollectProposalsCallRegex.IsMatch(arbiterStripped), Is.True,
            "OrchestrationArbiter must poll domains via CollectProposals() seam in C03.");

        Assert.That(ArbiterDirectEvaluateCallRegex.IsMatch(arbiterStripped), Is.False,
            "OrchestrationArbiter still calls domain.Evaluate() directly; use CollectProposals() seam.");

        Assert.That(ArbiterCollectorArbitrateCallRegex.IsMatch(arbiterStripped), Is.True,
            "OrchestrationArbiter must use proposal-collector arbitration path in C04.");

        Assert.That(LegacyProposalsArbitrationInputCallRegex.IsMatch(arbiterStripped), Is.False,
            "OrchestrationArbiter still arbitrates directly from legacy _proposals container.");

        Assert.That(ProposalCollectorLegacyArbitrationInputCallRegex.IsMatch(arbiterStripped), Is.False,
            "OrchestrationArbiter still arbitrates from collector via legacy ArbitrationInput adapter in C04 path.");

        Assert.That(DomainOrchestratorImportLegacyCallRegex.IsMatch(domainOrchestratorStripped), Is.True,
            "DomainOrchestrator compatibility producer seam must import legacy proposals into collector.");

        Assert.That(ArbiterCollectorProposalIterationRegex.IsMatch(arbiterStripped), Is.True,
            "Arbiter proposal-list path must iterate proposal collector entries (collector.Count/Get).");

        Assert.That(IArbiterProposalListOverloadRegex.IsMatch(arbiterContractStripped), Is.True,
            "IArbiter must expose proposal-list arbitration overload in C04.");
    }

    [Test]
    public void RuntimeHost_Arbiter_ProposalListLoop_HasNoDirectCombatIdleBranching()
    {
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");

        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));
        string proposalArbitrateBody = ExtractMethodBody(
            arbiterStripped,
            "public ArbiterDecision Arbitrate(IReadOnlyList<Proposal> proposals, bool threatPresent, float now)");

        Assert.That(string.IsNullOrEmpty(proposalArbitrateBody), Is.False,
            "Could not extract canonical proposal-list Arbitrate(...) body.");

        Assert.That(Regex.IsMatch(proposalArbitrateBody, @"\bIsStickyPrimaryProposal\s*\("), Is.True,
            "Canonical proposal-list Arbitrate(...) must route domain-specific sticky classification through explicit transitional seam.");

        Assert.That(DomainKeyCombatIdleTokenRegex.IsMatch(proposalArbitrateBody), Is.False,
            "Canonical proposal-list Arbitrate(...) still contains direct Combat/Idle domain tokens; keep them isolated in transitional classifier seam only.");
    }

    [Test]
    public void RuntimeHost_Arbiter_StickyClassifier_UsesProfileCache_NotDomainNames()
    {
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");

        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));
        string classifierBody = ExtractMethodBody(
            arbiterStripped,
            "bool IsStickyPrimaryProposal(in Proposal proposal)");
        string domainKeyLookupBody = ExtractMethodBody(
            arbiterStripped,
            "bool IsStickyPrimaryDomainKey(OrchestrationDomainId domainKey)");

        Assert.That(string.IsNullOrEmpty(classifierBody), Is.False,
            "Could not extract IsStickyPrimaryProposal(...) body.");
        Assert.That(string.IsNullOrEmpty(domainKeyLookupBody), Is.False,
            "Could not extract IsStickyPrimaryDomainKey(...) body.");

        Assert.That(Regex.IsMatch(classifierBody, @"\bIsStickyPrimaryDomainKey\s*\("), Is.True,
            "Sticky classifier must delegate to cached domain arbitration profile lookup.");
        Assert.That(DomainKeyCombatIdleTokenRegex.IsMatch(classifierBody), Is.False,
            "Sticky classifier must not hardcode Combat/Idle domain names.");
        Assert.That(DomainKeyCombatIdleTokenRegex.IsMatch(domainKeyLookupBody), Is.False,
            "Sticky domain-key lookup must remain metadata/cache based, not domain-name based.");
        Assert.That(Regex.IsMatch(domainKeyLookupBody, @"_stickyPrimaryDomainKeys"), Is.True,
            "Sticky domain-key lookup should read cached sticky domain keys collected from domain profiles.");
    }

    [Test]
    public void RuntimeHost_Arbiter_UsesDomainRegistrationCache_ForDomainCapabilities()
    {
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");
        Assert.That(File.Exists(RuntimeHostDomainOrchestratorPath), Is.True,
            $"Missing file: {RuntimeHostDomainOrchestratorPath}");

        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));
        string domainOrchestratorStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostDomainOrchestratorPath));

        Assert.That(DomainRegistrationTypeRegex.IsMatch(arbiterStripped), Is.True,
            "OrchestrationArbiter should use DomainRegistration cache in C04A onboarding seam.");

        Assert.That(DomainGetRegistrationCallRegex.IsMatch(arbiterStripped), Is.True,
            "OrchestrationArbiter must build cached registrations via DomainOrchestrator.GetRegistration().");

        Assert.That(CachedDomainRegistrationPolicyProviderRegex.IsMatch(arbiterStripped), Is.True,
            "Policy-map refresh should read cached domain capabilities from DomainRegistration (provider slots or contributor seam), not rediscover via per-tick host-side casts.");

        Assert.That(Regex.IsMatch(arbiterStripped, @"\bContributeArbiterBindingTargets\s*\("), Is.True,
            "Arbiter binding target registry should be sourced from cached domain binding contributors, not hardcoded arbiter init.");

        Assert.That(DomainRegistrationTypeRegex.IsMatch(domainOrchestratorStripped), Is.True,
            "DomainOrchestrator should expose DomainRegistration seam in C04A.");

        Assert.That(DomainGetRegistrationMethodRegex.IsMatch(domainOrchestratorStripped), Is.True,
            "DomainOrchestrator must implement/declare GetRegistration() seam in C04A.");

        Assert.That(Regex.IsMatch(domainOrchestratorStripped,
            @"\b(IIdleRolePolicyMapSource|ICombatRolePolicyMapSource|ICombatRoleConstraintsMapSource)\b"), Is.False,
            "Base DomainOrchestrator should not know StrategyCombat policy-map source interfaces; domains must provide binding contributors explicitly.");
    }

    [Test]
    public void RuntimeHost_Arbiter_UsesBindingApplierRegistry_NotTargetKindSwitch()
    {
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");

        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));
        string refreshBody = ExtractMethodBody(arbiterStripped, "void RefreshPolicyMapsFromDomains()");

        Assert.That(string.IsNullOrEmpty(refreshBody), Is.False,
            "Could not extract RefreshPolicyMapsFromDomains() body.");

        Assert.That(Regex.IsMatch(arbiterStripped, @"\bTryResolveArbiterBindingApplier\s*\("), Is.True,
            "Arbiter should resolve binding appliers from cached registry, not target-kind enums.");
        Assert.That(ArbiterBindingTargetKindRegex.IsMatch(arbiterStripped), Is.False,
            "OrchestrationArbiter should not depend on DomainArbiterBindingTargetKind after key->applier registry step.");
        Assert.That(Regex.IsMatch(refreshBody, @"\bapply\s*\(\s*this\s*,\s*entry\s*\.\s*Asset\s*\)"), Is.True,
            "Policy-map refresh should invoke cached binding appliers.");
    }

    [Test]
    public void BindingAppliers_AreOwnedBy_StrategyCombat_Not_RuntimeHost()
    {
        Assert.That(File.Exists(RuntimeHostBindingContributorPath), Is.True,
            $"Missing file: {RuntimeHostBindingContributorPath}");
        Assert.That(File.Exists(StrategyCombatArbiterBindingAppliersPath), Is.True,
            $"Missing file: {StrategyCombatArbiterBindingAppliersPath}");

        string runtimeHostContributorStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostBindingContributorPath));
        string strategyCombatAppliersStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatArbiterBindingAppliersPath));

        Assert.That(Regex.IsMatch(runtimeHostContributorStripped, @"\bIDomainArbiterBindingApplyTarget\b"), Is.True,
            "RuntimeHost should expose apply-target interface seam for external domain-owned binding appliers.");
        Assert.That(Regex.IsMatch(runtimeHostContributorStripped, @"\bTryApplyArbiterBindingConsumer\s*(?:<[^>]+>)?\s*\("), Is.True,
            "Apply-target seam should expose generic consumer-based application method.");
        Assert.That(Regex.IsMatch(runtimeHostContributorStripped,
            @"\bTryApply(IdleRolePolicyMapBinding|CombatRolePolicyMapBinding|CombatRoleConstraintsMapBinding)\s*\("), Is.False,
            "Domain-specific apply methods must not remain on IDomainArbiterBindingApplyTarget seam.");
        Assert.That(RuntimeHostConsumerKeysTypeRegex.IsMatch(runtimeHostContributorStripped), Is.False,
            "RuntimeHost should not expose consumer-slot key constants after type-based apply-target routing step.");
        Assert.That(RuntimeHostBindingTargetAppliersTypeRegex.IsMatch(runtimeHostContributorStripped), Is.False,
            "RuntimeHost should not own built-in DomainArbiterBindingTargetAppliers after C04A applier ownership move.");
        Assert.That(StrategyCombatBindingTargetAppliersTypeRegex.IsMatch(strategyCombatAppliersStripped), Is.True,
            "StrategyCombat should own concrete arbiter binding appliers for Combat/Idle policy-map bindings.");
    }

    [Test]
    public void RuntimeHost_BindingContributorFactory_IsGeneric_NotPolicyMapTyped()
    {
        Assert.That(File.Exists(RuntimeHostBindingContributorPath), Is.True,
            $"Missing file: {RuntimeHostBindingContributorPath}");

        string runtimeHostContributorStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostBindingContributorPath));

        Assert.That(Regex.IsMatch(runtimeHostContributorStripped, @"\bCreateFixedContributor\s*\("), Is.True,
            "RuntimeHost binding contributor helpers should expose generic fixed-contributor factory.");
        Assert.That(Regex.IsMatch(runtimeHostContributorStripped, @"\bCreatePolicyMapContributor\s*\("), Is.False,
            "RuntimeHost must not keep StrategyCombat-shaped CreatePolicyMapContributor factory after generic helper step.");
        Assert.That(Regex.IsMatch(runtimeHostContributorStripped,
            @"\b(IdleRolePolicyMapAsset|CombatRolePolicyMapAsset|CombatRoleConstraintsMapAsset)\b"), Is.False,
            "RuntimeHost binding contributor helper path should not depend on StrategyCombat policy-map asset types.");
        Assert.That(Regex.IsMatch(runtimeHostContributorStripped,
            @"\bDomainArbiterBinding(Target)?Entry\s+_entry[0-9]+\b"), Is.False,
            "Binding contribution payloads should not be hardcoded to fixed entry slots (_entry0/_entry1/...) after extensibility step.");
    }

    [Test]
    public void RuntimeHost_ApplyTarget_UsesDirectGenericAssetApply_NoConsumerRegistry()
    {
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");

        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));
        string applyTargetBody = ExtractMethodBody(
            arbiterStripped,
            "bool IDomainArbiterBindingApplyTarget.TryApplyArbiterBindingConsumer<TAsset>(");

        Assert.That(string.IsNullOrEmpty(applyTargetBody), Is.False,
            "Could not extract IDomainArbiterBindingApplyTarget.TryApplyArbiterBindingConsumer(...) body.");
        Assert.That(Regex.IsMatch(arbiterStripped, @"\bTryResolveArbiterBindingConsumer\s*\("), Is.False,
            "OrchestrationArbiter should not keep consumer-registry lookup after direct generic asset-apply step.");
        Assert.That(Regex.IsMatch(applyTargetBody, @"\bTryResolveArbiterBindingConsumer\s*\("), Is.False,
            "Apply-target method should not resolve local consumer registry after direct generic asset-apply step.");
        Assert.That(Regex.IsMatch(applyTargetBody, @"\bTryApplyArbiterBindingAsset\s*<\s*TAsset\s*>\s*\("), Is.True,
            "Apply-target method should directly delegate to generic asset-apply helper.");
        Assert.That(RuntimeHostConsumerKeysTypeRegex.IsMatch(applyTargetBody), Is.False,
            "Apply-target method must not depend on RuntimeHostArbiterBindingConsumerKeys after type-based routing step.");
        Assert.That(Regex.IsMatch(arbiterStripped,
            @"\bprivate\s+bool\s+TryApply(IdleRolePolicyMapBinding|CombatRolePolicyMapBinding|CombatRoleConstraintsMapBinding)\s*\("), Is.False,
            "OrchestrationArbiter should not keep domain-specific private apply helpers after generic asset-apply helper step.");
    }

    [Test]
    public void RuntimeHost_Arbiter_UsesGenericBindingAssetStore_ForPolicyMapState()
    {
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");

        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));

        Assert.That(Regex.IsMatch(arbiterStripped, @"\bArbiterBindingAssetEntry\b"), Is.True,
            "OrchestrationArbiter should keep an internal generic binding-asset store for domain-contributed execution bindings.");
        Assert.That(Regex.IsMatch(arbiterStripped,
            @"\b(IdleRolePolicyMapAsset|CombatRolePolicyMapAsset|CombatRoleConstraintsMapAsset)\s+_(idleRolePolicyMap|combatRolePolicyMap|combatRoleConstraintsMap)\b"),
            Is.False,
            "OrchestrationArbiter should not own typed policy-map state fields after generic binding-asset store step.");
        Assert.That(Regex.IsMatch(arbiterStripped, @"\bCopyArbiterBindingAssetsTo\s*\("), Is.True,
            "OrchestrationArbiter should project execution bindings generically into ExecutionContext.");
        Assert.That(Regex.IsMatch(arbiterStripped,
            @"\b(IdleRolePolicyMapAsset|CombatRolePolicyMapAsset|CombatRoleConstraintsMapAsset)\b"), Is.False,
            "OrchestrationArbiter should not reference concrete policy-map asset types after generic execution-binding copy step.");
        Assert.That(Regex.IsMatch(arbiterStripped,
            @"\bnew\s+(?:ArbiterBindingAssetEntry|DomainArbiterBindingTargetEntry|DomainArbiterBindingKey)\s*\[\s*3\s*\]"), Is.False,
            "OrchestrationArbiter binding registries/caches should not use hidden fixed-size [3] limits after extensibility step.");
        Assert.That(Regex.IsMatch(arbiterStripped, @"\bseenKey[0-9]+\b"), Is.False,
            "OrchestrationArbiter duplicate-binding detection should not use fixed local seenKey0/1/2 slots after extensibility step.");
    }

    [Test]
    public void RuntimeHost_ExecutionContext_UsesGenericBindingStore_WithoutTypedPolicyProperties()
    {
        Assert.That(File.Exists(RuntimeHostExecutionContextPath), Is.True,
            $"Missing file: {RuntimeHostExecutionContextPath}");

        string executionContextStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostExecutionContextPath));

        Assert.That(Regex.IsMatch(executionContextStripped, @"\bTryGetBinding\s*<"), Is.True,
            "ExecutionContext should expose generic binding lookup after C04A binding-store step.");
        Assert.That(Regex.IsMatch(executionContextStripped, @"\bSetBinding\s*<"), Is.True,
            "ExecutionContext should keep generic binding set path for arbiter compatibility projection.");
        Assert.That(Regex.IsMatch(executionContextStripped,
            @"\bpublic\s+(IdleRolePolicyMapAsset|CombatRolePolicyMapAsset|CombatRoleConstraintsMapAsset)\s+(IdleRolePolicyMap|CombatRolePolicyMap|CombatRoleConstraintsMap)\s*;"),
            Is.False,
            "ExecutionContext should not store domain-specific policy maps as public fields after generic binding-store step.");
        Assert.That(Regex.IsMatch(executionContextStripped,
            @"\bpublic\s+(IdleRolePolicyMapAsset|CombatRolePolicyMapAsset|CombatRoleConstraintsMapAsset)\s+(IdleRolePolicyMap|CombatRolePolicyMap|CombatRoleConstraintsMap)\s*\{"),
            Is.False,
            "ExecutionContext should drop compatibility typed properties once ExecutionRouter migrates to generic binding lookup.");
    }

    [Test]
    public void StrategyCombat_ExecutionRoutes_Idle_UsesGenericBindingLookup_ForIdlePolicyMap()
    {
        Assert.That(File.Exists(StrategyCombatIdleExecutionRoutePath), Is.True,
            $"Missing file: {StrategyCombatIdleExecutionRoutePath}");

        string helperStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatIdleExecutionRoutePath));
        string emitIdleBody = ExtractMethodBody(helperStripped, "void EmitIdlePerUnit(");

        Assert.That(string.IsNullOrEmpty(emitIdleBody), Is.False,
            "Could not extract StrategyCombatIdleExecutionRoute.EmitIdlePerUnit(...) body.");
        Assert.That(Regex.IsMatch(emitIdleBody, @"\bctx\s*\.\s*TryGetBinding\s*<\s*IdleRolePolicyMapAsset\s*>"), Is.True,
            "StrategyCombat idle route executor should read idle policy map via generic ExecutionContext binding lookup.");
        Assert.That(Regex.IsMatch(emitIdleBody, @"\bctx\s*\.\s*IdleRolePolicyMap\b"), Is.False,
            "StrategyCombat idle route executor should not depend on typed ExecutionContext.IdleRolePolicyMap after generic binding-lookup step.");
    }

    [Test]
    public void RuntimeHost_ExecutionRouter_UsesRouteRegistrationSeam()
    {
        Assert.That(File.Exists(RuntimeHostRouterPath), Is.True,
            $"Missing file: {RuntimeHostRouterPath}");

        string routerStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostRouterPath));
        string executeBody = ExtractMethodBody(
            routerStripped,
            "public ExecutionResult Execute(");

        Assert.That(ExecutionRouteRegistrationTypeRegex.IsMatch(routerStripped), Is.True,
            "ExecutionRouter should use ExecutionRouteRegistration seam in C04A.");

        Assert.That(RegisterRouteCallRegex.IsMatch(routerStripped), Is.True,
            "ExecutionRouter should expose RegisterRoute() seam in C04A.");

        Assert.That(string.IsNullOrEmpty(executeBody), Is.False,
            "Could not extract ExecutionRouter.Execute(...) body.");

        Assert.That(Regex.IsMatch(executeBody, @"\bswitch\s*\("), Is.False,
            "ExecutionRouter.Execute(...) should not hardcode domain switch after route-registration seam bootstrap.");

        Assert.That(Regex.IsMatch(executeBody, @"\bTryExecuteRegisteredRoute\s*\("), Is.True,
            "ExecutionRouter.Execute(...) must dispatch via registered execution routes.");
        Assert.That(Regex.IsMatch(routerStripped, @"\bRegisterBuiltInRoute\s*\("), Is.False,
            "ExecutionRouter should not own built-in route registration after StrategyCombat route-executor ownership move.");
    }

    [Test]
    public void RuntimeHost_Pipeline_RegistersRoutesFromDomainRegistrationContributors()
    {
        Assert.That(File.Exists(RuntimeHostDomainOrchestratorPath), Is.True,
            $"Missing file: {RuntimeHostDomainOrchestratorPath}");
        Assert.That(File.Exists(RuntimeHostLoopPath), Is.True,
            $"Missing file: {RuntimeHostLoopPath}");
        Assert.That(File.Exists(RuntimeHostPipelinePath), Is.True,
            $"Missing file: {RuntimeHostPipelinePath}");
        Assert.That(File.Exists(RuntimeHostRouterPath), Is.True,
            $"Missing file: {RuntimeHostRouterPath}");

        string domainOrchestratorStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostDomainOrchestratorPath));
        string loopStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostLoopPath));
        string pipelineStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostPipelinePath));
        string routerStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostRouterPath));
        string routerCtorBody = ExtractMethodBody(routerStripped, "public ExecutionRouter(InProcessCommandBus bus)");

        Assert.That(Regex.IsMatch(domainOrchestratorStripped, @"\bIDomainExecutionRouteContributor\b"), Is.True,
            "DomainOrchestrator registration seam should include IDomainExecutionRouteContributor for route onboarding.");
        Assert.That(Regex.IsMatch(domainOrchestratorStripped, @"\bCreateExecutionRouteContributor\s*\("), Is.True,
            "DomainOrchestrator should expose execution route contributor hook.");
        Assert.That(
            Regex.IsMatch(loopStripped, @"\b_pipeline\s*\.\s*ApplyConfiguredDomains\s*\(") ||
            Regex.IsMatch(loopStripped, @"\bpipeline\s*\.\s*ApplyConfiguredDomains\s*\("),
            Is.True,
            "OrchestrationLoop should delegate resolved domain composition into OrchestrationPipeline after B2 extraction (single-pipeline or pipelines[] host path).");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bregistration\s*\.\s*ExecutionRouteContributor\b"), Is.True,
            "OrchestrationPipeline should read cached route contributors from DomainRegistration.");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\brouteContributor\s*\.\s*RegisterRoutes\s*\(\s*_router\s*\)"), Is.True,
            "OrchestrationPipeline should apply route contributor registrations into ExecutionRouter.");

        Assert.That(string.IsNullOrEmpty(routerCtorBody), Is.False,
            "Could not extract ExecutionRouter constructor body.");
        Assert.That(Regex.IsMatch(routerCtorBody, @"\bRegisterBuiltInRoute\s*\("), Is.False,
            "ExecutionRouter constructor should not seed domain routes after loop/domain-registration route-contributor seam.");
        Assert.That(Regex.IsMatch(routerCtorBody, @"\bRegisterRoute\s*\("), Is.False,
            "ExecutionRouter constructor should not directly register domain routes after loop/domain-registration route-contributor seam.");
    }

    [Test]
    public void RuntimeHost_RouteContributorHelper_SupportsUnknownRouteFallbackRegistration()
    {
        Assert.That(File.Exists(RuntimeHostRouteContributorPath), Is.True,
            $"Missing file: {RuntimeHostRouteContributorPath}");
        Assert.That(File.Exists(RuntimeHostRouterPath), Is.True,
            $"Missing file: {RuntimeHostRouterPath}");

        string contributorStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostRouteContributorPath));
        string routerStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostRouterPath));

        Assert.That(Regex.IsMatch(contributorStripped, @"\bCreateFixedWithUnknownRouteFallback\s*\("), Is.True,
            "DomainExecutionRouteContributors should expose helper for unknown-route fallback registration.");
        Assert.That(Regex.IsMatch(contributorStripped, @"\bRegisterUnknownRouteFallback\s*\("), Is.True,
            "Fixed route contributor should forward unknown-route fallback into ExecutionRouter.");
        Assert.That(Regex.IsMatch(routerStripped, @"\bRegisterUnknownRouteFallback\s*\("), Is.True,
            "ExecutionRouter should expose unknown-route fallback registration seam.");
    }

    [Test]
    public void RuntimeHost_RouteExecutorDelegate_ReceivesRouterHost_ForExternalizationPrep()
    {
        Assert.That(File.Exists(RuntimeHostExecutionRouteRegistrationPath), Is.True,
            $"Missing file: {RuntimeHostExecutionRouteRegistrationPath}");
        Assert.That(File.Exists(RuntimeHostRouterPath), Is.True,
            $"Missing file: {RuntimeHostRouterPath}");

        string routeRegistrationStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostExecutionRouteRegistrationPath));
        string routerStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostRouterPath));
        string tryExecuteRouteBody = ExtractMethodBody(routerStripped, "bool TryExecuteRegisteredRoute(");

        Assert.That(Regex.IsMatch(routeRegistrationStripped,
            @"\bdelegate\s+void\s+DomainExecutionRouteExecutor\s*\(\s*IExecutionRouteHost\s+\w+\s*,\s*ArbiterDecision\s+\w+\s*,\s*OrchestrationWorldCache\s+\w+\s*,\s*ExecutionContext\s+\w+\s*\)"), Is.True,
            "DomainExecutionRouteExecutor should receive IExecutionRouteHost seam to allow route-executor ownership outside RuntimeHost.");

        Assert.That(string.IsNullOrEmpty(tryExecuteRouteBody), Is.False,
            "Could not extract ExecutionRouter.TryExecuteRegisteredRoute(...) body.");
        Assert.That(Regex.IsMatch(tryExecuteRouteBody, @"\bexecute\s*\(\s*this\s*,\s*decision\s*,\s*world\s*,\s*ctx\s*\)"), Is.True,
            "ExecutionRouter should pass itself as host argument into registered route executors.");
    }

    [Test]
    public void RuntimeHost_ExecutionRouter_DoesNotOwnBuiltInRouteExecutors_AndStrategyCombatOwnsThem()
    {
        Assert.That(File.Exists(RuntimeHostRouterPath), Is.True,
            $"Missing file: {RuntimeHostRouterPath}");
        Assert.That(File.Exists(StrategyCombatCombatExecutionRoutePath), Is.True,
            $"Missing file: {StrategyCombatCombatExecutionRoutePath}");
        Assert.That(File.Exists(StrategyCombatIdleExecutionRoutePath), Is.True,
            $"Missing file: {StrategyCombatIdleExecutionRoutePath}");
        Assert.That(File.Exists(StrategyCombatNoneExecutionRoutePath), Is.True,
            $"Missing file: {StrategyCombatNoneExecutionRoutePath}");
        Assert.That(File.Exists(StrategyCombatUnknownRouteFallbackExecutionRoutePath), Is.True,
            $"Missing file: {StrategyCombatUnknownRouteFallbackExecutionRoutePath}");

        string routerStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostRouterPath));
        string combatRouteStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatCombatExecutionRoutePath));
        string idleRouteStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatIdleExecutionRoutePath));
        string noneRouteStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatNoneExecutionRoutePath));
        string unknownFallbackStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatUnknownRouteFallbackExecutionRoutePath));
        string unknownFallbackSource = File.ReadAllText(StrategyCombatUnknownRouteFallbackExecutionRoutePath);
        string allRouteStripped = string.Concat(combatRouteStripped, "\n", idleRouteStripped, "\n", noneRouteStripped, "\n", unknownFallbackStripped);

        Assert.That(Regex.IsMatch(routerStripped, @"\bRuntimeHostBuiltInExecutionRoutes\b"), Is.False,
            "RuntimeHost should not keep RuntimeHostBuiltInExecutionRoutes helper after StrategyCombat route-executor ownership move.");
        Assert.That(Regex.IsMatch(routerStripped, @"\bStrategyCombatExecutionRoutes\b"), Is.False,
            "ExecutionRouter should not depend on the old aggregate StrategyCombatExecutionRoutes helper after split into per-route executors.");
        Assert.That(Regex.IsMatch(routerStripped, @"\bExecute(Combat|Idle|None)Route\s*\("), Is.False,
            "ExecutionRouter should not keep built-in route executor methods after helper extraction step.");
        Assert.That(Regex.IsMatch(routerStripped, @"\bEmitIdlePerUnit\s*\("), Is.False,
            "ExecutionRouter should not keep Idle route body after StrategyCombat route-executor ownership move is completed.");
        Assert.That(Regex.IsMatch(routerStripped, @"\bEmitCombat(?!Command)\s*\("), Is.False,
            "ExecutionRouter should not keep Combat emit primitives after StrategyCombat route helper switches to PublishCommand loops.");
        Assert.That(Regex.IsMatch(routerStripped, @"\bEmit(CombatHoldAll|IdleHoldAll)\s*\("), Is.False,
            "ExecutionRouter should not keep hold-all emit primitives after StrategyCombat route helper switches to PublishCommand loops.");
        Assert.That(Regex.IsMatch(routerStripped, @"\b(CombatCommand\.Create\s*\(|IdleCommand\.Hold\s*\()"), Is.False,
            "ExecutionRouter should not construct domain-specific hold fallback commands after unknown-route fallback seam extraction.");
        Assert.That(Regex.IsMatch(routerStripped, @"\bRegisterUnknownRouteFallback\s*\("), Is.True,
            "ExecutionRouter should expose unknown-route fallback registration seam after fallback externalization step.");

        Assert.That(Regex.IsMatch(combatRouteStripped, @"\bclass\s+StrategyCombatCombatExecutionRoute\b"), Is.True,
            "StrategyCombat should own the Combat route executor in its own file after route split.");
        Assert.That(Regex.IsMatch(idleRouteStripped, @"\bclass\s+StrategyCombatIdleExecutionRoute\b"), Is.True,
            "StrategyCombat should own the Idle route executor in its own file after route split.");
        Assert.That(Regex.IsMatch(noneRouteStripped, @"\bclass\s+StrategyCombatNoneExecutionRoute\b"), Is.True,
            "StrategyCombat should own the None route executor in its own file after route split.");
        Assert.That(Regex.IsMatch(unknownFallbackStripped, @"\bclass\s+StrategyCombatUnknownRouteFallbackExecutionRoute\b"), Is.True,
            "StrategyCombat should own the unknown-route fallback executor after fallback seam extraction.");
        Assert.That(Regex.IsMatch(combatRouteStripped, @"\bpublic\s+(?:static\s+)?void\s+Execute\s*\("), Is.True,
            "StrategyCombat combat route executor should expose Execute(...) entrypoint.");
        Assert.That(Regex.IsMatch(idleRouteStripped, @"\bpublic\s+(?:static\s+)?void\s+Execute\s*\("), Is.True,
            "StrategyCombat idle route executor should expose Execute(...) entrypoint.");
        Assert.That(Regex.IsMatch(noneRouteStripped, @"\bpublic\s+(?:static\s+)?void\s+Execute\s*\("), Is.True,
            "StrategyCombat none route executor should expose Execute(...) entrypoint.");
        Assert.That(Regex.IsMatch(unknownFallbackStripped, @"\bpublic\s+(?:static\s+)?void\s+Execute\s*\("), Is.True,
            "StrategyCombat unknown-route fallback executor should expose Execute(...) entrypoint.");
        Assert.That(Regex.IsMatch(allRouteStripped, @"\bhost\s*\.\s*PublishCommand\s*\("), Is.True,
            "StrategyCombat route executors should publish dispatch commands through generic IExecutionRouteHost.PublishCommand seam.");
        Assert.That(Regex.IsMatch(idleRouteStripped, @"\bvoid\s+EmitIdlePerUnit\s*\("), Is.True,
            "StrategyCombat idle route executor should own the Idle per-unit route body after route externalization step.");
        Assert.That(Regex.IsMatch(unknownFallbackSource, @"Router=UnknownRoute"), Is.True,
            "Unknown-route fallback executor should preserve legacy debug label semantics during seam extraction.");
    }

    [Test]
    public void MorbooBridge_DomainRoutePolicyBridge_UsesGenericCompositionRefs_AndNoUntypedLookupFallback()
    {
        Assert.That(File.Exists(DomainRouteExecutionPolicyBridgePath), Is.True,
            $"Missing file: {DomainRouteExecutionPolicyBridgePath}");

        string source = File.ReadAllText(DomainRouteExecutionPolicyBridgePath);
        string stripped = StripCommentsAndStrings(source);

        Assert.That(Regex.IsMatch(stripped, @"\bDefaultExecutionOrder\s*\(\s*-1000\s*\)"), Is.True,
            "Bridge route-policy component should run before OrchestrationLoop Awake to apply route policy before route-registration build.");
        Assert.That(Regex.IsMatch(stripped, @"\bDomainRouteExecutionPolicy\s+routeExecutionPolicy\b"), Is.True,
            "Bridge route-policy component should hold a generic DomainRouteExecutionPolicy policy reference (not genre-specific).");
        Assert.That(Regex.IsMatch(stripped, @"\bOrchestrationLoop\s+orchestrationLoop\b"), Is.True,
            "Bridge route-policy component should reference OrchestrationLoop as the single scene composition source-of-truth.");
        Assert.That(Regex.IsMatch(stripped, @"\bConfiguredDomainOrchestrators\b"), Is.True,
            "Bridge route-policy component should read configured domains from OrchestrationLoop.ConfiguredDomainOrchestrators.");
        Assert.That(Regex.IsMatch(stripped, @"\bCombatDomainComponent\s*\[\s*\]\s+combatDomains\b"), Is.False,
            "Bridge route-policy component should not keep a duplicate Combat domain list once OrchestrationLoop is source-of-truth.");
        Assert.That(Regex.IsMatch(stripped, @"\bIdleDomainComponent\s*\[\s*\]\s+idleDomains\b"), Is.False,
            "Bridge route-policy component should not keep a duplicate Idle domain list once OrchestrationLoop is source-of-truth.");
        Assert.That(Regex.IsMatch(stripped, @"\bIDomainRouteExecutionPolicyConsumer\b"), Is.True,
            "Bridge route-policy component should apply route-policy through generic RuntimeHost policy-consumer interface (no concrete domain branching).");
        Assert.That(Regex.IsMatch(stripped, @"\b(domain\s+is\s+CombatDomainComponent|domain\s+is\s+IdleDomainComponent)\b"), Is.False,
            "Bridge route-policy component should not branch by concrete Combat/Idle orchestrator types when applying route policies.");
        Assert.That(Regex.IsMatch(stripped, @"\bApplyRouteExecutionPolicy\s*\("), Is.True,
            "Bridge route-policy component should apply route-policy through domain composition seam.");

        Assert.That(SerializedUntypedDependencyHolderRegex.IsMatch(stripped), Is.False,
            "Bridge route-policy component must not use untyped serialized dependency holders.");
        Assert.That(Regex.IsMatch(stripped, @"\bGetComponent\s*<"), Is.False,
            "Bridge route-policy component must not discover scene dependencies via GetComponent fallback.");
        Assert.That(OrchestrationLoopUntypedCastRegex.IsMatch(stripped), Is.False,
            "Bridge route-policy component must not cast untyped refs to OrchestrationLoop.");
    }

    [Test]
    public void RuntimeHost_OrchestrationLoop_DoesNotExposeDomainModuleCompositionSeam_InCurrentSingleScenePath()
    {
        Assert.That(File.Exists(RuntimeHostLoopPath), Is.True,
            $"Missing file: {RuntimeHostLoopPath}");

        string loopStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostLoopPath));

        Assert.That(Regex.IsMatch(loopStripped, @"\bOrchestrationDomainModule\b"), Is.False,
            "Current single-scene orchestration path should not expose OrchestrationDomainModule seam in OrchestrationLoop.");
        Assert.That(Regex.IsMatch(loopStripped, @"\bdomainModules\b"), Is.False,
            "Current single-scene orchestration path should not keep a second domain-module list in OrchestrationLoop.");
        Assert.That(Regex.IsMatch(loopStripped, @"\bConfigureDomainModules\s*\("), Is.False,
            "Current single-scene orchestration path should not keep ConfigureDomainModules() in OrchestrationLoop.");
    }

    [Test]
    public void C04B_B2_B4_RuntimeHost_LoopHostsPipelines_AndPipelineOwnsPerPipelineComposition()
    {
        Assert.That(File.Exists(RuntimeHostPipelinePath), Is.True,
            $"Missing file: {RuntimeHostPipelinePath}");
        Assert.That(File.Exists(RuntimeHostLoopPath), Is.True,
            $"Missing file: {RuntimeHostLoopPath}");
        Assert.That(File.Exists(RuntimeHostPipelineComponentPath), Is.True,
            $"Missing file: {RuntimeHostPipelineComponentPath}");

        string pipelineStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostPipelinePath));
        string loopStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostLoopPath));
        string pipelineComponentStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostPipelineComponentPath));
        string onTickBody = ExtractMethodBody(loopStripped, "void OnTick(TickContext tickCtx)");
        string awakeBody = ExtractMethodBody(loopStripped, "void Awake()");
        string buildPipelinesBody = ExtractMethodBody(loopStripped, "void BuildAndApplyConfiguredPipelines()");

        Assert.That(Regex.IsMatch(pipelineStripped, @"\bsealed\s+class\s+OrchestrationPipeline\b"), Is.True,
            "C04B/B2 should introduce OrchestrationPipeline runtime container in RuntimeHost.");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bExecutionRouter\b"), Is.True,
            "OrchestrationPipeline should own an ExecutionRouter instance.");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bInProcessCommandBus\b"), Is.True,
            "OrchestrationPipeline should own an InProcessCommandBus instance (or receive one as injected runtime bus).");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bvoid\s+Tick\s*\(\s*float\s+\w+\s*\)"), Is.True,
            "OrchestrationPipeline should expose Tick(float) runtime entrypoint.");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bvoid\s+ApplyConfiguredDomains\s*\("), Is.True,
            "OrchestrationPipeline should own domain composition application seam in B2.");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bRegisterDomainRoutes\s*\("), Is.True,
            "OrchestrationPipeline should own route registration from domain contributors after B3-B4.");
        Assert.That(Regex.IsMatch(pipelineComponentStripped, @"\bsealed\s+class\s+OrchestrationPipelineComponent\s*:\s*MonoBehaviour\b"), Is.True,
            "C04B/B3-B4 should introduce scene-level OrchestrationPipelineComponent owner.");
        Assert.That(Regex.IsMatch(pipelineComponentStripped, @"\bOrchestrationArbiter\s+arbiter\b"), Is.True,
            "Pipeline component should own arbiter reference.");
        Assert.That(Regex.IsMatch(pipelineComponentStripped, @"\bFactionAsset\s+orchestratorFaction\b"), Is.True,
            "Pipeline component should own Faction-first identity.");
        Assert.That(Regex.IsMatch(pipelineComponentStripped, @"\bDomainOrchestrator\s*\[\s*\]\s+domainOrchestrators\b"), Is.True,
            "Pipeline component should own per-pipeline domain composition list after B4.");
        Assert.That(Regex.IsMatch(pipelineComponentStripped, @"\b(bool\s+Enabled|string\s+Label)\b"), Is.False,
            "Pipeline component should not keep redundant Enabled/Label fields; scene component enable state and object name are sufficient.");

        Assert.That(Regex.IsMatch(loopStripped, @"\bOrchestrationPipelineComponent\s*\[\s*\]\s+pipelines\b"), Is.True,
            "OrchestrationLoop should host ordered pipeline composition components after B3-B4.");
        Assert.That(Regex.IsMatch(loopStripped, @"\bOrchestrationPipeline\s*\[\s*\]\s+_runtimePipelines\b"), Is.True,
            "OrchestrationLoop should store runtime pipeline array after B3.");
        Assert.That(Regex.IsMatch(loopStripped, @"\bBuildAndApplyConfiguredPipelines\s*\("), Is.True,
            "OrchestrationLoop should build runtime pipelines from scene pipeline configs.");
        Assert.That(string.IsNullOrEmpty(awakeBody), Is.False,
            "Could not extract OrchestrationLoop.Awake() body.");
        Assert.That(Regex.IsMatch(awakeBody, @"\bBuildAndApplyConfiguredPipelines\s*\("), Is.True,
            "OrchestrationLoop.Awake should build/apply configured pipelines.");
        Assert.That(string.IsNullOrEmpty(buildPipelinesBody), Is.False,
            "Could not extract OrchestrationLoop.BuildAndApplyConfiguredPipelines() body.");
        Assert.That(Regex.IsMatch(buildPipelinesBody, @"\bnew\s+OrchestrationPipeline\s*\("), Is.True,
            "OrchestrationLoop should construct runtime pipeline instances from scene configs.");
        Assert.That(Regex.IsMatch(buildPipelinesBody, @"\bpipelineComponent\s*\.\s*Arbiter\b"), Is.True,
            "OrchestrationLoop should read arbiter from the pipeline composition component.");
        Assert.That(Regex.IsMatch(buildPipelinesBody, @"\bpipeline\s*\.\s*ApplyConfiguredDomains\s*\("), Is.True,
            "OrchestrationLoop should pass per-pipeline domain composition into OrchestrationPipeline.");

        Assert.That(string.IsNullOrEmpty(onTickBody), Is.False,
            "Could not extract OrchestrationLoop.OnTick(...) body.");
        Assert.That(Regex.IsMatch(onTickBody, @"\bfor\s*\("), Is.True,
            "OrchestrationLoop should iterate runtime pipelines in B3.");
        Assert.That(Regex.IsMatch(onTickBody, @"\b_runtimePipelines\s*\[\s*i\s*\]\s*\.\s*Tick\s*\(\s*tickCtx\s*\.\s*Now\s*\)"), Is.True,
            "OrchestrationLoop should tick each runtime pipeline in B3.");
        Assert.That(Regex.IsMatch(onTickBody, @"\bProduceTick\s*\("), Is.False,
            "OrchestrationLoop host should not call arbiter.ProduceTick directly after pipeline extraction.");
        Assert.That(Regex.IsMatch(onTickBody, @"\bExecute\s*\("), Is.False,
            "OrchestrationLoop host should not call router.Execute directly after pipeline extraction.");
        Assert.That(Regex.IsMatch(onTickBody, @"\bFlush\s*\("), Is.False,
            "OrchestrationLoop host should not flush the command bus directly after pipeline extraction.");
    }

    [Test]
    public void C04B_B7_RuntimeHost_LoopTreatsPipelineComponentAsCompositionOwner_NotTickingBehaviour()
    {
        Assert.That(File.Exists(RuntimeHostLoopPath), Is.True,
            $"Missing file: {RuntimeHostLoopPath}");

        string loopStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostLoopPath));
        string buildPipelinesBody = ExtractMethodBody(loopStripped, "void BuildAndApplyConfiguredPipelines()");

        Assert.That(string.IsNullOrEmpty(buildPipelinesBody), Is.False,
            "Could not extract OrchestrationLoop.BuildAndApplyConfiguredPipelines() body.");
        Assert.That(Regex.IsMatch(buildPipelinesBody, @"\bpipelineComponent\s*\.\s*gameObject\s*\.\s*activeInHierarchy\b"), Is.True,
            "Pipeline composition owners should be filtered by GameObject active state (composition visibility), not by MonoBehaviour enabled state.");
        Assert.That(Regex.IsMatch(buildPipelinesBody, @"\bpipelineComponent\s*\.\s*isActiveAndEnabled\b"), Is.False,
            "OrchestrationPipelineComponent is a composition owner and must not be treated as a ticking behaviour via isActiveAndEnabled.");
    }

    [Test]
    public void C04B_B7_RuntimeHost_UsesSharedLoopBusAcrossPipelines_AndPerFlushDispatchContextOverride()
    {
        Assert.That(File.Exists(RuntimeHostLoopPath), Is.True,
            $"Missing file: {RuntimeHostLoopPath}");
        Assert.That(File.Exists(RuntimeHostPipelinePath), Is.True,
            $"Missing file: {RuntimeHostPipelinePath}");

        string loopStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostLoopPath));
        string pipelineStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostPipelinePath));
        string buildPipelinesBody = ExtractMethodBody(loopStripped, "void BuildAndApplyConfiguredPipelines()");
        string onTickBody = ExtractMethodBody(loopStripped, "void OnTick(TickContext tickCtx)");
        string currentWorldProperty = ExtractPropertyBody(loopStripped, "public OrchestrationWorldCache CurrentWorld");
        string currentExecProperty = ExtractPropertyBody(loopStripped, "public ExecutionContext CurrentExecContext");
        string pipelineTickBody = ExtractMethodBody(pipelineStripped, "public void Tick(float now)");

        Assert.That(string.IsNullOrEmpty(buildPipelinesBody), Is.False,
            "Could not extract OrchestrationLoop.BuildAndApplyConfiguredPipelines() body.");
        Assert.That(Regex.IsMatch(buildPipelinesBody, @"new\s+OrchestrationPipeline\s*\(\s*pipelineArbiter\s*,\s*_commandBus\b"), Is.True,
            "B7 host/path migration should inject the shared loop command bus into every runtime pipeline so existing adapters can consume commands from all pipelines.");
        Assert.That(Regex.IsMatch(buildPipelinesBody, @"\bpipeline\s*\.\s*SetDispatchContextSink\s*\(\s*SetCurrentDispatchContext\s*\)"), Is.True,
            "Loop should register a dispatch-context sink on each pipeline so loop-level CurrentWorld/CurrentExecContext point at the pipeline currently flushing commands.");

        Assert.That(string.IsNullOrEmpty(onTickBody), Is.False,
            "Could not extract OrchestrationLoop.OnTick(...) body.");
        Assert.That(Regex.IsMatch(onTickBody, @"\b_runtimePipelines\s*\[\s*i\s*\]\s*\.\s*Tick\s*\(\s*tickCtx\s*\.\s*Now\s*\)"), Is.True,
            "Loop should tick each runtime pipeline in multi-pipeline mode.");
        Assert.That(Regex.IsMatch(onTickBody, @"\bClearCurrentDispatchContext\s*\(\s*\)"), Is.True,
            "Loop should clear per-flush dispatch context override after each pipeline tick.");

        Assert.That(string.IsNullOrEmpty(currentWorldProperty), Is.False,
            "Could not extract OrchestrationLoop.CurrentWorld property body.");
        Assert.That(Regex.IsMatch(currentWorldProperty, @"_hasCurrentDispatchContext"), Is.True,
            "Loop.CurrentWorld should support a temporary per-flush dispatch context override.");
        Assert.That(Regex.IsMatch(currentWorldProperty, @"_primaryPipeline"), Is.True,
            "Loop.CurrentWorld should keep compatibility fallback to primary pipeline outside dispatch windows.");

        Assert.That(string.IsNullOrEmpty(currentExecProperty), Is.False,
            "Could not extract OrchestrationLoop.CurrentExecContext property body.");
        Assert.That(Regex.IsMatch(currentExecProperty, @"_hasCurrentDispatchContext"), Is.True,
            "Loop.CurrentExecContext should support a temporary per-flush dispatch context override.");
        Assert.That(Regex.IsMatch(currentExecProperty, @"_primaryPipeline"), Is.True,
            "Loop.CurrentExecContext should keep compatibility fallback to primary pipeline outside dispatch windows.");

        Assert.That(Regex.IsMatch(pipelineStripped, @"delegate\s+void\s+DispatchContextSink"), Is.True,
            "OrchestrationPipeline should expose a dispatch-context sink seam for loop-managed compatibility context routing.");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bSetDispatchContextSink\s*\("), Is.True,
            "OrchestrationPipeline should allow loop to inject a dispatch-context sink.");
        Assert.That(string.IsNullOrEmpty(pipelineTickBody), Is.False,
            "Could not extract OrchestrationPipeline.Tick(...) body.");
        Assert.That(Regex.IsMatch(pipelineTickBody, @"_dispatchContextSink\s*\?\.\s*Invoke\s*\(\s*_currentWorld\s*,\s*_currentExecContext\s*\)"), Is.True,
            "Pipeline should publish current world/exec context to the loop before flushing the shared bus.");
        Assert.That(Regex.IsMatch(pipelineTickBody, @"_commandBus\s*\.\s*Flush\s*\(\s*\)"), Is.True,
            "Pipeline should still flush the bus after publishing dispatch context.");
    }

    [Test]
    public void C04B_B5_FactionFirst_PipelineContext_PropagatesViaPipelineCompositionSeam()
    {
        Assert.That(File.Exists(RuntimeHostPipelineComponentPath), Is.True,
            $"Missing file: {RuntimeHostPipelineComponentPath}");
        Assert.That(File.Exists(RuntimeHostPipelinePath), Is.True,
            $"Missing file: {RuntimeHostPipelinePath}");
        Assert.That(File.Exists(RuntimeHostLoopPath), Is.True,
            $"Missing file: {RuntimeHostLoopPath}");
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");
        Assert.That(File.Exists(RuntimeHostExecutionContextPath), Is.True,
            $"Missing file: {RuntimeHostExecutionContextPath}");

        string pipelineComponentStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostPipelineComponentPath));
        string pipelineStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostPipelinePath));
        string loopStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostLoopPath));
        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));
        string execCtxStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostExecutionContextPath));
        string buildPipelinesBody = ExtractMethodBody(loopStripped, "void BuildAndApplyConfiguredPipelines()");
        string produceTickBody = ExtractMethodBody(arbiterStripped, "public OrchestrationTickResult ProduceTick(float now)");
        string setFactionBody = ExtractMethodBody(arbiterStripped, "public void SetFactionContextForComposition(FactionAsset faction, FactionRelationTableAsset relations)");

        Assert.That(Regex.IsMatch(pipelineComponentStripped, @"\bFactionAsset\s+orchestratorFaction\b"), Is.True,
            "Pipeline component should carry Faction-first pipeline identity.");
        Assert.That(Regex.IsMatch(pipelineComponentStripped, @"\bFactionRelationTableAsset\b"), Is.False,
            "Pipeline component should not carry relation tables in Faction-first C04B; relations are global on the host.");

        Assert.That(Regex.IsMatch(loopStripped, @"\bFactionRelationTableAsset\s+sharedRelations\b"), Is.True,
            "OrchestrationLoop should own one shared relation table for all pipelines to avoid conflicting per-pipeline relation configs.");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bvoid\s+ApplyFactionContext\s*\(\s*FactionAsset\s+\w+\s*,\s*FactionRelationTableAsset\s+\w+\s*\)"), Is.True,
            "OrchestrationPipeline should expose faction-first composition application seam in B5.");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bSetFactionContextForComposition\s*\("), Is.True,
            "OrchestrationPipeline should apply faction context into arbiter via composition seam.");

        Assert.That(string.IsNullOrEmpty(buildPipelinesBody), Is.False,
            "Could not extract OrchestrationLoop.BuildAndApplyConfiguredPipelines() body.");
        Assert.That(Regex.IsMatch(buildPipelinesBody, @"\bpipeline\s*\.\s*ApplyFactionContext\s*\(\s*pipelineComponent\s*\.\s*OrchestratorFaction\s*,\s*sharedRelations\s*\)"), Is.True,
            "OrchestrationLoop should apply per-pipeline faction identity plus shared relation table into each runtime pipeline.");

        Assert.That(HiddenSerializedDomainOrchestratorsFieldRegex.IsMatch(arbiterStripped), Is.True,
            "Domain composition storage in arbiter must remain hidden (source-of-truth is loop/pipeline composition).");
        Assert.That(Regex.IsMatch(arbiterStripped, @"\[(?:HideInInspector\s*,\s*SerializeField|SerializeField\s*,\s*HideInInspector)\]\s*FactionAsset\s+orchestratorFaction\s*;"), Is.True,
            "Arbiter local faction field should be hidden legacy storage once pipeline composition owns faction identity.");
        Assert.That(Regex.IsMatch(arbiterStripped, @"\[(?:HideInInspector\s*,\s*SerializeField|SerializeField\s*,\s*HideInInspector)\]\s*FactionRelationTableAsset\s+typedRelations\s*;"), Is.True,
            "Arbiter local relation table field should be hidden legacy storage once pipeline composition owns faction identity.");

        Assert.That(string.IsNullOrEmpty(setFactionBody), Is.False,
            "Could not extract SetFactionContextForComposition(...) body.");
        Assert.That(Regex.IsMatch(setFactionBody, @"_factionCompositionApplied\s*=\s*true\s*;"), Is.True,
            "Faction composition seam must mark faction context as applied.");
        Assert.That(Regex.IsMatch(setFactionBody, @"_composedOrchestratorFaction\s*=\s*faction\s*;"), Is.True,
            "Faction composition seam must store composed orchestrator faction.");
        Assert.That(Regex.IsMatch(setFactionBody, @"_composedTypedRelations\s*=\s*relations\s*;"), Is.True,
            "Faction composition seam must store composed relation table.");

        Assert.That(string.IsNullOrEmpty(produceTickBody), Is.False,
            "Could not extract ProduceTick(...) body.");
        Assert.That(Regex.IsMatch(produceTickBody, @"\bif\s*\(\s*!\s*_factionCompositionApplied\s*\)"), Is.True,
            "ProduceTick must fail-fast when faction-first pipeline composition has not been applied.");
        Assert.That(Regex.IsMatch(produceTickBody, @"effectiveOrchestratorFaction"), Is.True,
            "ProduceTick should use effective faction from pipeline-applied composition.");
        Assert.That(Regex.IsMatch(produceTickBody, @"effectiveRelations"), Is.True,
            "ProduceTick should use effective relation table from pipeline-applied composition.");
        Assert.That(Regex.IsMatch(produceTickBody, @"_ctx\s*\.\s*OrchestratorFaction\s*=\s*effectiveOrchestratorFaction"), Is.True,
            "Arbiter context must receive pipeline faction identity.");
        Assert.That(Regex.IsMatch(produceTickBody, @"_ctx\s*\.\s*Relations\s*=\s*effectiveRelations"), Is.True,
            "Arbiter context must receive pipeline relation table.");
        Assert.That(Regex.IsMatch(produceTickBody, @"ExecutionContext\s+\w+\s*=\s*new\s+ExecutionContext"), Is.True,
            "ProduceTick should still build ExecutionContext for execution path.");
        Assert.That(Regex.IsMatch(produceTickBody, @"OrchestratorFaction\s*=\s*effectiveOrchestratorFaction"), Is.True,
            "ExecutionContext must receive pipeline faction identity.");
        Assert.That(Regex.IsMatch(produceTickBody, @"Relations\s*=\s*effectiveRelations"), Is.True,
            "ExecutionContext must receive pipeline relation table.");

        Assert.That(Regex.IsMatch(execCtxStripped, @"\bFactionAsset\s+OrchestratorFaction\b"), Is.True,
            "ExecutionContext should keep orchestrator faction field while C04B uses Faction-first pipeline identity.");
        Assert.That(Regex.IsMatch(execCtxStripped, @"\bFactionRelationTableAsset\s+Relations\b"), Is.True,
            "ExecutionContext should keep relation table field while C04B uses Faction-first pipeline identity.");
    }

    [Test]
    public void C04B_B6_RuntimeHost_PipelineStaysDomainAgnostic_AndStrategyCombatOwnsTargetProviders()
    {
        Assert.That(File.Exists(RuntimeHostPipelineComponentPath), Is.True,
            $"Missing file: {RuntimeHostPipelineComponentPath}");
        Assert.That(File.Exists(RuntimeHostPipelinePath), Is.True,
            $"Missing file: {RuntimeHostPipelinePath}");
        Assert.That(File.Exists(RuntimeHostLoopPath), Is.True,
            $"Missing file: {RuntimeHostLoopPath}");
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");
        Assert.That(File.Exists(StrategyCombatCombatTargetProviderPath), Is.True,
            $"Missing file: {StrategyCombatCombatTargetProviderPath}");
        Assert.That(File.Exists(StrategyCombatIdleTargetProviderPath), Is.True,
            $"Missing file: {StrategyCombatIdleTargetProviderPath}");
        Assert.That(File.Exists(CombatDomainComponentPath), Is.True,
            $"Missing file: {CombatDomainComponentPath}");
        Assert.That(File.Exists(IdleDomainComponentPath), Is.True,
            $"Missing file: {IdleDomainComponentPath}");
        Assert.That(File.Exists(StrategyCombatIdleExecutionRoutePath), Is.True,
            $"Missing file: {StrategyCombatIdleExecutionRoutePath}");

        string pipelineComponentStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostPipelineComponentPath));
        string pipelineStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostPipelinePath));
        string loopStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostLoopPath));
        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));
        string combatProviderStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatCombatTargetProviderPath));
        string idleProviderStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatIdleTargetProviderPath));
        string combatDomainStripped = StripCommentsAndStrings(File.ReadAllText(CombatDomainComponentPath));
        string idleDomainStripped = StripCommentsAndStrings(File.ReadAllText(IdleDomainComponentPath));
        string idleRouteStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatIdleExecutionRoutePath));

        string buildPipelinesBody = ExtractMethodBody(loopStripped, "void BuildAndApplyConfiguredPipelines()");
        string buildWorldCacheBody = ExtractMethodBody(arbiterStripped, "void BuildWorldCache(OrchestrationArbiterContext ctx)");

        Assert.That(Regex.IsMatch(pipelineComponentStripped, @"\bCombatTargetSet\s+combatTargetSet\b"), Is.False,
            "Pipeline component must not own StrategyCombat CombatTargetSet; targeting ownership belongs to StrategyCombat domain providers.");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bCombatTargetSet\s+_combatTargetSet\b"), Is.False,
            "OrchestrationPipeline must remain domain-agnostic and must not store CombatTargetSet.");
        Assert.That(Regex.IsMatch(pipelineStripped, @"\bvoid\s+ApplyTargetingContext\s*\("), Is.False,
            "OrchestrationPipeline must not expose domain-specific targeting composition seam.");
        Assert.That(Regex.IsMatch(arbiterStripped, @"\bSetCombatTargetSetForComposition\s*\("), Is.False,
            "OrchestrationArbiter must not expose CombatTargetSet composition seam in RuntimeHost.");

        Assert.That(string.IsNullOrEmpty(buildPipelinesBody), Is.False,
            "Could not extract OrchestrationLoop.BuildAndApplyConfiguredPipelines() body.");
        Assert.That(Regex.IsMatch(buildPipelinesBody, @"\bApplyTargetingContext\s*\("), Is.False,
            "OrchestrationLoop must not push StrategyCombat targeting ownership through RuntimeHost pipelines.");

        Assert.That(string.IsNullOrEmpty(buildWorldCacheBody), Is.False,
            "Could not extract BuildWorldCache(...) body.");
        Assert.That(Regex.IsMatch(buildWorldCacheBody, @"\bOrchestrationRegistry\s*\.\s*TryGetCombatTargetSet\s*\("), Is.False,
            "BuildWorldCache must not keep registry fallback once B6 targeting ownership moves into StrategyCombat providers.");
        Assert.That(Regex.IsMatch(buildWorldCacheBody, @"_composedCombatTargetSet"), Is.False,
            "BuildWorldCache must not reference removed RuntimeHost CombatTargetSet composition field.");

        Assert.That(Regex.IsMatch(combatProviderStripped, @"\bclass\s+CombatTargetProvider\b"), Is.True,
            "StrategyCombat should own CombatTargetProvider component.");
        Assert.That(Regex.IsMatch(combatProviderStripped, @"\bCombatTargetSet\s+ResolveTargetSet\s*\("), Is.True,
            "CombatTargetProvider should resolve CombatTargetSet for the combat domain.");
        Assert.That(Regex.IsMatch(combatProviderStripped, @"\b(autoResolveTargetSet|preferWorldCacheResolvedTargetSet|registryFallbackByFaction)\b"), Is.False,
            "CombatTargetProvider must not keep legacy auto-resolve/registry fallback toggles.");
        Assert.That(Regex.IsMatch(combatProviderStripped, @"\bOrchestrationRegistry\s*\.\s*TryGetCombatTargetSet\s*\("), Is.False,
            "CombatTargetProvider must not silently fallback to OrchestrationRegistry after no-parallel-fallback rule.");
        Assert.That(Regex.IsMatch(idleProviderStripped, @"\bclass\s+IdleTargetProvider\b"), Is.True,
            "StrategyCombat should own IdleTargetProvider component.");
        Assert.That(Regex.IsMatch(idleProviderStripped, @"\bResolveSelfPosition\b"), Is.True,
            "IdleTargetProvider should own idle self-position target resolution.");
        Assert.That(Regex.IsMatch(idleProviderStripped, @"\bResolveDefaultSelfPosition\s*\("), Is.False,
            "IdleTargetProvider should not expose legacy default fallback helper once explicit provider ownership is required.");

        Assert.That(Regex.IsMatch(combatDomainStripped, @"\bCombatTargetProvider\s+combatTargetProvider\b"), Is.True,
            "CombatDomainComponent should depend on CombatTargetProvider component.");
        Assert.That(Regex.IsMatch(combatDomainStripped, @"combatTargetProvider\s*\.\s*ResolveTargetSet\s*\("), Is.True,
            "CombatDomainComponent should resolve target set via CombatTargetProvider.");
        Assert.That(Regex.IsMatch(idleDomainStripped, @"\bIdleTargetProvider\s+idleTargetProvider\b"), Is.True,
            "IdleDomainComponent should depend on IdleTargetProvider component.");
        Assert.That(Regex.IsMatch(idleRouteStripped, @"\bIdleTargetProvider\b"), Is.True,
            "StrategyCombat idle route should use IdleTargetProvider ownership seam.");
        Assert.That(Regex.IsMatch(idleRouteStripped, @"ResolveSelfPosition\s*\("), Is.True,
            "StrategyCombat idle route should resolve self-position through IdleTargetProvider.");
        Assert.That(Regex.IsMatch(idleRouteStripped, @"ResolveDefaultSelfPosition\s*\("), Is.False,
            "StrategyCombat idle route must not use legacy self-position fallback helper.");
        Assert.That(Regex.IsMatch(idleRouteStripped, @"_idleTargetProvider\s*!=\s*null"), Is.False,
            "StrategyCombat idle route should not branch between provider path and legacy fallback path.");
    }

    [Test]
    public void C04D_GenericOrchestrationComposition_ExtractedToRuntimeHost()
    {
        // ── File existence: RuntimeHost generic types ──────────────────
        Assert.That(File.Exists(RuntimeHostDomainTargetProviderPath), Is.True,
            $"Missing file: {RuntimeHostDomainTargetProviderPath}");
        Assert.That(File.Exists(RuntimeHostDomainOrchestratorComponentPath), Is.True,
            $"Missing file: {RuntimeHostDomainOrchestratorComponentPath}");
        Assert.That(File.Exists(RuntimeHostDomainOrchestratorCompositionPath), Is.True,
            $"Missing file: {RuntimeHostDomainOrchestratorCompositionPath}");
        Assert.That(File.Exists(RuntimeHostDomainComponentPath), Is.True,
            $"Missing file: {RuntimeHostDomainComponentPath}");
        Assert.That(File.Exists(RuntimeHostDomainRouteExecutionPolicyPath), Is.True,
            $"Missing file: {RuntimeHostDomainRouteExecutionPolicyPath}");
        Assert.That(File.Exists(RuntimeHostDomainRouteExecutionPolicyConsumerPath), Is.True,
            $"Missing file: {RuntimeHostDomainRouteExecutionPolicyConsumerPath}");
        Assert.That(File.Exists(RuntimeHostDomainRouteExecutionPolicyProviderPath), Is.True,
            $"Missing file: {RuntimeHostDomainRouteExecutionPolicyProviderPath}");
        Assert.That(File.Exists(StrategyCombatCombatTargetProviderPath), Is.True,
            $"Missing file: {StrategyCombatCombatTargetProviderPath}");
        Assert.That(File.Exists(StrategyCombatIdleTargetProviderPath), Is.True,
            $"Missing file: {StrategyCombatIdleTargetProviderPath}");

        // ── Read sources ───────────────────────────────────────────────
        string targetProviderBaseStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostDomainTargetProviderPath));
        string orchestratorComponentStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostDomainOrchestratorComponentPath));
        string orchestratorCompositionStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostDomainOrchestratorCompositionPath));
        string domainComponentStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostDomainComponentPath));
        string routePolicyStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostDomainRouteExecutionPolicyPath));
        string policyConsumerStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostDomainRouteExecutionPolicyConsumerPath));
        string routePolicyProviderStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostDomainRouteExecutionPolicyProviderPath));
        string combatProviderStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatCombatTargetProviderPath));
        string idleProviderStripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatIdleTargetProviderPath));
        string combatDomainComponentStripped = StripCommentsAndStrings(File.ReadAllText(CombatDomainComponentPath));
        string idleDomainComponentStripped = StripCommentsAndStrings(File.ReadAllText(IdleDomainComponentPath));

        // ── DomainTargetProvider (RuntimeHost, no Base suffix) ─────────
        Assert.That(Regex.IsMatch(targetProviderBaseStripped, @"\binterface\s+IDomainTargetProvider\b"), Is.True,
            "RuntimeHost must own the common orchestration-facing target-provider interface.");
        Assert.That(Regex.IsMatch(targetProviderBaseStripped, @"\benum\s+DomainTargetProviderValidationFailure\b"), Is.True,
            "RuntimeHost DomainTargetProvider file should define the shared target-provider validation result enum.");
        Assert.That(Regex.IsMatch(targetProviderBaseStripped, @"\bstatic\s+class\s+DomainTargetProviderValidation\b"), Is.True,
            "RuntimeHost DomainTargetProvider file should define shared target-provider validation helper.");
        Assert.That(Regex.IsMatch(targetProviderBaseStripped, @"\bValidate\s*\(\s*IDomainTargetProvider\s+\w+\s*,\s*OrchestrationDomainId\s+\w+\s*\)"), Is.True,
            "Shared target-provider validation helper should validate against the common interface + DomainId.");
        Assert.That(Regex.IsMatch(targetProviderBaseStripped, @"\babstract\s+class\s+DomainTargetProvider\s*:\s*MonoBehaviour\s*,\s*IDomainTargetProvider\b"), Is.True,
            "C04D target form: DomainTargetProvider (no Base suffix) abstract MonoBehaviour in RuntimeHost.");
        Assert.That(Regex.IsMatch(targetProviderBaseStripped, @"\bOrchestrationDomainId\s+DomainId\b"), Is.True,
            "Common target-provider interface/base should expose a typed DomainId.");
        Assert.That(Regex.IsMatch(targetProviderBaseStripped, @"\bIsConfiguredForOrchestration\b"), Is.True,
            "Common target-provider interface/base should expose a minimal orchestration readiness signal.");

        // ── Genre providers inherit generic DomainTargetProvider ───────
        Assert.That(Regex.IsMatch(combatProviderStripped, @"\bclass\s+CombatTargetProvider\s*:\s*DomainTargetProvider\b"), Is.True,
            "CombatTargetProvider should inherit generic DomainTargetProvider (no Base suffix).");
        Assert.That(Regex.IsMatch(combatProviderStripped, @"\boverride\s+OrchestrationDomainId\s+DomainId\s*=>\s*OrchestrationDomainId\s*\.\s*Combat\b"), Is.True,
            "CombatTargetProvider should identify itself via the shared DomainId contract.");
        Assert.That(Regex.IsMatch(combatProviderStripped, @"\boverride\s+bool\s+IsConfiguredForOrchestration\b"), Is.True,
            "CombatTargetProvider should expose readiness via shared orchestration-facing contract.");

        Assert.That(Regex.IsMatch(idleProviderStripped, @"\bclass\s+IdleTargetProvider\s*:\s*DomainTargetProvider\b"), Is.True,
            "IdleTargetProvider should inherit generic DomainTargetProvider (no Base suffix).");
        Assert.That(Regex.IsMatch(idleProviderStripped, @"\boverride\s+OrchestrationDomainId\s+DomainId\s*=>\s*OrchestrationDomainId\s*\.\s*Idle\b"), Is.True,
            "IdleTargetProvider should identify itself via the shared DomainId contract.");

        Assert.That(Regex.IsMatch(File.ReadAllText(CombatDomainComponentPath), @"\bDomainTargetProviderValidation\s*\.\s*Validate\s*\("), Is.True,
            "CombatDomainComponent should use the shared target-provider validation helper in runtime path.");
        Assert.That(Regex.IsMatch(File.ReadAllText(StrategyCombatIdleExecutionRoutePath), @"\bDomainTargetProviderValidation\s*\.\s*Validate\s*\("), Is.True,
            "StrategyCombatIdleExecutionRoute should use the shared target-provider validation helper in runtime path.");

        // ── DomainRouteExecutionPolicy (RuntimeHost abstract SO) ──────
        Assert.That(Regex.IsMatch(routePolicyStripped, @"\babstract\s+class\s+DomainRouteExecutionPolicy\s*:\s*ScriptableObject\b"), Is.True,
            "C04D: DomainRouteExecutionPolicy is abstract ScriptableObject base in RuntimeHost.");

        // ── IDomainRouteExecutionPolicyConsumer (RuntimeHost) ─────────
        Assert.That(Regex.IsMatch(policyConsumerStripped, @"\binterface\s+IDomainRouteExecutionPolicyConsumer\b"), Is.True,
            "C04D: generic route-policy consumer interface lives in RuntimeHost (not genre-specific).");
        Assert.That(Regex.IsMatch(policyConsumerStripped, @"\bApplyRouteExecutionPolicy\s*\(\s*DomainRouteExecutionPolicy\b"), Is.True,
            "IDomainRouteExecutionPolicyConsumer.ApplyRouteExecutionPolicy takes generic DomainRouteExecutionPolicy.");

        // ── DomainOrchestratorComposition (RuntimeHost static helper) ──
        Assert.That(Regex.IsMatch(orchestratorCompositionStripped, @"\bstatic\s+class\s+DomainOrchestratorComposition\b"), Is.True,
            "C04D: DomainOrchestratorComposition is the generic static composition helper in RuntimeHost.");
        Assert.That(Regex.IsMatch(orchestratorCompositionStripped, @"\bCreateArbitrationProfile\s*\("), Is.True,
            "DomainOrchestratorComposition should own common arbitration-profile construction.");
        Assert.That(Regex.IsMatch(orchestratorCompositionStripped, @"\bCreateFixedRouteContributorWithUnknownFallback\s*\("), Is.True,
            "DomainOrchestratorComposition should own the common route-contributor pattern.");
        Assert.That(Regex.IsMatch(orchestratorCompositionStripped, @"\bShouldRebuildRouteExecutorForPolicyChange\s*\("), Is.True,
            "DomainOrchestratorComposition should own the route-policy rebuild decision.");

        // ── DomainOrchestratorComponent (RuntimeHost) ──────────────────
        Assert.That(Regex.IsMatch(orchestratorComponentStripped, @"\bclass\s+DomainOrchestratorComponent\s*:\s*DomainOrchestrator\s*,\s*IDomainArbitrationProfileSource\s*,\s*IDomainRouteExecutionPolicyConsumer\b"), Is.True,
            "C04D: DomainOrchestratorComponent implements generic IDomainRouteExecutionPolicyConsumer.");
        Assert.That(Regex.IsMatch(orchestratorComponentStripped, @"\bDomainComponent\s+domainComponent\b"), Is.True,
            "DomainOrchestratorComponent should own one typed DomainComponent reference.");
        Assert.That(Regex.IsMatch(orchestratorComponentStripped, @"\bcomponent\s*\.\s*EvaluateDomain\s*\("), Is.True,
            "DomainOrchestratorComponent should delegate proposal evaluation to the configured domain component.");
        Assert.That(Regex.IsMatch(orchestratorComponentStripped, @"\bcomponent\s*\.\s*CreateArbiterBindingContributor\s*\("), Is.True,
            "DomainOrchestratorComponent should delegate arbiter binding contributions to the domain component.");
        Assert.That(Regex.IsMatch(orchestratorComponentStripped, @"\bcomponent\s*\.\s*CreateExecutionRouteContributor\s*\("), Is.True,
            "DomainOrchestratorComponent should delegate execution route contribution to the domain component.");
        Assert.That(Regex.IsMatch(orchestratorComponentStripped, @"\bcomponent\s*\.\s*ApplyRouteExecutionPolicy\s*\("), Is.True,
            "DomainOrchestratorComponent should delegate route-policy application to the domain component.");
        Assert.That(Regex.IsMatch(orchestratorComponentStripped, @"\bDomainOrchestratorComposition\s*\.\s*CreateArbitrationProfile\s*\("), Is.True,
            "DomainOrchestratorComponent should use DomainOrchestratorComposition helper for arbitration-profile construction.");

        // ── DomainComponent (RuntimeHost abstract base) ────────────────
        Assert.That(Regex.IsMatch(domainComponentStripped, @"\babstract\s+class\s+DomainComponent\s*:\s*MonoBehaviour\b"), Is.True,
            "C04D: DomainComponent is abstract MonoBehaviour base in RuntimeHost (no Base suffix).");
        Assert.That(Regex.IsMatch(domainComponentStripped, @"\bApplyRouteExecutionPolicy\s*\(\s*DomainRouteExecutionPolicy\b"), Is.True,
            "DomainComponent.ApplyRouteExecutionPolicy takes generic DomainRouteExecutionPolicy.");

        // ── Genre domain components inherit generic DomainComponent ────
        Assert.That(Regex.IsMatch(combatDomainComponentStripped, @"\bclass\s+CombatDomainComponent\s*:\s*DomainComponent\b"), Is.True,
            "CombatDomainComponent should inherit generic DomainComponent (RuntimeHost).");
        Assert.That(Regex.IsMatch(idleDomainComponentStripped, @"\bclass\s+IdleDomainComponent\s*:\s*DomainComponent\b"), Is.True,
            "IdleDomainComponent should inherit generic DomainComponent (RuntimeHost).");
        Assert.That(Regex.IsMatch(combatDomainComponentStripped, @"\bclass\s+CombatDomainComponent\s*:\s*DomainOrchestrator\b"), Is.False,
            "CombatDomainComponent should not be a DomainOrchestrator entrypoint.");
        Assert.That(Regex.IsMatch(idleDomainComponentStripped, @"\bclass\s+IdleDomainComponent\s*:\s*DomainOrchestrator\b"), Is.False,
            "IdleDomainComponent should not be a DomainOrchestrator entrypoint.");
        Assert.That(Regex.IsMatch(combatDomainComponentStripped, @"\bDomainOrchestratorComposition\s*\.\s*CreateFixedRouteContributorWithUnknownFallback\s*\("), Is.True,
            "CombatDomainComponent should use generic DomainOrchestratorComposition helper for route-contributor pattern.");
        Assert.That(Regex.IsMatch(idleDomainComponentStripped, @"\bDomainOrchestratorComposition\s*\.\s*CreateFixedRouteContributorWithUnknownFallback\s*\("), Is.True,
            "IdleDomainComponent should use generic DomainOrchestratorComposition helper for route-contributor pattern.");
        Assert.That(Regex.IsMatch(combatDomainComponentStripped, @"\bDomainOrchestratorComposition\s*\.\s*ShouldRebuildRouteExecutorForPolicyChange\s*\("), Is.True,
            "CombatDomainComponent should use generic DomainOrchestratorComposition helper for route-policy rebuild decision.");
        Assert.That(Regex.IsMatch(idleDomainComponentStripped, @"\bDomainOrchestratorComposition\s*\.\s*ShouldRebuildRouteExecutorForPolicyChange\s*\("), Is.True,
            "IdleDomainComponent should use generic DomainOrchestratorComposition helper for route-policy rebuild decision.");

        // ── DomainRouteExecutionPolicyProvider (RuntimeHost) ───────────
        Assert.That(Regex.IsMatch(routePolicyProviderStripped, @"\bsealed\s+class\s+DomainRouteExecutionPolicyProvider\s*:\s*MonoBehaviour\s*,\s*IDomainRouteExecutionPolicyConsumer\b"), Is.True,
            "C04D: DomainRouteExecutionPolicyProvider implements generic IDomainRouteExecutionPolicyConsumer in RuntimeHost.");
        Assert.That(Regex.IsMatch(routePolicyProviderStripped, @"\bDomainRouteExecutionPolicy\s+routeExecutionPolicy\b"), Is.True,
            "Route-policy provider should own the serialized DomainRouteExecutionPolicy field (generic base type).");
        Assert.That(Regex.IsMatch(combatDomainComponentStripped, @"\bDomainRouteExecutionPolicyProvider\s+routeExecutionPolicyProvider\b"), Is.True,
            "CombatDomainComponent should depend on generic DomainRouteExecutionPolicyProvider component.");
        Assert.That(Regex.IsMatch(idleDomainComponentStripped, @"\bDomainRouteExecutionPolicyProvider\s+routeExecutionPolicyProvider\b"), Is.True,
            "IdleDomainComponent should depend on generic DomainRouteExecutionPolicyProvider component.");
        Assert.That(Regex.IsMatch(combatDomainComponentStripped, @"\bStrategyCombatRouteExecutionPolicyAsset\s+routeExecutionPolicy\b"), Is.False,
            "CombatDomainComponent should not keep duplicated serialized route policy asset ownership after provider extraction.");
        Assert.That(Regex.IsMatch(idleDomainComponentStripped, @"\bStrategyCombatRouteExecutionPolicyAsset\s+routeExecutionPolicy\b"), Is.False,
            "IdleDomainComponent should not keep duplicated serialized route policy asset ownership after provider extraction.");

        // ── No legacy StrategyCombat-specific types in RuntimeHost ─────
        Assert.That(Regex.IsMatch(orchestratorComponentStripped, @"\bCombatDomainOrchestrator\b|\bIdleDomainOrchestrator\b"), Is.False,
            "C04D target form should not reference separate Combat/Idle orchestrator entrypoint classes.");
    }

    [Test]
    public void RuntimeHost_Arbiter_HidesInspectorDomainFallback_AndFailFastsWithoutCompositionApply()
    {
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");

        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));
        string setDomainsBody = ExtractMethodBody(
            arbiterStripped,
            "public void SetDomainOrchestratorsForComposition(DomainOrchestrator[] domains)");
        string produceTickBody = ExtractMethodBody(
            arbiterStripped,
            "public OrchestrationTickResult ProduceTick(float now)");

        Assert.That(HiddenSerializedDomainOrchestratorsFieldRegex.IsMatch(arbiterStripped), Is.True,
            "OrchestrationArbiter domainOrchestrators storage must be hidden in inspector (OrchestrationLoop/composition seam is source-of-truth).");

        Assert.That(string.IsNullOrEmpty(setDomainsBody), Is.False,
            "Could not extract SetDomainOrchestratorsForComposition(...) body.");
        Assert.That(string.IsNullOrEmpty(produceTickBody), Is.False,
            "Could not extract ProduceTick(...) body.");

        Assert.That(Regex.IsMatch(setDomainsBody, @"_domainCompositionApplied\s*=\s*true\s*;"), Is.True,
            "Composition seam must mark domain composition as applied.");

        Assert.That(Regex.IsMatch(produceTickBody, @"\bif\s*\(\s*!\s*_domainCompositionApplied\s*\)"), Is.True,
            "ProduceTick must fail-fast when Bridge domain composition has not been applied.");

        Assert.That(Regex.IsMatch(produceTickBody, @"\bDebug\s*\.\s*LogError\s*\("), Is.True,
            "ProduceTick fail-fast path should log an error for missing domain composition source-of-truth.");
    }

    [Test]
    public void FutureGate_RuntimePipeline_UsesProposalContractsOutsideFramework()
    {
        var roots = new[]
        {
            "Packages/com.morboo.core/Runtime",
            "Packages/com.morboo.runtimehost/Runtime",
            StrategyCombatOrchestrationRoot,
            "Assets/Scripts"
        };

        int matches = CountMatchesInRoots(roots, new Regex(@"\b(IProposalSource|Proposal|WorldSnapshot)\b", RegexOptions.Compiled));

        Assert.That(matches, Is.GreaterThan(0),
            "Expected proposal contracts usage outside Framework after migration.");
    }

    [Test]
    public void FutureGate_RuntimePipeline_UsesDomainEvents()
    {
        var roots = new[]
        {
            "Packages/com.morboo.core/Runtime",
            "Packages/com.morboo.runtimehost/Runtime",
            StrategyCombatOrchestrationRoot,
            "Assets/Scripts"
        };

        int matches = CountMatchesInRoots(roots, new Regex(@"\b(IEventBus|IDomainEvent)\b", RegexOptions.Compiled));

        Assert.That(matches, Is.GreaterThan(0),
            "Expected IEventBus/IDomainEvent usage outside Framework/Systems after migration.");
    }

    [Test]
    public void OrchestrationLoop_ImplementsBusProviderInterfaces()
    {
        string loopPath = RuntimeHostLoopPath;
        Assert.That(File.Exists(loopPath), Is.True,
            $"OrchestrationLoop file not found at {loopPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(loopPath));
        Assert.That(Regex.IsMatch(stripped, @"\bIEventBusProvider\b"), Is.True,
            "OrchestrationLoop must implement IEventBusProvider (C05).");
        Assert.That(Regex.IsMatch(stripped, @"\bICommandBusProvider\b"), Is.True,
            "OrchestrationLoop must implement ICommandBusProvider (C05).");
    }

    [Test]
    public void OrchestrationPipeline_FlushesEventBusAfterCommandBus()
    {
        string pipelinePath = RuntimeHostPipelinePath;
        Assert.That(File.Exists(pipelinePath), Is.True,
            $"OrchestrationPipeline file not found at {pipelinePath}");

        string content = File.ReadAllText(pipelinePath);
        int commandFlushPos = content.IndexOf("_commandBus.Flush()");
        int eventFlushPos = content.IndexOf("_eventBus.Flush()");

        Assert.That(commandFlushPos, Is.GreaterThan(0),
            "OrchestrationPipeline must call _commandBus.Flush().");
        Assert.That(eventFlushPos, Is.GreaterThan(0),
            "OrchestrationPipeline must call _eventBus.Flush() (C05).");
        Assert.That(eventFlushPos, Is.GreaterThan(commandFlushPos),
            "EventBus.Flush must occur AFTER CommandBus.Flush in pipeline tick sequence (C05).");
    }

    [Test, Ignore("Enable after C04A domain onboarding simplification; only allowlisted transitional seam is currently permitted.")]
    public void FutureGate_RuntimeHost_ArbitrationWiring_HasNoDomainNameBranchingOutsideAllowlist()
    {
        var files = ResolveExistingFiles(
            RuntimeHostArbiterPath,
            RuntimeHostRouterPath,
            RuntimeHostLoopPath);

        Assert.That(files.Count, Is.GreaterThan(0),
            "Could not locate RuntimeHost orchestration arbitration/wiring files.");

        var violations = new List<string>();
        foreach (string file in files)
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            foreach (Match m in DomainNameBranchingConstructRegex.Matches(stripped))
            {
                if (IsAllowlistedRuntimeHostDomainBranching(file, stripped, m))
                    continue;

                violations.Add($"{file}: token '{m.Value.Trim()}'");
            }

            // Also catch expression-based domain classification (not only if/switch),
            // e.g. `return proposal.DomainKey == OrchestrationDomainKeys.Combat;`.
            foreach (Match m in DomainKeyCombatIdleTokenRegex.Matches(stripped))
            {
                if (IsAllowlistedRuntimeHostDomainToken(file, stripped, m))
                    continue;

                violations.Add($"{file}: token '{m.Value}'");
            }
        }

        Assert.That(violations, Is.Empty,
            "RuntimeHost arbitration/wiring contains domain-name specific branching/classification outside allowlist:\n" +
            string.Join("\n", violations));
    }

    [Test]
    public void StrategyCombat_Adapters_UseTypedOrchestrationLoopRef_WithoutGetComponentFallback()
    {
        foreach (string file in ResolveExistingFiles(
                     StrategyCombatCombatCommandAdapterPath,
                     StrategyCombatIdleCommandAdapterPath))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));

            Assert.That(SerializedUntypedDependencyHolderRegex.IsMatch(stripped), Is.False,
                $"Adapter should not use untyped serialized dependency holder: {file}");

            Assert.That(OrchestrationLoopGetComponentFallbackRegex.IsMatch(stripped), Is.False,
                $"Adapter should not resolve OrchestrationLoop via GetComponent fallback: {file}");

            Assert.That(OrchestrationLoopUntypedCastRegex.IsMatch(stripped), Is.False,
                $"Adapter should not cast serialized dependency holder to OrchestrationLoop: {file}");
        }
    }

    [Test, Ignore("Enable after world-query cleanup (commit C07).")]
    public void FutureGate_Domains_DoNotDowncastWorldQueryToConcreteCache()
    {
        Assert.That(Directory.Exists(StrategyCombatDomainsRoot), Is.True,
            $"Missing directory: {StrategyCombatDomainsRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(StrategyCombatDomainsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = WorldCacheDowncastRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "Domains downcast IWorldQuery to concrete cache:\n" + string.Join("\n", violations));
    }

    [Test]
    public void FutureGate_Orchestration_HasNoUntypedSerializedDependencyHolders()
    {
        Assert.That(Directory.Exists(StrategyCombatOrchestrationRoot), Is.True,
            $"Missing directory: {StrategyCombatOrchestrationRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(StrategyCombatOrchestrationRoot, "*.cs", SearchOption.AllDirectories))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = SerializedUntypedDependencyHolderRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "Orchestration still uses untyped serialized dependency holders:\n" + string.Join("\n", violations));
    }

    [Test, Ignore("Enable after data-driven domain onboarding migration (commit C04A).")]
    public void FutureGate_Orchestration_DomainVariation_PrefersDataDrivenOnboarding()
    {
        var hostPipelineFiles = ResolveExistingFiles(
            RuntimeHostArbiterPath,
            RuntimeHostRouterPath,
            RuntimeHostLoopPath,
            StrategyCombatArbiterPath,
            StrategyCombatRouterPath,
            StrategyCombatLoopPath);

        Assert.That(hostPipelineFiles.Count, Is.GreaterThan(0),
            "Could not locate orchestration host pipeline files (arbiter/router/loop).");

        int descriptorCount = CountMatchesInRoots(
            new[] { StrategyCombatOrchestrationRoot },
            DomainOnboardingDescriptorRegex);

        int hardcodedBranchCount = CountTokenOccurrencesInFiles(
            hostPipelineFiles,
            HardcodedDomainBranchingRegex);

        Assert.That(descriptorCount, Is.GreaterThan(0),
            "Expected data-driven onboarding descriptors/contracts (DomainModule/DomainRegistration/DomainDescriptor).");

        Assert.That(hardcodedBranchCount, Is.EqualTo(0),
            $"Host pipeline still contains hardcoded Combat/Idle branching tokens. Count={hardcodedBranchCount}.");
    }

    // ──────────────────────────────────────────────────────────────────
    //  C06 — Actor Capability Architecture Tests
    // ──────────────────────────────────────────────────────────────────

    const string RuntimeHostActorCapabilitiesRoot = "Packages/com.morboo.runtimehost/Runtime/Orchestration/ActorCapabilities";
    const string RuntimeHostIActorCapabilityQueryPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/ActorCapabilities/IActorCapabilityQuery.cs";
    const string RuntimeHostWorldCachePath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationWorldCache.cs";
    const string RuntimeHostArbiterContextPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiterContext.cs";
    const string RuntimeHostCombatTargetingPolicyAssetPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Domains/Combat/Targeting/CombatTargetingPolicyAsset.cs";
    const string RuntimeHostIdlePolicyAssetPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Domains/Idle/IdlePolicyAsset.cs";

    [Test]
    public void C06_RuntimeHost_IActorCapabilityQuery_Exists()
    {
        Assert.That(File.Exists(RuntimeHostIActorCapabilityQueryPath), Is.True,
            $"Missing IActorCapabilityQuery interface: {RuntimeHostIActorCapabilityQueryPath}");

        string content = File.ReadAllText(RuntimeHostIActorCapabilityQueryPath);
        Assert.That(content.Contains("interface IActorCapabilityQuery"), Is.True,
            "IActorCapabilityQuery interface declaration not found.");
        Assert.That(content.Contains("GetActorCapabilities"), Is.True,
            "IActorCapabilityQuery must declare GetActorCapabilities method.");
        Assert.That(content.Contains("TryGetActorCapabilities"), Is.True,
            "IActorCapabilityQuery must declare TryGetActorCapabilities method.");
    }

    [Test]
    public void C06_RuntimeHost_WorldCache_Implements_IActorCapabilityQuery()
    {
        Assert.That(File.Exists(RuntimeHostWorldCachePath), Is.True,
            $"Missing file: {RuntimeHostWorldCachePath}");

        string content = File.ReadAllText(RuntimeHostWorldCachePath);
        Assert.That(Regex.IsMatch(content, @"class\s+OrchestrationWorldCache\b[^{]*\bIActorCapabilityQuery\b"), Is.True,
            "OrchestrationWorldCache must implement IActorCapabilityQuery.");
        Assert.That(content.Contains("SnapshotActorCapabilities"), Is.True,
            "WorldCache must provide SnapshotActorCapabilities method.");
    }

    [Test]
    public void C06_RuntimeHost_ArbiterContext_Has_ActorCapabilities_Field()
    {
        Assert.That(File.Exists(RuntimeHostArbiterContextPath), Is.True,
            $"Missing file: {RuntimeHostArbiterContextPath}");

        string content = File.ReadAllText(RuntimeHostArbiterContextPath);
        Assert.That(Regex.IsMatch(content, @"\bIActorCapabilityQuery\s+ActorCapabilities\b"), Is.True,
            "OrchestrationArbiterContext must have an IActorCapabilityQuery ActorCapabilities field.");
    }

    [Test]
    public void C06_StrategyCombat_CombatDomainComponent_References_EngagementPolicy()
    {
        Assert.That(File.Exists(CombatDomainComponentPath), Is.True,
            $"Missing file: {CombatDomainComponentPath}");

        string content = File.ReadAllText(CombatDomainComponentPath);
        Assert.That(Regex.IsMatch(content, @"\bActorCapabilityEngagementPolicy\b.*\bengagementPolicy\b"), Is.True,
            "CombatDomainComponent must reference ActorCapabilityEngagementPolicy.");
    }

    [Test]
    public void C06_RuntimeHost_CombatTargetingPolicyAsset_Implements_IActorCapabilityGatedPolicy()
    {
        Assert.That(File.Exists(RuntimeHostCombatTargetingPolicyAssetPath), Is.True,
            $"Missing file: {RuntimeHostCombatTargetingPolicyAssetPath}");

        string content = File.ReadAllText(RuntimeHostCombatTargetingPolicyAssetPath);
        Assert.That(Regex.IsMatch(content, @"class\s+CombatTargetingPolicyAsset\b[^{]*\bIActorCapabilityGatedPolicy\b"), Is.True,
            "CombatTargetingPolicyAsset must implement IActorCapabilityGatedPolicy.");
    }

    [Test]
    public void C06_RuntimeHost_IdlePolicyAsset_Implements_IActorCapabilityGatedPolicy()
    {
        Assert.That(File.Exists(RuntimeHostIdlePolicyAssetPath), Is.True,
            $"Missing file: {RuntimeHostIdlePolicyAssetPath}");

        string content = File.ReadAllText(RuntimeHostIdlePolicyAssetPath);
        Assert.That(Regex.IsMatch(content, @"class\s+IdlePolicyAsset\b[^{]*\bIActorCapabilityGatedPolicy\b"), Is.True,
            "IdlePolicyAsset must implement IActorCapabilityGatedPolicy.");
    }

    static int CountMatchesInRoots(IEnumerable<string> roots, Regex regex)
    {
        int count = 0;

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string stripped = StripCommentsAndStrings(File.ReadAllText(file));
                if (regex.IsMatch(stripped))
                    count++;
            }
        }

        return count;
    }

    static int CountTokenOccurrencesInFiles(IEnumerable<string> files, Regex regex)
    {
        int count = 0;

        foreach (string file in files)
        {
            if (!File.Exists(file))
                continue;

            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            count += regex.Matches(stripped).Count;
        }

        return count;
    }

    static List<string> ResolveExistingFiles(params string[] candidatePaths)
    {
        var files = new List<string>();
        for (int i = 0; i < candidatePaths.Length; i++)
        {
            string path = candidatePaths[i];
            if (File.Exists(path))
                files.Add(path);
        }

        return files;
    }

    static bool IsAllowlistedRuntimeHostDomainBranching(string file, string strippedSource, Match match)
    {
        // Transitional C04 allowlist: explicit classifier seam only.
        if (file == RuntimeHostArbiterPath && IsInsideMethod(strippedSource, match.Index, "bool IsStickyPrimaryProposal(in Proposal proposal)"))
            return true;

        return false;
    }

    static bool IsAllowlistedRuntimeHostDomainToken(string file, string strippedSource, Match match)
    {
        // Transitional C04 allowlist: explicit classifier seam only.
        if (file == RuntimeHostArbiterPath && IsInsideMethod(strippedSource, match.Index, "bool IsStickyPrimaryProposal(in Proposal proposal)"))
            return true;

        return false;
    }

    static bool IsInsideMethod(string source, int tokenIndex, string methodSignaturePrefix)
    {
        if (string.IsNullOrEmpty(source) || tokenIndex < 0 || tokenIndex >= source.Length)
            return false;

        int signatureIndex = source.IndexOf(methodSignaturePrefix, StringComparison.Ordinal);
        if (signatureIndex < 0)
            return false;

        string body = ExtractMethodBody(source, methodSignaturePrefix, out int bodyStartIndex, out int bodyEndIndexExclusive);
        if (string.IsNullOrEmpty(body))
            return false;

        return tokenIndex >= bodyStartIndex && tokenIndex < bodyEndIndexExclusive;
    }

    static string ExtractMethodBody(string source, string methodSignaturePrefix)
    {
        return ExtractMethodBody(source, methodSignaturePrefix, out _, out _);
    }

    static string ExtractPropertyBody(string source, string propertySignaturePrefix)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(propertySignaturePrefix))
            return string.Empty;

        int sig = source.IndexOf(propertySignaturePrefix, StringComparison.Ordinal);
        if (sig < 0)
            return string.Empty;

        // Support expression-bodied properties: `public T P => expr;`
        int arrow = source.IndexOf("=>", sig, StringComparison.Ordinal);
        int firstSemicolon = source.IndexOf(';', sig);
        int firstBraceOpen = source.IndexOf('{', sig);
        if (arrow >= 0 && firstSemicolon > arrow && (firstBraceOpen < 0 || arrow < firstBraceOpen))
        {
            int exprStart = arrow + 2;
            int exprLen = firstSemicolon - exprStart;
            return exprLen > 0 ? source.Substring(exprStart, exprLen) : string.Empty;
        }

        int braceOpen = source.IndexOf('{', sig);
        if (braceOpen < 0)
            return string.Empty;

        int depth = 0;
        for (int i = braceOpen; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    int bodyStart = braceOpen + 1;
                    int bodyLen = i - bodyStart;
                    return bodyLen > 0 ? source.Substring(bodyStart, bodyLen) : string.Empty;
                }
            }
        }

        return string.Empty;
    }

    static string ExtractMethodBody(string source, string methodSignaturePrefix, out int bodyStartIndex, out int bodyEndIndexExclusive)
    {
        bodyStartIndex = -1;
        bodyEndIndexExclusive = -1;

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(methodSignaturePrefix))
            return string.Empty;

        int signatureIndex = source.IndexOf(methodSignaturePrefix, StringComparison.Ordinal);
        if (signatureIndex < 0)
            return string.Empty;

        int openBraceIndex = source.IndexOf('{', signatureIndex);
        if (openBraceIndex < 0)
            return string.Empty;

        int depth = 0;
        for (int i = openBraceIndex; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    bodyStartIndex = openBraceIndex + 1;
                    bodyEndIndexExclusive = i;
                    return source.Substring(bodyStartIndex, i - bodyStartIndex);
                }
            }
        }

        return string.Empty;
    }

    static string StripCommentsAndStrings(string code)
    {
        if (string.IsNullOrEmpty(code))
            return string.Empty;

        var sb = new StringBuilder(code.Length);
        bool inSingleLineComment = false;
        bool inMultiLineComment = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inChar = false;

        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            char next = i + 1 < code.Length ? code[i + 1] : '\0';

            if (inSingleLineComment)
            {
                if (c == '\n')
                {
                    inSingleLineComment = false;
                    sb.Append(c);
                }
                else
                {
                    sb.Append(' ');
                }
                continue;
            }

            if (inMultiLineComment)
            {
                if (c == '*' && next == '/')
                {
                    inMultiLineComment = false;
                    sb.Append("  ");
                    i++;
                }
                else
                {
                    sb.Append(c == '\n' ? '\n' : ' ');
                }
                continue;
            }

            if (inString)
            {
                if (!inVerbatimString && c == '\\')
                {
                    sb.Append("  ");
                    i++;
                    continue;
                }

                if (!inVerbatimString && c == '"')
                {
                    inString = false;
                    sb.Append(' ');
                    continue;
                }

                if (inVerbatimString && c == '"' && next == '"')
                {
                    sb.Append("  ");
                    i++;
                    continue;
                }

                if (inVerbatimString && c == '"')
                {
                    inString = false;
                    inVerbatimString = false;
                    sb.Append(' ');
                    continue;
                }

                sb.Append(c == '\n' ? '\n' : ' ');
                continue;
            }

            if (inChar)
            {
                if (c == '\\')
                {
                    sb.Append("  ");
                    i++;
                    continue;
                }

                if (c == '\'')
                {
                    inChar = false;
                    sb.Append(' ');
                    continue;
                }

                sb.Append(' ');
                continue;
            }

            if (c == '/' && next == '/')
            {
                inSingleLineComment = true;
                sb.Append("  ");
                i++;
                continue;
            }

            if (c == '/' && next == '*')
            {
                inMultiLineComment = true;
                sb.Append("  ");
                i++;
                continue;
            }

            if (c == '@' && next == '"')
            {
                inString = true;
                inVerbatimString = true;
                sb.Append("  ");
                i++;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                inVerbatimString = false;
                sb.Append(' ');
                continue;
            }

            if (c == '\'')
            {
                inChar = true;
                sb.Append(' ');
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
