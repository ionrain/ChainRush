using System;
using System.Collections.Generic;
using System.Text;
using Core;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Determinism;
using Core.Economy;
using Core.Economy.Modules.SpatialEconomyModule;
using Core.Orchestration;
using Core.World;
using UnityEngine;

namespace ChainRush.Board
{
    [CreateAssetMenu(
        fileName = "ShapePopulationPlannerData",
        menuName = "ChainRush/Activities/Population/Shape Population Planner")]
    public sealed class ShapePopulationPlannerData : PopulationPlannerData
    {

        [Serializable]
        public sealed class ContentRule
        {
            [SerializeField] CapabilityHostBaseData asset;
            [SerializeField] long weight;
            [SerializeField] long minimumPatternCount;
            [SerializeField, Range(0f, 1f)] float guaranteedCellShare;

            public CapabilityHostBaseData Asset => asset;
            public long Weight => weight;
            public long MinimumPatternCount => minimumPatternCount;
            public float GuaranteedCellShare => guaranteedCellShare;
        }

        [SerializeField] List<ContentRule> contentRules = new List<ContentRule>(0);

        public List<ContentRule> ContentRules => contentRules ?? new List<ContentRule>(0);

        public override bool TryBuild(
            in PopulationPlanContext context,
            IReadOnlyList<PopulationDistributionGroup> distribution,
            out PopulationPlan plan,
            out string failure)
        {
            plan = null;
            if (!TryBuildCells(context, out var cells, out failure)
                || !TryResolveContentRules(out var rules, out failure)) return false;
            if (distribution == null)
            { failure = "Population content requires a completed distribution."; return false; }
            var byReference = new Dictionary<SpaceRegionCellReference, ResolvedCell>(cells.Count);
            for (int i = 0; i < cells.Count; i++) byReference.Add(cells[i].Snapshot.Cell, cells[i]);
            var patterns = new List<PatternPlacement>(distribution.Count);
            var assignedCells = new HashSet<SpaceRegionCellReference>();
            for (int i = 0; i < distribution.Count; i++)
            {
                PopulationDistributionGroup group = distribution[i];
                var selected = new List<ResolvedCell>(group.Cells.Count);
                var coordinates = new List<Vector2Int>(group.Cells.Count);
                for (int j = 0; j < group.Cells.Count; j++)
                {
                    if (!byReference.TryGetValue(group.Cells[j], out var cell)
                        || !cell.Snapshot.AvailableForPlacement || cell.Snapshot.IsOccupied
                        || !assignedCells.Add(group.Cells[j]))
                    { failure = "Distribution contains an unavailable or duplicate content position."; return false; }
                    selected.Add(cell);
                }
                selected.Sort(CompareCells);
                for (int j = 0; j < selected.Count; j++) coordinates.Add(selected[j].GridCoordinate);
                patterns.Add(new PatternPlacement(group, selected, BuildCoordinateKey(coordinates)));
            }
            if (!TryAssignContent(cells, patterns, rules, CreateRandom(context), out var groups, out failure))
                return false;
            var result = new List<PopulationPlanGroup>(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                PlannedGroup group = groups[i];
                result.Add(new PopulationPlanGroup(group.Pattern.Distribution.Shape, group.Asset,
                    EconomyFormType.Token, group.Pattern.Distribution.Cells));
            }
            plan = new PopulationPlan(result);
            return true;
        }

