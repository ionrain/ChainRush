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
    public sealed class ShapePopulationPlannerEditModeTests
    {
        const string PlannerScriptPath =
            "Assets/Game/Activities/Board/Runtime/Population/ShapePopulationPlannerData.cs";

        static readonly ActivityId ActivityId = new ActivityId(7101);
        static readonly RuntimeDomainId DomainId = new RuntimeDomainId(7102);
        static readonly EntityId ParticipantEntityId = new EntityId(7103);
        static readonly EntityId PopulationEntityId = new EntityId(7104);
        static readonly EntityId MarkerScopeEntityId = new EntityId(7105);

        readonly List<Object> _ownedObjects = new List<Object>();
        readonly List<SpatialShapeData> _activeShapes = new List<SpatialShapeData>();
        readonly Dictionary<PopulationPlannerData, List<PopulationShapeRuleData>> _rulesByPlanner = new Dictionary<PopulationPlannerData, List<PopulationShapeRuleData>>();
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
            _rulesByPlanner.Clear();
            _topologyOpen = false;
        }

        [Test]
        public void EmptyDistribution_CompletesWithCompatibleContentRequirements()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            var planner = CreatePlanner(new[] { Pattern(ShapeFixtureType.Single, 1L, 1L, 0L) },
                new[] { Content(CreateHost("empty-content"), 1L, 0L, 0f) });
            var context = CreateContext(1L, new List<PopulationCellSnapshot>());

            Assert.IsTrue(Build(planner, context, out var plan, out var failure), failure);
            Assert.IsEmpty(plan.Groups);
        }

        [Test]
        public void EmptyDistribution_DoesNotBypassContentMinimum()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            var planner = CreatePlanner(new[] { Pattern(ShapeFixtureType.Single, 1L, 1L, 0L) },
                new[] { Content(CreateHost("required-content"), 1L, 1L, 0f) });
            var context = CreateContext(1L, new List<PopulationCellSnapshot>());

            Assert.IsFalse(Build(planner, context, out _, out var failure));
            StringAssert.Contains("minimum pattern count", failure);
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

            Assert.IsTrue(Build(planner, context, out PopulationPlan first, out string firstFailure), firstFailure);
            Assert.IsTrue(Build(planner, context, out PopulationPlan second, out string secondFailure), secondFailure);

            Assert.AreEqual(first.Groups.Count, second.Groups.Count);
            for (int i = 0; i < first.Groups.Count; i++)
            {
                Assert.AreSame(first.Groups[i].Shape, second.Groups[i].Shape);
                Assert.AreSame(first.Groups[i].Asset, second.Groups[i].Asset);
                Assert.AreEqual(first.Groups[i].FormType, second.Groups[i].FormType);
                CollectionAssert.AreEqual(first.Groups[i].Cells, second.Groups[i].Cells);
            }
        }

        [Test]
        public void Seed_DoesNotChangeAuthoredPatternSize()
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
            List<PopulationCellSnapshot> cells = CreateCells(
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) });

            Assert.IsTrue(
                Build(planner, CreateContext(1L, cells), out _, out string firstFailure),
                firstFailure);
            Assert.IsTrue(Build(planner, CreateContext(2L, cells), out PopulationPlan second, out string secondFailure), secondFailure);
            Assert.AreEqual(3, second.Groups.Single(group => group.Shape == _activeShapes[0]).Cells.Count);
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
                Build(planner, CreateContext(1L, cells), out PopulationPlan plan, out string failure),
                failure);

            List<SpaceRegionCellReference> markers = FlattenCells(plan);
            Assert.AreEqual(5, markers.Count);
            Assert.AreEqual(5, markers.Distinct().Count());
            Assert.IsFalse(markers.Contains(cells[2].Cell));
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
                Build(planner, CreateContext(1L, cells), out PopulationPlan plan, out string failure),
                failure);

            List<SpaceRegionCellReference> markers = FlattenCells(plan);
            Assert.AreEqual(5, markers.Count);
            Assert.IsFalse(cells[2].IsOccupied);
            Assert.IsFalse(cells[2].AvailableForPlacement);
            Assert.IsFalse(markers.Contains(cells[2].Cell));
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
                Build(planner,
                    CreateContext(1L, CreateCells(coordinates)),
                    out PopulationPlan plan,
                    out string failure),
                failure);
            Assert.AreEqual(coordinates.Length, FlattenCells(plan).Count);
            Assert.IsTrue(plan.Groups.All(group => ReferenceEquals(group.Asset, water)));
        }

        [Test]
        public void UnattainableDesiredShapeCount_AllowsWeightedSingles()
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

            Assert.IsTrue(Build(planner, CreateContext(1L, CreateCells(disconnected)), out var plan, out string failure), failure);
            Assert.AreEqual(3, FlattenCells(plan).Count);
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
                Build(planner, CreateContext(1L, cells), out PopulationPlan plan, out string failure),
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
                Build(planner, CreateContext(1L, cells), out PopulationPlan plan, out string failure),
                failure);
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                FlattenCells(plan).Select(marker => marker.LocalIndex));
        }

        [Test]
        public void PartialDistribution_DoesNotRelaxContentGuarantees()
        {
            OpenGridTopology(TopologyUpAxisType.Y);
            var water = CreateHost("partial-content");
            var planner = CreatePlanner(
                new[] { Pattern(ShapeFixtureType.Line, 3L, 1L, 0L) },
                new[] { Content(water, 1L, 0L, 1f) });
            Assert.IsFalse(Build(planner, CreateContext(1L, CreateGridCells(2, 2)), out _, out string failure));
            StringAssert.Contains("guaranteed cell share", failure);
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

            Assert.IsFalse(Build(planner, CreateContext(1L, cells), out _, out string failure));
            StringAssert.Contains("distinct", failure);
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
        public void ShapeAuthoring_UsesConstantNumericParameters()
        {
            Type plannerType = AssetDatabase.LoadAssetAtPath<MonoScript>(PlannerScriptPath)?.GetClass();
            Assert.NotNull(plannerType);
            Assert.IsNull(plannerType.GetNestedType("PatternRule", BindingFlags.Public));
            Assert.AreEqual(typeof(IntRange), typeof(PopulationShapeRuleData).GetProperty("DesiredCount").PropertyType);
            Type contentRuleType = plannerType.GetNestedType("ContentRule", BindingFlags.Public);
            Assert.NotNull(contentRuleType);
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            string[] contentFields = { "weight", "minimumPatternCount" };
            for (int i = 0; i < contentFields.Length; i++)
            {
                FieldInfo field = contentRuleType.GetField(contentFields[i], Flags);
                Assert.NotNull(field);
                Assert.AreEqual(typeof(long), field.FieldType);
                Assert.NotNull(field.GetCustomAttribute<SerializeField>());
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

        bool Build(PopulationPlannerData planner, in PopulationPlanContext context, out PopulationPlan plan, out string failure)
        {
            plan = null;
            var algorithm = new GridPopulationDistributionAlgorithmData();
            if (!algorithm.TryCreateSession(context, _rulesByPlanner[planner], context.Cells.Count,
                out var session, out failure)) return false;
            using (session)
            {
                for (int step = 0; step < 10000 && session.State == PopulationDistributionStateType.Pending; step++)
                    session.Advance(256);
                if (session.State != PopulationDistributionStateType.Ready)
                { failure = session.Failure; return false; }
                return planner.TryBuild(context, session.Groups, out plan, out failure);
            }
        }

        PopulationPlannerData CreatePlanner(
            IReadOnlyList<PatternSpec> patterns,
            IReadOnlyList<ContentSpec> contents)
        {
            Type plannerType = AssetDatabase.LoadAssetAtPath<MonoScript>(PlannerScriptPath)?.GetClass();
            Assert.NotNull(plannerType, "ShapePopulationPlannerData MonoScript did not resolve to a compiled type.");
            var planner = ScriptableObject.CreateInstance(plannerType) as PopulationPlannerData;
            Assert.NotNull(planner);
            _ownedObjects.Add(planner);

            Type contentRuleType = plannerType.GetNestedType("ContentRule", BindingFlags.Public);
            var rules = new List<PopulationShapeRuleData>();
            for (int i = 0; i < patterns.Count; i++)
            {
                PatternSpec spec = patterns[i];
                rules.Add(new PopulationShapeRuleData(spec.Shape, spec.Usages, checked((int)spec.Weight),
                    new IntRange(checked((int)spec.MinimumCount), 64)));
            }
            _rulesByPlanner.Add(planner, rules);

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

            SetField(planner, "contentRules", contentList);
            return planner;
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
            long minimumCount)
        {
            Vector2Int[] coordinates = CoordinatesFor(type);
            int width = type == ShapeFixtureType.Line ? checked((int)size) : coordinates.Max(cell => cell.x) + 1;
            int height = type == ShapeFixtureType.Line ? 1 : coordinates.Max(cell => cell.y) + 1;
            ResolvePlanarDirections(_upAxisType, out var first, out var second);
            Vector3Int up = Vector3Int.one - first - second;
            Vector3Int dimensions = first * width + second * height + up;
            var usages = new List<SpatialShapeUsageData>();
            for (int angle = 0; angle < 360; angle += 90)
                usages.Add(new SpatialShapeUsageData(SpatialShapeFillType.Inside, Vector3Int.zero, dimensions,
                    up * angle, first * 1000 + second * 1000 + up, Vector3Int.zero));
            return new PatternSpec(CreateShape(type), usages, weight, minimumCount);
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
                weight,
                minimumPatternCount,
                guaranteedCellShare);
        }

        PopulationPlanContext CreateContext(
            long seed,
            IReadOnlyList<PopulationCellSnapshot> cells)
        {
            var shapes = new List<SpatialShapeProjectionRecord>(_activeShapes.Count);
            for (int i = 0; i < _activeShapes.Count; i++)
                shapes.Add(new SpatialShapeProjectionRecord(_activeShapes[i], 1L));
            return new PopulationPlanContext(
                ActivityId,
                DomainId,
                ParticipantEntityId,
                PopulationEntityId,
                unchecked((ulong)seed),
                CreateRegions(cells),
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
                new SpaceRegionCellReference(new SpaceRegionCellSnapshot(TestRegionHandle(), 1L, index,
                    new Vector3Int(coordinate.x, 0, coordinate.y),
                    new SpatialPose(ResolvePosition(topologyCoordinates, upAxisType), topologyCoordinates, Quaternion.identity),
                    ResolveCellFootprint(upAxisType), default)),
                ResolvePosition(topologyCoordinates, upAxisType),
                topologyCoordinates,
                Quaternion.identity,
                ResolveCellFootprint(upAxisType),
                true,
                EntityId.Invalid,
                null,
                EconomyFormType.Token);
        }

        static SpaceRegionHandle TestRegionHandle()
        {
            return (SpaceRegionHandle)Activator.CreateInstance(typeof(SpaceRegionHandle),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1L }, null);
        }

        static List<SpaceRegionSnapshot> CreateRegions(IReadOnlyList<PopulationCellSnapshot> cells)
        {
            Assert.IsTrue(TopologyService.TryGetTopologyDescriptor(ActivityId, out var topology));
            return new List<SpaceRegionSnapshot>
            {
                new SpaceRegionSnapshot(TestRegionHandle(), 1L, ActivityId, MarkerScopeEntityId,
                    new SpaceRegionId("planner-grid"), default, default, topology, SpaceRegionGeometryType.Cells,
                    cells.Count, default, SpaceRegionStateType.Active, SpaceRegionPlanningAvailabilityType.Available, default, false)
            };
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

        static SpatialFootprint ResolveCellFootprint(TopologyUpAxisType upAxisType)
        {
            switch (upAxisType)
            {
                case TopologyUpAxisType.X:
                    return new SpatialFootprint(0, 1000, 1000);
                case TopologyUpAxisType.Z:
                    return new SpatialFootprint(1000, 1000, 0);
                default:
                    return new SpatialFootprint(1000, 0, 1000);
            }
        }

        static List<SpaceRegionCellReference> FlattenCells(PopulationPlan plan)
        {
            var markers = new List<SpaceRegionCellReference>();
            for (int i = 0; i < plan.Groups.Count; i++)
                markers.AddRange(plan.Groups[i].Cells);
            return markers;
        }

        static int CountPlannedCells(PopulationPlan plan, CapabilityHostData asset)
        {
            int count = 0;
            for (int i = 0; i < plan.Groups.Count; i++)
            {
                if (ReferenceEquals(plan.Groups[i].Asset, asset))
                    count += plan.Groups[i].Cells.Count;
            }
            return count;
        }

        static PopulationCellSnapshot Occupy(
            PopulationCellSnapshot cell,
            CapabilityHostData asset)
        {
            return new PopulationCellSnapshot(
                cell.Cell,
                cell.Position,
                cell.Coordinates,
                cell.Rotation,
                cell.CellFootprint,
                false,
                new EntityId(9000 + cell.Cell.LocalIndex),
                asset,
                EconomyFormType.Token);
        }

        static PopulationCellSnapshot MakeUnavailable(PopulationCellSnapshot cell)
        {
            return new PopulationCellSnapshot(
                cell.Cell,
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
                List<SpatialShapeUsageData> usages,
                long weight,
                long minimumCount)
            {
                Shape = shape;
                Usages = usages;
                Weight = weight;
                MinimumCount = minimumCount;
            }

            public SpatialShapeData Shape { get; }
            public List<SpatialShapeUsageData> Usages { get; }
            public long Weight { get; }
            public long MinimumCount { get; }
        }

        readonly struct ContentSpec
        {
            public ContentSpec(
                CapabilityHostData asset,
                long weight,
                long minimumPatternCount,
                float guaranteedCellShare)
            {
                Asset = asset;
                Weight = weight;
                MinimumPatternCount = minimumPatternCount;
                GuaranteedCellShare = guaranteedCellShare;
            }

            public CapabilityHostData Asset { get; }
            public long Weight { get; }
            public long MinimumPatternCount { get; }
            public float GuaranteedCellShare { get; }
        }
    }
}
