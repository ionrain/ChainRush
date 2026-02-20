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
    const string StrategyCombatArbiterPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs";
    const string StrategyCombatRouterPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Execution/ExecutionRouter.cs";
    const string StrategyCombatLoopPath = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/OrchestrationLoop.cs";
    const string RuntimeHostArbiterPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationArbiter.cs";
    const string RuntimeHostRouterPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouter.cs";
    const string RuntimeHostLoopPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/OrchestrationLoop.cs";

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

    [Test, Ignore("Enable after proposal-model migration (commit C04).")]
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
