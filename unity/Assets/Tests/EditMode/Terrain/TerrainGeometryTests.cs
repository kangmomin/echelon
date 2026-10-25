using System;
using System.Collections.Generic;
using Echelon.Unity.Presentation.Terrain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echelon.Unity.Presentation.Terrain.Tests
{
    public sealed class TerrainGeometryTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
        }

        [Test]
        public void CoordinatesPreserveSouthwestOriginAndMapNorthToPositiveZ()
        {
            Vector3 unity = TerrainCoordinates.ToUnity(new Vector2(12f, 34f), 5f);
            Assert.That(unity, Is.EqualTo(new Vector3(12f, 5f, 34f)));
            Assert.That(TerrainCoordinates.ToSimulation(unity), Is.EqualTo(new Vector2(12f, 34f)));
            Assert.That(TerrainCoordinates.DefaultMapSize, Is.EqualTo(new Vector2(4000f, 4000f)));
            Assert.That(TerrainCoordinates.DirectionToYawRadians(Vector2.right), Is.EqualTo(Mathf.PI * 0.5f).Within(0.00001f));
        }

        [Test]
        public void HeightMeshUsesUInt32IndicesAndExpectedGridLayout()
        {
            var data = new TerrainMeshData(
                2,
                2,
                10f,
                new Vector2(20f, 20f),
                new float[] { 1f, 2f, 3f, 4f },
                new[] { TerrainType.Open, TerrainType.Open, TerrainType.Open, TerrainType.Open },
                Array.Empty<TerrainVectorFeature>(),
                Array.Empty<TerrainVectorFeature>());
            Mesh mesh = Track(TerrainGeometry.BuildHeightMesh(data));

            Assert.That(mesh.indexFormat, Is.EqualTo(IndexFormat.UInt32));
            Assert.That(mesh.vertexCount, Is.EqualTo(9));
            Assert.That(mesh.GetIndices(0).Length, Is.EqualTo(24));
            int[] indices = mesh.GetIndices(0);
            CollectionAssert.AreEqual(new[] { 0, 3, 1, 1, 3, 4 }, new[] { indices[0], indices[1], indices[2], indices[3], indices[4], indices[5] });
            Assert.That(mesh.vertices[0], Is.EqualTo(new Vector3(0f, 1f, 0f)));
            Assert.That(mesh.vertices[8], Is.EqualTo(new Vector3(19.5f, 4f, 19.5f)));
            Assert.That(data.SampleGroundHeight(new Vector2(5f, 5f)), Is.EqualTo(1f));
            Assert.That(data.SampleGroundHeight(new Vector2(10f, 10f)), Is.EqualTo(2.5f));
            Assert.That(TerrainMeshValidation.ValidateGridMesh(mesh, data), Is.Empty);
        }

        [Test]
        public void FlatHeightMeshHasUpwardNormals()
        {
            TerrainMeshData data = TerrainTestData.Create(2, 2, groundHeight: 20f);
            Mesh mesh = Track(TerrainGeometry.BuildHeightMesh(data));
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++)
                Assert.That(normals[i], Is.EqualTo(Vector3.up));
        }

        [Test]
        public void RibbonHasThreeCrossSectionVerticesDistanceUVsAndUpwardNormals()
        {
            var points = new[] { new Vector2(5f, 5f), new Vector2(5f, 15f) };
            Mesh mesh = Track(TerrainGeometry.BuildRibbonMesh(points, 4f, _ => 7f));

            Assert.That(mesh.indexFormat, Is.EqualTo(IndexFormat.UInt32));
            Assert.That(mesh.vertexCount, Is.EqualTo(6));
            Assert.That(mesh.GetIndices(0).Length, Is.EqualTo(12));
            Assert.That(mesh.vertices[0], Is.EqualTo(new Vector3(7f, 7f, 5f)));
            Assert.That(mesh.vertices[2], Is.EqualTo(new Vector3(3f, 7f, 5f)));
            Assert.That(mesh.vertices[5], Is.EqualTo(new Vector3(3f, 7f, 15f)));
            Assert.That(mesh.uv[0], Is.EqualTo(Vector2.zero));
            Assert.That(mesh.uv[2], Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(mesh.uv[3], Is.EqualTo(new Vector2(0f, 10f)));
            Assert.That(mesh.tangents.Length, Is.EqualTo(6));
            for (int i = 0; i < mesh.normals.Length; i++)
                Assert.That(mesh.normals[i], Is.EqualTo(Vector3.up));
            Assert.That(TerrainMeshValidation.ValidateMesh(mesh, true), Is.Empty);
        }

        [Test]
        public void SmoothPolylineKeepsEndsAndResamplesAtFiveMetersOrLess()
        {
            var points = new[] { Vector2.zero, new Vector2(10f, 0f), new Vector2(10f, 10f) };
            IReadOnlyList<Vector2> smoothed = TerrainGeometry.SmoothPolyline(points);
            Assert.That(smoothed[0], Is.EqualTo(points[0]));
            Assert.That(smoothed[smoothed.Count - 1], Is.EqualTo(points[points.Length - 1]));
            Assert.That(smoothed.Count, Is.GreaterThan(points.Length));
            for (int i = 1; i < smoothed.Count; i++)
            {
                Assert.That(Vector2.Distance(smoothed[i - 1], smoothed[i]), Is.LessThanOrEqualTo(5.0001f));
                Assert.That(Vector2.Distance(smoothed[i - 1], smoothed[i]), Is.GreaterThan(0f));
            }
        }

        [Test]
        public void RiverHeightsUseThirteenSampleMovingAverage()
        {
            TerrainMeshData data = TerrainTestData.Create(2, 2, groundHeight: 8f);
            var points = new[] { new Vector2(5f, 5f), new Vector2(6f, 5f) };
            float[] heights = TerrainGeometry.BuildRiverSurfaceHeights(data, points);
            Assert.That(heights.Length, Is.EqualTo(2));
            Assert.That(heights[0], Is.EqualTo(10.2f).Within(0.0001f));
            Assert.That(heights[1], Is.EqualTo(10.2f).Within(0.0001f));
        }

        [Test]
        public void BridgeGeometryCreatesTwoRailsAndOneDeckSlab()
        {
            var points = new[] { new Vector2(10f, 10f), new Vector2(10f, 30f) };
            IReadOnlyList<TerrainInstance> boxes = TerrainGeometry.BuildBridgeBoxes(points, 14f, 8f);
            Assert.That(boxes.Count, Is.EqualTo(3));
            Assert.That(boxes[0].Position.x, Is.EqualTo(16.3f).Within(0.0001f));
            Assert.That(boxes[1].Position.x, Is.EqualTo(3.7f).Within(0.0001f));
            Assert.That(boxes[0].Position.y, Is.EqualTo(8.5f));
            Assert.That(boxes[0].Scale, Is.EqualTo(new Vector3(0.6f, 1f, 28f)));
            Assert.That(boxes[2].Position, Is.EqualTo(new Vector3(10f, 7.25f, 20f)));
            Assert.That(boxes[2].Scale.x, Is.EqualTo(12.6f).Within(0.0001f));
            Assert.That(boxes[2].Scale.y, Is.EqualTo(1.2f));
            Assert.That(boxes[2].Scale.z, Is.EqualTo(28f));
        }

        [Test]
        public void TypeMaskPacksRoadWaterForestAndUrbanChannels()
        {
            var data = new TerrainMeshData(
                2,
                2,
                10f,
                new Vector2(20f, 20f),
                new float[] { 0f, 0f, 0f, 0f },
                new[] { TerrainType.Road, TerrainType.Water, TerrainType.Forest, TerrainType.Urban },
                Array.Empty<TerrainVectorFeature>(),
                Array.Empty<TerrainVectorFeature>());
            Texture2D texture = Track(TerrainGeometry.BuildTypeMaskTexture(data));
            Color32[] pixels = texture.GetPixels32();
            Assert.That(pixels[0].r, Is.EqualTo(255));
            Assert.That(pixels[0].a, Is.EqualTo(0));
            Assert.That(pixels[1].g, Is.EqualTo(255));
            Assert.That(pixels[2].b, Is.EqualTo(255));
            Assert.That(pixels[3].a, Is.EqualTo(255));
            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _objects.Add(value);
            return value;
        }
    }
}
