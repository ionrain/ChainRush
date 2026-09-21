using System;
using System.Collections.Generic;
using Core;
using Core.Activities;
using Core.Economy;
using Core.GameRuntime.Installers;
using Core.Orchestration;
using Core.World;
using UnityEditor;
using UnityEngine;

namespace ChainRush.Editor
{
    public static partial class ChainRushBoardPlannerAuthoring
    {
        internal static void ApplyRunShapes()
        {
            var source = LoadRequired<LevelDifficultyData>("Assets/Game/Resources/LevelDifficulty/SimpleLevelDifficultyData.asset");
            var expected = new Dictionary<CellSelectPatternType, int>
            {
                { CellSelectPatternType.SelectOne, 1 }, { CellSelectPatternType.Line, 2 },
                { CellSelectPatternType.Corner, 3 }, { CellSelectPatternType.Box, 4 }, { CellSelectPatternType.Zigzag, 5 }
            };
            if (source.cellPatterns.Count != expected.Count) throw new InvalidOperationException("The selected source pattern set changed.");
            foreach (var pair in expected)
                if (!source.cellPatterns.TryGetValue(pair.Key, out int length) || length != pair.Value)
                    throw new InvalidOperationException("The selected source pattern parameters changed: " + pair.Key);
            var installer = LoadRequired<EconomyDefinitionsInstallerData>(EconomyDefinitionsInstallerPath);
            var definitions = GetField<List<EconomyAssetData>>(installer, "assets");
            var single = WriteFixedRunShape("Single", new List<Vector3Int> { Vector3Int.zero }, definitions);
            var line = WriteFixedRunShape("Line", new List<Vector3Int> { Vector3Int.zero, Vector3Int.right }, definitions);
            var diagonal = WriteFixedRunShape("DiagonalLine", new List<Vector3Int> { Vector3Int.zero, new Vector3Int(1, 0, 1) }, definitions);
            var corner = WriteFixedRunShape("Corner", new List<Vector3Int>
                { Vector3Int.zero, Vector3Int.right, new Vector3Int(1, 0, 1) }, definitions);
            var zigzag = WriteFixedRunShape("Zigzag", new List<Vector3Int>
                { Vector3Int.zero, Vector3Int.right, new Vector3Int(1, 0, 1), new Vector3Int(2, 0, 1), new Vector3Int(2, 0, 2) }, definitions);
            var mirrored = WriteFixedRunShape("MirroredZigzag", new List<Vector3Int>
                { Vector3Int.zero, Vector3Int.forward, new Vector3Int(1, 0, 1), new Vector3Int(1, 0, 2), new Vector3Int(2, 0, 2) }, definitions);
            var box = LoadRequired<SpatialShapeData>(BoxShapePath);
            var rules = new List<PopulationShapeRuleData>
            {
                RunShapeRule(single, Vector3Int.one, .2f, 1),
                RunShapeRule(line, new Vector3Int(2, 1, 1), .1f, 2),
                RunShapeRule(diagonal, new Vector3Int(2, 1, 2), .1f, 2),
                RunShapeRule(corner, new Vector3Int(2, 1, 2), .2f, 4),
                new PopulationShapeRuleData(box, new List<SpatialShapeUsageData>
                {
                    RunShapeUsage(new Vector3Int(2, 1, 2), 0), RunShapeUsage(new Vector3Int(3, 1, 1), 0)
                }, .2f, new IntRange(0, 16)),
                RunShapeRule(zigzag, new Vector3Int(3, 1, 3), .1f, 4),
                RunShapeRule(mirrored, new Vector3Int(3, 1, 3), .1f, 4)
            };
            var definition = LoadRequired<AgentDefinitionData>(PopulationAgentPath);
            var population = (PopulationAgentData)definition.Agent;
            var distribution = new GridPopulationDistributionAlgorithmData();
            SetField(distribution, "shapeAllocationType", GridPopulationShapeAllocationType.PatternWeights);
            SetField(population, "distribution", distribution);
            if (population.Releases.Count != 1) throw new InvalidOperationException("Run shape authoring requires the explicit Board release.");
            SetField(population.Releases[0], "shapes", rules);
            var activity = LoadRequired<ActivityData>(BoardActivityPath);
            int walletIndex = activity.Teams[0].Wallets.FindIndex(entry => entry.Wallet == LoadRequired<EconomyWalletData>(BoardWalletPath));
            var wallet = activity.Teams[0].Wallets[walletIndex];
            var seeds = new List<ActivityWalletSeedEntryData>(wallet.Seed);
            foreach (var rule in rules)
            {
                seeds.RemoveAll(entry => entry.Seed.Asset == rule.Shape);
                seeds.Add(new ActivityWalletSeedEntryData(new SeedEntry(rule.Shape, 1, EconomyFormType.Stack),
                    ActivitySeedMaterializationType.None, null));
            }
            SetStructField(ref wallet, "seed", seeds);
            activity.Teams[0].Wallets[walletIndex] = wallet;
            var selection = LoadRequired<AgentDefinitionData>(SelectionAgentPath);
            // The source board accepts all eight neighbours, including the diagonal line forms.
            SetField(selection, "targetSelectionCriteria", new List<EntityCriterionEntryData>
            {
                Required(CreateCapabilityHostCriterion(null, null, new List<Core.Taxonomy.TaxonomyTermData>
                    { LoadRequired<Core.Taxonomy.TaxonomyTermData>(BoardContentTagPath) })),
                Required(CreateOwnerCriterion()), Required(CreateAssetCountCriterion()),
                Required(CreateSegmentLengthCriterion(1000, 1414))
            });
            EditorUtility.SetDirty(selection);
            EditorUtility.SetDirty(activity);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(installer);
        }

        static SpatialShapeData WriteFixedRunShape(string name, List<Vector3Int> cells, List<EconomyAssetData> definitions)
        {
            var rule = WriteContentAsset<SpatialShapeRuleData>(ShapeRulesRoot + "/" + name + "Rule.asset", null);
            SetField(rule, "requiredCells", cells);
            SetField(rule, "continuationPaths", new List<SpatialShapeRuleData.ContinuationPathData>());
            SetField(rule, "forbiddenRelations", new List<SpatialShapeRuleData.ForbiddenRelationData>());
            var shape = WriteContentAsset(ShapesRoot + "/" + name + ".asset", LoadRequired<SpatialShapeData>(SingleShapePath),
                "chainrush.spatial.shape." + name.ToLowerInvariant(), definitions);
            SetField(shape, "shapeType", SpatialShapeType.Custom);
            SetField(shape, "customRule", rule);
            EditorUtility.SetDirty(rule);
            EditorUtility.SetDirty(shape);
            return shape;
        }

        static PopulationShapeRuleData RunShapeRule(SpatialShapeData shape, Vector3Int size, float weight, int rotations)
        {
            var usages = new List<SpatialShapeUsageData>();
            for (int rotation = 0; rotation < rotations; rotation++) usages.Add(RunShapeUsage(size, rotation * 90));
            return new PopulationShapeRuleData(shape, usages, weight, new IntRange(0, 16));
        }

        static SpatialShapeUsageData RunShapeUsage(Vector3Int size, int angle) => new SpatialShapeUsageData(
            SpatialShapeFillType.Inside, Vector3Int.zero, size, new Vector3Int(0, angle, 0),
            new Vector3Int(1000, 1, 1000), Vector3Int.zero);
    }
}
