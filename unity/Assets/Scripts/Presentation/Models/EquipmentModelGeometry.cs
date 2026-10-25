using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Presentation.Models
{
    internal sealed class EquipmentMeshData
    {
        internal readonly List<Vector3> Vertices = new List<Vector3>();
        internal readonly List<Vector3> Normals = new List<Vector3>();
        internal readonly List<int> Triangles = new List<int>();

        internal void AddFace(Vector3[] points, Vector3 normal)
        {
            var normals = new Vector3[points.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = normal;
            }
            AddSmoothFace(points, normals);
        }

        internal void AddSmoothFace(Vector3[] points, Vector3[] normals)
        {
            int start = Vertices.Count;
            Vector3 outward = Vector3.zero;
            for (int i = 0; i < points.Length; i++)
            {
                Vertices.Add(points[i]);
                Normals.Add(normals[i]);
                outward += normals[i];
            }

            Vector3 cross = Vector3.Cross(points[1] - points[0], points[2] - points[0]);
            bool reverse = Vector3.Dot(cross, outward) > 0.0f;
            for (int i = 1; i < points.Length - 1; i++)
            {
                Triangles.Add(start);
                if (reverse)
                {
                    Triangles.Add(start + i + 1);
                    Triangles.Add(start + i);
                }
                else
                {
                    Triangles.Add(start + i);
                    Triangles.Add(start + i + 1);
                }
            }
        }
    }

    internal struct EquipmentPart
    {
        internal EquipmentMeshData Mesh;
        internal Matrix4x4 Transform;

        internal EquipmentPart(EquipmentMeshData mesh, Matrix4x4 transform)
        {
            Mesh = mesh;
            Transform = transform;
        }
    }

    internal sealed class EquipmentParts
    {
        private readonly List<EquipmentPart>[] byRole =
        {
            new List<EquipmentPart>(),
            new List<EquipmentPart>(),
            new List<EquipmentPart>()
        };

        internal bool IsEmpty
        {
            get
            {
                return byRole[0].Count == 0 && byRole[1].Count == 0 && byRole[2].Count == 0;
            }
        }

        internal void Add(EquipmentMeshRole role, EquipmentMeshData mesh, Matrix4x4 transform)
        {
            byRole[(int)role].Add(new EquipmentPart(mesh, transform));
        }

        internal List<EquipmentPart> ForRole(EquipmentMeshRole role)
        {
            return byRole[(int)role];
        }
    }

    internal static class EquipmentGeometry
    {
        private static readonly EquipmentMeshRole[] RoleOrder =
        {
            EquipmentMeshRole.Camo,
            EquipmentMeshRole.Dark,
            EquipmentMeshRole.Marker
        };

        internal static Mesh Commit(EquipmentParts parts, string name, out EquipmentMeshRole[] roles)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var roleList = new List<EquipmentMeshRole>();
            var submeshes = new List<List<int>>();

            for (int r = 0; r < RoleOrder.Length; r++)
            {
                EquipmentMeshRole role = RoleOrder[r];
                List<EquipmentPart> roleParts = parts.ForRole(role);
                if (roleParts.Count == 0)
                {
                    continue;
                }

                roleList.Add(role);
                var triangles = new List<int>();
                submeshes.Add(triangles);
                for (int p = 0; p < roleParts.Count; p++)
                {
                    EquipmentPart part = roleParts[p];
                    int vertexOffset = vertices.Count;
                    Matrix4x4 normalTransform = part.Transform.inverse.transpose;
                    for (int i = 0; i < part.Mesh.Vertices.Count; i++)
                    {
                        vertices.Add(part.Transform.MultiplyPoint3x4(part.Mesh.Vertices[i]));
                        normals.Add(normalTransform.MultiplyVector(part.Mesh.Normals[i]).normalized);
                    }
                    for (int i = 0; i < part.Mesh.Triangles.Count; i++)
                    {
                        triangles.Add(vertexOffset + part.Mesh.Triangles[i]);
                    }
                }
            }

            var mesh = new Mesh();
            mesh.name = name;
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++)
            {
                mesh.SetTriangles(submeshes[i], i, false);
            }
            mesh.RecalculateBounds();
            roles = roleList.ToArray();
            return mesh;
        }

        internal static EquipmentMeshData Box(Vector3 size)
        {
            float x = size.x * 0.5f;
            float y = size.y * 0.5f;
            float z = size.z * 0.5f;
            var p = new[]
            {
                new Vector3(-x, -y, -z), new Vector3(x, -y, -z),
                new Vector3(x, y, -z), new Vector3(-x, y, -z),
                new Vector3(-x, -y, z), new Vector3(x, -y, z),
                new Vector3(x, y, z), new Vector3(-x, y, z)
            };
            var mesh = new EquipmentMeshData();
            mesh.AddFace(new[] { p[0], p[3], p[2], p[1] }, Vector3.back);
            mesh.AddFace(new[] { p[4], p[5], p[6], p[7] }, Vector3.forward);
            mesh.AddFace(new[] { p[0], p[4], p[7], p[3] }, Vector3.left);
            mesh.AddFace(new[] { p[1], p[2], p[6], p[5] }, Vector3.right);
            mesh.AddFace(new[] { p[3], p[7], p[6], p[2] }, Vector3.up);
            mesh.AddFace(new[] { p[0], p[1], p[5], p[4] }, Vector3.down);
            return mesh;
        }

        internal static EquipmentMeshData Wedge(float width, float height, float length, bool front)
        {
            float x = width * 0.5f;
            float y = height * 0.5f;
            float z = length * 0.5f;
            float frontZ = front ? -z : z;
            float backZ = -frontZ;
            var a = new Vector3(-x, -y, frontZ);
            var b = new Vector3(x, -y, frontZ);
            var c = new Vector3(-x, -y, backZ);
            var d = new Vector3(x, -y, backZ);
            var e = new Vector3(-x, y, backZ);
            var f = new Vector3(x, y, backZ);
            Vector3 slopeNormal = new Vector3(0.0f, length, front ? -height : height).normalized;
            Vector3 verticalNormal = front ? Vector3.forward : Vector3.back;

            var mesh = new EquipmentMeshData();
            mesh.AddFace(new[] { a, b, d, c }, Vector3.down);
            mesh.AddFace(new[] { a, e, f, b }, slopeNormal);
            mesh.AddFace(new[] { c, d, f, e }, verticalNormal);
            mesh.AddFace(new[] { a, c, e }, Vector3.left);
            mesh.AddFace(new[] { b, f, d }, Vector3.right);
            return mesh;
        }

        internal static EquipmentMeshData Cylinder(float radius, float length, int segments)
        {
            var mesh = new EquipmentMeshData();
            float half = length * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2.0f * i / segments;
                float a1 = Mathf.PI * 2.0f * (i + 1) / segments;
                var n0 = new Vector3(Mathf.Sin(a0), 0.0f, Mathf.Cos(a0));
                var n1 = new Vector3(Mathf.Sin(a1), 0.0f, Mathf.Cos(a1));
                Vector3 b0 = new Vector3(n0.x * radius, -half, n0.z * radius);
                Vector3 b1 = new Vector3(n1.x * radius, -half, n1.z * radius);
                Vector3 t0 = new Vector3(n0.x * radius, half, n0.z * radius);
                Vector3 t1 = new Vector3(n1.x * radius, half, n1.z * radius);
                mesh.AddSmoothFace(
                    new[] { b0, b1, t1, t0 },
                    new[] { n0, n1, n1, n0 });
                mesh.AddFace(
                    new[] { new Vector3(0.0f, half, 0.0f), t0, t1 },
                    Vector3.up);
                mesh.AddFace(
                    new[] { new Vector3(0.0f, -half, 0.0f), b1, b0 },
                    Vector3.down);
            }
            return mesh;
        }

        internal static EquipmentMeshData Sphere(int segments, int rings)
        {
            var mesh = new EquipmentMeshData();
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i < segments; i++)
                {
                    Vector3 a = SpherePoint(j, i, segments, rings);
                    Vector3 b = SpherePoint(j, i + 1, segments, rings);
                    Vector3 c = SpherePoint(j + 1, i + 1, segments, rings);
                    Vector3 d = SpherePoint(j + 1, i, segments, rings);
                    if (j == 0)
                    {
                        mesh.AddSmoothFace(new[] { a, c, d }, new[] { a, c, d });
                    }
                    else if (j == rings - 1)
                    {
                        mesh.AddSmoothFace(new[] { a, b, c }, new[] { a, b, c });
                    }
                    else
                    {
                        mesh.AddSmoothFace(new[] { a, b, c, d }, new[] { a, b, c, d });
                    }
                }
            }
            return mesh;
        }

        private static Vector3 SpherePoint(int ring, int segment, int segments, int rings)
        {
            float theta = Mathf.PI * ring / rings;
            float phi = Mathf.PI * 2.0f * segment / segments;
            float radius = Mathf.Sin(theta);
            return new Vector3(radius * Mathf.Sin(phi), Mathf.Cos(theta), radius * Mathf.Cos(phi));
        }
    }
}
