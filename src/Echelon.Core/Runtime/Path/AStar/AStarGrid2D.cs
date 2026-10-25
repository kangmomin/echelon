using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class AStarGrid2D
    {
        public enum DiagonalModeKind
        {
            Always = 0,
            Never = 1,
            AtLeastOneWalkable = 2,
            OnlyIfNoObstacles = 3,
        }

        public enum Heuristic
        {
            Euclidean = 0,
            Manhattan = 1,
            Octile = 2,
            Chebyshev = 3,
        }

        private sealed class Point
        {
            internal readonly SimVec2I Id;
            internal float WeightScale = 1.0f;
            internal Point? Previous;
            internal float GScore;
            internal float FScore;
            internal ulong OpenPass;
            internal ulong ClosedPass;
            internal float AbsGScore;
            internal float AbsFScore;

            internal Point(SimVec2I id)
            {
                Id = id;
            }
        }

        private static readonly float OctileFactor = (float)(1.4142135623730950488016887242 - 1.0);

        private readonly List<Point> _openList = new List<Point>();
        private readonly List<Point> _neighbors = new List<Point>(8);
        private Point[] _points = Array.Empty<Point>();
        private bool[] _solid = Array.Empty<bool>();
        private Point? _lastClosestPoint;
        private ulong _pass = 1;
        private SimVec2 _offset = SimVec2.Zero;
        private SimVec2 _cellSize = SimVec2.One;

        public SimVec2I RegionPosition { get; private set; } = SimVec2I.Zero;
        public SimVec2I RegionSize { get; private set; } = SimVec2I.Zero;
        public DiagonalModeKind DiagonalMode { get; set; } = DiagonalModeKind.Always;
        public Heuristic DefaultComputeHeuristic { get; set; } = Heuristic.Euclidean;
        public Heuristic DefaultEstimateHeuristic { get; set; } = Heuristic.Euclidean;
        public bool IsDirty { get; private set; }

        public SimVec2 Offset
        {
            get => _offset;
            set
            {
                if (!_offset.IsEqualApprox(value))
                {
                    _offset = value;
                    IsDirty = true;
                }
            }
        }

        public SimVec2 CellSize
        {
            get => _cellSize;
            set
            {
                if (!_cellSize.IsEqualApprox(value))
                {
                    _cellSize = value;
                    IsDirty = true;
                }
            }
        }

        public void SetRegion(SimVec2I position, SimVec2I size)
        {
            ValidateRegionSize(size);
            if (RegionPosition != position || RegionSize != size)
            {
                RegionPosition = position;
                RegionSize = size;
                IsDirty = true;
            }
        }

        public void SetRegion(int x, int y, int width, int height)
        {
            SetRegion(new SimVec2I(x, y), new SimVec2I(width, height));
        }

        public void SetSize(SimVec2I size)
        {
            SetRegion(RegionPosition, size);
        }

        public void Update()
        {
            if (!IsDirty)
            {
                return;
            }

            int count = GetCellCount();
            _points = new Point[count];
            _solid = new bool[count];
            int index = 0;
            for (int y = 0; y < RegionSize.Y; y++)
            {
                for (int x = 0; x < RegionSize.X; x++)
                {
                    _points[index++] = new Point(new SimVec2I(RegionPosition.X + x, RegionPosition.Y + y));
                }
            }

            _openList.Clear();
            _neighbors.Clear();
            _lastClosestPoint = null;
            IsDirty = false;
        }

        public void Clear()
        {
            _points = Array.Empty<Point>();
            _solid = Array.Empty<bool>();
            RegionPosition = SimVec2I.Zero;
            RegionSize = SimVec2I.Zero;
            _openList.Clear();
            _lastClosestPoint = null;
        }

        public bool IsInBounds(int x, int y)
        {
            return x >= RegionPosition.X && y >= RegionPosition.Y &&
                x < RegionPosition.X + RegionSize.X && y < RegionPosition.Y + RegionSize.Y;
        }

        public bool IsInBounds(SimVec2I id)
        {
            return IsInBounds(id.X, id.Y);
        }

        public void SetPointSolid(SimVec2I id, bool solid = true)
        {
            int index = GetWritablePointIndex(id);
            _solid[index] = solid;
        }

        public bool IsPointSolid(SimVec2I id)
        {
            return _solid[GetWritablePointIndex(id)];
        }

        public void SetPointWeightScale(SimVec2I id, float weightScale)
        {
            if (weightScale < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(weightScale), "Weight scale cannot be negative.");
            }

            _points[GetWritablePointIndex(id)].WeightScale = weightScale;
        }

        public float GetPointWeightScale(SimVec2I id)
        {
            return _points[GetWritablePointIndex(id)].WeightScale;
        }

        public SimVec2 GetPointPosition(SimVec2I id)
        {
            GetWritablePointIndex(id);
            return Offset + id.ToSimVec2() * CellSize;
        }

        public SimVec2I[] GetIdPath(SimVec2I fromId, SimVec2I toId, bool allowPartialPath = false)
        {
            Point? end = SolveForPath(fromId, toId, allowPartialPath);
            if (end == null)
            {
                return Array.Empty<SimVec2I>();
            }

            Point begin = GetPoint(fromId);
            var reversed = new List<SimVec2I>();
            Point point = end;
            while (point != begin)
            {
                reversed.Add(point.Id);
                point = point.Previous!;
            }

            reversed.Add(begin.Id);
            reversed.Reverse();
            return reversed.ToArray();
        }

        public SimVec2[] GetPointPath(SimVec2I fromId, SimVec2I toId, bool allowPartialPath = false)
        {
            Point? end = SolveForPath(fromId, toId, allowPartialPath);
            if (end == null)
            {
                return Array.Empty<SimVec2>();
            }

            Point begin = GetPoint(fromId);
            var reversed = new List<SimVec2>();
            Point point = end;
            while (point != begin)
            {
                reversed.Add(GetPointPosition(point.Id));
                point = point.Previous!;
            }

            reversed.Add(GetPointPosition(begin.Id));
            reversed.Reverse();
            return reversed.ToArray();
        }

        private Point? SolveForPath(SimVec2I fromId, SimVec2I toId, bool allowPartialPath)
        {
            if (IsDirty || !IsInBounds(fromId) || !IsInBounds(toId))
            {
                return null;
            }

            Point begin = GetPoint(fromId);
            Point requestedEnd = GetPoint(toId);
            Point end = Solve(begin, requestedEnd, allowPartialPath, out bool foundRoute);
            if (foundRoute)
            {
                return end;
            }

            return allowPartialPath ? _lastClosestPoint : null;
        }

        private Point Solve(Point begin, Point requestedEnd, bool allowPartialPath, out bool foundRoute)
        {
            _lastClosestPoint = null;
            _pass++;
            foundRoute = false;

            if (_solid[GetPointIndex(begin.Id)])
            {
                return requestedEnd;
            }

            if (begin == requestedEnd)
            {
                foundRoute = true;
                return requestedEnd;
            }

            if (_solid[GetPointIndex(requestedEnd.Id)] && !allowPartialPath)
            {
                return requestedEnd;
            }

            _openList.Clear();
            begin.GScore = 0.0f;
            begin.FScore = EstimateCost(begin.Id, requestedEnd.Id);
            begin.AbsGScore = 0.0f;
            begin.AbsFScore = EstimateCost(begin.Id, requestedEnd.Id);
            _openList.Add(begin);

            while (_openList.Count != 0)
            {
                Point point = _openList[0];
                if (_lastClosestPoint == null || _lastClosestPoint.AbsFScore > point.AbsFScore ||
                    (_lastClosestPoint.AbsFScore >= point.AbsFScore && _lastClosestPoint.AbsGScore > point.AbsGScore))
                {
                    _lastClosestPoint = point;
                }

                if (point == requestedEnd)
                {
                    foundRoute = true;
                    break;
                }

                PopHeap(_openList);
                point.ClosedPass = _pass;
                GetNeighbors(point, _neighbors);

                for (int i = 0; i < _neighbors.Count; i++)
                {
                    Point neighbor = _neighbors[i];
                    int neighborIndex = GetPointIndex(neighbor.Id);
                    if (_solid[neighborIndex] || neighbor.ClosedPass == _pass)
                    {
                        continue;
                    }

                    float tentativeGScore = point.GScore + ComputeCost(point.Id, neighbor.Id) * neighbor.WeightScale;
                    bool newPoint = false;
                    if (neighbor.OpenPass != _pass)
                    {
                        neighbor.OpenPass = _pass;
                        _openList.Add(neighbor);
                        newPoint = true;
                    }
                    else if (tentativeGScore >= neighbor.GScore)
                    {
                        continue;
                    }

                    neighbor.Previous = point;
                    neighbor.GScore = tentativeGScore;
                    neighbor.FScore = neighbor.GScore + EstimateCost(neighbor.Id, requestedEnd.Id);
                    neighbor.AbsGScore = tentativeGScore;
                    neighbor.AbsFScore = neighbor.FScore - neighbor.GScore;
                    if (newPoint)
                    {
                        PushHeap(_openList, _openList.Count - 1, 0, neighbor);
                    }
                    else
                    {
                        PushHeap(_openList, _openList.IndexOf(neighbor), 0, neighbor);
                    }
                }
            }

            return requestedEnd;
        }

        private void GetNeighbors(Point point, List<Point> neighbors)
        {
            neighbors.Clear();
            bool ts0 = false;
            bool ts1 = false;
            bool ts2 = false;
            bool ts3 = false;

            Point? left = GetOpenPoint(point.Id.X - 1, point.Id.Y);
            Point? right = GetOpenPoint(point.Id.X + 1, point.Id.Y);
            Point? top = GetOpenPoint(point.Id.X, point.Id.Y - 1);
            Point? bottom = GetOpenPoint(point.Id.X, point.Id.Y + 1);
            Point? topLeft = GetOpenPoint(point.Id.X - 1, point.Id.Y - 1);
            Point? topRight = GetOpenPoint(point.Id.X + 1, point.Id.Y - 1);
            Point? bottomRight = GetOpenPoint(point.Id.X + 1, point.Id.Y + 1);
            Point? bottomLeft = GetOpenPoint(point.Id.X - 1, point.Id.Y + 1);

            if (top != null)
            {
                neighbors.Add(top);
                ts0 = true;
            }

            if (right != null)
            {
                neighbors.Add(right);
                ts1 = true;
            }

            if (bottom != null)
            {
                neighbors.Add(bottom);
                ts2 = true;
            }

            if (left != null)
            {
                neighbors.Add(left);
                ts3 = true;
            }

            bool td0;
            bool td1;
            bool td2;
            bool td3;
            switch (DiagonalMode)
            {
                case DiagonalModeKind.Always:
                    td0 = td1 = td2 = td3 = true;
                    break;
                case DiagonalModeKind.Never:
                    td0 = td1 = td2 = td3 = false;
                    break;
                case DiagonalModeKind.AtLeastOneWalkable:
                    td0 = ts3 || ts0;
                    td1 = ts0 || ts1;
                    td2 = ts1 || ts2;
                    td3 = ts2 || ts3;
                    break;
                case DiagonalModeKind.OnlyIfNoObstacles:
                    td0 = ts3 && ts0;
                    td1 = ts0 && ts1;
                    td2 = ts1 && ts2;
                    td3 = ts2 && ts3;
                    break;
                default:
                    throw new InvalidOperationException("Unknown diagonal mode.");
            }

            if (td0 && topLeft != null)
            {
                neighbors.Add(topLeft);
            }

            if (td1 && topRight != null)
            {
                neighbors.Add(topRight);
            }

            if (td2 && bottomRight != null)
            {
                neighbors.Add(bottomRight);
            }

            if (td3 && bottomLeft != null)
            {
                neighbors.Add(bottomLeft);
            }
        }

        private Point? GetOpenPoint(int x, int y)
        {
            var id = new SimVec2I(x, y);
            if (!IsInBounds(id))
            {
                return null;
            }

            int index = GetPointIndex(id);
            return _solid[index] ? null : _points[index];
        }

        private float ComputeCost(SimVec2I fromId, SimVec2I toId)
        {
            return EvaluateHeuristic(DefaultComputeHeuristic, fromId, toId);
        }

        private float EstimateCost(SimVec2I fromId, SimVec2I toId)
        {
            return EvaluateHeuristic(DefaultEstimateHeuristic, fromId, toId);
        }

        private static float EvaluateHeuristic(Heuristic heuristic, SimVec2I fromId, SimVec2I toId)
        {
            float dx = Math.Abs(toId.X - fromId.X);
            float dy = Math.Abs(toId.Y - fromId.Y);
            switch (heuristic)
            {
                case Heuristic.Euclidean:
                    return MathF.Sqrt(dx * dx + dy * dy);
                case Heuristic.Manhattan:
                    return dx + dy;
                case Heuristic.Octile:
                    return dx < dy ? OctileFactor * dx + dy : OctileFactor * dy + dx;
                case Heuristic.Chebyshev:
                    return MathF.Max(dx, dy);
                default:
                    throw new InvalidOperationException("Unknown heuristic.");
            }
        }

        private static bool IsWorse(Point a, Point b)
        {
            if (a.FScore > b.FScore)
            {
                return true;
            }

            if (a.FScore < b.FScore)
            {
                return false;
            }

            return a.GScore < b.GScore;
        }

        private static void PushHeap(List<Point> heap, int holeIndex, int topIndex, Point value)
        {
            int parent = (holeIndex - 1) / 2;
            while (holeIndex > topIndex && IsWorse(heap[parent], value))
            {
                heap[holeIndex] = heap[parent];
                holeIndex = parent;
                parent = (holeIndex - 1) / 2;
            }

            heap[holeIndex] = value;
        }

        private static void PopHeap(List<Point> heap)
        {
            int last = heap.Count - 1;
            Point value = heap[last];
            heap[last] = heap[0];
            AdjustHeap(heap, 0, last, value);
            heap.RemoveAt(last);
        }

        private static void AdjustHeap(List<Point> heap, int holeIndex, int length, Point value)
        {
            int topIndex = holeIndex;
            int secondChild = 2 * holeIndex + 2;
            while (secondChild < length)
            {
                if (IsWorse(heap[secondChild], heap[secondChild - 1]))
                {
                    secondChild--;
                }

                heap[holeIndex] = heap[secondChild];
                holeIndex = secondChild;
                secondChild = 2 * (secondChild + 1);
            }

            if (secondChild == length)
            {
                heap[holeIndex] = heap[secondChild - 1];
                holeIndex = secondChild - 1;
            }

            PushHeap(heap, holeIndex, topIndex, value);
        }

        private Point GetPoint(SimVec2I id)
        {
            return _points[GetPointIndex(id)];
        }

        private int GetWritablePointIndex(SimVec2I id)
        {
            if (IsDirty)
            {
                throw new InvalidOperationException("Grid is not initialized. Call Update first.");
            }

            if (!IsInBounds(id))
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Point is outside the grid region.");
            }

            return GetPointIndex(id);
        }

        private int GetPointIndex(SimVec2I id)
        {
            return (id.Y - RegionPosition.Y) * RegionSize.X + id.X - RegionPosition.X;
        }

        private int GetCellCount()
        {
            long count = (long)RegionSize.X * RegionSize.Y;
            if (count > int.MaxValue)
            {
                throw new InvalidOperationException("Grid region is too large.");
            }

            return (int)count;
        }

        private static void ValidateRegionSize(SimVec2I size)
        {
            if (size.X < 0 || size.Y < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Region dimensions cannot be negative.");
            }
        }
    }
}
