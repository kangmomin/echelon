using System;
using System.Collections.Generic;
using Echelon.Presentation.Models;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Tests.EditMode.Models
{
    public sealed class EquipmentModelLibraryTests
    {
        [TestCase("us_sniper_team", "inf_squad", EquipmentModelKind.InfantrySniper, false, 0)]
        [TestCase("us_sapper_squad", "inf_squad", EquipmentModelKind.InfantrySquad, false, 0)]
        [TestCase("us_m7_fist", "fo", EquipmentModelKind.BradleyFist, true, 0)]
        [TestCase("us_mshorad", "aa", EquipmentModelKind.StrykerMshorad, true, 0)]
        [TestCase("us_m3a3", "recon", EquipmentModelKind.Bradley, true, 0)]
        [TestCase("us_m2a3", "ifv", EquipmentModelKind.Bradley, true, 0)]
        [TestCase("us_m1a2_sepv3_tusk", "tank", EquipmentModelKind.AbramsTusk, true, 0)]
        [TestCase("us_m1a2_sepv3", "tank", EquipmentModelKind.Abrams, true, 0)]
        [TestCase("us_m1167", "at", EquipmentModelKind.HumveeTow, true, 0)]
        [TestCase("us_m1296", "ifv", EquipmentModelKind.StrykerDragoon, true, 0)]
        [TestCase("us_m1150_abv", "eng", EquipmentModelKind.Abv, false, 0)]
        [TestCase("us_m1134", "at", EquipmentModelKind.StrykerAtgm, true, 0)]
        [TestCase("us_m1126", "apc", EquipmentModelKind.StrykerIcv, true, 0)]
        [TestCase("us_m10_booker", "tank", EquipmentModelKind.Booker, true, 0)]
        [TestCase("us_jtac", "fo", EquipmentModelKind.HumveeJtac, true, 0)]
        [TestCase("us_m1064", "mortar", EquipmentModelKind.M113Mortar, false, 0)]
        [TestCase("us_m1068_cp", "cp", EquipmentModelKind.M113Cp, false, 0)]
        [TestCase("us_inf_squad", "inf_squad", EquipmentModelKind.InfantrySquad, false, 0)]
        [TestCase("us_inf_squad_jav2", "inf_squad", EquipmentModelKind.InfantrySquad, false, 0)]
        [TestCase("us_hq_team", "inf_squad", EquipmentModelKind.InfantryHq, false, 0)]
        [TestCase("us_hemtt", "truck", EquipmentModelKind.Hemtt, false, 0)]
        [TestCase("us_avenger", "aa", EquipmentModelKind.HumveeAvenger, true, 0)]
        [TestCase("us_ah64e", "heli", EquipmentModelKind.Apache, true, 1)]
        [TestCase("ru_ur77", "eng", EquipmentModelKind.Ur77, true, 0)]
        [TestCase("ru_tunguska", "aa", EquipmentModelKind.Tunguska, true, 0)]
        [TestCase("ru_t90m", "tank", EquipmentModelKind.T90, true, 0)]
        [TestCase("ru_tor_m2", "aa", EquipmentModelKind.Tor, true, 0)]
        [TestCase("ru_t80bvm", "tank", EquipmentModelKind.T80, true, 0)]
        [TestCase("ru_t72b3", "tank", EquipmentModelKind.T72, true, 0)]
        [TestCase("ru_sniper_team", "inf_squad", EquipmentModelKind.InfantrySniper, false, 0)]
        [TestCase("ru_shturm_s", "at", EquipmentModelKind.ShturmS, true, 0)]
        [TestCase("ru_r330zh", "ew", EquipmentModelKind.Jammer, false, 0)]
        [TestCase("ru_sapper_squad", "inf_squad", EquipmentModelKind.InfantrySquad, false, 0)]
        [TestCase("ru_msr_squad", "inf_squad", EquipmentModelKind.InfantrySquad, false, 0)]
        [TestCase("ru_kamaz", "truck", EquipmentModelKind.Truck, false, 0)]
        [TestCase("ru_ka52", "heli", EquipmentModelKind.Ka52, true, 2)]
        [TestCase("ru_fo_team", "fo", EquipmentModelKind.Bmp1Recon, true, 0)]
        [TestCase("ru_hq_team", "inf_squad", EquipmentModelKind.InfantryHq, false, 0)]
        [TestCase("ru_btr82a", "apc", EquipmentModelKind.Btr82A, true, 0)]
        [TestCase("ru_brm3k", "recon", EquipmentModelKind.Brm3K, true, 0)]
        [TestCase("ru_bmp3", "ifv", EquipmentModelKind.Bmp3, true, 0)]
        [TestCase("ru_bmpt", "ifv", EquipmentModelKind.Bmpt, true, 0)]
        [TestCase("ru_bmp1ksh_cp", "cp", EquipmentModelKind.Bmp1Command, true, 0)]
        [TestCase("ru_bmp2m", "ifv", EquipmentModelKind.Bmp2, true, 0)]
        [TestCase("ru_at_squad", "inf_squad", EquipmentModelKind.InfantryAt, false, 0)]
        [TestCase("ru_2s9", "mortar", EquipmentModelKind.Nona, true, 0)]
        public void DataElement_HasNonemptyGeometryAndExpectedParts(
            string elementId,
            string elementClass,
            EquipmentModelKind expectedKind,
            bool expectTurret,
            int expectedRotors)
        {
            string side = elementId.StartsWith("us_", StringComparison.Ordinal) ? "blue" : "red";
            EquipmentModelHandle handle = EquipmentModelLibrary.Build(elementId, elementClass, side);
            try
            {
                Assert.AreEqual(expectedKind, handle.Kind, elementId);
                Assert.AreEqual(elementId, handle.ElementId);
                Assert.AreEqual(elementClass, handle.ElementClass);
                Assert.AreEqual(side, handle.Side);
                Assert.IsNotNull(handle.Root);
                Assert.IsNull(handle.RootTransform.parent);
                Assert.AreEqual(Vector3.zero, handle.RootTransform.localPosition);
                Assert.AreEqual(Vector3.one, handle.RootTransform.localScale);
                Assert.IsNotNull(handle.Tilt);
                Assert.AreSame(handle.RootTransform, handle.Tilt.parent);
                Assert.AreEqual(Vector3.back, EquipmentModelLibrary.Front);

                AssertMesh(handle.HullMesh, handle.HullRoles, handle.HullRenderer);
                Assert.GreaterOrEqual(handle.HullRoles.Count, 2);
                Assert.LessOrEqual(handle.HullRoles.Count, 3);
                Assert.AreEqual(EquipmentMeshRole.Camo, handle.HullRoles[0]);
                Assert.AreEqual(EquipmentMeshRole.Dark, handle.HullRoles[1]);
                if (handle.HullRoles.Count == 3)
                {
                    Assert.AreEqual(EquipmentMeshRole.Marker, handle.HullRoles[2]);
                }
                bool hasMarker = handle.HullRoles.Count == 3;
                for (int i = 0; i < handle.TurretRoles.Count; i++)
                {
                    hasMarker |= handle.TurretRoles[i] == EquipmentMeshRole.Marker;
                }
                Assert.IsTrue(hasMarker, elementId);
                Assert.AreSame(handle.HullMesh, handle.Hull.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreSame(handle.Tilt, handle.Hull.parent);
                Assert.AreEqual(Vector3.zero, handle.Hull.localPosition);

                Assert.AreEqual(expectTurret, handle.HasTurret, elementId);
                if (expectTurret)
                {
                    Assert.IsNotNull(handle.Turret, elementId);
                    AssertMesh(handle.TurretMesh, handle.TurretRoles, handle.TurretRenderer);
                    Assert.AreSame(handle.TurretMesh, handle.Turret.GetComponent<MeshFilter>().sharedMesh);
                    Assert.AreSame(handle.Tilt, handle.Turret.parent);
                    Assert.Greater(handle.Turret.localPosition.y, 0.0f, elementId);
                }
                else
                {
                    Assert.IsNull(handle.Turret);
                    Assert.IsNull(handle.TurretMesh);
                    Assert.IsNull(handle.TurretRenderer);
                }

                Assert.AreEqual(expectedRotors, handle.RotorCount, elementId);
                if (expectedRotors > 0)
                {
                    AssertRotor(handle.RotorMesh, handle.RotorRoles, handle.Rotor, handle.RotorRenderer, handle.Tilt);
                }
                else
                {
                    Assert.IsNull(handle.Rotor);
                    Assert.IsNull(handle.RotorMesh);
                    Assert.IsNull(handle.RotorRenderer);
                }
                if (expectedRotors > 1)
                {
                    AssertRotor(handle.Rotor2Mesh, handle.Rotor2Roles, handle.Rotor2, handle.Rotor2Renderer, handle.Tilt);
                    Assert.Greater(handle.Rotor2.localPosition.y, handle.Rotor.localPosition.y);
                }
                else
                {
                    Assert.IsNull(handle.Rotor2);
                    Assert.IsNull(handle.Rotor2Mesh);
                    Assert.IsNull(handle.Rotor2Renderer);
                }

                int expectedRenderers = 1 + (expectTurret ? 1 : 0) + expectedRotors;
                Assert.AreEqual(expectedRenderers, handle.Root.GetComponentsInChildren<MeshRenderer>(true).Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(handle.Root);
            }
        }

        [Test]
        public void Pivots_PreserveGodotScaleAndLocalCoordinates()
        {
            EquipmentModelHandle abrams = EquipmentModelLibrary.Build("us_m1a2_sepv3", "tank", "blue");
            EquipmentModelHandle apache = EquipmentModelLibrary.Build("us_ah64e", "heli", "blue");
            EquipmentModelHandle ka52 = EquipmentModelLibrary.Build("ru_ka52", "heli", "red");
            try
            {
                AssertVector(new Vector3(0.0f, 1.55f, 0.3f) * EquipmentModelLibrary.ModelScale, abrams.Turret.localPosition);
                AssertVector(new Vector3(0.0f, 0.75f, -2.6f) * EquipmentModelLibrary.ModelScale, apache.Turret.localPosition);
                AssertVector(new Vector3(0.0f, 3.85f, -0.2f) * EquipmentModelLibrary.ModelScale, apache.Rotor.localPosition);
                AssertVector(new Vector3(0.0f, 0.9f, -0.8f) * EquipmentModelLibrary.ModelScale, ka52.Turret.localPosition);
                AssertVector(new Vector3(0.0f, 3.6f, 0.0f) * EquipmentModelLibrary.ModelScale, ka52.Rotor.localPosition);
                AssertVector(new Vector3(0.0f, 4.3f, 0.0f) * EquipmentModelLibrary.ModelScale, ka52.Rotor2.localPosition);
                Assert.AreEqual(EquipmentModelLibrary.ModelScale, 1.3f, 0.00001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(abrams.Root);
                UnityEngine.Object.DestroyImmediate(apache.Root);
                UnityEngine.Object.DestroyImmediate(ka52.Root);
            }
        }

        [Test]
        public void Build_SharesMeshesAndSideMaterialsWithoutMixingTuskLoadout()
        {
            var handles = new List<EquipmentModelHandle>
            {
                EquipmentModelLibrary.Build("us_m1a2_sepv3", "tank", "blue"),
                EquipmentModelLibrary.Build("us_m1a2_sepv3", "tank", "blue"),
                EquipmentModelLibrary.Build("us_m1a2_sepv3", "tank", "red"),
                EquipmentModelLibrary.Build("us_m1a2_sepv3", "tank", "blue", "sepv3_tusk"),
                EquipmentModelLibrary.Build("us_m1a2_sepv3", "tank", "blue", "sepv3")
            };
            try
            {
                Assert.AreSame(handles[0].HullMesh, handles[1].HullMesh);
                Assert.AreSame(handles[0].HullMesh, handles[2].HullMesh);
                Assert.AreSame(handles[0].HullMesh, handles[4].HullMesh);
                Assert.AreSame(handles[0].TurretMesh, handles[1].TurretMesh);
                Assert.AreSame(handles[0].HullRenderer.sharedMaterials[0], handles[1].HullRenderer.sharedMaterials[0]);
                Assert.AreNotSame(handles[0].HullRenderer.sharedMaterials[0], handles[2].HullRenderer.sharedMaterials[0]);
                Assert.AreNotSame(handles[0].TurretRenderer.sharedMaterials[2], handles[2].TurretRenderer.sharedMaterials[2]);
                Assert.AreEqual(EquipmentModelKind.Abrams, handles[0].Kind);
                Assert.AreEqual(EquipmentModelKind.Abrams, handles[4].Kind);
                Assert.AreEqual(EquipmentModelKind.AbramsTusk, handles[3].Kind);
                Assert.AreNotSame(handles[0].HullMesh, handles[3].HullMesh);
                Assert.Greater(handles[3].HullMesh.vertexCount, handles[0].HullMesh.vertexCount);
            }
            finally
            {
                for (int i = 0; i < handles.Count; i++)
                {
                    UnityEngine.Object.DestroyImmediate(handles[i].Root);
                }
            }
        }

        private static void AssertRotor(
            Mesh mesh,
            IReadOnlyList<EquipmentMeshRole> roles,
            Transform transform,
            MeshRenderer renderer,
            Transform expectedParent)
        {
            Assert.IsNotNull(transform);
            AssertMesh(mesh, roles, renderer);
            Assert.AreEqual(1, roles.Count);
            Assert.AreEqual(EquipmentMeshRole.Dark, roles[0]);
            Assert.AreSame(expectedParent, transform.parent);
            Assert.Greater(transform.localPosition.y, 0.0f);
            Assert.Less(renderer.sharedMaterials[0].GetColor("_BaseColor").a, 1.0f);
        }

        private static void AssertMesh(
            Mesh mesh,
            IReadOnlyList<EquipmentMeshRole> roles,
            MeshRenderer renderer)
        {
            Assert.IsNotNull(mesh);
            Assert.IsNotNull(renderer);
            Assert.Greater(mesh.vertexCount, 0);
            Assert.Greater(mesh.bounds.size.sqrMagnitude, 0.0f);
            Assert.AreEqual(roles.Count, mesh.subMeshCount);
            Material[] materials = renderer.sharedMaterials;
            Assert.AreEqual(roles.Count, materials.Length);
            for (int i = 0; i < roles.Count; i++)
            {
                Assert.IsNotNull(materials[i]);
                Assert.Greater(mesh.GetTriangles(i).Length, 0);
                Assert.AreEqual(0, mesh.GetTriangles(i).Length % 3);
            }
            AssertFrontFacesFollowNormals(mesh);
        }

        // Unity 의 앞면은 시계 방향이라 앞면 쪽 기하 법선이 Cross(b - a, c - a) 다. 이것이 정점 법선과 반대면
        // 바깥 면이 컬링되고 안쪽 면(아래를 보는 바닥 등)만 보여 차량이 검게 그려진다.
        private static void AssertFrontFacesFollowNormals(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] triangles = mesh.GetTriangles(s);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]];
                    Vector3 face = Vector3.Cross(vertices[triangles[t + 1]] - a, vertices[triangles[t + 2]] - a);
                    if (face.sqrMagnitude < 1e-12f)
                        continue;
                    Vector3 normal = normals[triangles[t]] + normals[triangles[t + 1]] + normals[triangles[t + 2]];
                    Assert.Greater(Vector3.Dot(face, normal), 0.0f, mesh.name + " submesh " + s + " triangle " + t / 3);
                }
            }
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.00001f);
            Assert.AreEqual(expected.y, actual.y, 0.00001f);
            Assert.AreEqual(expected.z, actual.z, 0.00001f);
        }
    }
}
