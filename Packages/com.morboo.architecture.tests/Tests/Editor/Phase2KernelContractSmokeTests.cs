using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public sealed class Phase2KernelContractSmokeTests
{
    const string CoreAssemblyName = "Morboo.Core";

    static readonly string[] RequiredKernelContracts =
    {
        "IGameFlowService",
        "IScenarioService",
        "IObjectiveService",
        "IOutcomeService",
        "IRulebookProvider",
        "ISessionStateStore",
        "IProfileStateStore",
        "ISaveLoadService",
        "IEconomyLedger",
        "IRewardService"
    };

    static readonly string[] RequiredEntityContracts =
    {
        "IEntityRegistry",
        "IEntityFactory",
        "IEntityLifecycleService",
        "IEntitySnapshotStore",
        "IEntityViewBinder",
        "IEntityStateQuery",
        "IEntityStateAccessor"
    };

    static readonly ObjectiveScope[] RequiredObjectiveScopes =
    {
        ObjectiveScope.Meta,
        ObjectiveScope.Campaign,
        ObjectiveScope.Run,
        ObjectiveScope.Encounter,
        ObjectiveScope.Task
    };

    [Test]
    public void Core_DeclaresRequiredKernelContracts()
    {
        Assembly core = FindLoadedAssembly(CoreAssemblyName);
        Assert.That(core, Is.Not.Null, $"Assembly {CoreAssemblyName} is not loaded.");

        Type[] types = GetLoadableTypes(core).ToArray();
        var missing = RequiredKernelContracts
            .Where(name => !types.Any(t => t != null && t.IsInterface && t.Name == name))
            .ToArray();

        Assert.That(missing, Is.Empty,
            "Missing required kernel contracts in Morboo.Core:\n" + string.Join("\n", missing));
    }

    [Test]
    public void Core_DeclaresRequiredEntityContracts()
    {
        Assembly core = FindLoadedAssembly(CoreAssemblyName);
        Assert.That(core, Is.Not.Null, $"Assembly {CoreAssemblyName} is not loaded.");

        Type[] types = GetLoadableTypes(core).ToArray();
        var missing = RequiredEntityContracts
            .Where(name => !types.Any(t => t != null && t.IsInterface && t.Name == name))
            .ToArray();

        Assert.That(missing, Is.Empty,
            "Missing required entity contracts in Morboo.Core:\n" + string.Join("\n", missing));
    }

    [Test]
    public void ObjectiveScope_DefinesAllMandatoryValues()
    {
        var declared = new HashSet<ObjectiveScope>((ObjectiveScope[])Enum.GetValues(typeof(ObjectiveScope)));
        var missing = RequiredObjectiveScopes.Where(scope => !declared.Contains(scope)).ToArray();

        Assert.That(missing, Is.Empty,
            "ObjectiveScope is missing required values:\n" + string.Join("\n", missing.Select(x => x.ToString())));
    }

    [Test]
    public void CoreKernelContracts_DoNotExposeUnityTypesInSignatures()
    {
        Assembly core = FindLoadedAssembly(CoreAssemblyName);
        Assert.That(core, Is.Not.Null, $"Assembly {CoreAssemblyName} is not loaded.");

        var targetNames = new HashSet<string>(RequiredKernelContracts.Concat(RequiredEntityContracts), StringComparer.Ordinal);
        var targetInterfaces = GetLoadableTypes(core)
            .Where(t => t != null && t.IsInterface && targetNames.Contains(t.Name))
            .ToArray();

        var violations = new List<string>();
        foreach (Type iface in targetInterfaces)
        {
            foreach (PropertyInfo property in iface.GetProperties())
            {
                if (ReferencesUnityType(property.PropertyType))
                    violations.Add($"property {iface.Name}.{property.Name} -> {property.PropertyType.FullName}");
            }

            foreach (MethodInfo method in iface.GetMethods())
            {
                if (ReferencesUnityType(method.ReturnType))
                    violations.Add($"return {iface.Name}.{method.Name} -> {method.ReturnType.FullName}");

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    if (ReferencesUnityType(parameter.ParameterType))
                        violations.Add($"param {iface.Name}.{method.Name}({parameter.Name}) -> {parameter.ParameterType.FullName}");
                }
            }

            foreach (EventInfo ev in iface.GetEvents())
            {
                if (ReferencesUnityType(ev.EventHandlerType))
                    violations.Add($"event {iface.Name}.{ev.Name} -> {ev.EventHandlerType.FullName}");
            }
        }

        Assert.That(violations, Is.Empty,
            "Core kernel/entity contracts expose UnityEngine types:\n" + string.Join("\n", violations));
    }

    static Assembly FindLoadedAssembly(string assemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
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

    static bool ReferencesUnityType(Type type)
    {
        var visited = new HashSet<Type>();
        return ReferencesUnityType(type, visited);
    }

    static bool ReferencesUnityType(Type type, ISet<Type> visited)
    {
        Type root = UnwrapType(type);
        if (root == null)
            return false;
        if (!visited.Add(root))
            return false;

        string assemblyName = root.Assembly.GetName().Name;
        if (!string.IsNullOrEmpty(assemblyName) && assemblyName.StartsWith("UnityEngine", StringComparison.Ordinal))
            return true;

        if (root.IsGenericType)
        {
            foreach (Type arg in root.GetGenericArguments())
            {
                if (ReferencesUnityType(arg, visited))
                    return true;
            }
        }

        if (root.BaseType != null && ReferencesUnityType(root.BaseType, visited))
            return true;

        foreach (Type iface in root.GetInterfaces())
        {
            if (ReferencesUnityType(iface, visited))
                return true;
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
}
