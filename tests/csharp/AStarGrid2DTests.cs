using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class AStarGrid2DTests
    {
        [Test]
        public void DiagonalOnlyIfNoObstaclesRejectsCornerCutting()
        {
            AStarGrid2D grid = CreateGrid(2, 2);
            grid.SetPointSolid(new SimVec2I(1, 0));
            grid.SetPointSolid(new SimVec2I(0, 1));

            Assert.That(grid.GetIdPath(new SimVec2I(0, 0), new SimVec2I(1, 1)), Is.Empty);
            Assert.That(grid.GetIdPath(new SimVec2I(0, 0), new SimVec2I(1, 1), true),
                Is.EqualTo(new[] { new SimVec2I(0, 0) }));
        }

        [Test]
        public void WeightScaleAppliesToTheDestinationCell()
        {
            AStarGrid2D grid = CreateGrid(3, 2);
            grid.SetPointWeightScale(new SimVec2I(1, 0), 100.0f);

            SimVec2I[] actual = grid.GetIdPath(new SimVec2I(0, 0), new SimVec2I(2, 0));
            SimVec2I[] expected =
            {
                new SimVec2I(0, 0), new SimVec2I(1, 1), new SimVec2I(2, 0),
            };
            AssertCells(actual, expected);
        }

        [Test]
        public void PartialPathUsesGodotClosestPointRules()
        {
            AStarGrid2D grid = CreateGrid(5, 3);
            grid.SetPointSolid(new SimVec2I(3, 0));
            grid.SetPointSolid(new SimVec2I(3, 1));
            grid.SetPointSolid(new SimVec2I(3, 2));

            SimVec2I[] expected = { new SimVec2I(0, 1), new SimVec2I(1, 1), new SimVec2I(2, 1) };
            Assert.That(grid.GetIdPath(new SimVec2I(0, 1), new SimVec2I(4, 1)), Is.Empty);
            AssertCells(grid.GetIdPath(new SimVec2I(0, 1), new SimVec2I(4, 1), true), expected);
            grid.SetPointSolid(new SimVec2I(4, 1));
            Assert.That(grid.GetIdPath(new SimVec2I(0, 1), new SimVec2I(4, 1)), Is.Empty);
            AssertCells(grid.GetIdPath(new SimVec2I(0, 1), new SimVec2I(4, 1), true), expected);
        }

        [Test]
        public void SolidStartIsEmptyEvenWhenStartEqualsGoal()
        {
            AStarGrid2D grid = CreateGrid(1, 1);
            grid.SetPointSolid(new SimVec2I(0, 0));

            Assert.That(grid.GetIdPath(new SimVec2I(0, 0), new SimVec2I(0, 0), true), Is.Empty);
        }

        [Test]
        public void PointPositionsUseAbsoluteCellIdsCellSizeAndOffset()
        {
            var grid = new AStarGrid2D
            {
                CellSize = new SimVec2(10.0f, 20.0f),
                Offset = new SimVec2(5.0f, 7.0f),
            };
            grid.SetRegion(2, 3, 2, 2);
            grid.Update();

            Assert.That(grid.GetPointPosition(new SimVec2I(2, 3)), Is.EqualTo(new SimVec2(25.0f, 67.0f)));
            AssertCells(grid.GetIdPath(new SimVec2I(2, 3), new SimVec2I(3, 4)),
                new[] { new SimVec2I(2, 3), new SimVec2I(3, 4) });
        }

        [Test]
        public void UpdateClearsSolidityAndWeights()
        {
            AStarGrid2D grid = CreateGrid(2, 1);
            grid.SetPointSolid(new SimVec2I(1, 0));
            grid.SetPointWeightScale(new SimVec2I(0, 0), 3.0f);
            grid.SetRegion(0, 0, 2, 1);
            grid.SetSize(new SimVec2I(2, 1));
            grid.Offset = new SimVec2(1.0f, 0.0f);
            grid.Update();

            Assert.That(grid.IsPointSolid(new SimVec2I(1, 0)), Is.False);
            Assert.That(grid.GetPointWeightScale(new SimVec2I(0, 0)), Is.EqualTo(1.0f));
        }

        private static AStarGrid2D CreateGrid(int width, int height)
        {
            var grid = new AStarGrid2D
            {
                DiagonalMode = AStarGrid2D.DiagonalModeKind.OnlyIfNoObstacles,
                DefaultComputeHeuristic = AStarGrid2D.Heuristic.Octile,
                DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile,
            };
            grid.SetRegion(0, 0, width, height);
            grid.Update();
            return grid;
        }

        internal static void AssertCells(SimVec2I[] actual, SimVec2I[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length), "Path length");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]), $"Cell {i}");
            }
        }
    }

    [TestFixture]
    public sealed class AStarGoldenTests
    {
        private static readonly double[] VehicleWeights = { 1.0, 0.6, 1.8, 1.5, 0.0 };
        private static readonly double[] FootWeights = { 1.0, 0.8, 1.2, 1.0, 0.0 };

        private JsonDocument _golden = null!;
        private byte[] _types = null!;
        private byte[] _passableVehicle = null!;
        private byte[] _passableFoot = null!;
        private float[] _slopes = null!;
        private int _width;
        private int _height;
        private float _cellSize;

        [OneTimeSetUp]
        public void LoadOracle()
        {
            string repositoryRoot = FindRepositoryRoot();
            string goldenPath = Path.Combine(repositoryRoot, "tests", "golden", "map01.json");
            _golden = JsonDocument.Parse(File.ReadAllText(goldenPath));
            JsonElement root = _golden.RootElement;
            _width = root.GetProperty("width").GetInt32();
            _height = root.GetProperty("height").GetInt32();
            _cellSize = root.GetProperty("cell_m").GetSingle();
            string oracle = Path.Combine(repositoryRoot, "build", "unity_oracle");
            _types = File.ReadAllBytes(Path.Combine(oracle, "types.bin"));
            _slopes = ReadFloats(Path.Combine(oracle, "slopes.bin"));
            _passableVehicle = File.ReadAllBytes(Path.Combine(oracle, "passable_vehicle.bin"));
            _passableFoot = File.ReadAllBytes(Path.Combine(oracle, "passable_foot.bin"));
            Assert.That(_types.Length, Is.EqualTo(_width * _height));
            Assert.That(_slopes.Length, Is.EqualTo(_width * _height));
        }

        [OneTimeTearDown]
        public void DisposeOracle()
        {
            _golden.Dispose();
        }

        [TestCase("river_bridge", false)]
        [TestCase("open_simplify", false)]
        [TestCase("road_forest", false)]
        [TestCase("foot_path", false)]
        [TestCase("threat_before", false)]
        [TestCase("threat_after", true)]
        [TestCase("threat_other_side", false)]
        public void RawCellsAndPointsMatchGodotExactly(string caseName, bool applyThreat)
        {
            JsonElement pathCase = FindPathCase(caseName);
            string mobility = pathCase.GetProperty("mobility").GetString()!;
            AStarGrid2D grid = CreateOracleGrid(mobility, applyThreat);
            SimVec2I from = ReadCell(pathCase.GetProperty("from"));
            SimVec2I to = ReadCell(pathCase.GetProperty("to"));
            SimVec2I[] expectedCells = ReadCells(pathCase.GetProperty("raw_cells"));
            SimVec2[] expectedPoints = ReadPoints(pathCase.GetProperty("raw_points"));

            SimVec2I[] actualCells = grid.GetIdPath(from, to, true);
            SimVec2[] actualPoints = grid.GetPointPath(from, to, true);

            AStarGrid2DTests.AssertCells(actualCells, expectedCells);
            Assert.That(actualPoints.Length, Is.EqualTo(expectedPoints.Length), "Point path length");
            for (int i = 0; i < expectedPoints.Length; i++)
            {
                Assert.That(actualPoints[i], Is.EqualTo(expectedPoints[i]), $"Point {i}");
            }
        }

        private AStarGrid2D CreateOracleGrid(string mobility, bool applyThreat)
        {
            var grid = new AStarGrid2D
            {
                CellSize = new SimVec2(_cellSize, _cellSize),
                Offset = new SimVec2(_cellSize * 0.5f, _cellSize * 0.5f),
                DiagonalMode = AStarGrid2D.DiagonalModeKind.OnlyIfNoObstacles,
                DefaultComputeHeuristic = AStarGrid2D.Heuristic.Octile,
                DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile,
            };
            grid.SetRegion(0, 0, _width, _height);
            grid.Update();

            double[] table = mobility == "vehicle" ? VehicleWeights : FootWeights;
            byte[] passable = mobility == "vehicle" ? _passableVehicle : _passableFoot;
            var baseWeights = new float[_width * _height];
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int index = y * _width + x;
                    var cell = new SimVec2I(x, y);
                    if (passable[index] != 1)
                    {
                        grid.SetPointSolid(cell);
                        continue;
                    }

                    int type = _types[index];
                    double weight = table[type];
                    double slope = _slopes[index];
                    if (type != 1 && slope > 10.0)
                    {
                        weight += 0.5 * Math.Ceiling((slope - 10.0) / 5.0);
                    }

                    baseWeights[index] = (float)weight;
                    if (weight != 1.0)
                    {
                        grid.SetPointWeightScale(cell, (float)weight);
                    }
                }
            }

            if (applyThreat)
            {
                var threat = new float[baseWeights.Length];
                var center = new SimVec2I(310, 340);
                const int radiusCells = 20;
                for (int dy = -radiusCells; dy <= radiusCells; dy++)
                {
                    for (int dx = -radiusCells; dx <= radiusCells; dx++)
                    {
                        int x = center.X + dx;
                        int y = center.Y + dy;
                        if (x < 0 || y < 0 || x >= _width || y >= _height ||
                            MathF.Sqrt(dx * dx + dy * dy) * _cellSize > 200.0f)
                        {
                            continue;
                        }

                        int index = y * _width + x;
                        threat[index] = MathF.Min(4.0f, threat[index] + 4.0f);
                    }
                }

                for (int y = 0; y < _height; y++)
                {
                    for (int x = 0; x < _width; x++)
                    {
                        int index = y * _width + x;
                        if (threat[index] != 0.0f && baseWeights[index] > 0.0f)
                        {
                            grid.SetPointWeightScale(new SimVec2I(x, y), baseWeights[index] + threat[index]);
                        }
                    }
                }
            }

            return grid;
        }

        private JsonElement FindPathCase(string name)
        {
            foreach (JsonElement pathCase in _golden.RootElement.GetProperty("path_cases").EnumerateArray())
            {
                if (pathCase.GetProperty("name").GetString() == name)
                {
                    return pathCase;
                }
            }

            throw new AssertionException($"Missing golden path case {name}.");
        }

        private SimVec2I ReadCell(JsonElement point)
        {
            JsonElement.ArrayEnumerator values = point.EnumerateArray();
            values.MoveNext();
            float x = values.Current.GetSingle();
            values.MoveNext();
            float y = values.Current.GetSingle();
            return new SimVec2I((int)MathF.Floor(x / _cellSize), (int)MathF.Floor(y / _cellSize));
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
                JsonElement.ArrayEnumerator values = point.EnumerateArray();
                values.MoveNext();
                float x = values.Current.GetSingle();
                values.MoveNext();
                float y = values.Current.GetSingle();
                points.Add(new SimVec2(x, y));
            }

            return points.ToArray();
        }

        private static float[] ReadFloats(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length % 4, Is.Zero);
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        private static string FindRepositoryRoot()
        {
            var starts = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
            foreach (string start in starts)
            {
                var directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "tests", "golden", "map01.json")) &&
                        File.Exists(Path.Combine(directory.FullName, "build", "unity_oracle", "types.bin")))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new AssertionException("Could not locate the repository and Unity oracle files.");
        }
    }
}
