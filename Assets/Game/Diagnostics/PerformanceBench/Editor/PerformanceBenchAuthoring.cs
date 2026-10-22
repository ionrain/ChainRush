#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using ChainRush.Diagnostics;
using Core;
using Core.Activities;
using Core.Activities.GameRuntime.Installers;
using Core.AI;
using Core.AI.Actions;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime;
using Core.GameRuntime.Installers;
using Core.HostValues;
using Core.Knowledge;
using Core.Players;
using Core.Projection;
using Core.Runtime;
using Core.Scheduling;
using Core.Simulation;
using Core.Skills;
using Core.Taxonomy;
using Core.World;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChainRush.Editor
{
    // Explicit creation of new diagnostic content. Existing gameplay assets are never rewritten.
    public static partial class PerformanceBenchAuthoring
    {
        internal const string ScenePath = "Assets/Game/Scenes/Diagnostics/PerformanceBench.unity";
        const string Content = "Assets/Game/Diagnostics/PerformanceBench/Content";
        const string Installers = "Assets/Game/Runtime/Installers/";
        const string Taxonomy = "Assets/Game/Activities/Autobattle/AI/Taxonomy/";
        const string Occupancy = "Assets/Game/Activities/Shared/Spatial/Occupancy/";
        static readonly List<string> Created = new();
        static AddressableAssetGroup _createdGroup;

        [MenuItem("ChainRush/Diagnostics/Create Performance Bench")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Создание стенда выполняется вне Play Mode.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                || AssetDatabase.LoadAssetAtPath<PerformanceBenchAssets>(Content + "/BenchAssets.asset") != null)
                throw new InvalidOperationException("Стенд уже существует; создание не перезаписывает существующие ассеты.");
            Created.Clear();
            var priorScenes = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode)
                foreach (var scene in priorScenes)
                    if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("Сохраните открытую сцену перед созданием стенда.");
            try
            {
                Folder(Content); Folder("Assets/Game/Scenes/Diagnostics");
                var worldWallet = New<EconomyWalletData>("WorldWallet"); Set(worldWallet, "id", "chainrush.diagnostics.bench.world");
                var actorWallet = New<EconomyWalletData>("ActorWallet"); Set(actorWallet, "id", "chainrush.diagnostics.bench.actor");
                var speed = Economic<HostValueData>("Speed", "speed");
                var node = Require<TaxonomyTermData>(Taxonomy + "CombatNode.asset");
                var move = Require<TaxonomyTermData>(Taxonomy + "CombatStateA.asset");
                var idle = Require<TaxonomyTermData>(Taxonomy + "WaitingState.asset");
                var movement = Economic<MovementData>("Movement", "movement");
                Set(movement, "spacing", 0); Set(movement, "avoidancePriority", 0);
                Set(movement, "destinationFallback", new MovementDestinationFallbackData(MovementDestinationFallbackType.NearestReachable, 1400));
                var skill = Economic<Core.Skills.SkillData>("MoveSkill", "move-skill");
                Set(skill, "targetType", SkillTargetType.Entities);
                var effect = new SkillMoveToTargetEffectData(); Set(effect, "targetGeometryType", WorldTargetGeometryType.Spatial);
                Set(effect, "targetTrackingMode", TargetTrackingMode.None); Set(effect, "accelerationDuration", 2); Set(effect, "decelerationDuration", 2);
                Set(effect, "formula", new SkillEffectFormulaData(new SkillEffectHostValueNodeData(SkillParameterTargetType.Owner, speed)));
                Set(skill, "effects", new List<SkillEffectData> { effect });
                var useSkill = new UseSkillAIBrainActionData(); Set(useSkill, "skill", skill);
                Set(useSkill, "completionPolicy", SkillCompletionPolicyType.OnExecutionComplete);
                var movingState = new AIBrainStateData(); Set(movingState, "tag", move);
                Set(movingState, "onTickActions", new List<AIBrainActionData> { useSkill });
                var idleState = new AIBrainStateData(); Set(idleState, "tag", idle);
                var brainNode = new AIBrainNodeData(); Set(brainNode, "nodeId", node); Set(brainNode, "entryState", idle);
                Set(brainNode, "states", new List<AIBrainStateData> { idleState, movingState });
                var brain = Economic<AIBrainData>("Brain", "brain"); Set(brain, "defaultControlNodeId", node);
                Set(brain, "nodes", new List<AIBrainNodeData> { brainNode });

                var sprite = CreateSprite();
                var projection = CreateProjection(sprite);
                var staticObject = Host("StaticObject", actorWallet, new List<SeedEntry>(), projection,
                    Require<TaxonomyTermData>(Occupancy + "NonOccupying.asset"), new List<CapabilityEntry>(), new Vector3Int(500, 500, 0));
                var movingObject = Host("MovingObject", actorWallet, new List<SeedEntry>
                {
                    new(brain, 1, EconomyFormType.Stack, new List<TaxonomyTermData> { node }),
                    new(skill, 1, EconomyFormType.Stack, new List<TaxonomyTermData> { node }),
                    new(movement, 1, EconomyFormType.Stack, new List<TaxonomyTermData> { node }),
                    new(speed, 67, EconomyFormType.Stack),
                }, projection, Require<TaxonomyTermData>(Occupancy + "MobileSolid.asset"),
                    new List<CapabilityEntry> { Capability(CapabilityHostType.AIBrainOwner, node), Capability(CapabilityHostType.SkillOwner, node),
                        Capability(CapabilityHostType.MovementOwner, node), Capability(CapabilityHostType.ControlOwner, null) }, new Vector3Int(500, 500, 0));
                var obstacle = Host("ObstacleObject", actorWallet, new List<SeedEntry>(), null,
                    Require<TaxonomyTermData>(Occupancy + "StaticSolid.asset"), new List<CapabilityEntry>(), new Vector3Int(600, 8000, 0));

                var emptyRoot = new GameObject("BenchSpace");
                var space = PrefabUtility.SaveAsPrefabAsset(emptyRoot, Content + "/BenchSpace.prefab");
                Object.DestroyImmediate(emptyRoot); Created.Add(Content + "/BenchSpace.prefab"); Addressable(Content + "/BenchSpace.prefab");
                var walletSize = Require<EconomyAssetData>(Installers + "ChainRushWalletSize.asset");
                var straight = Activity("StraightActivity", NavigationAlgorithmType.StraightLine, worldWallet, walletSize);
                var navigation = Activity("NavigationActivity", NavigationAlgorithmType.AStar, worldWallet, walletSize);
                var bundle = New<PerformanceBenchAssets>("BenchAssets");
                bundle.straightActivity = straight; bundle.navigationActivity = navigation;
                bundle.staticObject = staticObject; bundle.movingObject = movingObject; bundle.obstacleObject = obstacle;
                bundle.worldWallet = worldWallet; bundle.speedValue = speed; bundle.controlNode = node; bundle.moveState = move;
                bundle.projectionPrefab = projection;
                var foundation = Require<GameplayFoundationInstallerData>(Installers + "ChainRushGameplayFoundationInstaller.asset");
                bundle.trafficProfile = Get<MovementTrafficProfileData>(foundation, "movementTrafficProfile");
                bundle.navigationBudget = Get<NavigationWorkBudgetData>(foundation, "navigationWorkBudget");
                EditorUtility.SetDirty(bundle); AssetDatabase.SaveAssetIfDirty(bundle);

                var definitions = New<EconomyDefinitionsInstallerData>("BenchEconomyDefinitions");
                Set(definitions, "assets", new List<EconomyAssetData> { walletSize, speed, movement, skill, brain,
                    staticObject, movingObject, obstacle, straight, navigation });
                Set(definitions, "wallets", new List<EconomyWalletData> { worldWallet, actorWallet });
                Set(definitions, "walletSizeAsset", walletSize);
                var skills = Copy(Require<GameplaySkillsInstallerData>(Installers + "ChainRushGameplaySkillsInstaller.asset"), "BenchSkillsInstaller");
                Set(skills, "skills", new List<Core.Skills.SkillData> { skill });
                var values = New<GameplayHostValuesInstallerData>("BenchHostValuesInstaller");
                var valueDefinition = new HostValueDefinitionData(); Set(valueDefinition, "value", speed);
                Set(values, "definitions", new List<HostValueDefinitionData> { valueDefinition });
                var activityInstaller = New<ActivityRuntimeInstallerData>("BenchActivityInstaller");
                Set(activityInstaller, "activities", new List<ActivityData> { straight, navigation });
                var benchInstaller = New<PerformanceBenchRuntimeInstallerData>("BenchInstaller");
                Set(benchInstaller, "assets", bundle);
                var storage = New<StorageRuntimeInstallerData>("BenchStorageInstaller");
                var players = New<PlayerDefinitionsInstallerData>("BenchPlayerInstaller");
                var profile = New<GameRuntimeProfileData>("BenchRuntimeProfile");
                Set(profile, "installers", new List<GameRuntimeInstallerData>
                {
                    Require<LocalRealtimeDriverInstallerData>(Installers + "ChainRushLocalRealtimeDriverInstaller.asset"),
                    Require<SimulationRuntimeInstallerData>(Installers + "ChainRushSimulationRuntimeInstaller.asset"),
                    Require<EconomyRuntimeInstallerData>(Installers + "ChainRushEconomyRuntimeInstaller.asset"), storage,
                    Require<TaxonomyRuntimeInstallerData>(Installers + "ChainRushTaxonomyRuntimeInstaller.asset"),
                    Require<DiplomacyRuntimeInstallerData>(Installers + "ChainRushDiplomacyRuntimeInstaller.asset"),
                    definitions, values, skills, players, activityInstaller, foundation,
                    Require<ProjectionServiceRuntimeData>(Installers + "ChainRushProjectionRuntimeInstaller.asset"), benchInstaller,
                });
                var player = New<PlayerData>("BenchPlayer"); Set(player, "id", "chainrush.diagnostics.performance-bench");
                var startup = New<GameStartupPlanData>("BenchStartupPlan");
                var determinism = New<GameDeterminismConfigData>("BenchDeterminism"); Set(determinism, "fixedSeed", 1337);
                CreateScene(profile, startup, determinism, player);
                foreach (var path in Created) { var asset = AssetDatabase.LoadMainAssetAtPath(path); if (asset != null) EditorUtility.SetDirty(asset); }
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                Debug.Log("PerformanceBench scene and diagnostic assets created. Play Mode and tests were not started.");
            }
            catch
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (_createdGroup != null && settings != null) settings.RemoveGroup(_createdGroup);
                for (int i = Created.Count - 1; i >= 0; i--) AssetDatabase.DeleteAsset(Created[i]);
                AssetDatabase.SaveAssets(); throw;
            }
            finally
            {
                if (Application.isBatchMode || priorScenes.Length == 0)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else EditorSceneManager.RestoreSceneManagerSetup(priorScenes);
                Created.Clear(); _createdGroup = null;
            }
        }
        static ActivityData Activity(string name, NavigationAlgorithmType algorithm, EconomyWalletData wallet, EconomyAssetData walletSize)
        {
            var topology = New<TopologyDefinitionData>(name + "Topology"); Set(topology, "upAxisType", TopologyUpAxisType.Z);
            Set(topology, "navigationAlgorithmType", algorithm); Set(topology, "coordinateOccupationPolicy", TopologyCoordinateOccupationPolicy.MultipleOccupants);
            var activity = Economic<ActivityData>(name, name.ToLowerInvariant());
            Set(activity, "activityType", Require<ActivityTypeData>("Assets/Game/Activities/Autobattle/Definition/AutobattleActivityType.asset"));
            Set(activity, "topology", topology);
            Set(activity, "result", new ActivityResultSettingsData(ActivityEndMode.Manual, 1, -1));
            object schedule = default(ActivitySchedulePolicyData); Set(schedule, "scheduleType", ScheduleType.Realtime);
            // working model: at most one complete benchmark step per rendered frame.
            Set(schedule, "tickDelta", 1f / 30); Set(schedule, "catchUpPolicyType", RealtimeCatchUpPolicyType.Budgeted); Set(schedule, "maxStepsPerFrame", 1);
            Set(activity, "schedule", schedule);
            object simulation = default(ActivitySimulationPolicyData); Set(simulation, "bindingType", ActivitySimulationBindingType.OwnContext);
            Set(simulation, "scope", SimulationScope.Activity); Set(activity, "simulation", simulation);
            object team = default(ActivityTeamData); Set(team, "slotCount", 1);
            Set(team, "features", new List<ActivityFeatureData>
            { Require<ActivityKnowledgeConfigData>("Assets/Game/Activities/Autobattle/Knowledge/PlayerKnowledge.asset") });
            Set(activity, "teams", new List<ActivityTeamData> { (ActivityTeamData)team });
            Set(activity, "worldWallets", new List<WalletEntry> { new(wallet, new List<SeedEntry> { new(walletSize, 100000, EconomyFormType.Stack) }) });
            var space = new ActivityPrefabSpaceData(); Set(space, "prefabReference", new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(Content + "/BenchSpace.prefab")));
            Set(activity, "space", space); return activity;
        }
        static CapabilityHostData Host(string name, EconomyWalletData wallet, List<SeedEntry> seeds, GameObject prefab,
            TaxonomyTermData occupancy, List<CapabilityEntry> capabilities, Vector3Int footprint)
        {
            var host = Economic<CapabilityHostData>(name, name.ToLowerInvariant()); host.Tags.Add(occupancy);
            Set(host, "footprintSize", footprint); Set(host, "walletEntries", new List<WalletEntry> { new(wallet, seeds) });
            Set(host, "capabilities", capabilities);
            if (prefab != null)
            {
                Set(host, "projectionPrefabReference", new ProjectionPrefabReference(AssetDatabase.AssetPathToGUID(Content + "/BenchObject.prefab")));
                var pool = new ProjectionPoolData(); Set(pool, "demandValue", 0); Set(pool, "maxCapacity", 0); Set(host, "projectionPool", pool);
            }
            return host;
        }
        static CapabilityEntry Capability(CapabilityHostType type, TaxonomyTermData selector)
        {
            var result = new CapabilityEntry(); Set(result, "capabilityType", type);
            Set(result, "selectorTags", selector == null ? new List<TaxonomyTermData>() : new List<TaxonomyTermData> { selector }); return result;
        }
        static Sprite CreateSprite()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "BenchSprite", filterMode = FilterMode.Bilinear };
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                pixels[y * 32 + x] = (x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f) <= 225 ? Color.white : Color.clear;
            texture.SetPixels(pixels); texture.Apply();
            AssetDatabase.CreateAsset(texture, Content + "/BenchSprite.asset"); Created.Add(Content + "/BenchSprite.asset");
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 64); sprite.name = "BenchSprite";
            AssetDatabase.AddObjectToAsset(sprite, texture); return sprite;
        }
        static GameObject CreateProjection(Sprite sprite)
        {
            var root = new GameObject("BenchObject"); root.AddComponent<ProjectionBindingController>(); root.AddComponent<ProjectionMovementController>();
            var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = new Color(.2f, .85f, .7f);
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("URP 2D unlit sprite shader is unavailable.");
            var material = new Material(shader); AssetDatabase.CreateAsset(material, Content + "/BenchSpriteMaterial.mat"); Created.Add(Content + "/BenchSpriteMaterial.mat");
            renderer.sharedMaterial = material;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Content + "/BenchObject.prefab"); Object.DestroyImmediate(root);
            Created.Add(Content + "/BenchObject.prefab"); Addressable(Content + "/BenchObject.prefab"); return prefab;
        }
        static void Addressable(string path)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Addressables settings are required.");
            var group = settings.FindGroup("PerformanceBench");
            if (group == null)
            {
                group = settings.CreateGroup("PerformanceBench", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                _createdGroup = group;
                group.GetSchema<BundledAssetGroupSchema>().IncludeInBuild = false;
            }
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
            EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
        }
        static T New<T>(string name) where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>(); value.name = name;
            string path = Content + "/" + name + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Asset already exists: " + path);
            AssetDatabase.CreateAsset(value, path); Created.Add(path); return value;
        }
        static T Economic<T>(string name, string id) where T : EconomyAssetData
        {
            var value = New<T>(name); Set(value, "id", "chainrush.diagnostics.performance-bench." + id);
            Set(value, "allowedOperations", EconomyOperation.Require | EconomyOperation.Issue | EconomyOperation.Consume | EconomyOperation.Transfer | EconomyOperation.Destroy);
            return value;
        }
        static T Copy<T>(T source, string name) where T : ScriptableObject
        {
            var value = Object.Instantiate(source); value.name = name; string path = Content + "/" + name + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("Asset already exists: " + path);
            AssetDatabase.CreateAsset(value, path); Created.Add(path); return value;
        }
        static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Required asset is unavailable: " + path);
        static FieldInfo Field(object value, string name)
        {
            for (var type = value.GetType(); type != null; type = type.BaseType)
            { var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly); if (field != null) return field; }
            throw new MissingFieldException(value.GetType().Name, name);
        }
        static void Set(object value, string name, object fieldValue)
        {
            Field(value, name).SetValue(value, fieldValue);
            if (value is Object asset)
            {
                EditorUtility.SetDirty(asset);
                if (AssetDatabase.Contains(asset)) AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
        static T Get<T>(object value, string name) => (T)Field(value, name).GetValue(value);
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
#endif
