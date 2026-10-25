using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class GridPathfinder
    {
        public const int MaxRequestsPerTick = 2;
        public const float SlopeFreeDeg = 10.0f;
        public const float SlopeStepDeg = 5.0f;
        public const float SlopeStepCost = 0.5f;
        public const int SimplifyLookahead = 40;
        public const double SimplifyWeightTolerance = 0.05;
        public const float ThreatMax = 4.0f;

        public static readonly string[] Sides = { "blue", "red" };
        public static readonly string[] Mobilities = { Terrain.MobilityVehicle, Terrain.MobilityFoot };

        private static readonly double[] WeightVehicle = { 1.0, 0.6, 1.8, 1.5, 0.0 };
        private static readonly double[] WeightFoot = { 1.0, 0.8, 1.2, 1.0, 0.0 };

        public sealed class PathRequest
        {
            public SimVec2 From { get; internal set; }
            public SimVec2 To { get; internal set; }
            public string Mobility { get; internal set; } = "";
            public string Side { get; internal set; } = "";
            public SimVec2[] Points { get; internal set; } = Array.Empty<SimVec2>();
            public bool Done { get; internal set; }
            public bool Ok { get; internal set; }
            public bool Cancelled { get; set; }

            public void Cancel()
            {
                Cancelled = true;
            }
        }

        private sealed class InsertionOrderSet
        {
            private readonly HashSet<int> _members = new HashSet<int>();
            private readonly List<int> _order = new List<int>();

            internal void Add(int value)
            {
                if (_members.Add(value))
                {
                    _order.Add(value);
                }
            }

            internal void Remove(int value)
            {
                if (_members.Remove(value))
                {
                    _order.Remove(value);
                }
            }

            internal int[] Snapshot()
            {
                return _order.ToArray();
            }
        }

        private readonly double _cellM;
        private readonly Dictionary<string, AStarGrid2D> _grids = new Dictionary<string, AStarGrid2D>();
        private readonly Dictionary<string, float[]> _baseWeights = new Dictionary<string, float[]>();
        private readonly Dictionary<string, float[]> _threat = new Dictionary<string, float[]>();
        private readonly Dictionary<string, InsertionOrderSet> _threatCells = new Dictionary<string, InsertionOrderSet>();
        private readonly Queue<PathRequest> _queue = new Queue<PathRequest>();

        public GridPathfinder(Terrain terrain)
        {
            Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _cellM = terrain.CellM;
            foreach (string mobility in Mobilities)
            {
                _baseWeights[mobility] = ComputeBaseWeights(mobility);
            }

            foreach (string side in Sides)
            {
                _threat[side] = new float[terrain.Width * terrain.HeightCells];
                _threatCells[side] = new InsertionOrderSet();
            }
        }

        public Terrain Terrain { get; }
        public int RequestsProcessed { get; private set; }

        public PathRequest Request(SimVec2 from, SimVec2 to, string mobility, string side)
        {
            var request = new PathRequest
            {
                From = from,
                To = to,
                Mobility = mobility,
                Side = NormalizeSide(side),
            };
            if (mobility == Terrain.MobilityAir)
            {
                request.Points = FindPathNow(from, to, mobility, side);
                request.Ok = request.Points.Length >= 2;
                request.Done = true;
                return request;
            }

            _queue.Enqueue(request);
            return request;
        }

        public int PendingCount()
        {
            return _queue.Count;
        }

        public int Process(int budget = MaxRequestsPerTick)
        {
            int processed = 0;
            while (processed < budget && _queue.Count != 0)
            {
                PathRequest request = _queue.Dequeue();
                if (request.Cancelled)
                {
                    continue;
                }

                request.Points = FindPathNow(request.From, request.To, request.Mobility, request.Side);
                request.Ok = request.Points.Length >= 2;
                request.Done = true;
                processed++;
                RequestsProcessed++;
            }

            return processed;
        }

        public SimVec2[] FindPathNow(SimVec2 from, SimVec2 to, string mobility, string side = "blue")
        {
            if (mobility == Terrain.MobilityAir)
            {
                return new[] { from, Terrain.ClampToMap(to, 20.0) };
            }

            AStarGrid2D grid = GetGrid(NormalizeSide(side), mobility);
            SimVec2 start = Terrain.NearestPassable(from, mobility);
            SimVec2 goal = Terrain.NearestPassable(to, mobility);
            SimVec2I fromCell = Terrain.CellOf(start);
            SimVec2I toCell = Terrain.CellOf(goal);
            if (!grid.IsInBounds(fromCell) || !grid.IsInBounds(toCell))
            {
                return Array.Empty<SimVec2>();
            }

            if (fromCell == toCell)
            {
                return new[] { from, goal };
            }

            SimVec2[] raw = grid.GetPointPath(fromCell, toCell, true);
            if (raw.Length < 2)
            {
                return Array.Empty<SimVec2>();
            }

            raw[0] = start;
            if (Terrain.CellOf(raw[raw.Length - 1]) == toCell)
            {
                raw[raw.Length - 1] = goal;
            }

            SimVec2[] simplified = Simplify(raw, mobility);
            if (start.DistanceTo(from) > 0.5 && Terrain.IsPassable(from, mobility))
            {
                simplified[0] = from;
            }

            return simplified;
        }

        public SimVec2[] Simplify(SimVec2[] points, string mobility)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            int count = points.Length;
            if (count <= 2)
            {
                return points;
            }

            float[] weights = _baseWeights[mobility];
            var output = new List<SimVec2> { points[0] };
            int i = 0;
            var weightAtPoints = new float[count];
            for (int k = 0; k < count; k++)
            {
                weightAtPoints[k] = WeightAt(weights, points[k]);
            }

            var prefixMax = new float[SimplifyLookahead + 1];
            while (i < count - 1)
            {
                int j = Math.Min(count - 1, i + SimplifyLookahead);
                float running = 0.0f;
                for (int k = i; k <= j; k++)
                {
                    running = MathF.Max(running, weightAtPoints[k]);
                    prefixMax[k - i] = running;
                }

                while (j > i + 1)
                {
                    if (SegmentOk(points[i], points[j], mobility, weights,
                        prefixMax[j - i] + (double)SimplifyWeightTolerance))
                    {
                        break;
                    }

                    j--;
                }

                output.Add(points[j]);
                i = j;
            }

            return output.ToArray();
        }

        public void AddThreat(string side, SimVec2 position, double radiusM, double amount)
        {
            if (!IsSide(side))
            {
                return;
            }

            float[] threat = _threat[side];
            InsertionOrderSet cells = _threatCells[side];
            SimVec2I center = Terrain.CellOf(position);
            int cellRadius = (int)Math.Ceiling(radiusM / _cellM);
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    var cell = new SimVec2I(center.X + dx, center.Y + dy);
                    if (!Terrain.InBounds(cell) || new SimVec2(dx, dy).Length() * _cellM > radiusM)
                    {
                        continue;
                    }

                    int index = Terrain.IndexOf(cell);
                    threat[index] = (float)Math.Min(ThreatMax, threat[index] + amount);
                    cells.Add(index);
                }
            }

            ApplyCells(side, cells.Snapshot());
        }

        public void DecayThreat(double factor)
        {
            foreach (string side in Sides)
            {
                float[] threat = _threat[side];
                InsertionOrderSet cells = _threatCells[side];
                int[] keys = cells.Snapshot();
                foreach (int index in keys)
                {
                    threat[index] = (float)(threat[index] * factor);
                    if (threat[index] < 0.05)
                    {
                        threat[index] = 0.0f;
                        cells.Remove(index);
                    }
                }

                ApplyCells(side, keys);
            }
        }

        public float ThreatAt(string side, SimVec2 position)
        {
            SimVec2I cell = Terrain.CellOf(position);
            if (!Terrain.InBounds(cell) || !_threat.TryGetValue(side, out float[]? threat))
            {
                return 0.0f;
            }

            return threat[Terrain.IndexOf(cell)];
        }

        private AStarGrid2D GetGrid(string side, string mobility)
        {
            string key = Key(side, mobility);
            if (!_grids.TryGetValue(key, out AStarGrid2D? grid))
            {
                grid = MakeGrid(mobility);
                _grids[key] = grid;
                float[] threat = _threat[side];
                float[] weights = _baseWeights[mobility];
                foreach (int index in _threatCells[side].Snapshot())
                {
                    if (weights[index] > 0.0f)
                    {
                        grid.SetPointWeightScale(CellOfIndex(index), weights[index] + threat[index]);
                    }
                }
            }

            return grid;
        }

        private float[] ComputeBaseWeights(string mobility)
        {
            double[] table = mobility == Terrain.MobilityVehicle ? WeightVehicle : WeightFoot;
            var output = new float[Terrain.Width * Terrain.HeightCells];
            for (int y = 0; y < Terrain.HeightCells; y++)
            {
                for (int x = 0; x < Terrain.Width; x++)
                {
                    var cell = new SimVec2I(x, y);
                    int index = Terrain.IndexOf(cell);
                    if (!Terrain.IsPassableCell(cell, mobility))
                    {
                        output[index] = 0.0f;
                        continue;
                    }

                    int type = Terrain.Types[index];
                    double weight = table[type];
                    double slope = Terrain.Slopes[index];
                    if (type != (int)Terrain.Type.ROAD && slope > SlopeFreeDeg)
                    {
                        weight += SlopeStepCost * Math.Ceiling((slope - SlopeFreeDeg) / SlopeStepDeg);
                    }

                    output[index] = (float)weight;
                }
            }

            return output;
        }

        private AStarGrid2D MakeGrid(string mobility)
        {
            var grid = new AStarGrid2D
            {
                CellSize = new SimVec2(_cellM, _cellM),
                Offset = new SimVec2(_cellM * 0.5f, _cellM * 0.5f),
                DiagonalMode = AStarGrid2D.DiagonalModeKind.OnlyIfNoObstacles,
                DefaultComputeHeuristic = AStarGrid2D.Heuristic.Octile,
                DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Octile,
            };
            grid.SetRegion(0, 0, Terrain.Width, Terrain.HeightCells);
            grid.Update();
            float[] weights = _baseWeights[mobility];
            for (int y = 0; y < Terrain.HeightCells; y++)
            {
                for (int x = 0; x < Terrain.Width; x++)
                {
                    float weight = weights[y * Terrain.Width + x];
                    var cell = new SimVec2I(x, y);
                    if (weight <= 0.0f)
                    {
                        grid.SetPointSolid(cell);
                    }
                    else if (weight != 1.0f)
                    {
                        grid.SetPointWeightScale(cell, weight);
                    }
                }
            }

            return grid;
        }

        private void ApplyCells(string side, int[] cellIndices)
        {
            float[] threat = _threat[side];
            foreach (string mobility in Mobilities)
            {
                if (!_grids.TryGetValue(Key(side, mobility), out AStarGrid2D? grid))
                {
                    continue;
                }

                float[] weights = _baseWeights[mobility];
                foreach (int index in cellIndices)
                {
                    if (weights[index] <= 0.0f)
                    {
                        continue;
                    }

                    grid.SetPointWeightScale(CellOfIndex(index), weights[index] + threat[index]);
                }
            }
        }

        private float WeightAt(float[] weights, SimVec2 point)
        {
            SimVec2I cell = Terrain.CellOf(point);
            return Terrain.InBounds(cell) ? weights[Terrain.IndexOf(cell)] : 0.0f;
        }

        private bool SegmentOk(SimVec2 a, SimVec2 b, string mobility, float[] weights, double maxWeight)
        {
            double distance = a.DistanceTo(b);
            int steps = Math.Max(1, (int)Math.Ceiling(distance / (_cellM * 0.5)));
            for (int sample = 0; sample <= steps; sample++)
            {
                SimVec2 point = a.Lerp(b, (double)sample / steps);
                if (!Terrain.IsPassable(point, mobility) || WeightAt(weights, point) > maxWeight)
                {
                    return false;
                }
            }

            return true;
        }

        private SimVec2I CellOfIndex(int index)
        {
            return new SimVec2I(index % Terrain.Width, index / Terrain.Width);
        }

        private static string Key(string side, string mobility)
        {
            return side + ":" + mobility;
        }

        private static string NormalizeSide(string side)
        {
            return IsSide(side) ? side : "blue";
        }

        private static bool IsSide(string side)
        {
            return side == Sides[0] || side == Sides[1];
        }
    }
}
