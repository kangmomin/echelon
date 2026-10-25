using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain
{
    public static class TerrainScatter
    {
        public const float TreeSpacingMeters = 13f;
        public const float BuildingSpacingMeters = 24f;
        public const float FieldAngleRadians = 0.35f;
        public const float HedgeStepMeters = 6f;
        public const float HedgeChance = 0.45f;
        public const float RoadClearanceMeters = 30f;

        public static Vector2 FieldSize => new Vector2(230f, 150f);

        private static readonly Color[] LeafColors =
        {
            new Color(0.14f, 0.24f, 0.10f), new Color(0.18f, 0.27f, 0.11f),
            new Color(0.12f, 0.21f, 0.09f), new Color(0.22f, 0.29f, 0.12f),
            new Color(0.16f, 0.23f, 0.09f)
        };

        private static readonly Color[] ConiferColors =
        {
            new Color(0.08f, 0.16f, 0.09f), new Color(0.10f, 0.18f, 0.11f),
            new Color(0.07f, 0.14f, 0.08f)
        };

        private static readonly Color[] WallColors =
        {
            new Color(0.86f, 0.83f, 0.76f), new Color(0.78f, 0.72f, 0.62f),
            new Color(0.70f, 0.55f, 0.45f), new Color(0.82f, 0.80f, 0.78f),
            new Color(0.66f, 0.62f, 0.55f)
        };

        private static readonly Color[] RoofColors =
        {
            new Color(0.48f, 0.20f, 0.16f), new Color(0.35f, 0.33f, 0.32f),
            new Color(0.40f, 0.27f, 0.20f), new Color(0.25f, 0.26f, 0.29f)
        };

        public static TerrainScatterResult Build(TerrainMeshData data, TerrainViewQuality quality)
        {
            TerrainMeshValidation.EnsureValid(data);
            if (quality != TerrainViewQuality.Desktop && quality != TerrainViewQuality.Web)
                throw new ArgumentOutOfRangeException(nameof(quality));

            var scatter = new ScatterPass(data, quality == TerrainViewQuality.Web);
            return scatter.Run();
        }

        private sealed class ScatterPass
        {
            private readonly TerrainMeshData _data;
            private readonly bool _web;
            private readonly TerrainRandom _random;
            private readonly List<TerrainInstance> _broadleaf = new List<TerrainInstance>();
            private readonly List<TerrainInstance> _conifer = new List<TerrainInstance>();
            private readonly List<TerrainInstance> _bushes = new List<TerrainInstance>();
            private readonly List<TerrainHouse> _houses = new List<TerrainHouse>();

            internal ScatterPass(TerrainMeshData data, bool web)
            {
                _data = data;
                _web = web;
                _random = new TerrainRandom(data.DecorationSeed);
            }

            internal TerrainScatterResult Run()
            {
                ScatterForest();
                ScatterTowns();
                ScatterHedgerows();
                ScatterFarmsteads();
                return new TerrainScatterResult(_broadleaf, _conifer, _bushes, _houses);
            }

            private void ScatterForest()
            {
                float spacing = TreeSpacingMeters * (_web ? 1.4f : 1f);
                int nx = Mathf.FloorToInt(_data.MapSize.x / spacing);
                int ny = Mathf.FloorToInt(_data.MapSize.y / spacing);
                for (int j = 0; j < ny; j++)
                {
                    for (int i = 0; i < nx; i++)
                    {
                        var p = new Vector2(
                            (i + 0.5f) * spacing + _random.Range(-0.45f, 0.45f) * spacing,
                            (j + 0.5f) * spacing + _random.Range(-0.45f, 0.45f) * spacing);
                        if (_data.TypeAt(p) != TerrainType.Forest)
                            continue;
                        if (_data.HasTypeNear(p, TerrainType.Open, 15f) && _random.NextFloat() < 0.35f)
                            continue;
                        AddTree(p, _random.NextFloat() < 0.35f);
                    }
                }
            }

            private void ScatterTowns()
            {
                int nx = Mathf.FloorToInt(_data.MapSize.x / BuildingSpacingMeters);
                int ny = Mathf.FloorToInt(_data.MapSize.y / BuildingSpacingMeters);
                for (int j = 0; j < ny; j++)
                {
                    for (int i = 0; i < nx; i++)
                    {
                        var p = new Vector2(
                            (i + 0.5f) * BuildingSpacingMeters + _random.Range(-0.2f, 0.2f) * BuildingSpacingMeters,
                            (j + 0.5f) * BuildingSpacingMeters + _random.Range(-0.2f, 0.2f) * BuildingSpacingMeters);
                        if (_data.TypeAt(p) != TerrainType.Urban || _random.NextFloat() < 0.18f)
                            continue;
                        float angle = RoadAngleNear(p);
                        float scale = _random.Range(0.8f, 1.35f);
                        AddHouse(p, angle, scale);
                        if (_random.NextFloat() < 0.25f)
                        {
                            AddTree(p + new Vector2(_random.Range(-10f, 10f), _random.Range(-10f, 10f)), false);
                        }
                    }
                }
            }

            private void ScatterHedgerows()
            {
                float step = HedgeStepMeters * (_web ? 1.6f : 1f);
                float c = Mathf.Cos(FieldAngleRadians);
                float s = Mathf.Sin(FieldAngleRadians);
                float diagonal = _data.MapSize.magnitude;
                Vector2 fieldSize = FieldSize;
                int columns = Mathf.CeilToInt(diagonal / fieldSize.x) + 2;
                int rows = Mathf.CeilToInt(diagonal / fieldSize.y) + 2;
                for (int ci = -columns; ci < columns; ci++)
                {
                    float offset = PositiveModulo(ci * 0.6180339f, 1f) * fieldSize.y;
                    for (int ri = -rows; ri < rows; ri++)
                    {
                        float x0 = ci * fieldSize.x;
                        float y0 = ri * fieldSize.y - offset;
                        for (int edge = 0; edge < 2; edge++)
                        {
                            if (EdgeHash(ci, ri, edge) > HedgeChance)
                                continue;
                            var a = new Vector2(x0, y0);
                            var b = edge == 0 ? new Vector2(x0 + fieldSize.x, y0) : new Vector2(x0, y0 + fieldSize.y);
                            float length = Vector2.Distance(a, b);
                            int count = Mathf.FloorToInt(length / step);
                            for (int k = 0; k < count; k++)
                            {
                                Vector2 r = Vector2.Lerp(a, b, (k + 0.5f) / count);
                                var p = new Vector2(c * r.x - s * r.y, s * r.x + c * r.y);
                                if (p.x < 5f || p.y < 5f || p.x > _data.MapSize.x - 5f || p.y > _data.MapSize.y - 5f)
                                    continue;
                                if (_data.TypeAt(p) != TerrainType.Open)
                                    continue;
                                if (_data.HasTypeNear(p, TerrainType.Road, RoadClearanceMeters)
                                    || _data.HasTypeNear(p, TerrainType.Water, 20f))
                                    continue;
                                if (_random.NextFloat() < 0.12f)
                                    continue;
                                if (_random.NextFloat() < 0.07f)
                                {
                                    AddTree(p, false);
                                }
                                else
                                {
                                    float scale = _random.Range(0.7f, 1.3f);
                                    Color color = Darken(LeafColors[_random.NextInt(LeafColors.Length)], _random.Range(0f, 0.15f));
                                    float yaw = _random.NextFloat() * Mathf.PI * 2f;
                                    float heightScale = scale * _random.Range(0.7f, 1f);
                                    _bushes.Add(new TerrainInstance(
                                        Ground(p),
                                        Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up),
                                        new Vector3(scale, heightScale, scale),
                                        color));
                                }
                            }
                        }
                    }
                }
            }

            private void ScatterFarmsteads()
            {
                ReadOnlySpan<TerrainVectorFeature> roads = _data.Roads.Span;
                for (int roadIndex = 0; roadIndex < roads.Length; roadIndex++)
                {
                    ReadOnlySpan<Vector2> points = roads[roadIndex].Points.Span;
                    for (int i = 0; i < points.Length - 1; i++)
                    {
                        Vector2 a = points[i];
                        Vector2 b = points[i + 1];
                        float length = Vector2.Distance(a, b);
                        Vector2 direction = (b - a) / Mathf.Max(length, 1f);
                        var side = new Vector2(-direction.y, direction.x);
                        float distance = 150f;
                        while (distance < length - 100f)
                        {
                            if (_random.NextFloat() < 0.32f)
                            {
                                float sign = _random.NextFloat() < 0.5f ? 1f : -1f;
                                Vector2 center = a + direction * distance + side * sign * _random.Range(45f, 70f);
                                if (_data.TypeAt(center) == TerrainType.Open && !_data.HasTypeNear(center, TerrainType.Water, 40f))
                                {
                                    float yaw = TerrainCoordinates.DirectionToYawRadians(direction);
                                    int count = _random.NextInt(1, 4);
                                    for (int k = 0; k < count; k++)
                                    {
                                        Vector2 offset = direction * _random.Range(-18f, 18f) + side * sign * k * 16f;
                                        float scale = _random.Range(0.8f, 1.1f);
                                        AddHouse(center + offset, yaw + (k == 2 ? Mathf.PI * 0.5f : 0f), scale);
                                    }
                                    int treeCount = _random.NextInt(2, 6);
                                    for (int k = 0; k < treeCount; k++)
                                    {
                                        AddTree(
                                            center + new Vector2(_random.Range(-35f, 35f), _random.Range(-35f, 35f)),
                                            _random.NextFloat() < 0.3f);
                                    }
                                }
                            }
                            distance += _random.Range(220f, 420f);
                        }
                    }
                }
            }

            private void AddTree(Vector2 p, bool conifer)
            {
                float scale = _random.Range(0.75f, 1.3f);
                if (conifer)
                {
                    Color color = ConiferColors[_random.NextInt(ConiferColors.Length)];
                    float yaw = _random.NextFloat() * Mathf.PI * 2f;
                    float heightScale = scale * _random.Range(0.9f, 1.25f);
                    _conifer.Add(new TerrainInstance(
                        Ground(p),
                        Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up),
                        new Vector3(scale, heightScale, scale),
                        color));
                }
                else
                {
                    Color color = Lighten(LeafColors[_random.NextInt(LeafColors.Length)], _random.Range(-0.05f, 0.08f));
                    float yaw = _random.NextFloat() * Mathf.PI * 2f;
                    float heightScale = scale * _random.Range(0.85f, 1.1f);
                    _broadleaf.Add(new TerrainInstance(
                        Ground(p),
                        Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up),
                        new Vector3(scale, heightScale, scale),
                        color));
                }
            }

            private void AddHouse(Vector2 p, float angle, float scale)
            {
                var size = new Vector3(
                    _random.Range(9f, 14f),
                    _random.Range(5.5f, 8f),
                    _random.Range(8f, 11f)) * scale;
                Color wall = WallColors[_random.NextInt(WallColors.Length)];
                Color roof = RoofColors[_random.NextInt(RoofColors.Length)];
                float yaw = angle + (_random.NextFloat() < 0.3f ? Mathf.PI * 0.5f : 0f);
                _houses.Add(new TerrainHouse(Ground(p), yaw, size, wall, roof));
            }

            private float RoadAngleNear(Vector2 p)
            {
                float bestDistance = float.PositiveInfinity;
                Vector2 bestDirection = Vector2.up;
                ReadOnlySpan<TerrainVectorFeature> roads = _data.Roads.Span;
                for (int roadIndex = 0; roadIndex < roads.Length; roadIndex++)
                {
                    ReadOnlySpan<Vector2> points = roads[roadIndex].Points.Span;
                    for (int i = 0; i < points.Length - 1; i++)
                    {
                        Vector2 closest = ClosestPointOnSegment(p, points[i], points[i + 1]);
                        float distance = (closest - p).sqrMagnitude;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestDirection = points[i + 1] - points[i];
                        }
                    }
                }
                if (bestDistance > 300f * 300f)
                    return _random.NextInt(0, 4) * Mathf.PI * 0.5f;
                return TerrainCoordinates.DirectionToYawRadians(bestDirection);
            }

            private Vector3 Ground(Vector2 p)
            {
                return TerrainCoordinates.ToUnity(p, _data.SampleGroundHeight(p));
            }

            private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float lengthSquared = ab.sqrMagnitude;
                if (lengthSquared < 0.000001f)
                    return a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
                return a + ab * t;
            }

            private static float EdgeHash(int ci, int ri, int edge)
            {
                return PositiveModulo(Mathf.Sin(ci * 12.9898f + ri * 78.233f + edge * 37.719f) * 43758.5453f, 1f);
            }

            private static float PositiveModulo(float value, float modulus)
            {
                return value - Mathf.Floor(value / modulus) * modulus;
            }

            private static Color Lighten(Color color, float amount)
            {
                color.r += (1f - color.r) * amount;
                color.g += (1f - color.g) * amount;
                color.b += (1f - color.b) * amount;
                return color;
            }

            private static Color Darken(Color color, float amount)
            {
                color.r *= 1f - amount;
                color.g *= 1f - amount;
                color.b *= 1f - amount;
                return color;
            }
        }
    }
}
