using System;
using System.Collections.Generic;
using System.Text;
using Core;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Determinism;
using Core.Economy;
using Core.World;
using UnityEngine;

namespace ChainRush.Board
{
    [CreateAssetMenu(
        fileName = "ProgressivePlannerData",
        menuName = "ChainRush/Activities/Population/Progressive Planner")]
    public sealed class ProgressivePlannerData : PopulationPlannerData
    {
        public enum PatternType
        {
            Single = 0,
            Line = 1,
            Corner = 2,
            Box = 3,
            Zigzag = 4,
        }

        [Serializable]
        public sealed class PatternRule
        {
            [SerializeField] PatternType patternType;
            [SerializeReference] LongProgressionData size;
            [SerializeReference] LongProgressionData weight;
            [SerializeReference] LongProgressionData minimumCount;

            public PatternType Pattern => patternType;
            public LongProgressionData Size => size;
            public LongProgressionData Weight => weight;
            public LongProgressionData MinimumCount => minimumCount;
        }

        [Serializable]
        public sealed class ContentRule
        {
            [SerializeField] CapabilityHostBaseData asset;
            [SerializeReference] LongProgressionData weight;
            [SerializeReference] LongProgressionData minimumPatternCount;
            [SerializeField, Range(0f, 1f)] float guaranteedCellShare;

            public CapabilityHostBaseData Asset => asset;
            public LongProgressionData Weight => weight;
            public LongProgressionData MinimumPatternCount => minimumPatternCount;
            public float GuaranteedCellShare => guaranteedCellShare;
        }

        static readonly List<Vector2Int> LineDirections = new List<Vector2Int>
        {
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
        };

        static readonly List<Vector2Int> CardinalDirections = new List<Vector2Int>
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        [SerializeField] List<PatternRule> patternRules = new List<PatternRule>(0);
        [SerializeField] List<ContentRule> contentRules = new List<ContentRule>(0);

        public List<PatternRule> PatternRules => patternRules ?? new List<PatternRule>(0);
        public List<ContentRule> ContentRules => contentRules ?? new List<ContentRule>(0);

        public override bool TryBuild(
            in PopulationPlanContext context,
            out PopulationPlan plan,
            out string failure)
        {
            plan = null;
            failure = null;

            if (!TryBuildCells(
                    context,
                    out List<ResolvedCell> cells,
                    out Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
                    out HashSet<Vector2Int> remaining,
                    out failure)
                || !TryResolvePatternRules(
                    context.Generation,
                    out List<ResolvedPatternRule> resolvedPatternRules,
                    out failure)
                || !TryResolveContentRules(
                    context.Generation,
                    out List<ResolvedContentRule> resolvedContentRules,
                    out failure))
            {
                return false;
            }

            if (remaining.Count == 0)
            {
                plan = new PopulationPlan(new List<PopulationPlanEntry>(0));
                return true;
            }

            if (!DeterminismService.IsInitialized)
            {
                failure = "Progressive planner requires an initialized deterministic session.";
                return false;
            }

            Pcg32Random random = CreateRandom(context);
            var patterns = new List<PatternPlacement>(remaining.Count);
            if (!TryBuildPatterns(
                    resolvedPatternRules,
                    cellsByCoordinate,
                    remaining,
                    random,
                    patterns,
                    out failure)
                || !TryAssignContent(
                    context.Generation,
                    cells,
                    patterns,
                    resolvedContentRules,
                    random,
                    out List<PlannedEntry> entries,
                    out failure))
            {
                return false;
            }

            entries.Sort(ComparePlannedEntries);
            var result = new List<PopulationPlanEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                PlannedEntry entry = entries[i];
                result.Add(new PopulationPlanEntry(
                    entry.Cell.Snapshot.Marker,
                    entry.Asset,
                    EconomyFormType.Token));
            }

            plan = new PopulationPlan(result);
            return true;
        }

