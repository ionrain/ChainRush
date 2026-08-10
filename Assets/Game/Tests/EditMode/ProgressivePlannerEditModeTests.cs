using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Core;
using Core.Activities;
using Core.CapabilityHosts;
using Core.Determinism;
using Core.Economy;
using Core.Entities;
using Core.Runtime;
using Core.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using EntityId = Core.Entities.EntityId;
using Object = UnityEngine.Object;

namespace ChainRush.Tests.EditMode
{
    public sealed class ProgressivePlannerEditModeTests
    {
        const string PlannerScriptPath =
            "Assets/Game/Activities/Board/Runtime/Population/ProgressivePlannerData.cs";

        static readonly ActivityId ActivityId = new ActivityId(7101);
        static readonly RuntimeDomainId DomainId = new RuntimeDomainId(7102);
        static readonly EntityId ParticipantEntityId = new EntityId(7103);
        static readonly EntityId PopulationEntityId = new EntityId(7104);
        static readonly EntityId MarkerScopeEntityId = new EntityId(7105);

        readonly List<Object> _ownedObjects = new List<Object>();
        bool _topologyOpen;

        [TearDown]
        public void TearDown()
        {
            if (_topologyOpen)
                InvokeTopology("CloseActivityContext", ActivityId);

            InvokeTopology("ResetRuntime");
            for (int i = _ownedObjects.Count - 1; i >= 0; i--)
            {
                if (_ownedObjects[i] != null)
                    Object.DestroyImmediate(_ownedObjects[i]);
            }

            _ownedObjects.Clear();
            _topologyOpen = false;
        }

        [Test]
        public void SameContext_BuildsIdenticalPlanAcrossRepeatedCalls()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData planner = CreatePlanner(
                new[]
                {
                    Pattern(PatternType.Line, 3L, 1L, 1L),
                    Pattern(PatternType.Single, 1L, 1L, 0L),
                },
                new[] { Content(water, 1L, 0L, 1f) });
            PopulationPlanContext context = CreateContext(1L, CreateGridCells(4, 4));

            Assert.IsTrue(planner.TryBuild(context, out PopulationPlan first, out string firstFailure), firstFailure);
            Assert.IsTrue(planner.TryBuild(context, out PopulationPlan second, out string secondFailure), secondFailure);

            Assert.AreEqual(first.Entries.Count, second.Entries.Count);
            for (int i = 0; i < first.Entries.Count; i++)
            {
                Assert.AreEqual(first.Entries[i].Marker, second.Entries[i].Marker);
                Assert.AreSame(first.Entries[i].Asset, second.Entries[i].Asset);
                Assert.AreEqual(first.Entries[i].FormType, second.Entries[i].FormType);
            }
        }

