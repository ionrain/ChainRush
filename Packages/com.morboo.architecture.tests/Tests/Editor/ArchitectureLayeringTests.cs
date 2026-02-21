using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public sealed class ArchitectureLayeringTests
{
    const string FrameworkAsmdefPath = "Packages/com.morboo.framework/Runtime/Morboo.Framework.asmdef";
    const string SystemsRuntimeAsmdefPath = "Packages/com.morboo.systems/Runtime/Morboo.Systems.asmdef";
    const string CoreAsmdefPath = "Packages/com.morboo.core/Runtime/Morboo.Core.asmdef";
    const string RuntimeHostAsmdefPath = "Packages/com.morboo.runtimehost/Runtime/Morboo.RuntimeHost.asmdef";
    const string ProjectTypeAsmdefPath = "Packages/com.morboo.integration.strategycombat/Runtime/Morboo.Integration.StrategyCombat.asmdef";
    const string FrameworkSourceRoot = "Packages/com.morboo.framework/Runtime";
    const string SystemsRuntimeSourceRoot = "Packages/com.morboo.systems/Runtime";
    const string CoreSourceRoot = "Packages/com.morboo.core/Runtime";
    const string RuntimeHostSourceRoot = "Packages/com.morboo.runtimehost/Runtime";
    const string ProjectTypeSourceRoot = "Packages/com.morboo.integration.strategycombat/Runtime";
    const string CoreEntityContractsPath = "Packages/com.morboo.core/Runtime/Entity/EntityContracts.cs";
    const string CoreActorContractsPath = "Packages/com.morboo.core/Runtime/Actor/ActorContracts.cs";
    const string CoreActorReadProjectionPath = "Packages/com.morboo.core/Runtime/Actor/ActorReadProjection.cs";
    const string StrategyCombatActorContractPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/IOrchestrationActor.cs";
    const string StrategyCombatWorldCachePath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Arbitration/OrchestrationWorldCache.cs";
    const string StrategyCombatExecutionRouterPath = "Packages/com.morboo.runtimehost/Runtime/Orchestration/Execution/ExecutionRouter.cs";

    const string FrameworkAssemblyName = "Morboo.Framework";

    static readonly string[] ForbiddenForFrameworkAsmdef =
    {
        "Orchestration.",
        "Orchestration.Integration.",
        "Morboo.Core",
        "Morboo.RuntimeHost",
        "Morboo.Systems",
        "Morboo.Integration.",
        "Integration.",
        "Game.Runtime"
    };

    static readonly string[] ForbiddenForCoreAndRuntimeHostAsmdef =
    {
        "Orchestration.Integration.",
        "Morboo.Integration.",
        "Integration.",
        "Game.Runtime"
    };

    static readonly string[] ForbiddenFrameworkAssemblies =
    {
        "Orchestration.",
        "Orchestration.Integration.",
        "Morboo.Core",
        "Morboo.RuntimeHost",
        "Morboo.Systems",
        "Morboo.Integration.",
        "Game.Runtime"
    };

    static readonly Regex ForbiddenEngineSymbolsRegex =
        new Regex(@"\b(Unit|Enemy|TopDownEngine|MoreMountains)\b", RegexOptions.Compiled);

    static readonly Regex ForbiddenRegistryInDomainsRegex =
        new Regex(@"\b(OrchestrationRegistry|IdleBoundsRegistry)\b", RegexOptions.Compiled);

    /// <summary>
    /// Tokens that must never appear in Framework source (outside comments/strings).
    /// IMPORTANT: Catches Unity types and orchestration-specific payloads leaking into Framework.
    /// </summary>
    static readonly Regex ForbiddenFrameworkSourceTokens =
        new Regex(@"\b(Transform|MonoBehaviour|ScriptableObject|CombatCommand|IdleCommand|Orchestration)\b",
            RegexOptions.Compiled);

    static readonly Regex ForbiddenFrameworkUsingDirective =
        new Regex(@"^\s*using\s+UnityEngine\b", RegexOptions.Compiled | RegexOptions.Multiline);

    static readonly Regex ForbiddenSystemsRuntimeTokensRegex =
        new Regex(@"\b(Orchestration|Economy|Goals|Generation)\b", RegexOptions.Compiled);

    static readonly Regex CoreForbiddenUnityUsingRegex =
        new Regex(@"^\s*using\s+UnityEngine\b", RegexOptions.Compiled | RegexOptions.Multiline);

    static readonly Regex CoreForbiddenUnityTypesRegex =
        new Regex(@"\b(UnityEngine\.Object|MonoBehaviour|Transform|Component|GameObject)\b", RegexOptions.Compiled);

    static readonly Regex RuntimeHostProjectRefsRegex =
        new Regex(@"Assets/_Project|Assets/Scripts/MorbooBridge|Integration\.Project|Morboo\.Bridge", RegexOptions.Compiled);

    static readonly Regex PackageRuntimeProjectRefsRegex =
        new Regex(@"Assets/Scripts/MorbooBridge|Game\.Runtime|Morboo\.Bridge|Integration\.Project", RegexOptions.Compiled);

    static readonly Regex ProjectTypeProjectAssetsRegex =
        new Regex(@"\b(UnitClassRoleMapAsset|EnemyTypeCapabilitiesMap)\b", RegexOptions.Compiled);

    static readonly Regex StateQueryUsageRegex =
        new Regex(@"\bTryGetState\s*\(", RegexOptions.Compiled);

    static readonly Regex StateWriteBackUsageRegex =
        new Regex(@"\b(SetLifecycleState|SetAlive)\s*\(", RegexOptions.Compiled);

    static readonly Regex LegacyEntityTraitKeysTokenRegex =
        new Regex(@"\b(EntityStateTraitKeys|BridgeEntityStateTraitKeys)\b", RegexOptions.Compiled);

    static readonly Regex UnitReporterGuardedLegacyUnitFallbackRegex =
        new Regex(@"else\s+if\s*\(\s*entityState\s*==\s*null\s*&&\s*_unit\s*!=\s*null\s*\)", RegexOptions.Compiled);

    static readonly Regex UnitReporterUngardedLegacyUnitFallbackRegex =
        new Regex(@"else\s+if\s*\(\s*_unit\s*!=\s*null\s*\)", RegexOptions.Compiled);

    static readonly Regex UnitReporterGuardedLegacyHealthFallbackRegex =
        new Regex(@"else\s+if\s*\(\s*entityState\s*==\s*null\s*&&\s*hasHealth\s*\)", RegexOptions.Compiled);

    static readonly Regex EnemyReporterGuardedLegacyEnemyFallbackRegex =
        new Regex(@"else\s+if\s*\(\s*entityState\s*==\s*null\s*&&\s*_enemy\s*!=\s*null\s*\)", RegexOptions.Compiled);

    static readonly Regex EnemyReporterUngardedLegacyEnemyFallbackRegex =
        new Regex(@"else\s+if\s*\(\s*_enemy\s*!=\s*null\s*\)", RegexOptions.Compiled);

    static readonly Regex EnemyReporterGuardedLegacyHealthFallbackRegex =
        new Regex(@"else\s+if\s*\(\s*entityState\s*==\s*null\s*&&\s*hasHealth\s*\)", RegexOptions.Compiled);

    static readonly Regex CoreEntityLifecycleEnumRegex =
        new Regex(@"\benum\s+EntityLifecycleState\b", RegexOptions.Compiled);

    static readonly Regex CoreEntityModelLifecyclePropertyRegex =
        new Regex(@"\bEntityLifecycleState\s+LifecycleState\s*\{\s*get\s*;\s*\}", RegexOptions.Compiled);

    static readonly Regex CoreEntityAccessorSetLifecycleRegex =
        new Regex(@"\bSetLifecycleState\s*\(\s*EntityLifecycleState\s+\w+\s*\)", RegexOptions.Compiled);

    static readonly Regex CoreActorContractsLifecycleEnumRegex =
        new Regex(@"\benum\s+EntityLifecycleState\b", RegexOptions.Compiled);

    static readonly Regex CoreActorRuntimeHandleRegex =
        new Regex(@"\binterface\s+IActorRuntimeHandle\s*:\s*IEntityIdProvider\b", RegexOptions.Compiled);

    static readonly Regex CoreActorRuntimeLifecycleMethodRegex =
        new Regex(@"\bEntityLifecycleState\s+GetLifecycleState\s*\(", RegexOptions.Compiled);

    static readonly Regex CoreActorRuntimeLegacyAliveMethodRegex =
        new Regex(@"\bIsAlive\s*\(", RegexOptions.Compiled);

    static readonly Regex StrategyCombatActorExtendsCoreHandleRegex =
        new Regex(@"\binterface\s+IOrchestrationActor\s*:\s*IFactionAssetProvider\s*,\s*IActorRuntimeHandle\b", RegexOptions.Compiled);

    static readonly Regex StrategyCombatWorldCacheActorDowncastRegex =
        new Regex(@"\bactor\s+as\s+IEntityIdProvider\b", RegexOptions.Compiled);

    static readonly Regex CoreActorReadProjectionContractRegex =
        new Regex(@"\binterface\s+IActorReadProjectionQuery\b", RegexOptions.Compiled);

    static readonly Regex CoreActorReadProjectionHostilityTokenRegex =
        new Regex(@"\bIsHostile\b", RegexOptions.Compiled);

    static readonly Regex CoreActorReadProjectionLifecycleStateRegex =
        new Regex(@"\bEntityLifecycleState\s+LifecycleState\b", RegexOptions.Compiled);

    static readonly Regex CoreActorReadProjectionLegacyAliveTokenRegex =
        new Regex(@"\bIsAlive\b", RegexOptions.Compiled);

    static readonly Regex StrategyCombatWorldCacheProjectionContractRegex =
        new Regex(@"\bclass\s+OrchestrationWorldCache\s*:\s*IWorldQuery\s*,\s*IActorReadProjectionQuery\b", RegexOptions.Compiled);

    static readonly Regex StrategyCombatExecutionRouterProjectionUsageRegex =
        new Regex(@"\bTryGetActorReadProjection\s*\(", RegexOptions.Compiled);

    static readonly Regex CoreCombatIdleContractsRegex =
        new Regex(@"\b(CombatCommand|IdleCommand|DispatchCombatCommand|DispatchIdleCommand|ICombatCommandReceiver|IIdleCommandReceiver|IIdleBoundsProvider|CombatActionId|CombatGoalId|CombatState)\b",
            RegexOptions.Compiled);

    static readonly Regex RuntimeHostDomainSpecificTokensRegex =
        new Regex(@"\b(CombatOrchestratorLite|IdleOrchestratorLite|CombatTargetingPolicyAsset|IdlePolicyAsset|CombatRolePolicyMapAsset|IdleRolePolicyMapAsset|CombatMoveConstraintsAsset|CombatRoleConstraintsMapAsset|CombatTargetSet|OrchestrationArbiter|OrchestrationLoop|ExecutionRouter|ExecutionContext|DispatchCombatCommand|DispatchIdleCommand)\b",
            RegexOptions.Compiled);

    static readonly Regex ForbiddenSirenixUsingRegex =
        new Regex(@"^\s*using\s+Sirenix\.", RegexOptions.Compiled | RegexOptions.Multiline);

    static readonly Regex ForbiddenSirenixTokenRegex =
        new Regex(@"\bSirenix\.", RegexOptions.Compiled);

    static readonly Regex SerializedGameObjectDependencyHolderRegex =
        new Regex(@"\[SerializeField\]\s*(?:\[[^\]]+\]\s*)*(?:private|public|protected|internal)?\s*GameObject\b",
            RegexOptions.Compiled);

    static readonly string[] ForbiddenProjectAssemblyReferences =
    {
        "Game.Runtime",
        "Morboo.Bridge",
        "Integration.Project"
    };

    static readonly string[] ForbiddenTopDownEngineAssemblyReferences =
    {
        "MoreMountains.TopDownEngine"
    };

    static readonly Regex ForbiddenTopDownEngineNamespaceRegex =
        new Regex(@"\bMoreMountains\.TopDownEngine\b", RegexOptions.Compiled);

    static readonly Regex KernelContractDeclarationRegex =
        new Regex(@"\b(interface|class|struct)\s+(IGameFlowService|IScenarioService|IObjectiveService|IOutcomeService|IRulebookProvider|ISessionStateStore|IProfileStateStore|ISaveLoadService|IEconomyLedger|IRewardService|IEntityRegistry|IEntityFactory|IEntityLifecycleService|IEntitySnapshotStore|IEntityViewBinder|IEntityStateQuery|IEntityStateAccessor)\b",
            RegexOptions.Compiled);

    static readonly Regex EntityKernelContractDeclarationRegex =
        new Regex(@"\binterface\s+(IEntityRegistry|IEntityFactory|IEntityLifecycleService|IEntitySnapshotStore|IEntityViewBinder|IEntityStateQuery|IEntityStateAccessor)\b",
            RegexOptions.Compiled);

    static readonly Regex BridgeEntityStateDowncastRegex =
        new Regex(@"\b(is|as)\s+EntityState\b|TryGet\s*\([^)]*out\s+IEntityModel\b", RegexOptions.Compiled);

    static readonly Regex EntityIdDeclarationRegex =
        new Regex(@"\b(struct|class)\s+EntityId\b", RegexOptions.Compiled);

    // ── Existing layering tests ──────────────────────────────────────

    [Test]
    public void FrameworkAsmdef_DoesNotReference_Orchestration_Integration_OrGameRuntime()
    {
        AssertAsmdefHasNoForbiddenReferences(FrameworkAsmdefPath, ForbiddenForFrameworkAsmdef);
    }

    [Test]
    public void OrchestrationCoreAsmdef_DoesNotReference_Integration_OrGameRuntime()
    {
        AssertAsmdefHasNoForbiddenReferences(CoreAsmdefPath, ForbiddenForCoreAndRuntimeHostAsmdef);
    }

    [Test]
    public void OrchestrationRuntimeHostAsmdef_DoesNotReference_Integration_OrGameRuntime()
    {
        AssertAsmdefHasNoForbiddenReferences(RuntimeHostAsmdefPath, ForbiddenForCoreAndRuntimeHostAsmdef);
    }

    [Test]
    public void SystemsRuntimeAsmdef_Exists()
    {
        Assert.That(File.Exists(SystemsRuntimeAsmdefPath), Is.True, $"Missing asmdef: {SystemsRuntimeAsmdefPath}");
    }

    [Test]
    public void ProjectTypeAsmdef_Exists()
    {
        Assert.That(File.Exists(ProjectTypeAsmdefPath), Is.True, $"Missing asmdef: {ProjectTypeAsmdefPath}");
    }

    [Test]
    public void FrameworkAssembly_HasNoTypeLeaksToOrchestrationAssemblies()
    {
        Assembly framework = FindLoadedAssembly(FrameworkAssemblyName);
        Assert.That(framework, Is.Not.Null, $"Assembly {FrameworkAssemblyName} is not loaded.");

        var violations = new List<string>();
        foreach (Type type in GetLoadableTypes(framework))
        {
            CheckType(type, $"type {type.FullName}", violations);

            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                CheckType(field.FieldType, $"field {type.FullName}.{field.Name}", violations);

            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                CheckType(prop.PropertyType, $"property {type.FullName}.{prop.Name}", violations);

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                CheckType(method.ReturnType, $"return {type.FullName}.{method.Name}()", violations);
                foreach (ParameterInfo p in method.GetParameters())
                    CheckType(p.ParameterType, $"param {type.FullName}.{method.Name}({p.Name})", violations);
            }

            foreach (ConstructorInfo ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (ParameterInfo p in ctor.GetParameters())
                    CheckType(p.ParameterType, $"ctor-param {type.FullName}({p.Name})", violations);
            }
        }

        Assert.That(violations, Is.Empty, "Framework assembly has forbidden type references:\n" + string.Join("\n", violations));
    }

    [Test]
    public void FrameworkWorldQuery_HasNoUnityObjectSignatures()
    {
        Assembly framework = FindLoadedAssembly(FrameworkAssemblyName);
        Assert.That(framework, Is.Not.Null, $"Assembly {FrameworkAssemblyName} is not loaded.");

        Type iWorldQuery = GetLoadableTypes(framework).FirstOrDefault(t => t.Name == "IWorldQuery");
        Assert.That(iWorldQuery, Is.Not.Null, $"IWorldQuery type not found in {FrameworkAssemblyName}.");

        var violations = new List<string>();
        var allInterfaces = new List<Type> { iWorldQuery };
        allInterfaces.AddRange(iWorldQuery.GetInterfaces());

        foreach (Type iface in allInterfaces.Distinct())
        {
            foreach (MethodInfo method in iface.GetMethods())
            {
                if (ReferencesUnityObject(method.ReturnType))
                    violations.Add($"return {iface.Name}.{method.Name} -> {method.ReturnType.FullName}");

                foreach (ParameterInfo p in method.GetParameters())
                {
                    if (ReferencesUnityObject(p.ParameterType))
                        violations.Add($"param {iface.Name}.{method.Name}({p.Name}) -> {p.ParameterType.FullName}");
                }
            }

            foreach (PropertyInfo prop in iface.GetProperties())
            {
                if (ReferencesUnityObject(prop.PropertyType))
                    violations.Add($"property {iface.Name}.{prop.Name} -> {prop.PropertyType.FullName}");
            }
        }

        Assert.That(violations, Is.Empty, "IWorldQuery exposes UnityEngine.Object-derived types:\n" + string.Join("\n", violations));
    }

    [Test]
    public void CoreAndRuntimeHost_DoNotMentionGameRuntimeTypesInCode()
    {
        var roots = new[]
        {
            CoreSourceRoot,
            RuntimeHostSourceRoot
        };

        var violations = new List<string>();

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string stripped = StripCommentsAndStrings(File.ReadAllText(file));
                Match m = ForbiddenEngineSymbolsRegex.Match(stripped);
                if (m.Success)
                    violations.Add($"{file}: token '{m.Value}'");
            }
        }

        Assert.That(violations, Is.Empty,
            "Core/RuntimeHost contain forbidden engine-domain identifiers:\n" + string.Join("\n", violations));
    }

    [Test]
    public void SystemsRuntime_HasNoSystemSpecificTokens()
    {
        Assert.That(Directory.Exists(SystemsRuntimeSourceRoot), Is.True,
            $"Systems.Runtime source root not found: {SystemsRuntimeSourceRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(SystemsRuntimeSourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            Match m = ForbiddenSystemsRuntimeTokensRegex.Match(source);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "Systems.Runtime contains system-specific tokens:\n" + string.Join("\n", violations));
    }

    [Test]
    public void Core_HasNoUnityEngineUsings()
    {
        Assert.That(Directory.Exists(CoreSourceRoot), Is.True,
            $"Core source root not found: {CoreSourceRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(CoreSourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            Match usingMatch = CoreForbiddenUnityUsingRegex.Match(source);
            if (usingMatch.Success)
                violations.Add($"{file}: using UnityEngine");

            Match typeMatch = CoreForbiddenUnityTypesRegex.Match(source);
            if (typeMatch.Success)
                violations.Add($"{file}: token '{typeMatch.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "Core contains UnityEngine dependencies:\n" + string.Join("\n", violations));
    }

    [Test]
    public void Core_HasNoCombatIdleDomainContracts()
    {
        Assert.That(Directory.Exists(CoreSourceRoot), Is.True,
            $"Core source root not found: {CoreSourceRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(CoreSourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = CoreCombatIdleContractsRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "Core still contains StrategyCombat domain contracts:\n" + string.Join("\n", violations));
    }

    [Test]
    public void Core_DeclaresActorRuntimeHandleContract()
    {
        Assert.That(File.Exists(CoreActorContractsPath), Is.True, $"Missing file: {CoreActorContractsPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(CoreActorContractsPath));
        Assert.That(CoreActorRuntimeHandleRegex.IsMatch(stripped), Is.True,
            "Core actor runtime boundary must declare IActorRuntimeHandle : IEntityIdProvider.");
        Assert.That(CoreActorRuntimeLifecycleMethodRegex.IsMatch(stripped), Is.True,
            "Core actor runtime boundary must expose lifecycle via GetLifecycleState().");
        Assert.That(CoreActorRuntimeLegacyAliveMethodRegex.IsMatch(stripped), Is.False,
            "Core actor runtime boundary must not expose legacy IsAlive().");
    }

    [Test]
    public void Core_DeclaresEntityLifecycleStateInEntityContracts()
    {
        Assert.That(File.Exists(CoreEntityContractsPath), Is.True, $"Missing file: {CoreEntityContractsPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(CoreEntityContractsPath));
        Assert.That(CoreEntityLifecycleEnumRegex.IsMatch(stripped), Is.True,
            "EntityLifecycleState must be declared in Core Entity contracts.");
        Assert.That(CoreEntityModelLifecyclePropertyRegex.IsMatch(stripped), Is.True,
            "IEntityModel must expose LifecycleState.");
        Assert.That(CoreEntityAccessorSetLifecycleRegex.IsMatch(stripped), Is.True,
            "IEntityStateAccessor must expose SetLifecycleState(EntityLifecycleState).");
    }

    [Test]
    public void CoreActorContracts_DoesNotDeclareLifecycleEnum()
    {
        Assert.That(File.Exists(CoreActorContractsPath), Is.True, $"Missing file: {CoreActorContractsPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(CoreActorContractsPath));
        Assert.That(CoreActorContractsLifecycleEnumRegex.IsMatch(stripped), Is.False,
            "Actor contracts must not declare EntityLifecycleState; lifecycle type ownership belongs to Entity contracts.");
    }

    [Test]
    public void StrategyCombat_IOrchestrationActor_ExtendsCoreActorRuntimeHandle()
    {
        Assert.That(File.Exists(StrategyCombatActorContractPath), Is.True, $"Missing file: {StrategyCombatActorContractPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatActorContractPath));
        Assert.That(StrategyCombatActorExtendsCoreHandleRegex.IsMatch(stripped), Is.True,
            "IOrchestrationActor must extend IActorRuntimeHandle to bind orchestration to Core actor boundary.");
    }

    [Test]
    public void StrategyCombatWorldCache_DoesNotDowncastActorsToEntityIdProvider()
    {
        Assert.That(File.Exists(StrategyCombatWorldCachePath), Is.True, $"Missing file: {StrategyCombatWorldCachePath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatWorldCachePath));
        Assert.That(StrategyCombatWorldCacheActorDowncastRegex.IsMatch(stripped), Is.False,
            "OrchestrationWorldCache must use actor boundary contract (IActorRuntimeHandle), not actor as IEntityIdProvider downcasts.");
    }

    [Test]
    public void Core_DeclaresActorReadProjectionQueryContract()
    {
        Assert.That(File.Exists(CoreActorReadProjectionPath), Is.True, $"Missing file: {CoreActorReadProjectionPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(CoreActorReadProjectionPath));
        Assert.That(CoreActorReadProjectionContractRegex.IsMatch(stripped), Is.True,
            "Core must declare IActorReadProjectionQuery contract for orchestration read-side projection.");
    }

    [Test]
    public void Core_ActorReadProjection_DoesNotContainDomainHostilityFlag()
    {
        Assert.That(File.Exists(CoreActorReadProjectionPath), Is.True, $"Missing file: {CoreActorReadProjectionPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(CoreActorReadProjectionPath));
        Assert.That(CoreActorReadProjectionHostilityTokenRegex.IsMatch(stripped), Is.False,
            "Core actor read projection must not contain domain-specific hostility flags.");
        Assert.That(CoreActorReadProjectionLifecycleStateRegex.IsMatch(stripped), Is.True,
            "Core actor read projection must expose lifecycle via LifecycleState.");
        Assert.That(CoreActorReadProjectionLegacyAliveTokenRegex.IsMatch(stripped), Is.False,
            "Core actor read projection must not expose legacy IsAlive flag.");
    }

    [Test]
    public void StrategyCombatWorldCache_ImplementsActorReadProjectionQuery()
    {
        Assert.That(File.Exists(StrategyCombatWorldCachePath), Is.True, $"Missing file: {StrategyCombatWorldCachePath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatWorldCachePath));
        Assert.That(StrategyCombatWorldCacheProjectionContractRegex.IsMatch(stripped), Is.True,
            "OrchestrationWorldCache must implement IActorReadProjectionQuery.");
    }

    [Test]
    public void StrategyCombatExecutionRouter_UsesActorReadProjectionQuery()
    {
        Assert.That(File.Exists(StrategyCombatExecutionRouterPath), Is.True, $"Missing file: {StrategyCombatExecutionRouterPath}");

        string stripped = StripCommentsAndStrings(File.ReadAllText(StrategyCombatExecutionRouterPath));
        Assert.That(StrategyCombatExecutionRouterProjectionUsageRegex.IsMatch(stripped), Is.True,
            "ExecutionRouter must use IActorReadProjectionQuery (TryGetActorReadProjection) for actor read-side projection.");
    }

    [Test]
    public void RuntimeHost_HasNoProjectRefs()
    {
        Assert.That(Directory.Exists(RuntimeHostSourceRoot), Is.True,
            $"RuntimeHost source root not found: {RuntimeHostSourceRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(RuntimeHostSourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            Match m = RuntimeHostProjectRefsRegex.Match(source);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "RuntimeHost references project layer:\n" + string.Join("\n", violations));
    }

    [Test]
    public void PackageRuntimeSources_HaveNoProjectAssemblyRefs()
    {
        string[] roots =
        {
            FrameworkSourceRoot,
            SystemsRuntimeSourceRoot,
            CoreSourceRoot,
            RuntimeHostSourceRoot,
            ProjectTypeSourceRoot
        };

        var violations = new List<string>();
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string stripped = StripCommentsAndStrings(File.ReadAllText(file));
                Match m = PackageRuntimeProjectRefsRegex.Match(stripped);
                if (m.Success)
                    violations.Add($"{file}: token '{m.Value}'");
            }
        }

        Assert.That(violations, Is.Empty,
            "Package runtime sources reference project assemblies/layers:\n" + string.Join("\n", violations));
    }

    [Test, Ignore("Enable after C04/C07 when RuntimeHost is fully domain-agnostic.")]
    public void RuntimeHost_HasNoStrategyCombatTokens()
    {
        Assert.That(Directory.Exists(RuntimeHostSourceRoot), Is.True,
            $"RuntimeHost source root not found: {RuntimeHostSourceRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(RuntimeHostSourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = RuntimeHostDomainSpecificTokensRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "RuntimeHost still contains StrategyCombat domain-specific tokens:\n" + string.Join("\n", violations));
    }

    [Test]
    public void ProjectType_HasNoProjectAssets()
    {
        Assert.That(Directory.Exists(ProjectTypeSourceRoot), Is.True,
            $"ProjectType source root not found: {ProjectTypeSourceRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(ProjectTypeSourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            Match m = ProjectTypeProjectAssetsRegex.Match(source);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "ProjectType references project asset types:\n" + string.Join("\n", violations));
    }

    [Test]
    public void MorbooBridge_StateReporters_UseEntityBackboneStateQuery()
    {
        string[] reporterFiles =
        {
            "Assets/Scripts/MorbooBridge/Orchestration/Units/UnitStateReporter.cs",
            "Assets/Scripts/MorbooBridge/Orchestration/Enemies/EnemyStateReporter.cs"
        };

        var violations = new List<string>();
        foreach (string file in reporterFiles)
        {
            if (!File.Exists(file))
            {
                violations.Add($"{file}: missing file");
                continue;
            }

            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            if (!StateQueryUsageRegex.IsMatch(stripped))
                violations.Add($"{file}: missing TryGetState usage");
        }

        Assert.That(violations, Is.Empty,
            "State reporters must use entity backbone state query seam:\n" + string.Join("\n", violations));
    }

    [Test]
    public void MorbooBridge_StateReporters_WriteBackToEntityState()
    {
        string[] reporterFiles =
        {
            "Assets/Scripts/MorbooBridge/Orchestration/Units/UnitStateReporter.cs",
            "Assets/Scripts/MorbooBridge/Orchestration/Enemies/EnemyStateReporter.cs"
        };

        var violations = new List<string>();
        foreach (string file in reporterFiles)
        {
            if (!File.Exists(file))
            {
                violations.Add($"{file}: missing file");
                continue;
            }

            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            if (!StateWriteBackUsageRegex.IsMatch(stripped))
                violations.Add($"{file}: missing write-back to IEntityStateAccessor");
        }

        Assert.That(violations, Is.Empty,
            "State reporters must sync migrated state into Entity Backbone:\n" + string.Join("\n", violations));
    }

    [Test]
    public void PackageRuntime_DoesNotUseLegacyEntityTraitKeyConstants()
    {
        string[] roots =
        {
            FrameworkSourceRoot,
            SystemsRuntimeSourceRoot,
            CoreSourceRoot,
            RuntimeHostSourceRoot,
            ProjectTypeSourceRoot
        };

        var violations = new List<string>();
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string stripped = StripCommentsAndStrings(File.ReadAllText(file));
                Match m = LegacyEntityTraitKeysTokenRegex.Match(stripped);
                if (m.Success)
                    violations.Add($"{file}: token '{m.Value}'");
            }
        }

        Assert.That(violations, Is.Empty,
            "Legacy entity trait key constants are forbidden in package runtime layers:\n" + string.Join("\n", violations));
    }

    [Test]
    public void MorbooBridge_StateReporters_DoNotUseEntityStateNullFallbackBranches()
    {
        const string unitFile = "Assets/Scripts/MorbooBridge/Orchestration/Units/UnitStateReporter.cs";
        const string enemyFile = "Assets/Scripts/MorbooBridge/Orchestration/Enemies/EnemyStateReporter.cs";

        Assert.That(File.Exists(unitFile), Is.True, $"Missing file: {unitFile}");
        Assert.That(File.Exists(enemyFile), Is.True, $"Missing file: {enemyFile}");

        string unitSource = StripCommentsAndStrings(File.ReadAllText(unitFile));
        string enemySource = StripCommentsAndStrings(File.ReadAllText(enemyFile));

        Assert.That(UnitReporterGuardedLegacyUnitFallbackRegex.IsMatch(unitSource), Is.False,
            "UnitStateReporter must not use legacy entityState==null fallback branches.");
        Assert.That(UnitReporterUngardedLegacyUnitFallbackRegex.IsMatch(unitSource), Is.False,
            "UnitStateReporter contains unguarded legacy unit fallback.");
        Assert.That(UnitReporterGuardedLegacyHealthFallbackRegex.IsMatch(unitSource), Is.False,
            "UnitStateReporter must not use legacy entityState==null health fallback branches.");

        Assert.That(EnemyReporterGuardedLegacyEnemyFallbackRegex.IsMatch(enemySource), Is.False,
            "EnemyStateReporter must not use legacy entityState==null fallback branches.");
        Assert.That(EnemyReporterUngardedLegacyEnemyFallbackRegex.IsMatch(enemySource), Is.False,
            "EnemyStateReporter contains unguarded legacy enemy fallback.");
        Assert.That(EnemyReporterGuardedLegacyHealthFallbackRegex.IsMatch(enemySource), Is.False,
            "EnemyStateReporter must not use legacy entityState==null health fallback branches.");
    }

    [Test]
    public void StrategyCombatDomains_DoNotAccessRegistryStatics()
    {
        string domainsRoot = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains";
        if (!Directory.Exists(domainsRoot))
            Assert.Fail($"Missing directory: {domainsRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(domainsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = ForbiddenRegistryInDomainsRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "StrategyCombat Domains/Policies still access registry statics:\n" + string.Join("\n", violations));
    }

    // ── New architecture gate tests (Phase 2B) ──────────────────────

    [Test]
    public void Framework_DoesNotReference_OrchestrationAssemblies()
    {
        Assembly framework = FindLoadedAssembly(FrameworkAssemblyName);
        Assert.That(framework, Is.Not.Null, $"Assembly {FrameworkAssemblyName} is not loaded.");

        var referenced = framework.GetReferencedAssemblies();
        var violations = referenced
            .Where(r => r.Name.StartsWith("Orchestration", StringComparison.Ordinal))
            .Select(r => r.Name)
            .ToArray();

        Assert.That(violations, Is.Empty,
            $"{FrameworkAssemblyName} references Orchestration assemblies:\n" + string.Join("\n", violations));
    }

    [Test]
    public void FrameworkAsmdef_HasNoEngineReferences()
    {
        Assert.That(File.Exists(FrameworkAsmdefPath), Is.True, $"Missing asmdef: {FrameworkAsmdefPath}");
        AsmdefData asmdef = ReadAsmdef(FrameworkAsmdefPath);
        Assert.That(asmdef.noEngineReferences, Is.True,
            $"{asmdef.name} must have noEngineReferences: true");
    }

    [Test]
    public void Framework_SourceHasNoForbiddenTokens()
    {
        Assert.That(Directory.Exists(FrameworkSourceRoot), Is.True,
            $"Framework source root not found: {FrameworkSourceRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(FrameworkSourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            string stripped = StripCommentsAndStrings(source);

            Match tokenMatch = ForbiddenFrameworkSourceTokens.Match(stripped);
            if (tokenMatch.Success)
                violations.Add($"{file}: forbidden token '{tokenMatch.Value}'");

            Match usingMatch = ForbiddenFrameworkUsingDirective.Match(stripped);
            if (usingMatch.Success)
                violations.Add($"{file}: forbidden 'using UnityEngine'");
        }

        Assert.That(violations, Is.Empty,
            "Framework source contains forbidden tokens:\n" + string.Join("\n", violations));
    }

    [Test]
    public void Framework_Assembly_DoesNotReferenceUnityEngine()
    {
        Assembly framework = FindLoadedAssembly(FrameworkAssemblyName);
        Assert.That(framework, Is.Not.Null, $"Assembly {FrameworkAssemblyName} is not loaded.");

        var referenced = framework.GetReferencedAssemblies();
        var violations = referenced
            .Where(r => r.Name.StartsWith("UnityEngine", StringComparison.Ordinal))
            .Select(r => r.Name)
            .ToArray();

        Assert.That(violations, Is.Empty,
            $"{FrameworkAssemblyName} references UnityEngine assemblies:\n" + string.Join("\n", violations));
    }

    // ── RoleId migration gates ─────────────────────────────────────

    static readonly Regex RoleAssetTokenRegex =
        new Regex(@"\bRoleAsset\b", RegexOptions.Compiled);

    /// <summary>
    /// RuntimeHost Domains (policies, orchestrators) must use RoleId, never RoleAsset.
    /// IMPORTANT: Map assets (which live in Domains) keep RoleAsset in serialized Entry
    /// structs and OnValidate for inspector binding; those files are excluded.
    /// </summary>
    [Test]
    public void StrategyCombatDomains_PoliciesHaveNoRoleAsset()
    {
        string domainsRoot = "Packages/com.morboo.integration.strategycombat/Runtime/Orchestration/Domains";
        if (!Directory.Exists(domainsRoot))
            Assert.Fail($"Missing directory: {domainsRoot}");

        // Exclude map asset files — they legitimately keep RoleAsset in serialized Entry for inspector
        var excludedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "IdleRolePolicyMapAsset.cs",
            "CombatRolePolicyMapAsset.cs",
            "CombatRoleConstraintsMapAsset.cs"
        };

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(domainsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            if (excludedFiles.Contains(fileName)) continue;

            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = RoleAssetTokenRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "StrategyCombat Domains/Policies reference RoleAsset (should use RoleId):\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// StrategyCombat orchestration must not use GetInstanceID() for role keys or seeds.
    /// All role identification uses RoleId.ToStableInt() instead.
    /// IMPORTANT: GetInstanceID on Transform (legacy entity seed) is acceptable in
    /// non-role contexts; this test specifically checks files that deal with role keys.
    /// </summary>
    [Test]
    public void StrategyCombat_NoGetInstanceIDForRoleKeys()
    {
        string runtimeHostRoot = "Packages/com.morboo.runtimehost/Runtime/Orchestration";
        if (!Directory.Exists(runtimeHostRoot))
            Assert.Fail($"Missing directory: {runtimeHostRoot}");

        // Files that historically used GetInstanceID for role keys
        var targetFiles = new[]
        {
            "Arbitration/OrchestrationWorldCache.cs",
            "Execution/ExecutionRouter.cs"
        };

        var violations = new List<string>();
        foreach (string rel in targetFiles)
        {
            string path = Path.Combine(runtimeHostRoot, rel);
            if (!File.Exists(path)) continue;

            string stripped = StripCommentsAndStrings(File.ReadAllText(path));
            if (Regex.IsMatch(stripped, @"\bGetInstanceID\s*\("))
                violations.Add($"{path}: contains GetInstanceID() call");
        }

        Assert.That(violations, Is.Empty,
            "StrategyCombat uses GetInstanceID for role keys (should use RoleId):\n" + string.Join("\n", violations));
    }

    // ── Scheduler abstraction gates ───────────────────────────────────

    static readonly Regex TimeTimeRegex =
        new Regex(@"\bTime\.time\b", RegexOptions.Compiled);

    /// <summary>
    /// RuntimeHost must not use Time.time directly. All time comes from
    /// TickContext.Now or IWorldQuery.Now. The only exception is
    /// RealtimeScheduler which bridges to Time.time by design.
    /// </summary>
    [Test]
    public void RuntimeHost_NoTimeTimeDirect()
    {
        string runtimeHostRoot = "Packages/com.morboo.runtimehost/Runtime/Orchestration";
        if (!Directory.Exists(runtimeHostRoot))
            Assert.Fail($"Missing directory: {runtimeHostRoot}");

        // RealtimeScheduler is the bridge to Time.time — that's its job
        var excludedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RealtimeScheduler.cs"
        };

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(runtimeHostRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            if (excludedFiles.Contains(fileName)) continue;

            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = TimeTimeRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: contains Time.time");
        }

        Assert.That(violations, Is.Empty,
            "RuntimeHost uses Time.time directly (should use TickContext.Now):\n" + string.Join("\n", violations));
    }

    // ── Command bus gates ─────────────────────────────────────────────

    static readonly Regex ApplyCommandCallRegex =
        new Regex(@"(?:\.|\?\.)\s*(ApplyCombatCommand|ApplyIdleCommand)\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// RuntimeHost must not call ApplyCombatCommand / ApplyIdleCommand directly.
    /// IMPORTANT: Commands are emitted via ICommandBus. Integration adapters
    /// subscribe and call Apply. This test catches accidental direct dispatch.
    /// </summary>
    [Test]
    public void RuntimeHost_DoesNotCallApplyCommandDirectly()
    {
        string runtimeHostRoot = "Packages/com.morboo.runtimehost/Runtime/Orchestration";
        if (!Directory.Exists(runtimeHostRoot))
            Assert.Fail($"Missing directory: {runtimeHostRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(runtimeHostRoot, "*.cs", SearchOption.AllDirectories))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = ApplyCommandCallRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: contains call {m.Value}");
        }

        Assert.That(violations, Is.Empty,
            "RuntimeHost calls Apply*Command directly (should emit via ICommandBus):\n" + string.Join("\n", violations));
    }

    // ── Package placement policy gates ───────────────────────────────

    [Test]
    public void BaseMorbooPackageAsmdefs_RespectLayerReferencePolicy()
    {
        AssertAsmdefReferencesWithinAllowedSet(FrameworkAsmdefPath, Array.Empty<string>());
        AssertAsmdefReferencesWithinAllowedSet(SystemsRuntimeAsmdefPath, new[] { "Morboo.Framework", "Morboo.Core" });
        AssertAsmdefReferencesWithinAllowedSet(CoreAsmdefPath, new[] { "Morboo.Framework" });
        AssertAsmdefReferencesWithinAllowedSet(RuntimeHostAsmdefPath, new[] { "Morboo.Framework", "Morboo.Core", "Morboo.Systems" });
    }

    [Test]
    public void StrategyCombatAsmdef_ReferencesRequiredMorbooLayers()
    {
        string[] requiredRefs =
        {
            "Morboo.Framework",
            "Morboo.Core",
            "Morboo.RuntimeHost",
            "Morboo.Systems"
        };

        string[] refs = GetResolvedAsmdefReferences(ProjectTypeAsmdefPath);
        var refSet = new HashSet<string>(refs, StringComparer.Ordinal);

        var missing = requiredRefs.Where(r => !refSet.Contains(r)).ToArray();
        Assert.That(missing, Is.Empty,
            $"Morboo.Integration.StrategyCombat is missing required Morboo layer refs: {string.Join(", ", missing)}");
    }

    [Test]
    public void MorbooPackageRuntimeAsmdefs_DoNotReferenceProjectAssemblies()
    {
        string[] protectedAsmdefs =
        {
            FrameworkAsmdefPath,
            SystemsRuntimeAsmdefPath,
            CoreAsmdefPath,
            RuntimeHostAsmdefPath,
            ProjectTypeAsmdefPath
        };

        foreach (string asmdefPath in protectedAsmdefs)
            AssertAsmdefHasNoForbiddenReferences(asmdefPath, ForbiddenProjectAssemblyReferences);
    }

    [Test]
    public void BaseMorbooPackageAsmdefs_DoNotReferenceTopDownEngineAssemblies()
    {
        string[] protectedAsmdefs =
        {
            FrameworkAsmdefPath,
            SystemsRuntimeAsmdefPath,
            CoreAsmdefPath,
            RuntimeHostAsmdefPath
        };

        foreach (string asmdefPath in protectedAsmdefs)
            AssertAsmdefHasNoForbiddenReferences(asmdefPath, ForbiddenTopDownEngineAssemblyReferences);
    }

    [Test]
    public void KernelRuntimePackages_DoNotUseTopDownEngineNamespace()
    {
        string[] roots =
        {
            FrameworkSourceRoot,
            SystemsRuntimeSourceRoot,
            CoreSourceRoot,
            RuntimeHostSourceRoot
        };

        var violations = new List<string>();
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string stripped = StripCommentsAndStrings(File.ReadAllText(file));
                Match token = ForbiddenTopDownEngineNamespaceRegex.Match(stripped);
                if (token.Success)
                    violations.Add($"{file}: token '{token.Value}'");
            }
        }

        Assert.That(violations, Is.Empty,
            "Kernel/runtime packages must not use TopDownEngine namespace:\n" + string.Join("\n", violations));
    }

    [Test]
    public void MorbooPackageRuntimeAsmdefGraph_HasNoCycles()
    {
        Dictionary<string, AsmdefData> runtimeAsmdefs = CollectMorbooRuntimeAsmdefs();
        Assert.That(runtimeAsmdefs.Count, Is.GreaterThan(0), "No Morboo runtime asmdefs found.");

        var nodes = new HashSet<string>(runtimeAsmdefs.Keys, StringComparer.Ordinal);
        var guidToAsmName = BuildGuidToAssemblyNameMap();
        var adjacency = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, AsmdefData> pair in runtimeAsmdefs)
        {
            string[] resolvedRefs = (pair.Value.references ?? Array.Empty<string>())
                .Select(r => ResolveReferenceName(r, guidToAsmName))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Where(nodes.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            adjacency[pair.Key] = resolvedRefs;
        }

        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();
        var cycles = new HashSet<string>(StringComparer.Ordinal);

        foreach (string node in adjacency.Keys)
            VisitAsmdefNode(node, adjacency, state, stack, cycles);

        Assert.That(cycles, Is.Empty,
            "Detected cycles in Morboo runtime asmdef graph:\n" + string.Join("\n", cycles));
    }

    [Test]
    public void KernelRuntimePackages_DoNotUseSirenixOdin()
    {
        string[] roots =
        {
            FrameworkSourceRoot,
            SystemsRuntimeSourceRoot,
            CoreSourceRoot,
            RuntimeHostSourceRoot
        };

        var violations = new List<string>();
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(file);
                if (ForbiddenSirenixUsingRegex.IsMatch(source))
                    violations.Add($"{file}: using Sirenix.*");

                string stripped = StripCommentsAndStrings(source);
                Match token = ForbiddenSirenixTokenRegex.Match(stripped);
                if (token.Success)
                    violations.Add($"{file}: token '{token.Value}'");
            }
        }

        Assert.That(violations, Is.Empty,
            "Kernel/runtime packages must not use Sirenix Odin runtime dependencies:\n" + string.Join("\n", violations));
    }

    [Test]
    public void PackageRuntime_DoesNotUseSerializedGameObjectDependencyHolders()
    {
        string[] roots =
        {
            FrameworkSourceRoot,
            SystemsRuntimeSourceRoot,
            CoreSourceRoot,
            RuntimeHostSourceRoot,
            ProjectTypeSourceRoot
        };

        var violations = new List<string>();
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string stripped = StripCommentsAndStrings(File.ReadAllText(file));
                Match m = SerializedGameObjectDependencyHolderRegex.Match(stripped);
                if (m.Success)
                    violations.Add($"{file}: token '{m.Value}'");
            }
        }

        Assert.That(violations, Is.Empty,
            "Package runtime code uses serialized GameObject dependency holders:\n" + string.Join("\n", violations));
    }

    [Test, Ignore("Enable in Phase 7 when TopDownEngine runtime dependency is fully removed.")]
    public void FutureGate_StrategyCombatAsmdef_DoesNotReferenceTopDownEngineAssemblies()
    {
        AssertAsmdefHasNoForbiddenReferences(ProjectTypeAsmdefPath, ForbiddenTopDownEngineAssemblyReferences);
    }

    [Test]
    public void KernelContracts_AreDeclaredOnlyInApprovedKernelPackages()
    {
        const string packagesRoot = "Packages";
        if (!Directory.Exists(packagesRoot))
            Assert.Fail($"Missing directory: {packagesRoot}");

        string[] allowedRoots =
        {
            FrameworkSourceRoot,
            CoreSourceRoot,
            RuntimeHostSourceRoot
        };

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(packagesRoot, "*.cs", SearchOption.AllDirectories))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            foreach (Match match in KernelContractDeclarationRegex.Matches(stripped))
            {
                bool isAllowed = allowedRoots.Any(root => IsPathUnder(file, root));
                if (!isAllowed)
                    violations.Add($"{file}: declaration '{match.Value}'");
            }
        }

        Assert.That(violations, Is.Empty,
            "Kernel contracts are declared outside approved kernel packages:\n" + string.Join("\n", violations));
    }

    [Test]
    public void EntityKernelContracts_AreDeclaredOnlyInFrameworkOrCore()
    {
        var roots = new[] { "Packages", "Assets/Scripts" };
        var violations = new List<string>();

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string stripped = StripCommentsAndStrings(File.ReadAllText(file));
                foreach (Match match in EntityKernelContractDeclarationRegex.Matches(stripped))
                {
                    if (!IsPathUnder(file, FrameworkSourceRoot) && !IsPathUnder(file, CoreSourceRoot))
                        violations.Add($"{file}: declaration '{match.Value}'");
                }
            }
        }

        Assert.That(violations, Is.Empty,
            "Entity kernel contracts are declared outside Framework/Core:\n" + string.Join("\n", violations));
    }

    [Test]
    public void EntityId_DeclaredOnlyInFramework()
    {
        var roots = new[] { "Packages", "Assets/Scripts" };
        var violations = new List<string>();

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string stripped = StripCommentsAndStrings(File.ReadAllText(file));
                foreach (Match match in EntityIdDeclarationRegex.Matches(stripped))
                {
                    if (!IsPathUnder(file, FrameworkSourceRoot))
                        violations.Add($"{file}: declaration '{match.Value}'");
                }
            }
        }

        Assert.That(violations, Is.Empty,
            "EntityId must be declared only in Framework:\n" + string.Join("\n", violations));
    }

    [Test]
    public void MorbooBridge_EntityBackbone_DoesNotDowncastEntityModel()
    {
        const string bridgeRoot = "Assets/Scripts/MorbooBridge/EntityBackbone";
        if (!Directory.Exists(bridgeRoot))
            Assert.Fail($"Missing directory: {bridgeRoot}");

        var violations = new List<string>();
        foreach (string file in Directory.GetFiles(bridgeRoot, "*.cs", SearchOption.AllDirectories))
        {
            string stripped = StripCommentsAndStrings(File.ReadAllText(file));
            Match m = BridgeEntityStateDowncastRegex.Match(stripped);
            if (m.Success)
                violations.Add($"{file}: token '{m.Value}'");
        }

        Assert.That(violations, Is.Empty,
            "MorbooBridge entity backbone must use IEntityStateQuery/IEntityStateAccessor, not IEntityModel downcasts:\n"
            + string.Join("\n", violations));
    }

    // ── Helpers ──────────────────────────────────────────────────────

    static void AssertAsmdefReferencesWithinAllowedSet(string asmdefPath, IReadOnlyCollection<string> allowedReferences)
    {
        Assert.That(File.Exists(asmdefPath), Is.True, $"Missing asmdef: {asmdefPath}");

        string[] resolved = GetResolvedAsmdefReferences(asmdefPath);
        var allowed = new HashSet<string>(allowedReferences ?? Array.Empty<string>(), StringComparer.Ordinal);
        var violations = resolved.Where(r => !allowed.Contains(r)).ToArray();

        Assert.That(violations, Is.Empty,
            $"{asmdefPath} has references outside allowed set: {string.Join(", ", violations)}");
    }

    static string[] GetResolvedAsmdefReferences(string asmdefPath)
    {
        AsmdefData asmdef = ReadAsmdef(asmdefPath);
        var guidToAsmName = BuildGuidToAssemblyNameMap();

        return (asmdef.references ?? Array.Empty<string>())
            .Select(r => ResolveReferenceName(r, guidToAsmName))
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToArray();
    }

    static bool IsPathUnder(string path, string root)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
            return false;

        string normalizedPath = path.Replace('\\', '/');
        string normalizedRoot = root.Replace('\\', '/').TrimEnd('/');
        return normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    static void AssertAsmdefHasNoForbiddenReferences(string asmdefPath, IReadOnlyList<string> forbiddenPrefixes)
    {
        Assert.That(File.Exists(asmdefPath), Is.True, $"Missing asmdef: {asmdefPath}");

        AsmdefData asmdef = ReadAsmdef(asmdefPath);
        var guidToAsmName = BuildGuidToAssemblyNameMap();

        string[] refs = asmdef.references ?? Array.Empty<string>();
        var resolved = refs.Select(r => ResolveReferenceName(r, guidToAsmName)).ToArray();

        var violations = resolved.Where(name => IsForbiddenName(name, forbiddenPrefixes)).ToArray();
        Assert.That(violations, Is.Empty,
            $"{asmdef.name} has forbidden asmdef references: {string.Join(", ", violations)}");
    }

    static bool IsForbiddenName(string name, IReadOnlyList<string> forbiddenPrefixes)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        foreach (string prefix in forbiddenPrefixes)
        {
            if (string.Equals(name, prefix, StringComparison.Ordinal))
                return true;
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return true;
            if (prefix.EndsWith(".", StringComparison.Ordinal) && name.Contains(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static void CheckType(Type type, string owner, ICollection<string> violations)
    {
        var visited = new HashSet<Type>();
        CheckType(type, owner, violations, visited);
    }

    static void CheckType(Type type, string owner, ICollection<string> violations, ISet<Type> visited)
    {
        if (type == null)
            return;

        Type root = UnwrapType(type);
        if (root == null)
            return;
        if (!visited.Add(root))
            return;

        string asmName = root.Assembly.GetName().Name;
        if (IsForbiddenName(asmName, ForbiddenFrameworkAssemblies))
            violations.Add($"{owner} -> {root.FullName} ({asmName})");

        if (root.IsGenericType)
        {
            foreach (Type arg in root.GetGenericArguments())
                CheckType(arg, owner, violations, visited);
        }

        if (root.BaseType != null)
            CheckType(root.BaseType, owner, violations, visited);

        foreach (Type iface in root.GetInterfaces())
            CheckType(iface, owner, violations, visited);
    }

    static bool ReferencesUnityObject(Type type)
    {
        var visited = new HashSet<Type>();
        return ReferencesUnityObject(type, visited);
    }

    static bool ReferencesUnityObject(Type type, ISet<Type> visited)
    {
        Type root = UnwrapType(type);
        if (root == null)
            return false;
        if (!visited.Add(root))
            return false;

        if (typeof(UnityEngine.Object).IsAssignableFrom(root))
            return true;

        if (root.IsGenericType)
        {
            foreach (Type arg in root.GetGenericArguments())
            {
                if (ReferencesUnityObject(arg, visited))
                    return true;
            }
        }

        return false;
    }

    static Type UnwrapType(Type type)
    {
        if (type == null)
            return null;

        while (type.HasElementType)
            type = type.GetElementType();

        return type;
    }

    static Assembly FindLoadedAssembly(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
    }

    static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        if (assembly == null)
            return Array.Empty<Type>();

        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null);
        }
    }

    static string ResolveReferenceName(string rawRef, IReadOnlyDictionary<string, string> guidMap)
    {
        if (string.IsNullOrWhiteSpace(rawRef))
            return rawRef;

        const string guidPrefix = "GUID:";
        if (!rawRef.StartsWith(guidPrefix, StringComparison.Ordinal))
            return rawRef;

        string guid = rawRef.Substring(guidPrefix.Length);
        return guidMap.TryGetValue(guid, out string asmName) ? asmName : rawRef;
    }

    static Dictionary<string, string> BuildGuidToAssemblyNameMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string[] searchRoots = { "Assets", "Packages" };
        foreach (string root in searchRoots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string asmdefPath in Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories))
            {
                AsmdefData asmdef = ReadAsmdef(asmdefPath);
                string metaPath = asmdefPath + ".meta";
                if (!File.Exists(metaPath))
                    continue;

                string guid = ExtractGuidFromMeta(metaPath);
                if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(asmdef.name))
                    map[guid] = asmdef.name;
            }
        }

        return map;
    }

    static Dictionary<string, AsmdefData> CollectMorbooRuntimeAsmdefs()
    {
        const string packagesRoot = "Packages";
        var map = new Dictionary<string, AsmdefData>(StringComparer.Ordinal);

        if (!Directory.Exists(packagesRoot))
            return map;

        foreach (string packageDir in Directory.GetDirectories(packagesRoot, "com.morboo.*", SearchOption.TopDirectoryOnly))
        {
            foreach (string asmdefPath in Directory.GetFiles(packageDir, "*.asmdef", SearchOption.AllDirectories))
            {
                string normalized = asmdefPath.Replace('\\', '/');
                if (!normalized.Contains("/Runtime/", StringComparison.Ordinal))
                    continue;

                AsmdefData asmdef = ReadAsmdef(asmdefPath);
                if (!string.IsNullOrWhiteSpace(asmdef.name))
                    map[asmdef.name] = asmdef;
            }
        }

        return map;
    }

    static void VisitAsmdefNode(
        string node,
        IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency,
        IDictionary<string, int> state,
        IList<string> stack,
        ISet<string> cycles)
    {
        if (state.TryGetValue(node, out int visitState))
        {
            if (visitState == 1)
            {
                int cycleStart = stack.IndexOf(node);
                if (cycleStart >= 0)
                {
                    string cycle = string.Join(" -> ", stack.Skip(cycleStart).Concat(new[] { node }));
                    cycles.Add(cycle);
                }
            }

            return;
        }

        state[node] = 1;
        stack.Add(node);

        if (adjacency.TryGetValue(node, out IReadOnlyList<string> deps))
        {
            foreach (string dep in deps)
                VisitAsmdefNode(dep, adjacency, state, stack, cycles);
        }

        stack.RemoveAt(stack.Count - 1);
        state[node] = 2;
    }

    static string ExtractGuidFromMeta(string metaPath)
    {
        foreach (string line in File.ReadLines(metaPath))
        {
            if (!line.StartsWith("guid:", StringComparison.Ordinal))
                continue;
            return line.Substring("guid:".Length).Trim();
        }

        return null;
    }

    static AsmdefData ReadAsmdef(string path)
    {
        string json = File.ReadAllText(path);
        AsmdefData data = JsonUtility.FromJson<AsmdefData>(json);
        Assert.That(data, Is.Not.Null, $"Failed to parse asmdef JSON: {path}");
        Assert.That(string.IsNullOrEmpty(data.name), Is.False, $"asmdef without name: {path}");
        return data;
    }

    static string StripCommentsAndStrings(string source)
    {
        var sb = new StringBuilder(source.Length);

        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inChar = false;
        bool escape = false;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n')
                {
                    inLineComment = false;
                    sb.Append('\n');
                }
                else
                {
                    sb.Append(' ');
                }
                continue;
            }

            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    sb.Append("  ");
                    i++;
                }
                else if (c == '\n')
                {
                    sb.Append('\n');
                }
                else
                {
                    sb.Append(' ');
                }
                continue;
            }

            if (inString)
            {
                if (inVerbatimString)
                {
                    if (c == '"' && next == '"')
                    {
                        sb.Append("  ");
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                        inVerbatimString = false;
                        sb.Append(' ');
                    }
                    else
                    {
                        sb.Append(c == '\n' ? '\n' : ' ');
                    }
                }
                else
                {
                    if (!escape && c == '"')
                    {
                        inString = false;
                        sb.Append(' ');
                    }
                    else
                    {
                        sb.Append(c == '\n' ? '\n' : ' ');
                    }

                    escape = !escape && c == '\\';
                }

                continue;
            }

            if (inChar)
            {
                if (!escape && c == '\'')
                {
                    inChar = false;
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(c == '\n' ? '\n' : ' ');
                }

                escape = !escape && c == '\\';
                continue;
            }

            if (c == '/' && next == '/')
            {
                inLineComment = true;
                sb.Append("  ");
                i++;
                continue;
            }

            if (c == '/' && next == '*')
            {
                inBlockComment = true;
                sb.Append("  ");
                i++;
                continue;
            }

            if (c == '@' && next == '"')
            {
                inString = true;
                inVerbatimString = true;
                escape = false;
                sb.Append("  ");
                i++;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                inVerbatimString = false;
                escape = false;
                sb.Append(' ');
                continue;
            }

            if (c == '\'')
            {
                inChar = true;
                escape = false;
                sb.Append(' ');
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    [Serializable]
    sealed class AsmdefData
    {
        public string name;
        public string[] references;
        public bool noEngineReferences;
    }
}