        static bool TryBuildCells(
            in PopulationPlanContext context,
            out List<ResolvedCell> cells,
            out Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
            out HashSet<Vector2Int> remaining,
            out string failure)
        {
            cells = new List<ResolvedCell>(0);
            cellsByCoordinate = new Dictionary<Vector2Int, ResolvedCell>();
            remaining = new HashSet<Vector2Int>();
            failure = null;

            if (!context.ActivityId.IsValid)
            {
                failure = "Progressive planner requires a valid activity.";
                return false;
            }
            if (!context.DomainId.IsValid)
            {
                failure = "Progressive planner requires a valid runtime domain.";
                return false;
            }
            if (!context.ParticipantEntityId.IsValid || !context.PopulationEntityId.IsValid)
            {
                failure = "Progressive planner requires valid participant and population entities.";
                return false;
            }
            if (context.Generation <= 0L)
            {
                failure = "Progressive planner generation must be greater than zero.";
                return false;
            }
            if (context.Cells == null || context.Cells.Count == 0)
            {
                failure = "Progressive planner requires at least one population cell.";
                return false;
            }
            if (!TopologyService.TryGetTopologyDescriptor(
                    context.ActivityId,
                    out TopologyDescriptor descriptor)
                || descriptor.DimensionType != TopologyDimensionType.TwoDimensional
                || descriptor.TopologyType != TopologyType.Grid)
            {
                failure = "Progressive planner requires a two-dimensional grid topology.";
                return false;
            }

            float coordinateStep = descriptor.TopologyCoordinateSize
                / (float)descriptor.TopologyUnitsPerUnityUnit;
            if (coordinateStep <= 0f || float.IsNaN(coordinateStep) || float.IsInfinity(coordinateStep))
            {
                failure = "Progressive planner topology coordinate step is invalid.";
                return false;
            }

            var markerRefs = new HashSet<SpatialMarkerRef>();
            cells = new List<ResolvedCell>(context.Cells.Count);
            cellsByCoordinate = new Dictionary<Vector2Int, ResolvedCell>(context.Cells.Count);
            remaining = new HashSet<Vector2Int>();
            for (int i = 0; i < context.Cells.Count; i++)
            {
                PopulationCellSnapshot snapshot = context.Cells[i];
                if (!snapshot.Marker.IsValid || snapshot.Marker.ActivityId != context.ActivityId)
                {
                    failure = string.Concat(
                        "Progressive planner cell ",
                        i.ToString(),
                        " has an invalid marker reference.");
                    return false;
                }
                if (!markerRefs.Add(snapshot.Marker))
                {
                    failure = string.Concat(
                        "Progressive planner contains duplicate marker '",
                        snapshot.Marker.ToString(),
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
                        "Progressive planner cell '",
                        snapshot.Marker.ToString(),
                        " is not aligned to the activity grid.");
                    return false;
                }
                if (cellsByCoordinate.ContainsKey(gridCoordinate))
                {
                    failure = string.Concat(
                        "Progressive planner contains duplicate grid coordinate '",
                        gridCoordinate.ToString(),
                        "'.");
                    return false;
                }

                var cell = new ResolvedCell(snapshot, gridCoordinate);
                cells.Add(cell);
                cellsByCoordinate.Add(gridCoordinate, cell);
                if (!snapshot.IsOccupied)
                    remaining.Add(gridCoordinate);
            }

            cells.Sort(CompareCells);
            return true;
        }

