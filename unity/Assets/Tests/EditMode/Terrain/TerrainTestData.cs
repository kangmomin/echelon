using System;
using Echelon.Unity.Presentation.Terrain;
using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain.Tests
{
    internal static class TerrainTestData
    {
        internal static TerrainMeshData Create(
            int width,
            int height,
            TerrainType fill = TerrainType.Open,
            float groundHeight = 10f,
            int seed = TerrainMeshData.DefaultDecorationSeed,
            TerrainVectorFeature[] roads = null,
            TerrainVectorFeature[] rivers = null)
        {
            var heights = new float[width * height];
            var types = new TerrainType[width * height];
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = groundHeight;
                types[i] = fill;
            }
            return new TerrainMeshData(
                width,
                height,
                10f,
                new Vector2(width * 10f, height * 10f),
                heights,
                types,
                roads ?? Array.Empty<TerrainVectorFeature>(),
                rivers ?? Array.Empty<TerrainVectorFeature>(),
                decorationSeed: seed);
        }
    }
}
