using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MoreMountains.TopDownEngine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChainRush.Editor
{
    /// <summary>Explicit, read-only source inventory for the gameplay migration working model.</summary>
    public static class ChainRushGameplayContentRegistry
    {
        const string LocationsPath = "Assets/Game/Resources/AllLocationsData.asset";
        const string UnitsPath = "Assets/Game/Resources/AllUnitsData.asset";
        const string OutputDirectory = "Documentation/GameplayMigration";

        [MenuItem("Tools/ChainRush/Authoring/Export Gameplay Content Registry")]
        public static void Export()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("The content registry must be exported in Edit Mode.");

            AllLocationsData locations = Require<AllLocationsData>(LocationsPath);
            AllUnitsData units = Require<AllUnitsData>(UnitsPath);
            var sources = new HashSet<Object>();
            var levels = new List<LevelData>();
            var skills = new HashSet<SkillData>();
            var enemies = new HashSet<GameObject>();
            var errors = new List<string>();
            var excludedLevels = new List<LevelData>();
            sources.Add(locations);
            sources.Add(units);

            foreach (LocationData location in locations.locations)
            {
                if (location == null) { errors.Add("Null location in " + LocationsPath); continue; }
                sources.Add(location);
                foreach (LevelData level in location.levels)
                {
                    if (level == null) { errors.Add("Null level in " + PathOf(location)); continue; }
                    if (IsExcludedLevel(level))
                    {
                        excludedLevels.Add(level);
                        continue;
                    }
                    if (sources.Add(level)) levels.Add(level);
                    if (level.Goal?.goal == null) errors.Add("Missing goal in " + PathOf(level));
                    else if (level.Goal.GoalType != LevelGoalType.Survive && level.Goal.GoalType != LevelGoalType.Distance)
                        errors.Add("Goal outside the approved Survive/Distance scope in " + PathOf(level));
                    if (level.difficulty != null) sources.Add(level.difficulty);
                    else errors.Add("Missing board difficulty in " + PathOf(level));
                    if (level.enemyData == null) { errors.Add("Missing enemy data in " + PathOf(level)); continue; }
                    sources.Add(level.enemyData);
                    foreach (var composition in level.enemyData.enemyProportions.Values)
                        AddEnemies(composition, enemies);
                    foreach (var wave in level.enemyData.waves.Values.Concat(level.enemyData.triggers.Values))
                        if (wave != null)
                            foreach (var composition in wave.shares) AddEnemies(composition, enemies);
                }
            }

            foreach (UnitData unit in units.units)
            {
                if (unit == null) { errors.Add("Null unit in " + UnitsPath); continue; }
                sources.Add(unit);
                foreach (UnitSkill skill in unit.skills)
                    if (skill?.data != null) AddSkill(skill.data, skills);
                    else errors.Add("Missing skill in " + PathOf(unit));
            }
            foreach (GameObject enemy in enemies)
            {
                sources.Add(enemy);
                foreach (string path in AssetDatabase.GetDependencies(PathOf(enemy), true))
                {
                    Object dependency = AssetDatabase.LoadMainAssetAtPath(path);
                    if (dependency is SkillData skill) AddSkill(skill, skills);
                    if (dependency is EnemyData || dependency is UnitAIProfile) sources.Add(dependency);
                    if (dependency is GameObject weapon && (weapon.GetComponent<Weapon>() != null || weapon.GetComponent<Projectile>() != null))
                        sources.Add(dependency);
                }
            }
            foreach (SkillData skill in skills)
            {
                sources.Add(skill);
                if (skill.prefab != null) sources.Add(skill.prefab.gameObject);
                else errors.Add("Missing skill prefab in " + PathOf(skill));
            }
            // These profiles are selected by the source UnitManager, rather than referenced by UnitData.
            sources.Add(Require<UnitAIProfile>("Assets/Game/Resources/UnitAIProfile/WarriorUnitProfile.asset"));
            sources.Add(Require<UnitAIProfile>("Assets/Game/Resources/UnitAIProfile/RangeUnitProfile.asset"));
            foreach (string name in new[] { "Unit", "NormalHero", "GateHero" })
                sources.Add(Require<GameObject>("Assets/Game/Prefabs/Units/" + name + ".prefab"));
            sources.Add(Require<SceneAsset>("Assets/Game/Scenes/Level.unity"));
            foreach (string path in new[] {
                "Assets/Game/Resources/BuffsData.asset", "Assets/Game/Resources/ElementsData.asset",
                "Assets/Game/Resources/AttributesData.asset" })
                sources.Add(Require<ScriptableObject>(path));

            var registry = new Registry();
            var validatedTargets = new HashSet<string>(StringComparer.Ordinal);
            var targetPaths = AssetDatabase.FindAssets("", new[] { "Assets/Game/Activities", "Assets/Game/Runtime/Run" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => !AssetDatabase.IsValidFolder(path))
                .OrderBy(path => path, StringComparer.Ordinal).ToList();
            registry.excludedSourcePaths.AddRange(excludedLevels.Select(PathOf).OrderBy(value => value, StringComparer.Ordinal));
            registry.errors.AddRange(errors.OrderBy(value => value, StringComparer.Ordinal));
            foreach (Object source in sources.OrderBy(PathOf, StringComparer.Ordinal))
            {
                string path = PathOf(source);
                var entry = new Entry {
                    sourcePath = path,
                    sourceGuid = AssetDatabase.AssetPathToGUID(path),
                    sourceType = source.GetType().FullName,
                    dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                    serializedParameters = source is GameObject body && path.StartsWith("Assets/Game/Prefabs/Units/", StringComparison.Ordinal)
                        ? string.Join("\n", body.GetComponents<Component>().Where(component => component is Collider2D
                            || component is Rigidbody2D || component is UnitAIController || component is Unit
                            || component is Health || component is TopDownController2D)
                            .Select(component => component.GetType().FullName + "\n" + EditorJsonUtility.ToJson(component, true)))
                        : File.ReadAllText(path),
                    expectedResult = ExpectedResult(source)
                };
                if (source is GameObject prefab)
                {
                    foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                        if (behaviour != null)
                        {
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(behaviour, out string _, out long localId);
                            entry.components.Add(new ComponentEntry {
                                type = behaviour.GetType().FullName,
                                localId = localId
                            });
                        }
                }
                entry.dependencies.AddRange(AssetDatabase.GetDependencies(path, false)
                    .Where(dependency => dependency != path).OrderBy(value => value, StringComparer.Ordinal));
                AddExistingTargets(source, entry, levels, units.units, skills, targetPaths);
                foreach (string target in entry.existingTargets)
                    if (validatedTargets.Add(target)) ValidateTargetReferences(target, registry.errors);
                registry.entries.Add(entry);
            }

            var report = new StringBuilder("# ChainRush: gameplay content registry\n\n");
            report.AppendLine("Implementation decision: **working model**. Generated explicitly from the referenced source content; no source assets or runtime state are changed.");
            report.AppendLine("\nExisting target assets are evidence of partial authoring, not a claim of gameplay parity. Exact serialized source parameters and prefab components are recorded in `ContentRegistry.json`. Runtime progress and player saves are not migrated by this command.");
            report.AppendLine($"\nLocations: {locations.locations.Count}; levels: {levels.Count}; player definitions: {units.units.Count}; enemy prefabs: {enemies.Count}; skill definitions (including dependencies): {skills.Count}.");
            report.AppendLine("\nExplicitly excluded by the approved scope decision: Loc001Lvl03–05 (missing current goal, board difficulty and enemy configuration). Their source assets remain unchanged; no LevelStage data conversion is performed.");
            report.AppendLine("\nApproved board composition supersedes the source guaranteed shares: Gold 30%; Unit, HeroSkill, Buff and Heal 17.5% each. Heal cooldown: 15 seconds. All five categories start ready; subsequent availability is accumulated independently. Bomb and SlowTime are excluded.");
            report.AppendLine("\n## Levels\n\n| Source | Goal | Target | Board | Enemy data |\n|---|---|---:|---|---|");
            foreach (LevelData level in levels)
                report.AppendLine($"| {PathOf(level)} | {level.Goal?.GoalType} | {level.Goal?.GoalAmount} | {level.boardSize.x} × {level.boardSize.y} | {PathOf(level.enemyData)} |");
            report.AppendLine("\n## Enemy rules decoded from source assets\n");
            foreach (var level in levels)
            {
                var data = level.enemyData;
                if (data == null) continue;
                report.AppendLine($"### {level.id}\n");
                report.AppendLine($"Fill volume: {data.maxFillCount}; simultaneous limit: {data.maxSimulteneousCount}.");
                report.AppendLine("Count curve (progress:value; in/out tangents): " + string.Join("; ", data.enemyCountCurve.keys
                    .Select(key => $"{Number(key.time)}:{Number(key.value)} ({Number(key.inTangent)}/{Number(key.outTangent)})")) + ".");
                report.AppendLine("Level multipliers: " + string.Join(", ", level.enemyMultipliers.OrderBy(pair => pair.Key)
                    .Select(pair => pair.Key + "=" + Number(pair.Value))) + ".\n");
                foreach (var pair in data.enemyProportions.OrderBy(pair => pair.Key))
                    report.AppendLine($"- Fill from {Number(pair.Key)}: {Composition(pair.Value)}.");
                foreach (var pair in data.waves.OrderBy(pair => pair.Key))
                    DescribeWave(report, "Progress " + Number(pair.Key), pair.Value);
                foreach (var pair in data.triggers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    DescribeWave(report, "Signal " + pair.Key, pair.Value);
                report.AppendLine($"\nProgress waves: {data.waves.Count}; signal waves: {data.triggers.Count}.\n");
            }
            report.AppendLine("\n## Board rules decoded from source assets\n");
            foreach (var level in levels)
            {
                var data = level.difficulty;
                if (data == null) continue;
                report.AppendLine($"### {level.id}\n");
                report.AppendLine("Refresh interval: " + Curve(data.refreshInterval) + ".");
                report.AppendLine("Pattern parameters: " + string.Join(", ", data.cellPatterns.OrderBy(pair => pair.Key)
                    .Select(pair => pair.Key + "=" + pair.Value)) + ".");
                report.AppendLine("Source guaranteed shares: " + string.Join(", ", data.alwaysAvailableOnRefresh.OrderBy(pair => pair.Key)
                    .Select(pair => pair.Key + "=" + Number(pair.Value))) + ".");
                foreach (var pair in data.refreshCooldowns.OrderBy(pair => pair.Key))
                    report.AppendLine("- " + pair.Key + " cooldown: " + Curve(pair.Value) + ".");
                report.AppendLine();
            }
            report.AppendLine("\n## Player definitions\n\n| Source | Type | Forms | Skills |\n|---|---|---:|---|");
            foreach (UnitData unit in units.units.Where(value => value != null))
                report.AppendLine($"| {PathOf(unit)} | {unit.type} | {unit.MergeStatesCount} | {string.Join(", ", unit.skills.Where(value => value?.data != null).Select(value => value.data.name))} |");
            report.AppendLine("\n## Referenced skill implementations\n\n| Definition | Implementation | Levels |\n|---|---|---:|");
            foreach (SkillData skill in skills.OrderBy(PathOf, StringComparer.Ordinal))
                report.AppendLine($"| {PathOf(skill)} | {(skill.prefab == null ? "MISSING" : skill.prefab.GetType().Name)} | {skill.LevelsCount} |");
            report.AppendLine("\n## Mapping and verification\n\n| Source | Existing targets | Verification contract |\n|---|---|---|");
            foreach (Entry entry in registry.entries)
                report.AppendLine($"| {entry.sourcePath} | {(entry.existingTargets.Count == 0 ? "Pending" : string.Join("<br>", entry.existingTargets))} | {entry.expectedResult} |");
            report.AppendLine("\n## Source validation\n");
            registry.errors = registry.errors.Distinct().OrderBy(value => value, StringComparer.Ordinal).ToList();
            report.AppendLine(registry.errors.Count == 0 ? "No missing required source references or broken object references in mapped target assets were found." : string.Join("\n", registry.errors.Select(error => "- " + error)));
            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(OutputDirectory + "/ContentRegistry.json", JsonUtility.ToJson(registry, true));
            File.WriteAllText(OutputDirectory + "/ContentRegistry.md", report.ToString());
            Debug.Log($"Gameplay content registry: {levels.Count} levels, {units.units.Count} player definitions, {enemies.Count} enemy prefabs, {skills.Count} skills, {errors.Count} source errors. Written to {OutputDirectory}.");
        }

        static void AddSkill(SkillData skill, HashSet<SkillData> skills)
        {
            if (skill == null || !skills.Add(skill)) return;
            AddSkill(skill.exchangeSkill, skills);
            AddSkill(skill.complementarySkill, skills);
        }

        static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string Curve(AnimationCurve curve) => string.Join("; ", curve.keys.Select(key =>
            $"{Number(key.time)}:{Number(key.value)} ({Number(key.inTangent)}/{Number(key.outTangent)})"));
        static string Composition(Dictionary<GameObject, float> shares) => shares == null ? "missing" :
            string.Join(", ", shares.OrderBy(pair => PathOf(pair.Key), StringComparer.Ordinal)
                .Select(pair => PathOf(pair.Key) + "=" + Number(pair.Value)));

        static void DescribeWave(StringBuilder report, string activation, EnemyWaveGenerationData wave)
        {
            if (wave == null) { report.AppendLine("- " + activation + ": missing wave."); return; }
            report.AppendLine($"- {activation}: amount={wave.amount}, repeats={wave.wavesCount}, interval={Number(wave.waveInterval)}, " +
                $"selection={wave.shareSelection}, notify={wave.notify}, shape={wave.spawnShape}, " +
                $"size=({Number(wave.spawnerSize.x)},{Number(wave.spawnerSize.y)}), offset=({Number(wave.spawnerDistance.x)},{Number(wave.spawnerDistance.y)}); " +
                "compositions=[" + string.Join(" / ", wave.shares.Select(Composition)) + "]; multipliers=" +
                string.Join(", ", wave.multipliers.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + Number(pair.Value))) + ".");
        }

        static bool IsExcludedLevel(LevelData level)
        {
            string path = PathOf(level);
            return path == "Assets/Game/Resources/Levels/Location001/Loc001Lvl03Data.asset"
                || path == "Assets/Game/Resources/Levels/Location001/Loc001Lvl04Data.asset"
                || path == "Assets/Game/Resources/Levels/Location001/Loc001Lvl05Data.asset";
        }

        static void AddEnemies(Dictionary<GameObject, float> composition, HashSet<GameObject> enemies)
        {
            if (composition == null) return;
            foreach (var pair in composition)
                if (pair.Key != null && pair.Value > 0) enemies.Add(pair.Key);
        }

        static void AddExistingTargets(Object source, Entry entry, List<LevelData> levels, List<UnitData> units,
            HashSet<SkillData> skills, List<string> targets)
        {
            const string battle = "Assets/Game/Activities/Autobattle/";
            const string board = "Assets/Game/Activities/Board/";
            const string run = "Assets/Game/Runtime/Run/";
            const string shared = "Assets/Game/Activities/Shared/";
            void Add(string path)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null && !entry.existingTargets.Contains(path))
                    entry.existingTargets.Add(path);
            }
            void AddDirectory(string path, string prefix = null)
            {
                foreach (string target in targets)
                    if (target.StartsWith(path, StringComparison.Ordinal)
                        && (prefix == null || Path.GetFileNameWithoutExtension(target).StartsWith(prefix, StringComparison.Ordinal))) Add(target);
            }
            void AddCharacter(string name)
            {
                AddDirectory(shared + "Units/" + name + "/");
                AddDirectory(battle + "Skills/", name);
                AddDirectory(battle + "AI/", name);
                Add(run + "RunAttributesFeature.asset");
                AddDirectory(shared + "Movement/", name);
                AddDirectory(battle + "Space/", name);
                if (name == "Water") Add(battle + "Space/UnitBody.asset");
                if (name == "Perfume") Add(battle + "Space/NormalHeroBody.asset");
                Add(battle + "Space/GateHeroBody.asset");
            }
            void AddLevel(LevelData level)
            {
                bool distance = level.Goal.GoalType == LevelGoalType.Distance;
                Add(battle + "Definition/" + (distance ? "DistanceActivity" : "AutobattleActivity") + ".asset");
                Add(battle + "GameFlow/" + (distance ? "DistanceFlow" : "AutobattleFlow") + ".asset");
                Add(battle + "Definition/" + level.Id + "TabascoActivity.asset");
                Add(battle + "GameFlow/" + level.Id + "TabascoFlow.asset");
                Add(battle + "Agents/" + level.Id + "PopulationAgent.asset");
                Add(battle + "Objectives/" + level.Id + "ReplenishmentObjective.asset");
                Add(run + level.Id + "ReplenishmentRegion.asset");
                Add(run + level.Id + "EnemyField.asset");
                Add(run + (distance ? "Distance" : "Survive") + "RunSelection.asset");
                Add(run + (distance ? "DistanceProgressFeature" : "RunProgressFeature") + ".asset");
                Add(run + (distance ? "DistanceFollowFeature" : "RunFollowFeature") + ".asset");
                Add(run + (distance ? "DistanceHeroSkillFeature" : "HeroSkillFeature") + ".asset");
            }
            if (source is UnitData unit) AddCharacter(unit.name.Replace("Data", ""));
            if (source is LevelData level) AddLevel(level);
            if (source is AllLocationsData || source is LocationData || source is EnemyGenerationData)
                foreach (var candidate in levels)
                    if (source is AllLocationsData || source is LocationData location && location.levels.Contains(candidate)
                        || candidate.enemyData == source) AddLevel(candidate);
            if (source is LocationData emptyLocation && !levels.Any(candidate => emptyLocation.levels.Contains(candidate)))
                entry.expectedResult = "Location has no included level; no runtime content is required for part 1.";
            if (source is AllUnitsData)
                foreach (var character in units.Where(value => value != null)) AddCharacter(character.name.Replace("Data", ""));
            if (source is LevelDifficultyData)
            {
                Add(board + "Agents/BoardPopulationAgent.asset");
                AddDirectory(board + "Agents/", "UnitContent");
                AddDirectory(board + "Agents/", "BuffContent");
                AddDirectory(board + "Agents/", "HeroSkillContent");
                AddDirectory(board + "Agents/", "HealContent");
                AddDirectory(board + "Agents/", "GoldContent");
                AddDirectory(board + "Space/Shapes/");
            }
            if (source is UnitAIProfile profile)
                AddCharacter(profile.name == "WarriorUnitProfile" ? "Water" : "Cola");
            if (source is EnemyData || source is GameObject && PathOf(source).StartsWith("Assets/Game/Prefabs/Enemies/", StringComparison.Ordinal))
            {
                Add(battle + "Economy/" + source.name + ".asset");
                Add(battle + "Projection/" + source.name + ".prefab");
                AddDirectory(battle + "Skills/", source.name);
                AddDirectory(battle + "AI/", source.name);
                Add(battle + "Production/" + (source.name == "BugBrownSmall" ? "EnemyWave" : source.name) + "Recipe.asset");
                Add(battle + "Economy/ExperienceDrop.asset");
                Add(battle + "Space/" + source.name + "Body.asset");
                Add(battle + "Projection/ContactArea.asset");
            }
            if (source is GameObject equipment && (equipment.GetComponent<Weapon>() != null || equipment.GetComponent<Projectile>() != null))
                foreach (string name in new[] { "BugGreenSmall", "BugGreenMedium", "BugPurpleSmall", "BugPurpleMedium" })
                    if (AssetDatabase.GetDependencies("Assets/Game/Prefabs/Enemies/" + name + ".prefab", true).Contains(PathOf(source)))
                    { AddDirectory(battle + "Skills/", name); AddDirectory(battle + "Projection/", name); }
            if (source is GameObject && PathOf(source).StartsWith("Assets/Game/Prefabs/Units/", StringComparison.Ordinal))
            {
                Add(battle + "Space/" + source.name + "Body.asset");
                if (source.name == "Unit") { AddCharacter("Water"); AddCharacter("Cola"); }
            }
            if (source is SceneAsset)
            {
                AddDirectory(battle + "AI/", "Water"); AddDirectory(battle + "AI/", "Cola");
                Add(battle + "Economy/ExperienceDrop.asset");
                foreach (var includedLevel in levels) AddLevel(includedLevel);
            }
            foreach (var skill in skills)
                if (source == skill || skill.prefab != null && source == skill.prefab.gameObject)
                {
                    if (skill.name == "SkillLightningBolt")
                    {
                        AddDirectory(battle + "Skills/", "LightningBolt");
                        Add(run + "HeroSkillFeature.asset");
                        Add(run + "VisibleEnemiesQuery.asset");
                    }
                    foreach (var character in units.Where(value => value != null))
                    {
                        var used = new HashSet<SkillData>();
                        foreach (var binding in character.skills) AddSkill(binding?.data, used);
                        if (used.Contains(skill)) AddCharacter(character.name.Replace("Data", ""));
                    }
                }
            if (PathOf(source) == "Assets/Game/Resources/BuffsData.asset"
                || PathOf(source) == "Assets/Game/Resources/ElementsData.asset"
                || PathOf(source) == "Assets/Game/Resources/AttributesData.asset")
            {
                Add(run + "RunAttributesFeature.asset");
                AddDirectory(shared + "Attributes/");
            }
            entry.existingTargets.Sort(StringComparer.Ordinal);
        }

        static void ValidateTargetReferences(string path, List<string> errors)
        {
            Object target = AssetDatabase.LoadMainAssetAtPath(path);
            if (target == null) { errors.Add("Missing mapped target: " + path); return; }
            IEnumerable<Object> objects = target is GameObject prefab
                ? prefab.GetComponentsInChildren<Component>(true).Cast<Object>() : new[] { target };
            foreach (Object value in objects)
            {
                if (value == null) { errors.Add("Missing component in mapped target: " + path); continue; }
                using var serialized = new SerializedObject(value);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        errors.Add("Broken target reference: " + path + " :: " + property.propertyPath);
            }
        }

        static string ExpectedResult(Object source)
        {
            if (source is LevelData) return "Progress, board dimensions, enemy rules and multipliers match this level.";
            if (source is EnemyGenerationData) return "Count replenishment, progress and signal waves preserve compositions, geometry and repetitions.";
            if (source is LevelDifficultyData) return "Board shapes and category availability follow authored progress curves.";
            if (source is UnitData) return "All referenced forms, progression values, elemental attributes and acquired skills affect combat.";
            if (source is SkillData) return "Targeting, parameters, effects and skill dependencies execute through framework Skills.";
            if (source is GameObject) return "Required combat behaviour and presentation are expressed by framework definitions; original executors are absent from the integration runtime.";
            return "Referenced gameplay configuration has an explicit target mapping and validation.";
        }

        static string PathOf(Object value) => value == null ? "" : AssetDatabase.GetAssetPath(value);

        static T Require<T>(string path) where T : Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) throw new InvalidOperationException("Missing required source: " + path);
            return value;
        }

        [Serializable] sealed class Registry
        {
            public List<Entry> entries = new List<Entry>();
            public List<string> errors = new List<string>();
            public List<string> excludedSourcePaths = new List<string>();
        }

        [Serializable] sealed class Entry
        {
            public string sourcePath;
            public string sourceGuid;
            public string sourceType;
            public string dependencyHash;
            public string serializedParameters;
            public string expectedResult;
            public List<string> dependencies = new List<string>();
            public List<string> existingTargets = new List<string>();
            public List<ComponentEntry> components = new List<ComponentEntry>();
        }

        [Serializable] sealed class ComponentEntry
        {
            public string type;
            public long localId;
        }
    }
}