        [Test]
        public void Generation_IsTheOnlyProgressionOrdinal()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData planner = CreatePlanner(
                new[]
                {
                    Pattern(PatternType.Line, 3L, 1L, 1L, sizeStep: 1L),
                    Pattern(PatternType.Single, 1L, 1L, 0L),
                },
                new[] { Content(water, 1L, 0L, 1f) });
            List<PopulationCellSnapshot> cells = CreateCells(
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) });

            Assert.IsTrue(
                planner.TryBuild(CreateContext(1L, cells), out _, out string firstFailure),
                firstFailure);
            Assert.IsFalse(planner.TryBuild(CreateContext(2L, cells), out _, out string secondFailure));
            StringAssert.Contains("mandatory patterns", secondFailure);
        }

        [Test]
        public void FreeMarkers_AppearExactlyOnceAndOccupiedMarkersArePreserved()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData planner = CreatePlanner(
                new[] { Pattern(PatternType.Single, 1L, 1L, 0L) },
                new[] { Content(water, 1L, 0L, 1f) });
            List<PopulationCellSnapshot> cells = CreateGridCells(3, 2);
            cells[2] = Occupy(cells[2], water);

            Assert.IsTrue(
                planner.TryBuild(CreateContext(1L, cells), out PopulationPlan plan, out string failure),
                failure);

            Assert.AreEqual(5, plan.Entries.Count);
            Assert.AreEqual(5, plan.Entries.Select(entry => entry.Marker).Distinct().Count());
            Assert.IsFalse(plan.Entries.Any(entry => entry.Marker == cells[2].Marker));
            Assert.IsTrue(plan.Entries.All(entry => entry.FormType == EconomyFormType.Token));
        }

        [TestCase(PatternType.Single)]
        [TestCase(PatternType.Line)]
        [TestCase(PatternType.Corner)]
        [TestCase(PatternType.Box)]
        [TestCase(PatternType.Zigzag)]
        public void PatternRule_AcceptsItsExactConnectedGeometry(PatternType patternType)
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            Vector2Int[] coordinates = CoordinatesFor(patternType);
            PopulationPlannerData planner = CreatePlanner(
                new[]
                {
                    Pattern(patternType, coordinates.Length, 1L, 1L),
                    Pattern(PatternType.Single, 1L, 1L, 0L),
                },
                new[] { Content(water, 1L, 0L, 1f) });

            Assert.IsTrue(
                planner.TryBuild(
                    CreateContext(1L, CreateCells(coordinates)),
                    out PopulationPlan plan,
                    out string failure),
                failure);
            Assert.AreEqual(coordinates.Length, plan.Entries.Count);
            Assert.IsTrue(plan.Entries.All(entry => ReferenceEquals(entry.Asset, water)));
        }

        [Test]
        public void MandatoryPattern_CannotBeReplacedByWeightedSingles()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData planner = CreatePlanner(
                new[]
                {
                    Pattern(PatternType.Line, 3L, 1L, 1L),
                    Pattern(PatternType.Single, 1L, 1L, 0L),
                },
                new[] { Content(water, 1L, 0L, 1f) });
            var disconnected = new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(2, 0),
                new Vector2Int(0, 2),
            };

            Assert.IsFalse(planner.TryBuild(CreateContext(1L, CreateCells(disconnected)), out _, out string failure));
            StringAssert.Contains("mandatory patterns", failure);
        }

        [Test]
        public void GuaranteedShare_CountsExistingOccupants()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            CapabilityHostData fire = CreateHost("planner-fire");
            PopulationPlannerData planner = CreatePlanner(
                new[] { Pattern(PatternType.Single, 1L, 1L, 0L) },
                new[]
                {
                    Content(water, 1L, 0L, 0.5f),
                    Content(fire, 1L, 0L, 0.5f),
                });
            List<PopulationCellSnapshot> cells = CreateGridCells(4, 1);
            cells[0] = Occupy(cells[0], water);

            Assert.IsTrue(
                planner.TryBuild(CreateContext(1L, cells), out PopulationPlan plan, out string failure),
                failure);

            Assert.AreEqual(1, plan.Entries.Count(entry => ReferenceEquals(entry.Asset, water)));
            Assert.AreEqual(2, plan.Entries.Count(entry => ReferenceEquals(entry.Asset, fire)));
        }

        [TestCase(TopologyUpAxisType.X)]
        [TestCase(TopologyUpAxisType.Y)]
        [TestCase(TopologyUpAxisType.Z)]
        public void GridSemantics_AreIndependentFromTopologyUpAxis(TopologyUpAxisType upAxisType)
        {
            OpenGridTopology(upAxisType);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData planner = CreatePlanner(
                new[]
                {
                    Pattern(PatternType.Line, 3L, 1L, 1L),
                    Pattern(PatternType.Single, 1L, 1L, 0L),
                },
                new[] { Content(water, 1L, 0L, 1f) });
            List<PopulationCellSnapshot> cells = CreateCells(
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
                upAxisType);

            Assert.IsTrue(
                planner.TryBuild(CreateContext(1L, cells), out PopulationPlan plan, out string failure),
                failure);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, plan.Entries.Select(entry => entry.Marker.LocalIndex));
        }

        [Test]
        public void InvalidAuthoring_IsRejectedExplicitly()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData missingSingle = CreatePlanner(
                new[] { Pattern(PatternType.Line, 3L, 1L, 0L) },
                new[] { Content(water, 1L, 0L, 1f) });
            PopulationPlannerData missingProgression = CreatePlanner(
                new[] { new PatternSpec(PatternType.Single, null, Constant(1L), Constant(0L)) },
                new[] { Content(water, 1L, 0L, 1f) });
            PopulationPlanContext context = CreateContext(1L, CreateGridCells(2, 2));

            Assert.IsFalse(missingSingle.TryBuild(context, out _, out string singleFailure));
            StringAssert.Contains("active Single", singleFailure);
            Assert.IsFalse(missingProgression.TryBuild(context, out _, out string progressionFailure));
            StringAssert.Contains("is missing", progressionFailure);
        }

        [Test]
        public void DuplicateCoordinates_AreRejected()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData planner = CreatePlanner(
                new[] { Pattern(PatternType.Single, 1L, 1L, 0L) },
                new[] { Content(water, 1L, 0L, 1f) });
            var cells = new List<PopulationCellSnapshot>
            {
                CreateCell(0, new Vector2Int(0, 0), TopologyUpAxisType.Y),
                CreateCell(1, new Vector2Int(0, 0), TopologyUpAxisType.Y),
            };

            Assert.IsFalse(planner.TryBuild(CreateContext(1L, cells), out _, out string failure));
            StringAssert.Contains("duplicate grid coordinate", failure);
        }

        [Test]
        public void SourcePolicy_DoesNotDependOnLegacyBoardPipelineOrUnityRandom()
        {
            string source = File.ReadAllText(PlannerScriptPath);
            string[] banned =
            {
                "LevelData",
                "LevelManager",
                "BoardUi",
                "CellUi",
                "CellItemType",
                "CellSelectPatternType",
                "UnityEngine.Random",
            };

            var offenders = banned.Where(source.Contains).ToList();
            Assert.IsEmpty(offenders);
        }

        [Test]
        public void ProgressionAuthoring_IsInlineAndSelfContained()
        {
            Type plannerType = AssetDatabase.LoadAssetAtPath<MonoScript>(PlannerScriptPath)?.GetClass();
            Assert.NotNull(plannerType);
            Type patternRuleType = plannerType.GetNestedType("PatternRule", BindingFlags.Public);
            Type contentRuleType = plannerType.GetNestedType("ContentRule", BindingFlags.Public);
            Assert.NotNull(patternRuleType);
            Assert.NotNull(contentRuleType);

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            string[] patternFields = { "size", "weight", "minimumCount" };
            for (int i = 0; i < patternFields.Length; i++)
            {
                FieldInfo field = patternRuleType.GetField(patternFields[i], Flags);
                Assert.NotNull(field);
                Assert.NotNull(field.GetCustomAttribute<SerializeReference>());
            }

            string[] contentFields = { "weight", "minimumPatternCount" };
            for (int i = 0; i < contentFields.Length; i++)
            {
                FieldInfo field = contentRuleType.GetField(contentFields[i], Flags);
                Assert.NotNull(field);
                Assert.NotNull(field.GetCustomAttribute<SerializeReference>());
            }

            Assert.IsFalse(AssetDatabase.IsValidFolder(
                "Assets/Game/Activities/Board/Population/Progression"));
        }

        void OpenGridTopology(TopologyUpAxisType upAxisType)
        {
            DeterminismService.StartSession(8191);
            InvokeTopology("StartSession", 8191);
            TopologyDefinitionData definition = ScriptableObject.CreateInstance<TopologyDefinitionData>();
            _ownedObjects.Add(definition);
            SetField(definition, "topologyType", TopologyType.Grid);
            SetField(definition, "coordinateOccupationPolicy", TopologyCoordinateOccupationPolicy.SingleOccupant);
            SetField(definition, "dimensionType", TopologyDimensionType.TwoDimensional);
            SetField(definition, "upAxisType", upAxisType);
            SetField(definition, "topologyUnitsPerUnityUnit", 1000);
            SetField(definition, "topologyCoordinateSize", 1000);
            InvokeTopology("OpenActivityContext", ActivityId, definition);
            _topologyOpen = true;
        }

        PopulationPlannerData CreatePlanner(
            IReadOnlyList<PatternSpec> patterns,
            IReadOnlyList<ContentSpec> contents)
        {
            Type plannerType = AssetDatabase.LoadAssetAtPath<MonoScript>(PlannerScriptPath)?.GetClass();
            Assert.NotNull(plannerType, "ProgressivePlannerData MonoScript did not resolve to a compiled type.");
            var planner = ScriptableObject.CreateInstance(plannerType) as PopulationPlannerData;
            Assert.NotNull(planner);
            _ownedObjects.Add(planner);

            Type patternRuleType = plannerType.GetNestedType("PatternRule", BindingFlags.Public);
            Type patternEnumType = plannerType.GetNestedType("PatternType", BindingFlags.Public);
            Type contentRuleType = plannerType.GetNestedType("ContentRule", BindingFlags.Public);
            Assert.NotNull(patternRuleType);
            Assert.NotNull(patternEnumType);
            Assert.NotNull(contentRuleType);

            IList patternList = CreateList(patternRuleType);
            for (int i = 0; i < patterns.Count; i++)
            {
                PatternSpec spec = patterns[i];
                object rule = Activator.CreateInstance(patternRuleType);
                SetField(rule, "patternType", Enum.ToObject(patternEnumType, (int)spec.Type));
                SetField(rule, "size", spec.Size);
                SetField(rule, "weight", spec.Weight);
                SetField(rule, "minimumCount", spec.MinimumCount);
                patternList.Add(rule);
            }

            IList contentList = CreateList(contentRuleType);
            for (int i = 0; i < contents.Count; i++)
            {
                ContentSpec spec = contents[i];
                object rule = Activator.CreateInstance(contentRuleType);
                SetField(rule, "asset", spec.Asset);
                SetField(rule, "weight", spec.Weight);
                SetField(rule, "minimumPatternCount", spec.MinimumPatternCount);
                SetField(rule, "guaranteedCellShare", spec.GuaranteedCellShare);
                contentList.Add(rule);
            }

            SetField(planner, "patternRules", patternList);
            SetField(planner, "contentRules", contentList);
            return planner;
        }

        LongLinearProgressionData Constant(long value, long step = 0L)
        {
            return new LongLinearProgressionData(value, step);
        }

        CapabilityHostData CreateHost(string id)
        {
            CapabilityHostData host = ScriptableObject.CreateInstance<CapabilityHostData>();
            _ownedObjects.Add(host);
            SetField(host, "id", id);
            return host;
        }

        PatternSpec Pattern(
            PatternType type,
            long size,
            long weight,
            long minimumCount,
            long sizeStep = 0L)
        {
            return new PatternSpec(
                type,
                Constant(size, sizeStep),
                Constant(weight),
                Constant(minimumCount));
        }

        ContentSpec Content(
            CapabilityHostData asset,
            long weight,
            long minimumPatternCount,
            float guaranteedCellShare)
        {
            return new ContentSpec(
                asset,
                Constant(weight),
                Constant(minimumPatternCount),
                guaranteedCellShare);
        }

        static PopulationPlanContext CreateContext(long generation, IEnumerable<PopulationCellSnapshot> cells)
        {
            return new PopulationPlanContext(
                ActivityId,
                DomainId,
                ParticipantEntityId,
                PopulationEntityId,
                generation,
                cells);
        }

        static List<PopulationCellSnapshot> CreateGridCells(
            int width,
            int height,
            TopologyUpAxisType upAxisType = TopologyUpAxisType.Y)
        {
            var coordinates = new List<Vector2Int>(width * height);
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                    coordinates.Add(new Vector2Int(column, row));
            }

            return CreateCells(coordinates, upAxisType);
        }

        static List<PopulationCellSnapshot> CreateCells(
            IEnumerable<Vector2Int> coordinates,
            TopologyUpAxisType upAxisType = TopologyUpAxisType.Y)
        {
            var result = new List<PopulationCellSnapshot>();
            int index = 0;
            foreach (Vector2Int coordinate in coordinates)
                result.Add(CreateCell(index++, coordinate, upAxisType));
            return result;
        }

        static PopulationCellSnapshot CreateCell(
            int index,
            Vector2Int coordinate,
            TopologyUpAxisType upAxisType)
        {
            Vector3 topologyCoordinates;
            switch (upAxisType)
            {
                case TopologyUpAxisType.X:
                    topologyCoordinates = new Vector3(0f, coordinate.x, coordinate.y);
                    break;
                case TopologyUpAxisType.Z:
                    topologyCoordinates = new Vector3(coordinate.x, coordinate.y, 0f);
                    break;
                default:
                    topologyCoordinates = new Vector3(coordinate.x, 0f, coordinate.y);
                    break;
            }

            return new PopulationCellSnapshot(
                new SpatialMarkerRef(ActivityId, MarkerScopeEntityId, "planner-grid", index),
                WorldPosition.Invalid,
                topologyCoordinates,
                EntityId.Invalid,
                null,
                EconomyFormType.Token);
        }

        static PopulationCellSnapshot Occupy(
            PopulationCellSnapshot cell,
            CapabilityHostData asset)
        {
            return new PopulationCellSnapshot(
                cell.Marker,
                cell.Position,
                cell.Coordinates,
                new EntityId(9000 + cell.Marker.LocalIndex),
                asset,
                EconomyFormType.Token);
        }

        static Vector2Int[] CoordinatesFor(PatternType type)
        {
            switch (type)
            {
                case PatternType.Line:
                    return new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0),
                        new Vector2Int(2, 0),
                    };
                case PatternType.Corner:
                    return new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0),
                        new Vector2Int(0, 1),
                    };
                case PatternType.Box:
                    return new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0),
                        new Vector2Int(0, 1),
                        new Vector2Int(1, 1),
                    };
                case PatternType.Zigzag:
                    return new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0),
                        new Vector2Int(1, 1),
                        new Vector2Int(2, 1),
                    };
                default:
                    return new[] { new Vector2Int(0, 0) };
            }
        }

        static IList CreateList(Type elementType)
        {
            return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
        }

        static void InvokeTopology(string methodName, params object[] args)
        {
            MethodInfo method = typeof(TopologyService).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method, $"TopologyService.{methodName} was not found.");
            method.Invoke(null, args);
        }

        static void SetField(object target, string fieldName, object value)
        {
            Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.NotNull(field, $"Field '{fieldName}' was not found on {target.GetType().FullName}.");
            field.SetValue(target, value);
        }

        public enum PatternType
        {
            Single = 0,
            Line = 1,
            Corner = 2,
            Box = 3,
            Zigzag = 4,
        }

        readonly struct PatternSpec
        {
            public PatternSpec(
                PatternType type,
                LongProgressionData size,
                LongProgressionData weight,
                LongProgressionData minimumCount)
            {
                Type = type;
                Size = size;
                Weight = weight;
                MinimumCount = minimumCount;
            }

            public PatternType Type { get; }
            public LongProgressionData Size { get; }
            public LongProgressionData Weight { get; }
            public LongProgressionData MinimumCount { get; }
        }

        readonly struct ContentSpec
        {
            public ContentSpec(
                CapabilityHostData asset,
                LongProgressionData weight,
                LongProgressionData minimumPatternCount,
                float guaranteedCellShare)
            {
                Asset = asset;
                Weight = weight;
                MinimumPatternCount = minimumPatternCount;
                GuaranteedCellShare = guaranteedCellShare;
            }

            public CapabilityHostData Asset { get; }
            public LongProgressionData Weight { get; }
            public LongProgressionData MinimumPatternCount { get; }
            public float GuaranteedCellShare { get; }
        }
    }
}
