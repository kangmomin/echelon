using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class PathfindingBehaviorTests
    {
        [Test]
        public void QueueIsFifoBudgetedAndSkipsCancelledRequests()
        {
            var pathfinder = new GridPathfinder(CreateFlatTerrain(1000.0f));
            var requests = new List<GridPathfinder.PathRequest>();
            for (int i = 0; i < 6; i++)
            {
                requests.Add(pathfinder.Request(new SimVec2(100.0f, 100.0f),
                    new SimVec2(200.0f + i * 10.0f, 200.0f), Terrain.MobilityVehicle, "unknown"));
            }

            Assert.That(requests[0].Side, Is.EqualTo("blue"));
            Assert.That(pathfinder.PendingCount(), Is.EqualTo(6));
            Assert.That(pathfinder.Process(), Is.EqualTo(GridPathfinder.MaxRequestsPerTick));
            Assert.That(requests[0].Done && requests[0].Ok, Is.True);
            Assert.That(requests[1].Done && requests[1].Ok, Is.True);
            Assert.That(requests[2].Done, Is.False);
            Assert.That(pathfinder.RequestsProcessed, Is.EqualTo(2));

            requests[2].Cancel();
            Assert.That(pathfinder.Process(), Is.EqualTo(2));
            Assert.That(requests[2].Cancelled, Is.True);
            Assert.That(requests[2].Done, Is.False);
            Assert.That(requests[3].Done && requests[4].Done, Is.True);
            Assert.That(requests[5].Done, Is.False);
            Assert.That(pathfinder.PendingCount(), Is.EqualTo(1));
            Assert.That(pathfinder.Process(), Is.EqualTo(1));
            Assert.That(requests[5].Done && requests[5].Ok, Is.True);
            Assert.That(pathfinder.RequestsProcessed, Is.EqualTo(5));
        }

        [Test]
        public void AirRequestsBypassQueueAndClampOnlyTheDestination()
        {
            var pathfinder = new GridPathfinder(CreateFlatTerrain(1000.0f));
            var from = new SimVec2(-5.0f, -5.0f);
            GridPathfinder.PathRequest request = pathfinder.Request(from, new SimVec2(2000.0f, -10.0f),
                Terrain.MobilityAir, "red");

            Assert.That(pathfinder.PendingCount(), Is.Zero);
            Assert.That(request.Done && request.Ok, Is.True);
            Assert.That(request.Points, Is.EqualTo(new[] { from, new SimVec2(980.0f, 20.0f) }));
        }

        [Test]
        public void SameCellReturnsOriginalStartAndNearestGoal()
        {
            var pathfinder = new GridPathfinder(CreateFlatTerrain(200.0f));
            var from = new SimVec2(11.0f, 11.0f);
            var to = new SimVec2(19.0f, 19.0f);

            Assert.That(pathfinder.FindPathNow(from, to, Terrain.MobilityVehicle),
                Is.EqualTo(new[] { from, to }));
        }

        [Test]
        public void ImpassableEndpointsUseNearestPassablePoints()
        {
            Terrain terrain = CreateTerrainWithRiver();
            var pathfinder = new GridPathfinder(terrain);
            var inWater = new SimVec2(96.0f, 100.0f);
            var to = new SimVec2(20.0f, 100.0f);
            SimVec2 nearest = terrain.NearestPassable(inWater, Terrain.MobilityVehicle);

            SimVec2[] path = pathfinder.FindPathNow(inWater, to, Terrain.MobilityVehicle);

            Assert.That(path.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(nearest, Is.Not.EqualTo(inWater));
            Assert.That(terrain.IsPassable(nearest, Terrain.MobilityVehicle), Is.True);
            Assert.That(path[0], Is.EqualTo(nearest));
            Assert.That(path[path.Length - 1], Is.EqualTo(to));
        }

        [Test]
        public void ThreatAccumulatesCapsDecaysAndIsSideSpecific()
        {
            var pathfinder = new GridPathfinder(CreateFlatTerrain(200.0f));
            var center = new SimVec2(100.0f, 100.0f);
            pathfinder.AddThreat("red", center, 20.0, 2.0);
            pathfinder.AddThreat("red", center, 20.0, 3.0);
            pathfinder.AddThreat("red", center, 20.0, 1.0);
            pathfinder.AddThreat("invalid", new SimVec2(20.0f, 20.0f), 20.0, 4.0);

            Assert.That(pathfinder.ThreatAt("red", center), Is.EqualTo(4.0f));
            Assert.That(pathfinder.ThreatAt("blue", center), Is.Zero);
            Assert.That(pathfinder.ThreatAt("blue", new SimVec2(20.0f, 20.0f)), Is.Zero);
            pathfinder.DecayThreat(0.5);
            Assert.That(pathfinder.ThreatAt("red", center), Is.EqualTo(2.0f));
            pathfinder.DecayThreat(0.0);
            Assert.That(pathfinder.ThreatAt("red", center), Is.Zero);
        }

        private static Terrain CreateFlatTerrain(float size)
        {
            return Terrain.Build(new MapDef
            {
                Id = "flat",
                SizeM = new SimVec2(size, size),
                CellM = 10.0,
                BaseM = 0.0,
                AmplitudeM = 0.0,
                Frequency = 0.001,
                Octaves = 1,
            });
        }

        private static Terrain CreateTerrainWithRiver()
        {
            var map = new MapDef
            {
                Id = "river",
                SizeM = new SimVec2(200.0f, 200.0f),
                CellM = 10.0,
                BaseM = 0.0,
                AmplitudeM = 0.0,
                Frequency = 0.001,
                Octaves = 1,
            };
            var river = new MapDef.Feature { Name = "river", WidthM = 20.0 };
            river.Points.Add(new SimVec2(100.0f, 0.0f));
            river.Points.Add(new SimVec2(100.0f, 200.0f));
            map.Rivers.Add(river);
            return Terrain.Build(map);
        }
    }

    [TestFixture]
    public sealed class PathfindingGoldenTests
    {
        private static readonly MethodInfo GetGridMethod = typeof(GridPathfinder).GetMethod("GetGrid",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        private JsonDocument _golden = null!;
        private Terrain _terrain = null!;
        private GridPathfinder _pathfinder = null!;
        private float _cellSize;

        [OneTimeSetUp]
        public void BuildTerrainAndPathfinder()
        {
            _golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(DataTestAssets.GoldenRoot, "map01.json")));
            _cellSize = _golden.RootElement.GetProperty("cell_m").GetSingle();
            DataCatalog catalog = DataLoader.LoadCatalog(DataTestAssets.DataRoot);
            Assert.That(catalog.IsValid(), Is.True, string.Join("\n", catalog.Errors));
            MapDef? map = catalog.GetMap("map01");
            Assert.That(map, Is.Not.Null);
            _terrain = Terrain.Build(map!);
            _pathfinder = new GridPathfinder(_terrain);
        }

        [OneTimeTearDown]
        public void DisposeGolden()
        {
            _golden.Dispose();
        }

        [Test]
        public void RawAndSimplifiedPathsMatchGodotExactly()
        {
            foreach (JsonElement pathCase in _golden.RootElement.GetProperty("path_cases").EnumerateArray())
            {
                string name = pathCase.GetProperty("name").GetString()!;
                string mobility = pathCase.GetProperty("mobility").GetString()!;
                string side = pathCase.GetProperty("side").GetString()!;
                SimVec2 from = ReadPoint(pathCase.GetProperty("from"));
                SimVec2 to = ReadPoint(pathCase.GetProperty("to"));
                if (name == "threat_after")
                {
                    _pathfinder.AddThreat("red", new SimVec2(3100.0f, 3400.0f), 200.0, 4.0);
                }

                var grid = (AStarGrid2D)GetGridMethod.Invoke(_pathfinder, new object[] { side, mobility })!;
                SimVec2 start = _terrain.NearestPassable(from, mobility);
                SimVec2 goal = _terrain.NearestPassable(to, mobility);
                SimVec2I[] actualCells = grid.GetIdPath(_terrain.CellOf(start), _terrain.CellOf(goal), true);
                SimVec2[] actualRaw = grid.GetPointPath(_terrain.CellOf(start), _terrain.CellOf(goal), true);
                SimVec2[] actualSimplified = _pathfinder.FindPathNow(from, to, mobility, side);

                AssertCells(actualCells, ReadCells(pathCase.GetProperty("raw_cells")), name);
                AssertPoints(actualRaw, ReadPoints(pathCase.GetProperty("raw_points")), name + " raw");
                AssertPoints(actualSimplified, ReadPoints(pathCase.GetProperty("simplified")), name + " simplified");
            }

            var threatCell = new SimVec2I(310, 340);
            var redFoot = (AStarGrid2D)GetGridMethod.Invoke(_pathfinder,
                new object[] { "red", Terrain.MobilityFoot })!;
            var blueFoot = (AStarGrid2D)GetGridMethod.Invoke(_pathfinder,
                new object[] { "blue", Terrain.MobilityFoot })!;
            Assert.That(redFoot.GetPointWeightScale(threatCell), Is.EqualTo(5.0f), "Lazy grid inherits threat");
            Assert.That(blueFoot.GetPointWeightScale(threatCell), Is.EqualTo(1.0f), "Threat remains side-specific");
            _pathfinder.DecayThreat(0.0);
            Assert.That(redFoot.GetPointWeightScale(threatCell), Is.EqualTo(1.0f), "Decay resets an existing grid");
        }

        private SimVec2I ReadCell(JsonElement point)
        {
            SimVec2 value = ReadPoint(point);
            return new SimVec2I((int)MathF.Floor(value.X / _cellSize), (int)MathF.Floor(value.Y / _cellSize));
        }

        private static SimVec2 ReadPoint(JsonElement point)
        {
            JsonElement.ArrayEnumerator values = point.EnumerateArray();
            values.MoveNext();
            float x = values.Current.GetSingle();
            values.MoveNext();
            float y = values.Current.GetSingle();
            return new SimVec2(x, y);
        }

        private static SimVec2I[] ReadCells(JsonElement array)
        {
            var cells = new List<SimVec2I>();
            foreach (JsonElement cell in array.EnumerateArray())
            {
                JsonElement.ArrayEnumerator values = cell.EnumerateArray();
                values.MoveNext();
                int x = values.Current.GetInt32();
                values.MoveNext();
                int y = values.Current.GetInt32();
                cells.Add(new SimVec2I(x, y));
            }

            return cells.ToArray();
        }

        private static SimVec2[] ReadPoints(JsonElement array)
        {
            var points = new List<SimVec2>();
            foreach (JsonElement point in array.EnumerateArray())
            {
                points.Add(ReadPoint(point));
            }

            return points.ToArray();
        }

        private static void AssertCells(SimVec2I[] actual, SimVec2I[] expected, string context)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), context + " cell count");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"{context} cell {i}");
            }
        }

        private static void AssertPoints(SimVec2[] actual, SimVec2[] expected, string context)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), context + " point count");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"{context} point {i}");
            }
        }
    }
}