        static bool TryBuildCells(
            in PopulationPlanContext context,
            out List<ResolvedCell> cells,
            out string failure)
        {
            cells = new List<ResolvedCell>(0);
            failure = null;

            if (!context.ActivityId.IsValid)
            {
                failure = "Shape population planner requires a valid activity.";
                return false;
            }
            if (!context.DomainId.IsValid)
            {
                failure = "Shape population planner requires a valid runtime domain.";
                return false;
            }
            if (!context.ParticipantEntityId.IsValid || !context.PopulationEntityId.IsValid)
            {
                failure = "Shape population planner requires valid participant and population entities.";
                return false;
            }
            if (context.Cells == null)
            {
                failure = "Shape population planner requires a population cell snapshot.";
                return false;
            }
            if (context.Regions == null || context.Regions.Count != 1
                || context.Regions[0].GeometryType != SpaceRegionGeometryType.Cells
                || context.Regions[0].ActivityId != context.ActivityId)
            {
                failure = "Shape population planner requires one cellular region in its Activity.";
                return false;
            }
            if (!TopologyService.TryGetTopologyDescriptor(
                    context.ActivityId,
                    out TopologyDescriptor descriptor)
                || descriptor.DimensionType != TopologyDimensionType.TwoDimensional
                || descriptor.TopologyType != TopologyType.Grid)
            {
                failure = "Shape population planner requires a two-dimensional grid topology.";
                return false;
            }

            float coordinateStep = descriptor.TopologyCoordinateSize
                / (float)descriptor.TopologyUnitsPerUnityUnit;
            if (coordinateStep <= 0f || float.IsNaN(coordinateStep) || float.IsInfinity(coordinateStep))
            {
                failure = "Shape population planner topology coordinate step is invalid.";
                return false;
            }

            var cellRefs = new HashSet<SpaceRegionCellReference>();
            cells = new List<ResolvedCell>(context.Cells.Count);
            var cellsByCoordinate = new Dictionary<Vector2Int, ResolvedCell>(context.Cells.Count);
            for (int i = 0; i < context.Cells.Count; i++)
            {
                PopulationCellSnapshot snapshot = context.Cells[i];
                if (!snapshot.Cell.IsValid || snapshot.Cell.Handle != context.Regions[0].Handle
                    || snapshot.Cell.Revision != context.Regions[0].Revision)
                {
                    failure = string.Concat(
                        "Shape population planner cell ",
                        i.ToString(),
                        " has an invalid marker reference.");
                    return false;
                }
                if (!cellRefs.Add(snapshot.Cell))
                {
                    failure = string.Concat(
                        "Shape population planner contains duplicate marker '",
                        snapshot.Cell.ToString(),
                        "'.");
                    return false;
                }
                if (!TryResolveGridCoordinate(
                        snapshot.Coordinates,
                        descriptor.UpAxisType,
                        coordinateStep,
                        out Vector2Int gridCoordinate))
                {
                    failure = string.Concat(
                        "Shape population planner cell '",
                        snapshot.Cell.ToString(),
                        " is not aligned to the activity grid.");
                    return false;
                }
                if (cellsByCoordinate.ContainsKey(gridCoordinate))
                {
                    failure = string.Concat(
                        "Shape population planner contains duplicate grid coordinate '",
                        gridCoordinate.ToString(),
                        "'.");
                    return false;
                }

                var cell = new ResolvedCell(snapshot, gridCoordinate);
                cells.Add(cell);
                cellsByCoordinate.Add(gridCoordinate, cell);
            }

            cells.Sort(CompareCells);
            return true;
        }



