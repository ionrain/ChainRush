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
    const string RuntimeHostLoopPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/OrchestrationLoop.cs";
    const string StrategyCombatArbiterBindingAppliersPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Arbitration/StrategyCombatArbiterBindingAppliers.cs";
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
    static readonly Regex OrchestrationDomainModuleTypeRegex =
        new Regex(@"\bOrchestrationDomainModule\b", RegexOptions.Compiled);
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
    static readonly Regex ConsumerKeySwitchLikeRegex =
        new Regex(@"\bif\s*\([^)]*\bconsumerKey\b[^)]*==[^)]*RuntimeHostArbiterBindingConsumerKeys\.", RegexOptions.Compiled);

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
        Match m = PublishCallRegex.Match(stripped);

        Assert.That(m.Success, Is.False,
            $"OrchestrationArbiter must not publish commands directly: {m.Value}");
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
    public void StrategyCombat_ExecutionRouter_UsesDispatchContracts()
    {
        Assert.That(File.Exists(StrategyCombatRouterPath), Is.True,
            $"Missing file: {StrategyCombatRouterPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatRouterPath));

        Assert.That(Regex.IsMatch(stripped, @"\bDispatchCombatCommand\b"), Is.True,
            "ExecutionRouter must emit DispatchCombatCommand.");

        Assert.That(Regex.IsMatch(stripped, @"\bDispatchIdleCommand\b"), Is.True,
            "ExecutionRouter must emit DispatchIdleCommand.");
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
        Assert.That(Regex.IsMatch(runtimeHostContributorStripped, @"\bTryApplyArbiterBindingConsumer\s*\("), Is.True,
            "Apply-target seam should expose generic consumer-based application method.");
        Assert.That(Regex.IsMatch(runtimeHostContributorStripped,
            @"\bTryApply(IdleRolePolicyMapBinding|CombatRolePolicyMapBinding|CombatRoleConstraintsMapBinding)\s*\("), Is.False,
            "Domain-specific apply methods must not remain on IDomainArbiterBindingApplyTarget seam.");
        Assert.That(RuntimeHostBindingTargetAppliersTypeRegex.IsMatch(runtimeHostContributorStripped), Is.False,
            "RuntimeHost should not own built-in DomainArbiterBindingTargetAppliers after C04A applier ownership move.");
        Assert.That(StrategyCombatBindingTargetAppliersTypeRegex.IsMatch(strategyCombatAppliersStripped), Is.True,
            "StrategyCombat should own concrete arbiter binding appliers for Combat/Idle policy-map bindings.");
    }

    [Test]
    public void RuntimeHost_ApplyTarget_UsesConsumerRegistry_NotConsumerKeySwitch()
    {
        Assert.That(File.Exists(RuntimeHostArbiterPath), Is.True,
            $"Missing file: {RuntimeHostArbiterPath}");

        string arbiterStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostArbiterPath));
        string applyTargetBody = ExtractMethodBody(
            arbiterStripped,
            "bool IDomainArbiterBindingApplyTarget.TryApplyArbiterBindingConsumer(");

        Assert.That(string.IsNullOrEmpty(applyTargetBody), Is.False,
            "Could not extract IDomainArbiterBindingApplyTarget.TryApplyArbiterBindingConsumer(...) body.");
        Assert.That(Regex.IsMatch(arbiterStripped, @"\bTryResolveArbiterBindingConsumer\s*\("), Is.True,
            "OrchestrationArbiter should keep local consumer registry lookup seam for apply-target routing.");
        Assert.That(Regex.IsMatch(applyTargetBody, @"\bTryResolveArbiterBindingConsumer\s*\("), Is.True,
            "Apply-target method should resolve consumer handlers from local registry.");
        Assert.That(ConsumerKeySwitchLikeRegex.IsMatch(applyTargetBody), Is.False,
            "Apply-target method must not branch directly on RuntimeHostArbiterBindingConsumerKeys after consumer-registry step.");
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

        Assert.That(RegisterRouteCallRegex.Matches(routerStripped).Count, Is.GreaterThan(1),
            "ExecutionRouter should register built-in routes via RegisterRoute() in C04A bootstrap.");

        Assert.That(string.IsNullOrEmpty(executeBody), Is.False,
            "Could not extract ExecutionRouter.Execute(...) body.");

        Assert.That(Regex.IsMatch(executeBody, @"\bswitch\s*\("), Is.False,
            "ExecutionRouter.Execute(...) should not hardcode domain switch after route-registration seam bootstrap.");

        Assert.That(Regex.IsMatch(executeBody, @"\bTryExecuteRegisteredRoute\s*\("), Is.True,
            "ExecutionRouter.Execute(...) must dispatch via registered execution routes.");
    }

    [Test]
    public void RuntimeHost_OrchestrationLoop_UsesDomainModuleCompositionSeam()
    {
        Assert.That(File.Exists(RuntimeHostLoopPath), Is.True,
            $"Missing file: {RuntimeHostLoopPath}");

        string loopStripped = StripCommentsAndStrings(File.ReadAllText(RuntimeHostLoopPath));

        Assert.That(OrchestrationDomainModuleTypeRegex.IsMatch(loopStripped), Is.True,
            "OrchestrationLoop should expose OrchestrationDomainModule composition seam in C04A.");

        Assert.That(Regex.IsMatch(loopStripped, @"\bConfigureDomainModules\s*\("), Is.True,
            "OrchestrationLoop should centralize optional domain module configuration in ConfigureDomainModules().");

        Assert.That(Regex.IsMatch(loopStripped, @"\bmodule\s*\.\s*ConfigureArbiter\s*\("), Is.True,
            "Domain module seam should support arbiter configuration from loop composition entrypoint.");

        Assert.That(Regex.IsMatch(loopStripped, @"\bmodule\s*\.\s*ConfigureRouter\s*\("), Is.True,
            "Domain module seam should support router configuration from loop composition entrypoint.");
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

    [Test, Ignore("Enable after event-pipeline migration (commit C05).")]
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

    [Test, Ignore("Enable after typed-reference migration (commit C04A).")]
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
