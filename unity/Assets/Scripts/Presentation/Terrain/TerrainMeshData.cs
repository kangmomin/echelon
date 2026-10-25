using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain
{
    /// <summary>Immutable presentation snapshot; vector points use east/north simulation meters.</summary>
    public sealed class TerrainMeshData
    {
        public const int DefaultDecorationSeed = 20261023;

        public int GridWidth { get; }
        public int GridHeight { get; }
        public float CellSize { get; }
        public Vector2 MapSize { get; }
        /// <summary>Cell-centered heights in meters, row-major with the southern row first.</summary>
        public ReadOnlyMemory<float> Heights { get; }
        /// <summary>Cell terrain types in the same row-major order as Heights.</summary>
        public ReadOnlyMemory<TerrainType> Types { get; }
        public ReadOnlyMemory<TerrainVectorFeature> Roads { get; }
        public ReadOnlyMemory<TerrainVectorFeature> Rivers { get; }
        public ReadOnlyMemory<TerrainVectorFeature> Forests { get; }
        public ReadOnlyMemory<TerrainVectorFeature> Towns { get; }
        public int DecorationSeed { get; }

        public TerrainMeshData(
            int gridWidth,
            int gridHeight,
            float cellSize,
            Vector2 mapSize,
            IEnumerable<float> heights,
            IEnumerable<TerrainType> types,
            IEnumerable<TerrainVectorFeature> roads,
            IEnumerable<TerrainVectorFeature> rivers,
            IEnumerable<TerrainVectorFeature> forests = null,
            IEnumerable<TerrainVectorFeature> towns = null,
            int decorationSeed = DefaultDecorationSeed)
        {
            if (heights == null)
                throw new ArgumentNullException(nameof(heights));
            if (types == null)
                throw new ArgumentNullException(nameof(types));
            if (roads == null)
                throw new ArgumentNullException(nameof(roads));
            if (rivers == null)
                throw new ArgumentNullException(nameof(rivers));

            GridWidth = gridWidth;
            GridHeight = gridHeight;
            CellSize = cellSize;
            MapSize = mapSize;
            Heights = Copy(heights);
            Types = Copy(types);
            Roads = CopyFeatures(roads);
            Rivers = CopyFeatures(rivers);
            Forests = forests == null ? ReadOnlyMemory<TerrainVectorFeature>.Empty : CopyFeatures(forests);
            Towns = towns == null ? ReadOnlyMemory<TerrainVectorFeature>.Empty : CopyFeatures(towns);
            DecorationSeed = decorationSeed;
        }

        public float SampleGroundHeight(Vector2 simulationPosition)
        {
            float fx = simulationPosition.x / CellSize - 0.5f;
            float fy = simulationPosition.y / CellSize - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, GridWidth - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, GridHeight - 1);
            int x1 = Mathf.Min(x0 + 1, GridWidth - 1);
            int y1 = Mathf.Min(y0 + 1, GridHeight - 1);
            float tx = Mathf.Clamp01(fx - x0);
            float ty = Mathf.Clamp01(fy - y0);
            ReadOnlySpan<float> heights = Heights.Span;
            float h00 = heights[y0 * GridWidth + x0];
            float h10 = heights[y0 * GridWidth + x1];
            float h01 = heights[y1 * GridWidth + x0];
            float h11 = heights[y1 * GridWidth + x1];
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
        }

        public TerrainType TypeAt(Vector2 simulationPosition)
        {
            int x = Mathf.FloorToInt(simulationPosition.x / CellSize);
            int y = Mathf.FloorToInt(simulationPosition.y / CellSize);
            return TypeAtCell(x, y);
        }

        public TerrainType TypeAtCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= GridWidth || y >= GridHeight)
                return TerrainType.Water;
            return Types.Span[y * GridWidth + x];
        }

        public bool HasTypeNear(Vector2 simulationPosition, TerrainType type, float radiusMeters)
        {
            int centerX = Mathf.FloorToInt(simulationPosition.x / CellSize);
            int centerY = Mathf.FloorToInt(simulationPosition.y / CellSize);
            int radius = Mathf.CeilToInt(radiusMeters / CellSize);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (TypeAtCell(centerX + dx, centerY + dy) == type)
                        return true;
                }
            }
            return false;
        }

        public bool IsBridge(Vector2 simulationPosition)
        {
            int x = Mathf.FloorToInt(simulationPosition.x / CellSize);
            int y = Mathf.FloorToInt(simulationPosition.y / CellSize);
            return TypeAtCell(x, y) == TerrainType.Road && HasTypeNear(simulationPosition, TerrainType.Water, 30f);
        }

        public Vector2 ClampToMap(Vector2 simulationPosition, float margin = 1f)
        {
            return new Vector2(
                Mathf.Clamp(simulationPosition.x, margin, MapSize.x - margin),
                Mathf.Clamp(simulationPosition.y, margin, MapSize.y - margin));
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (GridWidth < 1)
                errors.Add("GridWidth must be at least 1.");
            if (GridHeight < 1)
                errors.Add("GridHeight must be at least 1.");
            if (!IsFinite(CellSize) || CellSize <= 0f)
                errors.Add("CellSize must be finite and positive.");
            if (!IsFinite(MapSize.x) || !IsFinite(MapSize.y) || MapSize.x < 2f || MapSize.y < 2f)
                errors.Add("MapSize components must be finite and at least 2 meters.");

            if (GridWidth > 0 && GridHeight > 0)
            {
                int expectedLength = GridWidth * GridHeight;
                if (Heights.Length != expectedLength)
                    errors.Add($"Heights length {Heights.Length} does not match grid cell count {expectedLength}.");
                if (Types.Length != expectedLength)
                    errors.Add($"Types length {Types.Length} does not match grid cell count {expectedLength}.");
            }

            if (IsFinite(CellSize) && CellSize > 0f && IsFinite(MapSize.x) && IsFinite(MapSize.y))
            {
                int expectedWidth = Mathf.CeilToInt(MapSize.x / CellSize);
                int expectedHeight = Mathf.CeilToInt(MapSize.y / CellSize);
                if (GridWidth != expectedWidth || GridHeight != expectedHeight)
                    errors.Add($"Grid dimensions {GridWidth}x{GridHeight} do not match MapSize/CellSize ({expectedWidth}x{expectedHeight}).");
            }

            ReadOnlySpan<float> heights = Heights.Span;
            for (int i = 0; i < heights.Length; i++)
            {
                if (!IsFinite(heights[i]))
                    errors.Add($"Heights[{i}] is not finite.");
            }

            ReadOnlySpan<TerrainType> types = Types.Span;
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] > TerrainType.Water)
                    errors.Add($"Types[{i}] has unknown value {(byte)types[i]}.");
            }

            ValidateFeatures(Roads, 2, true, "Roads", errors);
            ValidateFeatures(Rivers, 2, true, "Rivers", errors);
            ValidateFeatures(Forests, 3, false, "Forests", errors);
            ValidateFeatures(Towns, 3, false, "Towns", errors);
            return errors.AsReadOnly();
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void ValidateFeatures(
            ReadOnlyMemory<TerrainVectorFeature> features,
            int minimumPoints,
            bool requiresWidth,
            string label,
            List<string> errors)
        {
            ReadOnlySpan<TerrainVectorFeature> span = features.Span;
            for (int i = 0; i < span.Length; i++)
            {
                TerrainVectorFeature feature = span[i];
                if (feature == null)
                {
                    errors.Add($"{label}[{i}] is null.");
                    continue;
                }
                if (feature.Points.Length < minimumPoints)
                    errors.Add($"{label}[{i}] needs at least {minimumPoints} points.");
                if (!IsFinite(feature.WidthMeters) || (requiresWidth ? feature.WidthMeters <= 0f : feature.WidthMeters < 0f))
                    errors.Add($"{label}[{i}] has invalid width {feature.WidthMeters}.");
                ReadOnlySpan<Vector2> points = feature.Points.Span;
                for (int j = 0; j < points.Length; j++)
                {
                    if (!IsFinite(points[j].x) || !IsFinite(points[j].y))
                        errors.Add($"{label}[{i}].Points[{j}] is not finite.");
                }
            }
        }

        private static ReadOnlyMemory<T> Copy<T>(IEnumerable<T> source)
        {
            var copy = new List<T>();
            foreach (T value in source)
                copy.Add(value);
            return copy.ToArray();
        }

        private static ReadOnlyMemory<TerrainVectorFeature> CopyFeatures(IEnumerable<TerrainVectorFeature> source)
        {
            return Copy(source);
        }
    }
}