        bool TryResolveContentRules(
            out List<ResolvedContentRule> resolved,
            out string failure)
        {
            resolved = new List<ResolvedContentRule>(0);
            failure = null;
            List<ContentRule> authored = ContentRules;
            if (authored.Count == 0)
            {
                failure = "Shape population planner requires at least one content rule.";
                return false;
            }

            resolved = new List<ResolvedContentRule>(authored.Count);
            var assetKeys = new HashSet<string>(StringComparer.Ordinal);
            long totalWeight = 0L;
            float totalGuaranteedShare = 0f;
            for (int i = 0; i < authored.Count; i++)
            {
                ContentRule rule = authored[i];
                if (rule == null || rule.Asset == null || string.IsNullOrWhiteSpace(rule.Asset.Id))
                {
                    failure = string.Concat(
                        "Shape population planner content rule ",
                        i.ToString(),
                        " requires a capability-host asset with semantic identity.");
                    return false;
                }

                string assetKey = rule.Asset.BuildIdentityKey();
                if (string.IsNullOrWhiteSpace(assetKey) || !assetKeys.Add(assetKey))
                {
                    failure = string.Concat(
                        "Shape population planner content rule ",
                        i.ToString(),
                        " contains a duplicate asset.");
                    return false;
                }
                if (!TryResolveNonNegative(rule.Weight, "content weight", i, out int weight, out failure)
                    || !TryResolveNonNegative(
                        rule.MinimumPatternCount,
                        "content minimum pattern count",
                        i,
                        out int minimumPatternCount,
                        out failure))
                {
                    return false;
                }
                if (float.IsNaN(rule.GuaranteedCellShare)
                    || float.IsInfinity(rule.GuaranteedCellShare)
                    || rule.GuaranteedCellShare < 0f
                    || rule.GuaranteedCellShare > 1f)
                {
                    failure = string.Concat(
                        "Shape population planner content rule ",
                        i.ToString(),
                        " guaranteed share must be between zero and one.");
                    return false;
                }

                totalWeight = checked(totalWeight + weight);
                totalGuaranteedShare += rule.GuaranteedCellShare;
                resolved.Add(new ResolvedContentRule(
                    rule.Asset,
                    weight,
                    minimumPatternCount,
                    rule.GuaranteedCellShare,
                    i));
            }

            if (totalWeight <= 0L || totalWeight > int.MaxValue)
            {
                failure = "Shape population planner content weights must have a positive Int32 total.";
                return false;
            }
            if (totalGuaranteedShare > 1f + 0.0001f)
            {
                failure = "Shape population planner guaranteed content shares must not exceed one.";
                return false;
            }

            return true;
        }









        static bool TryAssignContent(
            List<ResolvedCell> cells,
            List<PatternPlacement> patterns,
            List<ResolvedContentRule> contentRules,
            Pcg32Random random,
            out List<PlannedGroup> groups,
            out string failure)
        {
            groups = new List<PlannedGroup>(0);
            failure = null;
            var assigned = new Dictionary<PatternPlacement, ResolvedContentRule>();

            for (int ruleIndex = 0; ruleIndex < contentRules.Count; ruleIndex++)
            {
                ResolvedContentRule rule = contentRules[ruleIndex];
                for (int count = 0; count < rule.MinimumPatternCount; count++)
                {
                    PatternPlacement pattern = FindFirstUnassignedPattern(patterns, assigned);
                    if (pattern == null)
                    {
                        failure = string.Concat(
                            "Shape population planner cannot satisfy content rule ",
                            rule.AuthoredIndex.ToString(),
                            " minimum pattern count.");
                        return false;
                    }

                    assigned.Add(pattern, rule);
                }
            }

            for (int ruleIndex = 0; ruleIndex < contentRules.Count; ruleIndex++)
            {
                ResolvedContentRule rule = contentRules[ruleIndex];
                int targetCellCount = Mathf.CeilToInt(
                    CountContentShareCells(cells) * rule.GuaranteedCellShare);
                int assignedCellCount = CountExistingCells(cells, rule.Asset)
                    + CountAssignedCells(assigned, rule);
                while (assignedCellCount < targetCellCount)
                {
                    PatternPlacement pattern = FindClosestUnassignedPattern(
                        patterns,
                        assigned,
                        targetCellCount - assignedCellCount);
                    if (pattern == null)
                    {
                        failure = string.Concat(
                            "Shape population planner cannot satisfy content rule ",
                            rule.AuthoredIndex.ToString(),
                            " guaranteed cell share.");
                        return false;
                    }

                    assigned.Add(pattern, rule);
                    assignedCellCount += pattern.Cells.Count;
                }
            }

            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                PatternPlacement pattern = patterns[patternIndex];
                if (assigned.ContainsKey(pattern))
                    continue;
                if (!TryChooseWeightedContentRule(contentRules, random, out ResolvedContentRule rule, out failure))
                    return false;

                assigned.Add(pattern, rule);
            }

            groups = new List<PlannedGroup>(patterns.Count);
            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                PatternPlacement pattern = patterns[patternIndex];
                if (!assigned.TryGetValue(pattern, out ResolvedContentRule rule))
                {
                    failure = "Shape population planner left a pattern without content.";
                    return false;
                }

