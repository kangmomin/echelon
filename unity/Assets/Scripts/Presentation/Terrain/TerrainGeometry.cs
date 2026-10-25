using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echelon.Unity.Presentation.Terrain
{
    public static class TerrainGeometry
    {
        public const float VertexStepMeters = 10f;
        public const float RibbonStepMeters = 5f;
        public const float RoadLiftMeters = 0.25f;
        public const float RiverLevelMeters = 2.2f;

        private static readonly Color BridgeColor = new Color(0.55f, 0.53f, 0.50f, 1f);

        public static Mesh BuildHeightMesh(TerrainMeshData data)
        {
            float heightMin;
            float heightMax;
            return BuildHeightMesh(data, out heightMin, out heightMax);
        }

        public static Mesh BuildHeightMesh(TerrainMeshData data, out float heightMin, out float heightMax)
        {
            TerrainMeshValidation.EnsureValid(data);
            int nx = Mathf.CeilToInt(data.MapSize.x / VertexStepMeters) + 1;
            int ny = Mathf.CeilToInt(data.MapSize.y / VertexStepMeters) + 1;
            var vertices = new Vector3[nx * ny];
            var normals = new Vector3[vertices.Length];
            heightMin = float.PositiveInfinity;
            heightMax = float.NegativeInfinity;

            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    var p = new Vector2(
                        Mathf.Min(i * VertexStepMeters, data.MapSize.x - 0.5f),
                        Mathf.Min(j * VertexStepMeters, data.MapSize.y - 0.5f));
                    float height = data.SampleGroundHeight(p);
                    heightMin = Mathf.Min(heightMin, height);
                    heightMax = Mathf.Max(heightMax, height);
                    vertices[j * nx + i] = TerrainCoordinates.ToUnity(p, height);
                }
            }

            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    float left = vertices[j * nx + Mathf.Max(i - 1, 0)].y;
                    float right = vertices[j * nx + Mathf.Min(i + 1, nx - 1)].y;
                    float down = vertices[Mathf.Max(j - 1, 0) * nx + i].y;
                    float up = vertices[Mathf.Min(j + 1, ny - 1) * nx + i].y;
                    normals[j * nx + i] = new Vector3(left - right, 2f * VertexStepMeters, down - up).normalized;
                }
            }

            var indices = new int[(nx - 1) * (ny - 1) * 6];
            int k = 0;
            for (int j = 0; j < ny - 1; j++)
            {
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i;
                    int c = a + nx;
                    indices[k++] = a;
                    indices[k++] = c;
                    indices[k++] = a + 1;
                    indices[k++] = a + 1;
                    indices[k++] = c;
                    indices[k++] = c + 1;
                }
            }

            var mesh = CreateMesh("TerrainGround", vertices, normals, null, null, indices);
            return mesh;
        }

        public static IReadOnlyList<Vector2> SmoothPolyline(
            IEnumerable<Vector2> points,
            bool keepEnds = true,
            float stepMeters = RibbonStepMeters)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            if (!TerrainMeshData.IsFinite(stepMeters) || stepMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(stepMeters));

            var current = new List<Vector2>(points);
            if (current.Count < 2)
                throw new ArgumentException("A polyline needs at least two points.", nameof(points));

            for (int iteration = 0; iteration < 2 && current.Count >= 3; iteration++)
            {
                var next = new List<Vector2> { current[0] };
                for (int i = 0; i < current.Count - 1; i++)
                {
                    Vector2 a = current[i];
                    Vector2 b = current[i + 1];
                    if (i > 0 || !keepEnds)
                        next.Add(Vector2.Lerp(a, b, 0.25f));
                    if (i < current.Count - 2 || !keepEnds)
                        next.Add(Vector2.Lerp(a, b, 0.75f));
                }
                next.Add(current[current.Count - 1]);
                current = next;
            }

            var output = new List<Vector2> { current[0] };
            for (int i = 0; i < current.Count - 1; i++)
            {
                Vector2 a = current[i];
                Vector2 b = current[i + 1];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / stepMeters));
                for (int j = 1; j <= steps; j++)
                    output.Add(Vector2.Lerp(a, b, j / (float)steps));
            }
            return output.AsReadOnly();
        }

        public static Mesh BuildRibbonMesh(
            IReadOnlyList<Vector2> points,
            float width,
            Func<Vector2, float> heightAt,
            IReadOnlyList<float> fixedCenterHeights = null)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            if (points.Count < 2)
                throw new ArgumentException("A ribbon needs at least two points.", nameof(points));
            if (!TerrainMeshData.IsFinite(width) || width <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (heightAt == null)
                throw new ArgumentNullException(nameof(heightAt));
            if (fixedCenterHeights != null && fixedCenterHeights.Count != points.Count)
                throw new ArgumentException("Fixed heights must match the point count.", nameof(fixedCenterHeights));

            var vertices = new List<Vector3>(points.Count * 3);
            var normals = new List<Vector3>(points.Count * 3);
            var uv = new List<Vector2>(points.Count * 3);
            var tangents = new List<Vector4>(points.Count * 3);
            var indices = new List<int>((points.Count - 1) * 12);
            float distance = 0f;
            float halfWidth = width * 0.5f;
            Vector2 lastTangent = Vector2.up;

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[Mathf.Max(i - 1, 0)];
                Vector2 b = points[Mathf.Min(i + 1, points.Count - 1)];
                Vector2 tangent = b - a;
                if (tangent.sqrMagnitude > 0.000001f)
                {
                    tangent.Normalize();
                    lastTangent = tangent;
                }
                else
                {
                    tangent = lastTangent;
                }
                var side = new Vector2(-tangent.y, tangent.x);
                Vector3 tangentUnity = TerrainCoordinates.ToUnity(side).normalized;
                if (i > 0)
                    distance += Vector2.Distance(points[i], points[i - 1]);

                for (int lane = 0; lane < 3; lane++)
                {
                    float u = lane * 0.5f;
                    Vector2 q = points[i] + side * Mathf.Lerp(-halfWidth, halfWidth, u);
                    float height = fixedCenterHeights != null && !float.IsNaN(fixedCenterHeights[i])
                        ? fixedCenterHeights[i]
                        : heightAt(q);
                    vertices.Add(TerrainCoordinates.ToUnity(q, height));
                    normals.Add(Vector3.up);
                    uv.Add(new Vector2(u, distance));
                    tangents.Add(new Vector4(tangentUnity.x, tangentUnity.y, tangentUnity.z, 1f));
                }

                if (i > 0)
                {
                    int baseIndex = (i - 1) * 3;
                    for (int lane = 0; lane < 2; lane++)
                    {
                        int a0 = baseIndex + lane;
                        int b0 = baseIndex + lane + 1;
                        int a1 = a0 + 3;
                        int b1 = b0 + 3;
                        indices.Add(a0);
                        indices.Add(b0);
                        indices.Add(a1);
                        indices.Add(b0);
                        indices.Add(b1);
                        indices.Add(a1);
                    }
                }
            }

            return CreateMesh("TerrainRibbon", vertices, normals, uv, tangents, indices);
        }

        public static float[] BuildRiverSurfaceHeights(TerrainMeshData data, IReadOnlyList<Vector2> points)
        {
            TerrainMeshValidation.EnsureValid(data);
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            var heights = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
                heights[i] = data.SampleGroundHeight(data.ClampToMap(points[i])) + RiverLevelMeters;

            var smooth = new float[heights.Length];
            for (int i = 0; i < heights.Length; i++)
            {
                float sum = 0f;
                int count = 0;
                int start = Mathf.Max(0, i - 6);
                int end = Mathf.Min(heights.Length, i + 7);
                for (int j = start; j < end; j++)
                {
                    sum += heights[j];
                    count++;
                }
                smooth[i] = count == 0 ? 0f : sum / count;
            }
            return smooth;
        }

        public static IReadOnlyList<TerrainInstance> BuildBridgeBoxes(
            IReadOnlyList<Vector2> points,
            float ribbonWidth,
            float deckHeight)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            if (points.Count < 2)
                return Array.Empty<TerrainInstance>();

            Vector2 a = points[0];
            Vector2 b = points[points.Count - 1];
            Vector2 direction = b - a;
            if (direction.sqrMagnitude < 0.000001f)
                direction = Vector2.up;
            direction.Normalize();
            var side = new Vector2(-direction.y, direction.x);
            float length = Vector2.Distance(a, b) + 8f;
            Quaternion rotation = TerrainCoordinates.DirectionToRotation(direction);
            Vector2 middle = (a + b) * 0.5f;
            var boxes = new List<TerrainInstance>(3);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector2 position = middle + side * sign * ribbonWidth * 0.45f;
                boxes.Add(new TerrainInstance(
                    TerrainCoordinates.ToUnity(position, deckHeight + 0.5f),
                    rotation,
                    new Vector3(0.6f, 1f, length),
                    BridgeColor));
            }
            boxes.Add(new TerrainInstance(
                TerrainCoordinates.ToUnity(middle, deckHeight - 0.75f),
                rotation,
                new Vector3(ribbonWidth * 0.9f, 1.2f, length),
                BridgeColor));
            return boxes.AsReadOnly();
        }

        public static Texture2D BuildTypeMaskTexture(TerrainMeshData data)
        {
            TerrainMeshValidation.EnsureValid(data);
            var texture = new Texture2D(data.GridWidth, data.GridHeight, TextureFormat.RGBA32, true, true)
            {
                name = "TerrainTypeMask",
                filterMode = FilterMode.Trilinear,
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Clamp
            };
            var pixels = new Color32[data.GridWidth * data.GridHeight];
            ReadOnlySpan<TerrainType> types = data.Types.Span;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(
                    types[i] == TerrainType.Road ? byte.MaxValue : (byte)0,
                    types[i] == TerrainType.Water ? byte.MaxValue : (byte)0,
                    types[i] == TerrainType.Forest ? byte.MaxValue : (byte)0,
                    types[i] == TerrainType.Urban ? byte.MaxValue : (byte)0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        internal static Mesh CreateMesh(
            string name,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Vector4> tangents,
            List<int> indices)
        {
            return CreateMesh(name, vertices.ToArray(), normals?.ToArray(), uv?.ToArray(), tangents?.ToArray(), indices.ToArray());
        }

        internal static Mesh CreateMesh(
            string name,
            Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uv,
            Vector4[] tangents,
            int[] indices)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            if (normals != null)
                mesh.SetNormals(normals);
            if (uv != null)
                mesh.SetUVs(0, uv);
            if (tangents != null)
                mesh.SetTangents(tangents);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
            if (normals == null)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