        bool TryResolvePatternRules(
            long generation,
            out List<ResolvedPatternRule> resolved,
            out string failure)
        {
            resolved = new List<ResolvedPatternRule>(0);
            failure = null;
            List<PatternRule> authored = PatternRules;
            if (authored.Count == 0)
            {
                failure = "Progressive planner requires at least one pattern rule.";
                return false;
            }

            resolved = new List<ResolvedPatternRule>(authored.Count);
            bool hasActiveSingle = false;
            for (int i = 0; i < authored.Count; i++)
            {
                PatternRule rule = authored[i];
                if (rule == null)
                {
                    failure = string.Concat("Progressive planner pattern rule ", i.ToString(), " is null.");
                    return false;
                }
                if (!TryEvaluatePositive(rule.Size, generation, "pattern size", i, out int size, out failure)
                    || !TryEvaluateNonNegative(rule.Weight, generation, "pattern weight", i, out int weight, out failure)
                    || !TryEvaluateNonNegative(
                        rule.MinimumCount,
                        generation,
                        "pattern minimum count",
                        i,
                        out int minimumCount,
                        out failure))
                {
                    return false;
                }
                if (!ValidatePatternSize(rule.Pattern, size, i, out failure))
                    return false;

                resolved.Add(new ResolvedPatternRule(
                    rule.Pattern,
                    size,
                    weight,
                    minimumCount,
                    i));
                if (rule.Pattern == PatternType.Single && size == 1 && weight > 0)
                    hasActiveSingle = true;
            }

            if (!hasActiveSingle)
            {
                failure = "Progressive planner requires an active Single pattern rule with size 1.";
                return false;
            }

            return true;
        }

        bool TryResolveContentRules(
            long generation,
            out List<ResolvedContentRule> resolved,
            out string failure)
        {
            resolved = new List<ResolvedContentRule>(0);
            failure = null;
            List<ContentRule> authored = ContentRules;
            if (authored.Count == 0)
            {
                failure = "Progressive planner requires at least one content rule.";
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
                        "Progressive planner content rule ",
                        i.ToString(),
                        " requires a capability-host asset with semantic identity.");
                    return false;
                }

                string assetKey = rule.Asset.BuildIdentityKey();
                if (string.IsNullOrWhiteSpace(assetKey) || !assetKeys.Add(assetKey))
                {
                    failure = string.Concat(
                        "Progressive planner content rule ",
                        i.ToString(),
                        " contains a duplicate asset.");
                    return false;
                }
                if (!TryEvaluateNonNegative(rule.Weight, generation, "content weight", i, out int weight, out failure)
                    || !TryEvaluateNonNegative(
                        rule.MinimumPatternCount,
                        generation,
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
                        "Progressive planner content rule ",
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
                failure = "Progressive planner content weights must have a positive Int32 total.";
                return false;
            }
            if (totalGuaranteedShare > 1f + 0.0001f)
            {
                failure = "Progressive planner guaranteed content shares must not exceed one.";
                return false;
            }

            return true;
        }

        static bool TryBuildPatterns(
            List<ResolvedPatternRule> rules,
            Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
            HashSet<Vector2Int> remaining,
            Pcg32Random random,
            List<PatternPlacement> patterns,
            out string failure)
        {
            failure = null;
            var mandatory = new List<ResolvedPatternRule>();
            for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                ResolvedPatternRule rule = rules[ruleIndex];
                for (int count = 0; count < rule.MinimumCount; count++)
                    mandatory.Add(rule);
            }

            if (!TryPlaceMandatoryPatterns(
                    0,
                    mandatory,
                    cellsByCoordinate,
                    remaining,
                    random,
                    patterns))
            {
                failure = "Progressive planner could not place all mandatory patterns.";
                return false;
            }

