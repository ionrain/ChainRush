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
using Core.Economy.Modules.SpatialEconomyModule;
using Core.Entities;
using Core.Orchestration;
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
        readonly List<SpatialShapeData> _activeShapes = new List<SpatialShapeData>();
        bool _topologyOpen;
        TopologyUpAxisType _upAxisType;

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
            _activeShapes.Clear();
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
                    Pattern(ShapeFixtureType.Line, 3L, 1L, 1L),
                    Pattern(ShapeFixtureType.Single, 1L, 1L, 0L),
                },
                new[] { Content(water, 1L, 0L, 1f) });
            PopulationPlanContext context = CreateContext(1L, CreateGridCells(4, 4));

            Assert.IsTrue(planner.TryBuild(context, out PopulationPlan first, out string firstFailure), firstFailure);
            Assert.IsTrue(planner.TryBuild(context, out PopulationPlan second, out string secondFailure), secondFailure);

            Assert.AreEqual(first.Groups.Count, second.Groups.Count);
            for (int i = 0; i < first.Groups.Count; i++)
            {
                Assert.AreSame(first.Groups[i].Shape, second.Groups[i].Shape);
                Assert.AreSame(first.Groups[i].Asset, second.Groups[i].Asset);
                Assert.AreEqual(first.Groups[i].FormType, second.Groups[i].FormType);
                CollectionAssert.AreEqual(first.Groups[i].Markers, second.Groups[i].Markers);
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
                    Pattern(ShapeFixtureType.Line, 3L, 1L, 1L, sizeStep: 1L),
                    Pattern(ShapeFixtureType.Single, 1L, 1L, 0L),
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
                new[] { Pattern(ShapeFixtureType.Single, 1L, 1L, 0L) },
                new[] { Content(water, 1L, 0L, 1f) });
            List<PopulationCellSnapshot> cells = CreateGridCells(3, 2);
            cells[2] = Occupy(cells[2], water);

            Assert.IsTrue(
                planner.TryBuild(CreateContext(1L, cells), out PopulationPlan plan, out string failure),
                failure);

            List<SpatialMarkerRef> markers = FlattenMarkers(plan);
            Assert.AreEqual(5, markers.Count);
            Assert.AreEqual(5, markers.Distinct().Count());
            Assert.IsFalse(markers.Contains(cells[2].Marker));
            Assert.IsTrue(plan.Groups.All(group => group.FormType == EconomyFormType.Token));
        }

        [Test]
        public void TemporarilyUnavailableMarker_RemainsInSnapshotButIsNotPlanned()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData planner = CreatePlanner(
                new[] { Pattern(ShapeFixtureType.Single, 1L, 1L, 0L) },
                new[] { Content(water, 1L, 0L, 1f) });
            List<PopulationCellSnapshot> cells = CreateGridCells(3, 2);
            cells[2] = MakeUnavailable(cells[2]);

            Assert.IsTrue(
                planner.TryBuild(CreateContext(1L, cells), out PopulationPlan plan, out string failure),
                failure);

            List<SpatialMarkerRef> markers = FlattenMarkers(plan);
            Assert.AreEqual(5, markers.Count);
            Assert.IsFalse(cells[2].IsOccupied);
            Assert.IsFalse(cells[2].AvailableForPlacement);
            Assert.IsFalse(markers.Contains(cells[2].Marker));
        }

        [TestCase(ShapeFixtureType.Single)]
        [TestCase(ShapeFixtureType.Line)]
        [TestCase(ShapeFixtureType.Corner)]
        [TestCase(ShapeFixtureType.Box)]
        [TestCase(ShapeFixtureType.Zigzag)]
        public void PatternRule_AcceptsItsExactConnectedGeometry(ShapeFixtureType patternType)
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            Vector2Int[] coordinates = CoordinatesFor(patternType);
            PopulationPlannerData planner = CreatePlanner(
                new[]
                {
                    Pattern(patternType, coordinates.Length, 1L, 1L),
                    Pattern(ShapeFixtureType.Single, 1L, 1L, 0L),
                },
                new[] { Content(water, 1L, 0L, 1f) });

            Assert.IsTrue(
                planner.TryBuild(
                    CreateContext(1L, CreateCells(coordinates)),
                    out PopulationPlan plan,
                    out string failure),
                failure);
            Assert.AreEqual(coordinates.Length, FlattenMarkers(plan).Count);
            Assert.IsTrue(plan.Groups.All(group => ReferenceEquals(group.Asset, water)));
        }

        [Test]
        public void MandatoryPattern_CannotBeReplacedByWeightedSingles()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData planner = CreatePlanner(
                new[]
                {
                    Pattern(ShapeFixtureType.Line, 3L, 1L, 1L),
                    Pattern(ShapeFixtureType.Single, 1L, 1L, 0L),
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
                new[] { Pattern(ShapeFixtureType.Single, 1L, 1L, 0L) },
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

            Assert.AreEqual(1, CountPlannedCells(plan, water));
            Assert.AreEqual(2, CountPlannedCells(plan, fire));
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
                    Pattern(ShapeFixtureType.Line, 3L, 1L, 1L),
                    Pattern(ShapeFixtureType.Single, 1L, 1L, 0L),
                },
                new[] { Content(water, 1L, 0L, 1f) });
            List<PopulationCellSnapshot> cells = CreateCells(
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
                upAxisType);

            Assert.IsTrue(
                planner.TryBuild(CreateContext(1L, cells), out PopulationPlan plan, out string failure),
                failure);
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                FlattenMarkers(plan).Select(marker => marker.LocalIndex));
        }

        [Test]
        public void InvalidAuthoring_IsRejectedExplicitly()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData missingSingle = CreatePlanner(
                new[] { Pattern(ShapeFixtureType.Line, 3L, 1L, 0L) },
                new[] { Content(water, 1L, 0L, 1f) });
            PopulationPlannerData missingProgression = CreatePlanner(
                new[] { new PatternSpec(CreateShape(ShapeFixtureType.Single), null, Constant(1L), Constant(0L)) },
                new[] { Content(water, 1L, 0L, 1f) });
            PopulationPlanContext context = CreateContext(1L, CreateGridCells(2, 2));

            Assert.IsFalse(missingSingle.TryBuild(context, out _, out string singleFailure));
            StringAssert.Contains("resolved size of one cell", singleFailure);
            Assert.IsFalse(missingProgression.TryBuild(context, out _, out string progressionFailure));
            StringAssert.Contains("is missing", progressionFailure);
        }

        [Test]
        public void DuplicateCoordinates_AreRejected()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            CapabilityHostData water = CreateHost("planner-water");
            PopulationPlannerData planner = CreatePlanner(
                new[] { Pattern(ShapeFixtureType.Single, 1L, 1L, 0L) },
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
            _upAxisType = upAxisType;
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
            Type contentRuleType = plannerType.GetNestedType("ContentRule", BindingFlags.Public);
            Assert.NotNull(patternRuleType);
            Assert.NotNull(contentRuleType);

            IList patternList = CreateList(patternRuleType);
            for (int i = 0; i < patterns.Count; i++)
            {
                PatternSpec spec = patterns[i];
                object rule = Activator.CreateInstance(patternRuleType);
                SetField(rule, "shape", spec.Shape);
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
            ShapeFixtureType type,
            long size,
            long weight,
            long minimumCount,
            long sizeStep = 0L)
        {
            return new PatternSpec(
                CreateShape(type),
                Constant(size, sizeStep),
                Constant(weight),
                Constant(minimumCount));
        }

        SpatialShapeData CreateShape(ShapeFixtureType type)
        {
            SpatialShapeData shape = ScriptableObject.CreateInstance<SpatialShapeData>();
            _ownedObjects.Add(shape);
            _activeShapes.Add(shape);
            SetField(shape, "id", string.Concat("planner-shape-", type.ToString(), "-", _activeShapes.Count.ToString()));
            if (type == ShapeFixtureType.Box)
            {
                SetField(shape, "shapeType", SpatialShapeType.Box);
                return shape;
            }

            SetField(shape, "shapeType", SpatialShapeType.Custom);
            SpatialShapeRuleData rule = ScriptableObject.CreateInstance<SpatialShapeRuleData>();
            _ownedObjects.Add(rule);
            SetField(rule, "requiredCells", new List<Vector3Int> { Vector3Int.zero });
            ResolvePlanarDirections(_upAxisType, out Vector3Int first, out Vector3Int second);
            var paths = new List<SpatialShapeRuleData.ContinuationPathData>();
            switch (type)
            {
                case ShapeFixtureType.Line:
                    paths.Add(new SpatialShapeRuleData.ContinuationPathData(
                        Vector3Int.zero,
                        new List<Vector3Int> { first }));
                    break;
                case ShapeFixtureType.Corner:
                    paths.Add(new SpatialShapeRuleData.ContinuationPathData(
                        Vector3Int.zero,
                        new List<Vector3Int> { first }));
                    paths.Add(new SpatialShapeRuleData.ContinuationPathData(
                        Vector3Int.zero,
                        new List<Vector3Int> { second }));
                    break;
                case ShapeFixtureType.Zigzag:
                    paths.Add(new SpatialShapeRuleData.ContinuationPathData(
                        Vector3Int.zero,
                        new List<Vector3Int> { first, second }));
                    break;
            }
            SetField(rule, "continuationPaths", paths);
            SetField(shape, "customRule", rule);
            return shape;
        }

        static void ResolvePlanarDirections(
            TopologyUpAxisType upAxisType,
            out Vector3Int first,
            out Vector3Int second)
        {
            switch (upAxisType)
            {
                case TopologyUpAxisType.X:
                    first = Vector3Int.up;
                    second = new Vector3Int(0, 0, 1);
                    break;
                case TopologyUpAxisType.Z:
                    first = Vector3Int.right;
                    second = Vector3Int.up;
                    break;
                default:
                    first = Vector3Int.right;
                    second = new Vector3Int(0, 0, 1);
                    break;
            }
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

        PopulationPlanContext CreateContext(long generation, IEnumerable<PopulationCellSnapshot> cells)
        {
            var shapes = new List<SpatialShapeProjectionRecord>(_activeShapes.Count);
            for (int i = 0; i < _activeShapes.Count; i++)
                shapes.Add(new SpatialShapeProjectionRecord(_activeShapes[i], 1L));
            return new PopulationPlanContext(
                ActivityId,
                DomainId,
                ParticipantEntityId,
                PopulationEntityId,
                generation,
                cells,
                shapes);
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
                ResolvePosition(topologyCoordinates, upAxisType),
                topologyCoordinates,
                Quaternion.identity,
                ResolveCellFootprint(upAxisType),
                true,
                EntityId.Invalid,
                null,
                EconomyFormType.Token);
        }

        static WorldPosition ResolvePosition(
            Vector3 topologyCoordinates,
            TopologyUpAxisType upAxisType)
        {
            _ = upAxisType;
            Assert.IsTrue(TopologyService.TryResolveTopologyPoint(
                ActivityId,
                Mathf.RoundToInt(topologyCoordinates.x),
                Mathf.RoundToInt(topologyCoordinates.y),
                Mathf.RoundToInt(topologyCoordinates.z),
                out WorldPosition position,
                out _));
            return position;
        }

        static NavigationFootprint ResolveCellFootprint(TopologyUpAxisType upAxisType)
        {
            switch (upAxisType)
            {
                case TopologyUpAxisType.X:
                    return new NavigationFootprint(0, 1000, 1000);
                case TopologyUpAxisType.Z:
                    return new NavigationFootprint(1000, 1000, 0);
                default:
                    return new NavigationFootprint(1000, 0, 1000);
            }
        }

        static List<SpatialMarkerRef> FlattenMarkers(PopulationPlan plan)
        {
            var markers = new List<SpatialMarkerRef>();
            for (int i = 0; i < plan.Groups.Count; i++)
                markers.AddRange(plan.Groups[i].Markers);
            return markers;
        }

        static int CountPlannedCells(PopulationPlan plan, CapabilityHostData asset)
        {
            int count = 0;
            for (int i = 0; i < plan.Groups.Count; i++)
            {
                if (ReferenceEquals(plan.Groups[i].Asset, asset))
                    count += plan.Groups[i].Markers.Count;
            }
            return count;
        }

        static PopulationCellSnapshot Occupy(
            PopulationCellSnapshot cell,
            CapabilityHostData asset)
        {
            return new PopulationCellSnapshot(
                cell.Marker,
                cell.Position,
                cell.Coordinates,
                cell.Rotation,
                cell.CellFootprint,
                false,
                new EntityId(9000 + cell.Marker.LocalIndex),
                asset,
                EconomyFormType.Token);
        }

        static PopulationCellSnapshot MakeUnavailable(PopulationCellSnapshot cell)
        {
            return new PopulationCellSnapshot(
                cell.Marker,
                cell.Position,
                cell.Coordinates,
                cell.Rotation,
                cell.CellFootprint,
                false,
                EntityId.Invalid,
                null,
                EconomyFormType.Token);
        }

        static Vector2Int[] CoordinatesFor(ShapeFixtureType type)
        {
            switch (type)
            {
                case ShapeFixtureType.Line:
                    return new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0),
                        new Vector2Int(2, 0),
                    };
                case ShapeFixtureType.Corner:
                    return new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0),
                        new Vector2Int(0, 1),
                    };
                case ShapeFixtureType.Box:
                    return new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0),
                        new Vector2Int(0, 1),
                        new Vector2Int(1, 1),
                    };
                case ShapeFixtureType.Zigzag:
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

        public enum ShapeFixtureType
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
                SpatialShapeData shape,
                LongProgressionData size,
                LongProgressionData weight,
                LongProgressionData minimumCount)
            {
                Shape = shape;
                Size = size;
                Weight = weight;
                MinimumCount = minimumCount;
            }

            public SpatialShapeData Shape { get; }
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