                groups.Add(new PlannedGroup(pattern, rule.Asset));
            }

            return true;
        }

        static int CountContentShareCells(List<ResolvedCell> cells)
        {
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                PopulationCellSnapshot snapshot = cells[i].Snapshot;
                if (snapshot.IsOccupied || snapshot.AvailableForPlacement)
                    count++;
            }

            return count;
        }


        static bool TryChooseWeightedContentRule(
            List<ResolvedContentRule> rules,
            Pcg32Random random,
            out ResolvedContentRule selected,
            out string failure)
        {
            selected = null;
            failure = null;
            long totalWeight = 0L;
            for (int i = 0; i < rules.Count; i++)
                totalWeight = checked(totalWeight + rules[i].Weight);
            if (totalWeight <= 0L || totalWeight > int.MaxValue)
            {
                failure = "Shape population planner active content weights must have a positive Int32 total.";
                return false;
            }

            int selection = random.NextInt(0, (int)totalWeight);
            int accumulated = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                accumulated += rules[i].Weight;
                if (selection < accumulated)
                {
                    selected = rules[i];
                    return true;
                }
            }

            failure = "Shape population planner could not resolve weighted content.";
            return false;
        }

        static PatternPlacement FindFirstUnassignedPattern(
            List<PatternPlacement> patterns,
            Dictionary<PatternPlacement, ResolvedContentRule> assigned)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                if (!assigned.ContainsKey(patterns[i]))
                    return patterns[i];
            }

            return null;
        }

        static PatternPlacement FindClosestUnassignedPattern(
            List<PatternPlacement> patterns,
            Dictionary<PatternPlacement, ResolvedContentRule> assigned,
            int desiredCellCount)
        {
            PatternPlacement selected = null;
            int selectedDistance = int.MaxValue;
            for (int i = 0; i < patterns.Count; i++)
            {
                PatternPlacement candidate = patterns[i];
                if (assigned.ContainsKey(candidate))
                    continue;

                int distance = Math.Abs(candidate.Cells.Count - desiredCellCount);
                if (selected == null
                    || distance < selectedDistance
                    || distance == selectedDistance && ComparePatterns(candidate, selected) < 0)
                {
                    selected = candidate;
                    selectedDistance = distance;
                }
            }

            return selected;
        }

        static int CountExistingCells(List<ResolvedCell> cells, CapabilityHostBaseData asset)
        {
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                PopulationCellSnapshot snapshot = cells[i].Snapshot;
                if (snapshot.IsOccupied && snapshot.OccupantAsset.Matches(asset))
                    count++;
            }

            return count;
        }

        static int CountAssignedCells(
            Dictionary<PatternPlacement, ResolvedContentRule> assigned,
            ResolvedContentRule rule)
        {
            int count = 0;
            foreach (KeyValuePair<PatternPlacement, ResolvedContentRule> entry in assigned)
            {
                if (entry.Value.Asset.Matches(rule.Asset))
                    count += entry.Key.Cells.Count;
            }

            return count;
        }


        static bool TryResolveNonNegative(
            long authoredValue,
            string field,
            int ruleIndex,
            out int value,
            out string failure)
        {
            value = 0;
            failure = null;
            long evaluated = authoredValue;
            if (evaluated < 0L || evaluated > int.MaxValue)
            {
                failure = string.Concat(
                    "Shape population planner ",
                    field,
                    " for rule ",
                    ruleIndex.ToString(),
                    " must fit a non-negative Int32 value.");
                return false;
            }

            value = (int)evaluated;
            return true;
        }

        static bool TryResolveGridCoordinate(
            Vector3 coordinates,
            TopologyUpAxisType upAxisType,
            float coordinateStep,
            out Vector2Int gridCoordinate)
        {
            gridCoordinate = default;
            if (!TryResolveCoordinateComponent(coordinates.x, coordinateStep, out int a)
                || !TryResolveCoordinateComponent(coordinates.y, coordinateStep, out int b)
                || !TryResolveCoordinateComponent(coordinates.z, coordinateStep, out int c))
            {
                return false;
            }

            switch (upAxisType)
            {
                case TopologyUpAxisType.X:
                    gridCoordinate = new Vector2Int(b, c);
                    break;
                case TopologyUpAxisType.Z:
                    gridCoordinate = new Vector2Int(a, b);
                    break;
                default:
                    gridCoordinate = new Vector2Int(a, c);
                    break;
            }

            return true;
        }

        static bool TryResolveCoordinateComponent(float value, float coordinateStep, out int component)
        {
            component = 0;
            float scaled = value / coordinateStep;
            if (float.IsNaN(scaled) || float.IsInfinity(scaled))
                return false;

            component = Mathf.RoundToInt(scaled);
            return Mathf.Abs(scaled - component) <= 0.001f;
        }

        static Pcg32Random CreateRandom(in PopulationPlanContext context)
        {
            ulong state = context.Seed;
            ulong sequence = Mix64(state ^ 0x9E3779B97F4A7C15UL);
            return new Pcg32Random(Mix64(state), sequence);
        }

        static ulong Mix64(ulong value)
        {
            ulong z = value + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }



        static string BuildCoordinateKey(List<Vector2Int> coordinates)
        {
            var builder = new StringBuilder(coordinates.Count * 12);
            for (int i = 0; i < coordinates.Count; i++)
            {
                if (i > 0)
                    builder.Append('|');
                builder.Append(coordinates[i].x);
                builder.Append(':');
                builder.Append(coordinates[i].y);
            }

            return builder.ToString();
        }

        static int CompareCoordinates(Vector2Int left, Vector2Int right)
        {
            int rowCompare = left.y.CompareTo(right.y);
            return rowCompare != 0 ? rowCompare : left.x.CompareTo(right.x);
        }

        static int CompareCells(ResolvedCell left, ResolvedCell right)
        {
            return CompareCoordinates(left.GridCoordinate, right.GridCoordinate);
        }

        static int ComparePatterns(PatternPlacement left, PatternPlacement right)
        {
            if (left == null)
                return right == null ? 0 : -1;
            if (right == null)
                return 1;
            return string.Compare(left.Key, right.Key, StringComparison.Ordinal);
        }


        sealed class ResolvedCell
        {
            public ResolvedCell(PopulationCellSnapshot snapshot, Vector2Int gridCoordinate)
            {
                Snapshot = snapshot;
                GridCoordinate = gridCoordinate;
            }

            public PopulationCellSnapshot Snapshot { get; }
            public Vector2Int GridCoordinate { get; }
        }


        sealed class ResolvedContentRule
        {
            public ResolvedContentRule(
                CapabilityHostBaseData asset,
                int weight,
                int minimumPatternCount,
                float guaranteedCellShare,
                int authoredIndex)
            {
                Asset = asset;
                Weight = weight;
                MinimumPatternCount = minimumPatternCount;
                GuaranteedCellShare = guaranteedCellShare;
                AuthoredIndex = authoredIndex;
            }

            public CapabilityHostBaseData Asset { get; }
            public int Weight { get; }
            public int MinimumPatternCount { get; }
            public float GuaranteedCellShare { get; }
            public int AuthoredIndex { get; }
        }

        sealed class PatternPlacement
        {
            public PatternPlacement(
                PopulationDistributionGroup distribution,
                List<ResolvedCell> cells,
                string key)
            {
                Distribution = distribution;
                Cells = cells;
                Key = key;
            }

            public PopulationDistributionGroup Distribution { get; }
            public List<ResolvedCell> Cells { get; }
            public string Key { get; }
        }


        readonly struct PlannedGroup
        {
            public PlannedGroup(PatternPlacement pattern, CapabilityHostBaseData asset)
            {
                Pattern = pattern;
                Asset = asset;
            }

            public PatternPlacement Pattern { get; }
            public CapabilityHostBaseData Asset { get; }
        }
    }
}