            var weightedRules = new List<ResolvedPatternRule>(rules.Count);
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i].Weight > 0)
                    weightedRules.Add(rules[i]);
            }

            while (remaining.Count > 0)
            {
                if (!TryChooseWeightedPatternRule(weightedRules, random, out int selectedIndex, out failure))
                    return false;

                bool placed = false;
                for (int offset = 0; offset < weightedRules.Count; offset++)
                {
                    int ruleIndex = (selectedIndex + offset) % weightedRules.Count;
                    if (!TryPlacePattern(
                            weightedRules[ruleIndex],
                            cellsByCoordinate,
                            remaining,
                            random,
                            out PatternPlacement placement))
                    {
                        continue;
                    }

                    patterns.Add(placement);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    failure = string.Concat(
                        "Progressive planner could not cover ",
                        remaining.Count.ToString(),
                        " remaining cells with active pattern rules.");
                    return false;
                }
            }

            patterns.Sort(ComparePatterns);
            return true;
        }

        static bool TryPlaceMandatoryPatterns(
            int mandatoryIndex,
            List<ResolvedPatternRule> mandatory,
            Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
            HashSet<Vector2Int> remaining,
            Pcg32Random random,
            List<PatternPlacement> patterns)
        {
            if (mandatoryIndex >= mandatory.Count)
                return true;

            List<PatternPlacement> candidates = BuildPatternCandidates(
                mandatory[mandatoryIndex],
                cellsByCoordinate,
                remaining);
            if (candidates.Count == 0)
                return false;

            int startIndex = random.NextInt(0, candidates.Count);
            for (int offset = 0; offset < candidates.Count; offset++)
            {
                PatternPlacement candidate = candidates[(startIndex + offset) % candidates.Count];
                RemovePattern(candidate, remaining);
                patterns.Add(candidate);
                if (TryPlaceMandatoryPatterns(
                        mandatoryIndex + 1,
                        mandatory,
                        cellsByCoordinate,
                        remaining,
                        random,
                        patterns))
                {
                    return true;
                }

                patterns.RemoveAt(patterns.Count - 1);
                RestorePattern(candidate, remaining);
            }

            return false;
        }

        static bool TryPlacePattern(
            ResolvedPatternRule rule,
            Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
            HashSet<Vector2Int> remaining,
            Pcg32Random random,
            out PatternPlacement placement)
        {
            placement = null;
            List<PatternPlacement> candidates = BuildPatternCandidates(rule, cellsByCoordinate, remaining);
            if (candidates.Count == 0)
                return false;

            placement = candidates[random.NextInt(0, candidates.Count)];
            RemovePattern(placement, remaining);
            return true;
        }

        static List<PatternPlacement> BuildPatternCandidates(
            ResolvedPatternRule rule,
            Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
            HashSet<Vector2Int> remaining)
        {
            var candidates = new List<PatternPlacement>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var starts = new List<Vector2Int>(remaining);
            starts.Sort(CompareCoordinates);

            for (int startIndex = 0; startIndex < starts.Count; startIndex++)
            {
                Vector2Int start = starts[startIndex];
                switch (rule.Pattern)
                {
                    case PatternType.Single:
                        TryAddCandidate(
                            new List<Vector2Int> { start },
                            rule,
                            cellsByCoordinate,
                            remaining,
                            keys,
                            candidates);
                        break;

                    case PatternType.Line:
                        AddLineCandidates(start, rule, cellsByCoordinate, remaining, keys, candidates);
                        break;

                    case PatternType.Corner:
                        AddCornerCandidates(start, rule, cellsByCoordinate, remaining, keys, candidates);
                        break;

                    case PatternType.Box:
                        AddBoxCandidates(start, rule, cellsByCoordinate, remaining, keys, candidates);
                        break;

                    case PatternType.Zigzag:
                        AddZigzagCandidates(start, rule, cellsByCoordinate, remaining, keys, candidates);
                        break;
                }
            }

            candidates.Sort(ComparePatterns);
            return candidates;
        }

        static void AddLineCandidates(
            Vector2Int start,
            ResolvedPatternRule rule,
            Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
            HashSet<Vector2Int> remaining,
            HashSet<string> keys,
            List<PatternPlacement> candidates)
        {
            for (int directionIndex = 0; directionIndex < LineDirections.Count; directionIndex++)
            {
                Vector2Int direction = LineDirections[directionIndex];
                var coordinates = new List<Vector2Int>(rule.Size);
                for (int step = 0; step < rule.Size; step++)
                    coordinates.Add(start + (direction * step));

                TryAddCandidate(coordinates, rule, cellsByCoordinate, remaining, keys, candidates);
            }
        }

        static void AddCornerCandidates(
            Vector2Int start,
            ResolvedPatternRule rule,
            Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
            HashSet<Vector2Int> remaining,
            HashSet<string> keys,
            List<PatternPlacement> candidates)
        {
            for (int firstDirectionIndex = 0; firstDirectionIndex < CardinalDirections.Count; firstDirectionIndex++)
            {
                Vector2Int firstDirection = CardinalDirections[firstDirectionIndex];
                for (int secondDirectionIndex = 0; secondDirectionIndex < CardinalDirections.Count; secondDirectionIndex++)
                {
                    Vector2Int secondDirection = CardinalDirections[secondDirectionIndex];
                    if ((firstDirection.x * secondDirection.x) + (firstDirection.y * secondDirection.y) != 0)
                        continue;

                    for (int firstLength = 1; firstLength <= rule.Size - 2; firstLength++)
                    {
                        int secondLength = rule.Size - 1 - firstLength;
                        var coordinates = new List<Vector2Int>(rule.Size) { start };
                        for (int step = 1; step <= firstLength; step++)
                            coordinates.Add(start + (firstDirection * step));
                        for (int step = 1; step <= secondLength; step++)
                            coordinates.Add(start + (secondDirection * step));

                        TryAddCandidate(coordinates, rule, cellsByCoordinate, remaining, keys, candidates);
                    }
                }
            }
        }

        static void AddBoxCandidates(
            Vector2Int start,
            ResolvedPatternRule rule,
            Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
            HashSet<Vector2Int> remaining,
            HashSet<string> keys,
            List<PatternPlacement> candidates)
        {
            for (int width = 2; width <= rule.Size / 2; width++)
            {
                if (rule.Size % width != 0)
                    continue;

                int height = rule.Size / width;
                if (height < 2)
                    continue;

                var coordinates = new List<Vector2Int>(rule.Size);
                for (int row = 0; row < height; row++)
                {
                    for (int column = 0; column < width; column++)
                        coordinates.Add(start + new Vector2Int(column, row));
                }

                TryAddCandidate(coordinates, rule, cellsByCoordinate, remaining, keys, candidates);
            }
        }

        static void AddZigzagCandidates(
            Vector2Int start,
            ResolvedPatternRule rule,
            Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
            HashSet<Vector2Int> remaining,
            HashSet<string> keys,
            List<PatternPlacement> candidates)
        {
            for (int firstDirectionIndex = 0; firstDirectionIndex < CardinalDirections.Count; firstDirectionIndex++)
            {
                Vector2Int firstDirection = CardinalDirections[firstDirectionIndex];
                for (int secondDirectionIndex = 0; secondDirectionIndex < CardinalDirections.Count; secondDirectionIndex++)
                {
                    Vector2Int secondDirection = CardinalDirections[secondDirectionIndex];
                    if ((firstDirection.x * secondDirection.x) + (firstDirection.y * secondDirection.y) != 0)
                        continue;

                    var coordinates = new List<Vector2Int>(rule.Size) { start };
                    Vector2Int current = start;
                    for (int step = 1; step < rule.Size; step++)
                    {
                        current += (step & 1) == 1 ? firstDirection : secondDirection;
                        coordinates.Add(current);
                    }

                    TryAddCandidate(coordinates, rule, cellsByCoordinate, remaining, keys, candidates);
                }
            }
        }

        static void TryAddCandidate(
            List<Vector2Int> coordinates,
            ResolvedPatternRule rule,
            Dictionary<Vector2Int, ResolvedCell> cellsByCoordinate,
            HashSet<Vector2Int> remaining,
            HashSet<string> keys,
            List<PatternPlacement> candidates)
        {
            if (coordinates == null || coordinates.Count != rule.Size)
                return;

            var unique = new HashSet<Vector2Int>();
            for (int i = 0; i < coordinates.Count; i++)
            {
                Vector2Int coordinate = coordinates[i];
                if (!unique.Add(coordinate)
                    || !remaining.Contains(coordinate)
                    || !cellsByCoordinate.ContainsKey(coordinate))
                {
                    return;
                }
            }

            coordinates.Sort(CompareCoordinates);
            string key = BuildCoordinateKey(coordinates);
            if (!keys.Add(key))
                return;

            var cells = new List<ResolvedCell>(coordinates.Count);
            for (int i = 0; i < coordinates.Count; i++)
                cells.Add(cellsByCoordinate[coordinates[i]]);
            candidates.Add(new PatternPlacement(rule, cells, key));
        }

        static bool TryAssignContent(
            long generation,
            List<ResolvedCell> cells,
            List<PatternPlacement> patterns,
            List<ResolvedContentRule> contentRules,
            Pcg32Random random,
            out List<PlannedEntry> entries,
            out string failure)
        {
            _ = generation;
            entries = new List<PlannedEntry>(0);
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
                            "Progressive planner cannot satisfy content rule ",
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
                int targetCellCount = Mathf.CeilToInt(cells.Count * rule.GuaranteedCellShare);
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
                            "Progressive planner cannot satisfy content rule ",
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

            entries = new List<PlannedEntry>();
            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                PatternPlacement pattern = patterns[patternIndex];
                if (!assigned.TryGetValue(pattern, out ResolvedContentRule rule))
                {
                    failure = "Progressive planner left a pattern without content.";
                    return false;
                }

                for (int cellIndex = 0; cellIndex < pattern.Cells.Count; cellIndex++)
                    entries.Add(new PlannedEntry(pattern.Cells[cellIndex], rule.Asset));
            }

            return true;
        }

        static bool TryChooseWeightedPatternRule(
            List<ResolvedPatternRule> rules,
            Pcg32Random random,
            out int selectedIndex,
            out string failure)
        {
            selectedIndex = -1;
            failure = null;
            long totalWeight = 0L;
            for (int i = 0; i < rules.Count; i++)
                totalWeight = checked(totalWeight + rules[i].Weight);
            if (totalWeight <= 0L || totalWeight > int.MaxValue)
            {
                failure = "Progressive planner active pattern weights must have a positive Int32 total.";
                return false;
            }

            int selection = random.NextInt(0, (int)totalWeight);
            int accumulated = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                accumulated += rules[i].Weight;
                if (selection < accumulated)
                {
                    selectedIndex = i;
                    return true;
                }
            }

            failure = "Progressive planner could not resolve a weighted pattern rule.";
            return false;
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
                failure = "Progressive planner active content weights must have a positive Int32 total.";
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

            failure = "Progressive planner could not resolve weighted content.";
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

        static bool TryEvaluatePositive(
            LongProgressionData progression,
            long generation,
            string field,
            int ruleIndex,
            out int value,
            out string failure)
        {
            if (!TryEvaluateNonNegative(progression, generation, field, ruleIndex, out value, out failure))
                return false;
            if (value > 0)
                return true;

            failure = string.Concat(
                "Progressive planner ",
                field,
                " for rule ",
                ruleIndex.ToString(),
                " must be greater than zero.");
            return false;
        }

        static bool TryEvaluateNonNegative(
            LongProgressionData progression,
            long generation,
            string field,
            int ruleIndex,
            out int value,
            out string failure)
        {
            value = 0;
            failure = null;
            if (progression == null)
            {
                failure = string.Concat(
                    "Progressive planner ",
                    field,
                    " for rule ",
                    ruleIndex.ToString(),
                    " is missing.");
                return false;
            }
            if (!progression.TryEvaluate(generation, out long evaluated, out string progressionFailure))
            {
                failure = string.Concat(
                    "Progressive planner ",
                    field,
                    " for rule ",
                    ruleIndex.ToString(),
                    " failed: ",
                    progressionFailure ?? "unknown progression failure");
                return false;
            }
            if (evaluated < 0L || evaluated > int.MaxValue)
            {
                failure = string.Concat(
                    "Progressive planner ",
                    field,
                    " for rule ",
                    ruleIndex.ToString(),
                    " must fit a non-negative Int32 value.");
                return false;
            }

            value = (int)evaluated;
            return true;
        }

        static bool ValidatePatternSize(PatternType pattern, int size, int ruleIndex, out string failure)
        {
            failure = null;
            switch (pattern)
            {
                case PatternType.Single:
                    if (size == 1)
                        return true;
                    failure = "Single pattern size must equal one.";
                    break;

                case PatternType.Line:
                    if (size >= 2)
                        return true;
                    failure = "Line pattern size must be at least two.";
                    break;

                case PatternType.Corner:
                    if (size >= 3)
                        return true;
                    failure = "Corner pattern size must be at least three.";
                    break;

                case PatternType.Box:
                    if (HasBoxDimensions(size))
                        return true;
                    failure = "Box pattern size must have two integer dimensions greater than one.";
                    break;

                case PatternType.Zigzag:
                    if (size >= 3)
                        return true;
                    failure = "Zigzag pattern size must be at least three.";
                    break;

                default:
                    failure = "Pattern type is unsupported.";
                    break;
            }

            failure = string.Concat(
                "Progressive planner pattern rule ",
                ruleIndex.ToString(),
                " is invalid: ",
                failure);
            return false;
        }

        static bool HasBoxDimensions(int size)
        {
            for (int width = 2; width <= size / 2; width++)
            {
                if (size % width == 0 && size / width >= 2)
                    return true;
            }

            return false;
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
            ulong state = 14695981039346656037UL;
            MixSeed(ref state, unchecked((uint)DeterminismService.SessionSeed));
            MixSeed(ref state, unchecked((uint)context.ActivityId.Value));
            MixSeed(ref state, unchecked((uint)context.DomainId.Value));
            MixSeed(ref state, unchecked((uint)context.ParticipantEntityId.Value));
            MixSeed(ref state, unchecked((uint)context.PopulationEntityId.Value));
            MixSeed(ref state, unchecked((ulong)context.Generation));
            ulong sequence = Mix64(state ^ 0x9E3779B97F4A7C15UL);
            return new Pcg32Random(Mix64(state), sequence);
        }

        static void MixSeed(ref ulong state, ulong value)
        {
            state ^= value;
            state *= 1099511628211UL;
        }

        static ulong Mix64(ulong value)
        {
            ulong z = value + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        static void RemovePattern(PatternPlacement pattern, HashSet<Vector2Int> remaining)
        {
            for (int i = 0; i < pattern.Cells.Count; i++)
                remaining.Remove(pattern.Cells[i].GridCoordinate);
        }

        static void RestorePattern(PatternPlacement pattern, HashSet<Vector2Int> remaining)
        {
            for (int i = 0; i < pattern.Cells.Count; i++)
                remaining.Add(pattern.Cells[i].GridCoordinate);
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

        static int ComparePlannedEntries(PlannedEntry left, PlannedEntry right)
        {
            return CompareCells(left.Cell, right.Cell);
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

        sealed class ResolvedPatternRule
        {
            public ResolvedPatternRule(
                PatternType pattern,
                int size,
                int weight,
                int minimumCount,
                int authoredIndex)
            {
                Pattern = pattern;
                Size = size;
                Weight = weight;
                MinimumCount = minimumCount;
                AuthoredIndex = authoredIndex;
            }

            public PatternType Pattern { get; }
            public int Size { get; }
            public int Weight { get; }
            public int MinimumCount { get; }
            public int AuthoredIndex { get; }
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
                ResolvedPatternRule rule,
                List<ResolvedCell> cells,
                string key)
            {
                Rule = rule;
                Cells = cells;
                Key = key;
            }

            public ResolvedPatternRule Rule { get; }
            public List<ResolvedCell> Cells { get; }
            public string Key { get; }
        }

        readonly struct PlannedEntry
        {
            public PlannedEntry(ResolvedCell cell, CapabilityHostBaseData asset)
            {
                Cell = cell;
                Asset = asset;
            }

            public ResolvedCell Cell { get; }
            public CapabilityHostBaseData Asset { get; }
        }
    }
}
