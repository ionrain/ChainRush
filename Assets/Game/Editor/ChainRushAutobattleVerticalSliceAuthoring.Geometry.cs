using System;
using System.Collections.Generic;
using System.Linq;
using ChainRush.Gameplay;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.Taxonomy;
using Core.World;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Editor
{
    public static partial class ChainRushAutobattleVerticalSliceAuthoring
    {
        internal static void ApplyRunGeometry()
        {
            var installer = LoadRequired<EconomyDefinitionsInstallerData>(EconomyInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(installer, "assets");
            var unitPrefab = LoadRequired<GameObject>("Assets/Game/Prefabs/Units/Unit.prefab");
            var heroPrefab = LoadRequired<GameObject>("Assets/Game/Prefabs/Units/NormalHero.prefab");
            var gateGeometry = WriteSourceBody("GateHero", LoadRequired<GameObject>("Assets/Game/Prefabs/Units/GateHero.prefab"), definitions);
            var wallet = LoadRequired<EconomyWalletData>(UnitWalletPath);
            var distanceHeroes = new List<ChainRushHeroDefinitionBinding>();
            var attributes = LoadRequired<ChainRushRunAttributesFeatureData>("Assets/Game/Runtime/Run/RunAttributesFeature.asset");
            var characters = GetField<List<ChainRushRunCharacterBinding>>(attributes, "characters");
            foreach (string name in new[] { "Water", "Cola", "Perfume", "Tabasco" })
            {
                var source = LoadRequired<UnitData>("Assets/Game/Resources/Units/" + name + "Data.asset");
                for (int form = 1; form <= source.MergeStatesCount; form++)
                {
                    string suffix = source.type == UnitType.Hero ? name : name + "Unit" + (form == 1 ? "" : form.ToString());
                    var host = LoadRequired<CapabilityHostData>(SharedRoot + "/Units/" + name + "/" + suffix + ".asset");
                    bool hero = source.type == UnitType.Hero;
                    var prefab = hero ? heroPrefab : unitPrefab;
                    bool mergeBody = new SerializedObject(prefab.GetComponent<Unit>()).FindProperty("changeColliderSettings").boolValue;
                    string bodyName = name == "Water" && form == 1 ? "Unit" : name == "Perfume" ? "NormalHero" : suffix;
                    var body = WriteSourceBody(bodyName, prefab, definitions, mergeBody ? source.GetMergeData(form - 1) : null);
                    SetBodySeed(host, wallet, hero ? gateGeometry : body);
                    if (hero)
                    {
                        var distanceHost = ChainRushBoardPlannerAuthoring.WriteContentAsset<CapabilityHostData>(
                            SharedRoot + "/Units/" + name + "/" + name + "Distance.asset", host, host.Id + ".distance", definitions);
                        SetBodySeed(distanceHost, wallet, body);
                        var binding = new ChainRushHeroDefinitionBinding();
                        SetField(binding, "contentId", source.name); SetField(binding, "definition", distanceHost);
                        distanceHeroes.Add(binding);
                        var character = characters.Single(value => value.Definition == host);
                        var variant = new ChainRushRunCharacterBinding();
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(character), variant);
                        variant.Definition = distanceHost;
                        characters.Add(variant);
                    }
                }
            }
            ConfigureDistanceHeroBodies(distanceHeroes);
            EditorUtility.SetDirty(attributes);
            foreach (string name in new[] { "BugBrownSmall", "BugBrownMedium", "BugGreenSmall", "BugGreenMedium", "BugPurpleSmall", "BugPurpleMedium" })
            {
                var body = WriteSourceBody(name, LoadRequired<GameObject>("Assets/Game/Prefabs/Enemies/" + name + ".prefab"), definitions);
                var host = LoadRequired<CapabilityHostData>(EconomyRoot + "/" + name + ".asset");
                var seeds = host.WalletEntries.Single(entry => entry.Wallet == wallet).Seed;
                seeds.RemoveAll(seed => seed.Asset is InteractionGeometryData);
                seeds.Add(new SeedEntry(body, 1, EconomyFormType.Stack));
                EditorUtility.SetDirty(host);
            }
            EditorUtility.SetDirty(installer);
        }

        static void SetBodySeed(CapabilityHostData host, EconomyWalletData wallet, InteractionGeometryData body)
        {
            var seeds = host.WalletEntries.Single(entry => entry.Wallet == wallet).Seed;
            seeds.RemoveAll(seed => seed.Asset is InteractionGeometryData);
            seeds.Add(new SeedEntry(body, 1, EconomyFormType.Stack));
            EditorUtility.SetDirty(host);
        }

        static void ConfigureDistanceHeroBodies(List<ChainRushHeroDefinitionBinding> heroes)
        {
            const string run = "Assets/Game/Runtime/Run/";
            var activity = LoadRequired<ActivityData>(DistanceActivityPath);
            var perfume = heroes.Single(value => value.ContentId == "PerfumeData").Definition;
            foreach (var wallet in activity.Teams[0].Wallets)
                for (int i = 0; i < wallet.Seed.Count; i++)
                {
                    var seed = wallet.Seed[i];
                    if (seed.Seed.Asset != LoadRequired<CapabilityHostData>(PerfumePath)) continue;
                    wallet.Seed[i] = new ActivityWalletSeedEntryData(new SeedEntry(perfume, 1, EconomyFormType.Token),
                        seed.MaterializationType, new List<TaxonomyTermData>(seed.ProjectionTargetTags),
                        new List<TaxonomyTermData>(seed.MaterializationMarkerTags));
                }
            var follow = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushRunFollowFeatureData>(
                run + "DistanceFollowFeature.asset", LoadRequired<ChainRushRunFollowFeatureData>(run + "RunFollowFeature.asset"));
            SetField(follow, "heroes", heroes);
            var progress = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushRunProgressFeatureData>(
                run + "DistanceProgressFeature.asset", LoadRequired<ChainRushRunProgressFeatureData>(run + "RunProgressFeature.asset"));
            SetField(progress, "heroes", CopyHeroBindings(heroes));
            var route = LoadRequired<ChainRushHeroRouteFeatureData>(run + "HeroRouteFeature.asset");
            SetField(route, "heroes", CopyHeroBindings(heroes));
            var skills = ChainRushBoardPlannerAuthoring.WriteContentAsset<ChainRushHeroSkillFeatureData>(
                run + "DistanceHeroSkillFeature.asset", LoadRequired<ChainRushHeroSkillFeatureData>(run + "HeroSkillFeature.asset"));
            SetField(skills, "hero", perfume);
            activity.Teams[0].Features.RemoveAll(value => value is ChainRushRunFollowFeatureData
                || value is ChainRushHeroSkillFeatureData || value is ChainRushRunProgressFeatureData);
            AddUnique(activity.Teams[0].Features, follow, skills, progress);
            EditorUtility.SetDirty(follow); EditorUtility.SetDirty(route); EditorUtility.SetDirty(skills);
            EditorUtility.SetDirty(progress); EditorUtility.SetDirty(activity);
        }

        static List<ChainRushHeroDefinitionBinding> CopyHeroBindings(List<ChainRushHeroDefinitionBinding> heroes) =>
            heroes.Select(value =>
            {
                var copy = new ChainRushHeroDefinitionBinding();
                SetField(copy, "contentId", value.ContentId); SetField(copy, "definition", value.Definition); return copy;
            }).ToList();

        static InteractionGeometryData WriteSourceBody(string name, GameObject source, List<EconomyAssetData> definitions,
            MergeStateData merge = null)
        {
            var collider = source.GetComponent<Collider2D>();
            if (collider == null) throw new InvalidOperationException("Missing source body collider: " + source.name);
            Vector3 scale = collider.transform.lossyScale;
            Vector2 sourceOffset = merge == null ? collider.offset : merge.colliderOffset;
            var offset = new Vector3Int(Coordinate(sourceOffset.x * scale.x), 0, Coordinate(sourceOffset.y * scale.y));
            Vector3Int size;
            InteractionGeometryPrimitiveType type;
            if (collider is BoxCollider2D box && box.edgeRadius == 0)
            {
                Vector2 sourceSize = merge == null ? box.size : merge.colliderSize;
                size = new Vector3Int(Distance(sourceSize.x * Mathf.Abs(scale.x)), 0, Distance(sourceSize.y * Mathf.Abs(scale.y)));
                type = InteractionGeometryPrimitiveType.Box;
            }
            else if (collider is CircleCollider2D circle && Mathf.Approximately(Mathf.Abs(scale.x), Mathf.Abs(scale.y)))
            {
                int diameter = Distance(circle.radius * Mathf.Abs(scale.x) * 2);
                size = new Vector3Int(diameter, 0, diameter); type = InteractionGeometryPrimitiveType.Sphere;
            }
            else throw new InvalidOperationException("Unmapped source body geometry: " + source.name);
            var geometry = ChainRushBoardPlannerAuthoring.WriteContentAsset<InteractionGeometryData>(
                SpaceRoot + "/" + name + "Body.asset", null, "chainrush.interaction." + name.ToLowerInvariant(), definitions);
            SetField(geometry, "allowedOperations", AllMutableOperations);
            var binding = new InteractionGeometryBindingData();
            SetField(binding, "shape", LoadRequired<SpatialShapeData>(SpawnAreaShapePath));
            SetField(binding, "primitiveType", type);
            SetField(binding, "interactionTags", new List<TaxonomyTermData> { LoadRequired<TaxonomyTermData>(CombatantRolePath) });
            SetField(binding, "usage", new SpatialShapeUsageData(SpatialShapeFillType.Inside, offset,
                Vector3Int.one, Vector3Int.zero, size, Vector3Int.zero));
            SetField(geometry, "bindings", new List<InteractionGeometryBindingData> { binding });
            EditorUtility.SetDirty(geometry);
            return geometry;
        }

        static int Coordinate(float value)
        {
            if (!float.IsFinite(value)) throw new InvalidOperationException("Source coordinate must be finite.");
            return checked((int)Math.Round(value * 1000d, MidpointRounding.AwayFromZero));
        }
    }
}
