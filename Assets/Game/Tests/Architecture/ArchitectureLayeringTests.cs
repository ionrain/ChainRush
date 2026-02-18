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
    const string FrameworkAsmdefPath = "Assets/Game/Scripts/Framework/Game.Framework.asmdef";
    const string CoreAsmdefPath = "Assets/Game/Scripts/Orchestration/Core/Orchestration.Core.asmdef";
    const string RuntimeHostAsmdefPath = "Assets/Game/Scripts/Orchestration/RuntimeHost/Orchestration.RuntimeHost.asmdef";

    static readonly string[] ForbiddenForFrameworkAsmdef =
    {
        "Orchestration.",
        "Orchestration.Integration.",
        "Integration.",
        "Game.Runtime"
    };

    static readonly string[] ForbiddenForCoreAndRuntimeHostAsmdef =
    {
        "Orchestration.Integration.",
        "Integration.",
        "Game.Runtime"
    };

    static readonly string[] ForbiddenFrameworkAssemblies =
    {
        "Orchestration.",
        "Orchestration.Integration.",
        "Game.Runtime"
    };

    static readonly Regex ForbiddenEngineSymbolsRegex =
        new Regex(@"\b(Unit|Enemy|TopDownEngine|MoreMountains)\b", RegexOptions.Compiled);

    static readonly Regex ForbiddenRegistryInDomainsRegex =
        new Regex(@"\b(OrchestrationRegistry|IdleBoundsRegistry)\b", RegexOptions.Compiled);

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
    public void FrameworkAssembly_HasNoTypeLeaksToOrchestrationAssemblies()
    {
        Assembly framework = FindLoadedAssembly("Game.Framework");
        Assert.That(framework, Is.Not.Null, "Assembly Game.Framework is not loaded.");

        var violations = new List<string>();
        foreach (Type type in framework.GetTypes())
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
        Assembly framework = FindLoadedAssembly("Game.Framework");
        Assert.That(framework, Is.Not.Null, "Assembly Game.Framework is not loaded.");

        Type iWorldQuery = framework.GetTypes().FirstOrDefault(t => t.Name == "IWorldQuery");
        Assert.That(iWorldQuery, Is.Not.Null, "IWorldQuery type not found in Game.Framework.");

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
            "Assets/Game/Scripts/Orchestration/Core",
            "Assets/Game/Scripts/Orchestration/RuntimeHost"
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
    public void RuntimeHostDomains_DoNotAccessRegistryStatics()
    {
        string domainsRoot = "Assets/Game/Scripts/Orchestration/RuntimeHost/Domains";
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
            "RuntimeHost Domains/Policies still access registry statics:\n" + string.Join("\n", violations));
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
        if (type == null)
            return;

        Type root = UnwrapType(type);
        if (root == null)
            return;

        string asmName = root.Assembly.GetName().Name;
        if (IsForbiddenName(asmName, ForbiddenFrameworkAssemblies))
            violations.Add($"{owner} -> {root.FullName} ({asmName})");

        if (root.IsGenericType)
        {
            foreach (Type arg in root.GetGenericArguments())
                CheckType(arg, owner, violations);
        }

        if (root.BaseType != null)
            CheckType(root.BaseType, owner, violations);

        foreach (Type iface in root.GetInterfaces())
            CheckType(iface, owner, violations);
    }

    static bool ReferencesUnityObject(Type type)
    {
        Type root = UnwrapType(type);
        if (root == null)
            return false;

        if (typeof(UnityEngine.Object).IsAssignableFrom(root))
            return true;

        if (root.IsGenericType)
        {
            foreach (Type arg in root.GetGenericArguments())
            {
                if (ReferencesUnityObject(arg))
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

        foreach (string asmdefPath in Directory.GetFiles("Assets", "*.asmdef", SearchOption.AllDirectories))
        {
            AsmdefData asmdef = ReadAsmdef(asmdefPath);
            string metaPath = asmdefPath + ".meta";
            if (!File.Exists(metaPath))
                continue;

            string guid = ExtractGuidFromMeta(metaPath);
            if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(asmdef.name))
                map[guid] = asmdef.name;
        }

        return map;
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
    }
}
