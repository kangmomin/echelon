using System;
using Echelon.Unity.Presentation.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain.Tests
{
    public sealed class TerrainViewBuilderTests
    {
        [Test]
        public void BuildCreatesValidatedGroundRibbonsBridgeAndInstancedDecorations()
        {
            const int width = 20;
            const int height = 20;
            var heights = new float[width * height];
            var types = new TerrainType[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    heights[index] = 10f;
                    types[index] = TerrainType.Open;
                    if (y >= 9 && y <= 10)
                    {
                        heights[index] = 4f;
                        types[index] = TerrainType.Water;
                    }
                    if (x >= 9 && x <= 10)
                        types[index] = TerrainType.Road;
                    if (x >= 2 && x <= 5 && y >= 14 && y <= 17)
                        types[index] = TerrainType.Forest;
                    if (x >= 14 && x <= 16 && y >= 14 && y <= 16)
                        types[index] = TerrainType.Urban;
                }
            }

            var road = new TerrainVectorFeature("NorthRoad", new[] { new Vector2(100f, 0f), new Vector2(100f, 200f) }, 12f);
            var river = new TerrainVectorFeature("River", new[] { new Vector2(0f, 100f), new Vector2(200f, 100f) }, 30f);
            var data = new TerrainMeshData(
                width, height, 10f, new Vector2(200f, 200f), heights, types,
                new[] { road }, new[] { river });
            TerrainView view = null;
            try
            {
                view = new TerrainViewBuilder().Build(data, TerrainViewQuality.Desktop);
                Assert.That(view.Validate(), Is.Empty);
                Assert.That(view.GroundMesh.vertexCount, Is.EqualTo(441));
                Assert.That(view.Statistics.RoadCount, Is.EqualTo(1));
                Assert.That(view.Statistics.RiverCount, Is.EqualTo(1));
                Assert.That(view.Statistics.BridgeCount, Is.GreaterThan(0));
                Assert.That(view.Statistics.BridgePieceCount, Is.GreaterThan(0));
                Assert.That(view.Statistics.SharedMeshCount, Is.GreaterThanOrEqualTo(4));
                Assert.That(view.Statistics.InstancedDrawCallCount, Is.GreaterThan(0));
                Assert.That(view.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(3));
                Assert.That(view.Decorations, Is.Not.Null);
            }
            finally
            {
                if (view != null)
                    view.Dispose();
            }
        }
    }
}
