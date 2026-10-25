using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain
{
    internal static class TerrainPrimitives
    {
        internal static Mesh CreateUnitBox()
        {
            var buffer = new MeshBuffer();
            buffer.AddBox(Vector3.zero, Vector3.one);
            return buffer.ToMesh("TerrainUnitBox");
        }

        internal static Mesh CreateBroadleafCrown()
        {
            var buffer = new MeshBuffer();
            buffer.AddEllipsoid(new Vector3(0f, 7f, 0f), new Vector3(4.2f, 3.5f, 4.2f), 7, 5);
            buffer.AddEllipsoid(new Vector3(1.2f, 9.6f, 0.6f), new Vector3(3f, 2.5f, 3f), 6, 4);
            return buffer.ToMesh("TerrainBroadleafCrown");
        }

        internal static Mesh CreateTreeTrunk()
        {
            var buffer = new MeshBuffer();
            buffer.AddCylinder(new Vector3(0f, 2f, 0f), 4f, 0.5f, 0.35f, 5);
            return buffer.ToMesh("TerrainTreeTrunk");
        }

        internal static Mesh CreateConifer()
        {
            var buffer = new MeshBuffer();
            buffer.AddCylinder(new Vector3(0f, 5.5f, 0f), 8f, 3.6f, 0f, 7);
            buffer.AddCylinder(new Vector3(0f, 10f, 0f), 6.5f, 2.6f, 0f, 7);
            buffer.AddCylinder(new Vector3(0f, 1.8f, 0f), 4f, 0.5f, 0.35f, 5);
            return buffer.ToMesh("TerrainConifer");
        }

        internal static Mesh CreateBush()
        {
            var buffer = new MeshBuffer();
            buffer.AddEllipsoid(new Vector3(0f, 1.2f, 0f), new Vector3(2.2f, 1.6f, 2.2f), 6, 4);
            return buffer.ToMesh("TerrainBush");
        }

        internal static Mesh CreateHouseWalls()
        {
            var buffer = new MeshBuffer();
            buffer.AddBox(new Vector3(0f, 0.5f, 0f), Vector3.one);
            return buffer.ToMesh("TerrainHouseWalls");
        }

        internal static Mesh CreateHouseRoof()
        {
            var buffer = new MeshBuffer();
            buffer.AddRoof(new Vector3(0f, 1.275f, 0f), new Vector3(1.08f, 0.55f, 1.06f));
            return buffer.ToMesh("TerrainHouseRoof");
        }

        private sealed class MeshBuffer
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uv = new List<Vector2>();
            private readonly List<int> _indices = new List<int>();

            internal void AddBox(Vector3 center, Vector3 size)
            {
                Vector3 half = size * 0.5f;
                float x = half.x;
                float y = half.y;
                float z = half.z;
                AddQuad(
                    center + new Vector3(-x, -y, -z), center + new Vector3(-x, y, -z),
                    center + new Vector3(x, y, -z), center + new Vector3(x, -y, -z), Vector3.back);
                AddQuad(
                    center + new Vector3(x, -y, z), center + new Vector3(x, y, z),
                    center + new Vector3(-x, y, z), center + new Vector3(-x, -y, z), Vector3.forward);
                AddQuad(
                    center + new Vector3(x, -y, -z), center + new Vector3(x, y, -z),
                    center + new Vector3(x, y, z), center + new Vector3(x, -y, z), Vector3.right);
                AddQuad(
                    center + new Vector3(-x, -y, z), center + new Vector3(-x, y, z),
                    center + new Vector3(-x, y, -z), center + new Vector3(-x, -y, -z), Vector3.left);
                AddQuad(
                    center + new Vector3(-x, y, -z), center + new Vector3(-x, y, z),
                    center + new Vector3(x, y, z), center + new Vector3(x, y, -z), Vector3.up);
                AddQuad(
                    center + new Vector3(-x, -y, z), center + new Vector3(-x, -y, -z),
                    center + new Vector3(x, -y, -z), center + new Vector3(x, -y, z), Vector3.down);
            }

            internal void AddEllipsoid(Vector3 center, Vector3 radii, int radialSegments, int latitudeSegments)
            {
                if (radialSegments < 3 || latitudeSegments < 2)
                    throw new ArgumentOutOfRangeException(nameof(radialSegments));
                int top = AddVertex(center + Vector3.up * radii.y, Vector3.up, new Vector2(0.5f, 1f));
                var rings = new int[latitudeSegments - 1][];
                for (int latitude = 1; latitude < latitudeSegments; latitude++)
                {
                    float theta = Mathf.PI * latitude / latitudeSegments;
                    float sinTheta = Mathf.Sin(theta);
                    float cosTheta = Mathf.Cos(theta);
                    var ring = new int[radialSegments + 1];
                    for (int longitude = 0; longitude <= radialSegments; longitude++)
                    {
                        float u = longitude / (float)radialSegments;
                        float phi = u * Mathf.PI * 2f;
                        var local = new Vector3(
                            radii.x * sinTheta * Mathf.Cos(phi),
                            radii.y * cosTheta,
                            radii.z * sinTheta * Mathf.Sin(phi));
                        Vector3 normal = new Vector3(
                            local.x / (radii.x * radii.x),
                            local.y / (radii.y * radii.y),
                            local.z / (radii.z * radii.z)).normalized;
                        ring[longitude] = AddVertex(center + local, normal, new Vector2(u, 1f - latitude / (float)latitudeSegments));
                    }
                    rings[latitude - 1] = ring;
                }
                int bottom = AddVertex(center + Vector3.down * radii.y, Vector3.down, new Vector2(0.5f, 0f));

                int[] first = rings[0];
                for (int longitude = 0; longitude < radialSegments; longitude++)
                    AddTriangleIndices(top, first[longitude + 1], first[longitude]);

                for (int ringIndex = 0; ringIndex < rings.Length - 1; ringIndex++)
                {
                    int[] upper = rings[ringIndex];
                    int[] lower = rings[ringIndex + 1];
                    for (int longitude = 0; longitude < radialSegments; longitude++)
                    {
                        AddTriangleIndices(upper[longitude], upper[longitude + 1], lower[longitude + 1]);
                        AddTriangleIndices(upper[longitude], lower[longitude + 1], lower[longitude]);
                    }
                }

                int[] last = rings[rings.Length - 1];
                for (int longitude = 0; longitude < radialSegments; longitude++)
                    AddTriangleIndices(last[longitude], last[longitude + 1], bottom);
            }

            internal void AddCylinder(Vector3 center, float height, float bottomRadius, float topRadius, int radialSegments)
            {
                if (radialSegments < 3 || height <= 0f || bottomRadius < 0f || topRadius < 0f)
                    throw new ArgumentOutOfRangeException(nameof(radialSegments));
                float bottomY = center.y - height * 0.5f;
                float topY = center.y + height * 0.5f;
                float slope = (bottomRadius - topRadius) / height;
                for (int segment = 0; segment < radialSegments; segment++)
                {
                    float u0 = segment / (float)radialSegments;
                    float u1 = (segment + 1) / (float)radialSegments;
                    float angle0 = u0 * Mathf.PI * 2f;
                    float angle1 = u1 * Mathf.PI * 2f;
                    Vector3 radial0 = new Vector3(Mathf.Cos(angle0), 0f, Mathf.Sin(angle0));
                    Vector3 radial1 = new Vector3(Mathf.Cos(angle1), 0f, Mathf.Sin(angle1));
                    Vector3 bottom0 = new Vector3(center.x, bottomY, center.z) + radial0 * bottomRadius;
                    Vector3 bottom1 = new Vector3(center.x, bottomY, center.z) + radial1 * bottomRadius;
                    Vector3 normal0 = new Vector3(radial0.x, slope, radial0.z).normalized;
                    Vector3 normal1 = new Vector3(radial1.x, slope, radial1.z).normalized;
                    int b0 = AddVertex(bottom0, normal0, new Vector2(u0, 0f));
                    int b1 = AddVertex(bottom1, normal1, new Vector2(u1, 0f));
                    if (topRadius > 0f)
                    {
                        Vector3 top0 = new Vector3(center.x, topY, center.z) + radial0 * topRadius;
                        Vector3 top1 = new Vector3(center.x, topY, center.z) + radial1 * topRadius;
                        int t0 = AddVertex(top0, normal0, new Vector2(u0, 1f));
                        int t1 = AddVertex(top1, normal1, new Vector2(u1, 1f));
                        AddTriangleIndices(b0, t0, t1);
                        AddTriangleIndices(b0, t1, b1);
                    }
                    else
                    {
                        int t0 = AddVertex(new Vector3(center.x, topY, center.z), normal0, new Vector2((u0 + u1) * 0.5f, 1f));
                        AddTriangleIndices(b0, t0, b1);
                    }
                }

                for (int segment = 0; segment < radialSegments; segment++)
                {
                    float angle0 = segment / (float)radialSegments * Mathf.PI * 2f;
                    float angle1 = (segment + 1) / (float)radialSegments * Mathf.PI * 2f;
                    if (bottomRadius > 0f)
                    {
                        var c = new Vector3(center.x, bottomY, center.z);
                        Vector3 p0 = c + new Vector3(Mathf.Cos(angle0), 0f, Mathf.Sin(angle0)) * bottomRadius;
                        Vector3 p1 = c + new Vector3(Mathf.Cos(angle1), 0f, Mathf.Sin(angle1)) * bottomRadius;
                        AddTriangle(c, p0, p1, Vector3.down);
                    }
                    if (topRadius > 0f)
                    {
                        var c = new Vector3(center.x, topY, center.z);
                        Vector3 p0 = c + new Vector3(Mathf.Cos(angle0), 0f, Mathf.Sin(angle0)) * topRadius;
                        Vector3 p1 = c + new Vector3(Mathf.Cos(angle1), 0f, Mathf.Sin(angle1)) * topRadius;
                        AddTriangle(c, p1, p0, Vector3.up);
                    }
                }
            }

            internal void AddRoof(Vector3 center, Vector3 size)
            {
                Vector3 half = size * 0.5f;
                Vector3 frontLeft = center + new Vector3(-half.x, -half.y, -half.z);
                Vector3 frontRight = center + new Vector3(half.x, -half.y, -half.z);
                Vector3 frontRidge = center + new Vector3(0f, half.y, -half.z);
                Vector3 backLeft = center + new Vector3(-half.x, -half.y, half.z);
                Vector3 backRight = center + new Vector3(half.x, -half.y, half.z);
                Vector3 backRidge = center + new Vector3(0f, half.y, half.z);
                AddTriangle(frontLeft, frontRidge, frontRight);
                AddTriangle(backRight, backRidge, backLeft);
                AddQuad(frontLeft, backLeft, backRidge, frontRidge);
                AddQuad(frontRidge, backRidge, backRight, frontRight);
                AddQuad(frontLeft, frontRight, backRight, backLeft);
            }

            internal Mesh ToMesh(string name)
            {
                return TerrainGeometry.CreateMesh(name, _vertices, _normals, _uv, null, _indices);
            }

            private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int index = AddVertex(a, normal, new Vector2(0f, 0f));
                AddVertex(b, normal, new Vector2(0f, 1f));
                AddVertex(c, normal, new Vector2(1f, 1f));
                AddVertex(d, normal, new Vector2(1f, 0f));
                AddTriangleIndices(index, index + 1, index + 2);
                AddTriangleIndices(index, index + 2, index + 3);
            }

            private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int index = AddVertex(a, normal, new Vector2(0f, 0f));
                AddVertex(b, normal, new Vector2(0f, 1f));
                AddVertex(c, normal, new Vector2(1f, 1f));
                AddVertex(d, normal, new Vector2(1f, 0f));
                AddTriangleIndices(index, index + 1, index + 2);
                AddTriangleIndices(index, index + 2, index + 3);
            }

            private void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
            {
                AddTriangle(a, b, c, Vector3.Cross(b - a, c - a).normalized);
            }

            private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                int index = AddVertex(a, normal, new Vector2(0f, 0f));
                AddVertex(b, normal, new Vector2(0.5f, 1f));
                AddVertex(c, normal, new Vector2(1f, 0f));
                AddTriangleIndices(index, index + 1, index + 2);
            }

            private int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                int index = _vertices.Count;
                _vertices.Add(position);
                _normals.Add(normal);
                _uv.Add(uv);
                return index;
            }

            private void AddTriangleIndices(int a, int b, int c)
            {
                _indices.Add(a);
                _indices.Add(b);
                _indices.Add(c);
            }
        }
    }
}
