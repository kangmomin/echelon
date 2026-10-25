using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echelon.Unity.Presentation.Terrain
{
    public static class TerrainMeshValidation
    {
        public static IReadOnlyList<string> ValidateData(TerrainMeshData data)
        {
            if (data == null)
                return new[] { "Terrain data is null." };
            return data.Validate();
        }

        public static IReadOnlyList<string> ValidateMesh(Mesh mesh, bool requireUV = false)
        {
            var errors = new List<string>();
            if (mesh == null)
            {
                errors.Add("Mesh is null.");
                return errors.AsReadOnly();
            }
            if (mesh.indexFormat != IndexFormat.UInt32)
                errors.Add($"{mesh.name}: index format must be UInt32.");
            if (mesh.vertexCount == 0)
                errors.Add($"{mesh.name}: mesh has no vertices.");
            if (mesh.subMeshCount == 0)
                errors.Add($"{mesh.name}: mesh has no submeshes.");

            Vector3[] vertices = mesh.vertices;
            if (vertices.Length != mesh.vertexCount)
                errors.Add($"{mesh.name}: vertex array length does not match vertexCount.");
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!IsFinite(vertices[i]))
                    errors.Add($"{mesh.name}: vertex {i} is not finite.");
            }

            Vector3[] normals = mesh.normals;
            if (normals.Length != 0 && normals.Length != mesh.vertexCount)
                errors.Add($"{mesh.name}: normal count does not match vertex count.");
            for (int i = 0; i < normals.Length; i++)
            {
                if (!IsFinite(normals[i]))
                    errors.Add($"{mesh.name}: normal {i} is not finite.");
            }

            Vector2[] uv = mesh.uv;
            if (uv.Length != mesh.vertexCount && (requireUV || uv.Length != 0))
                errors.Add($"{mesh.name}: UV count does not match vertex count.");
            for (int i = 0; i < uv.Length; i++)
            {
                if (!TerrainMeshData.IsFinite(uv[i].x) || !TerrainMeshData.IsFinite(uv[i].y))
                    errors.Add($"{mesh.name}: UV {i} is not finite.");
            }

            Vector4[] tangents = mesh.tangents;
            if (tangents.Length != 0 && tangents.Length != mesh.vertexCount)
                errors.Add($"{mesh.name}: tangent count does not match vertex count.");
            for (int i = 0; i < tangents.Length; i++)
            {
                if (!IsFinite(tangents[i]))
                    errors.Add($"{mesh.name}: tangent {i} is not finite.");
            }

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                {
                    errors.Add($"{mesh.name}: submesh {submesh} is not triangular.");
                    continue;
                }
                int[] indices = mesh.GetIndices(submesh);
                if (indices.Length == 0 || indices.Length % 3 != 0)
                    errors.Add($"{mesh.name}: submesh {submesh} index count is not a nonzero multiple of 3.");
                for (int i = 0; i < indices.Length; i++)
                {
                    if (indices[i] < 0 || indices[i] >= mesh.vertexCount)
                        errors.Add($"{mesh.name}: index {indices[i]} at {i} is outside the vertex array.");
                }
            }

            Bounds bounds = mesh.bounds;
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                errors.Add($"{mesh.name}: bounds are not finite.");
            return errors.AsReadOnly();
        }

        public static IReadOnlyList<string> ValidateGridMesh(Mesh mesh, TerrainMeshData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("Terrain data is null.");
                return errors.AsReadOnly();
            }
            errors.AddRange(ValidateMesh(mesh));
            if (mesh != null)
            {
                int nx = Mathf.CeilToInt(data.MapSize.x / TerrainGeometry.VertexStepMeters) + 1;
                int ny = Mathf.CeilToInt(data.MapSize.y / TerrainGeometry.VertexStepMeters) + 1;
                int expectedVertices = nx * ny;
                int expectedIndices = (nx - 1) * (ny - 1) * 6;
                if (mesh.vertexCount != expectedVertices)
                    errors.Add($"Ground mesh has {mesh.vertexCount} vertices; expected {expectedVertices}.");
                if (mesh.subMeshCount > 0 && mesh.GetIndices(0).Length != expectedIndices)
                    errors.Add($"Ground mesh has {mesh.GetIndices(0).Length} indices; expected {expectedIndices}.");
            }
            return errors.AsReadOnly();
        }

        internal static void EnsureValid(TerrainMeshData data)
        {
            IReadOnlyList<string> errors = ValidateData(data);
            if (errors.Count > 0)
                throw new ArgumentException("Invalid terrain data:\n" + string.Join("\n", errors), nameof(data));
        }

        internal static bool IsFinite(Vector3 value)
        {
            return TerrainMeshData.IsFinite(value.x) && TerrainMeshData.IsFinite(value.y) && TerrainMeshData.IsFinite(value.z);
        }

        private static bool IsFinite(Vector4 value)
        {
            return TerrainMeshData.IsFinite(value.x) && TerrainMeshData.IsFinite(value.y)
                && TerrainMeshData.IsFinite(value.z) && TerrainMeshData.IsFinite(value.w);
        }
    }
}
