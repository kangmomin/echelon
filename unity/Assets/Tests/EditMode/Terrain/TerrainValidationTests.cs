using System;
using System.Collections.Generic;
using Echelon.Unity.Presentation.Terrain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echelon.Unity.Presentation.Terrain.Tests
{
    public sealed class TerrainValidationTests
    {
        [Test]
        public void DataOwnsDefensiveCopiesOfArraysAndFeaturePoints()
        {
            var heights = new float[] { 1f, 2f, 3f, 4f };
            var types = new[] { TerrainType.Open, TerrainType.Open, TerrainType.Open, TerrainType.Open };
            var points = new[] { Vector2.zero, new Vector2(20f, 0f) };
            var feature = new TerrainVectorFeature("Road", points, 12f);
            var data = new TerrainMeshData(
                2, 2, 10f, new Vector2(20f, 20f), heights, types,
                new[] { feature }, Array.Empty<TerrainVectorFeature>());

            heights[0] = 99f;
            types[0] = TerrainType.Water;
            points[0] = new Vector2(500f, 500f);

            Assert.That(data.Heights.Span[0], Is.EqualTo(1f));
            Assert.That(data.Types.Span[0], Is.EqualTo(TerrainType.Open));
            Assert.That(data.Roads.Span[0].Points.Span[0], Is.EqualTo(Vector2.zero));
            Assert.That(data.Validate(), Is.Empty);
        }

        [Test]
        public void ValidationReportsGridLengthAndFeatureErrors()
        {
            var data = new TerrainMeshData(
                2,
                2,
                10f,
                new Vector2(20f, 20f),
                new float[] { 1f },
                new[] { TerrainType.Open },
                new[] { new TerrainVectorFeature("Bad", new[] { Vector2.zero }, 0f) },
                Array.Empty<TerrainVectorFeature>());
            IReadOnlyList<string> errors = data.Validate();
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(4));
            AssertErrorContains(errors, "Heights length");
            AssertErrorContains(errors, "Types length");
            AssertErrorContains(errors, "at least 2 points");
            AssertErrorContains(errors, "invalid width");
            Assert.Throws<ArgumentException>(() => TerrainGeometry.BuildHeightMesh(data));
        }

        [Test]
        public void MeshValidationRejectsUInt16Indices()
        {
            var mesh = new Mesh { name = "UInt16TestMesh", indexFormat = IndexFormat.UInt16 };
            try
            {
                mesh.SetVertices(new[] { Vector3.zero, Vector3.right, Vector3.forward });
                mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
                IReadOnlyList<string> errors = TerrainMeshValidation.ValidateMesh(mesh);
                AssertErrorContains(errors, "UInt32");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static void AssertErrorContains(IReadOnlyList<string> errors, string fragment)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i].Contains(fragment))
                    return;
            }
            Assert.Fail("Expected an error containing '" + fragment + "'.");
        }

        [Test]
        public void OutOfBoundsTypesAreWaterAndHeightsClampToEdgeCells()
        {
            var data = new TerrainMeshData(
                2,
                2,
                10f,
                new Vector2(20f, 20f),
                new float[] { 1f, 2f, 3f, 4f },
                new[] { TerrainType.Open, TerrainType.Road, TerrainType.Forest, TerrainType.Urban },
                Array.Empty<TerrainVectorFeature>(),
                Array.Empty<TerrainVectorFeature>());
            Assert.That(data.TypeAt(new Vector2(-1f, 5f)), Is.EqualTo(TerrainType.Water));
            Assert.That(data.TypeAt(new Vector2(25f, 5f)), Is.EqualTo(TerrainType.Water));
            Assert.That(data.SampleGroundHeight(new Vector2(-100f, -100f)), Is.EqualTo(1f));
            Assert.That(data.SampleGroundHeight(new Vector2(100f, 100f)), Is.EqualTo(4f));
        }
    }
}
