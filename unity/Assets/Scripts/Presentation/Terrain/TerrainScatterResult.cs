using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain
{
    public readonly struct TerrainHouse
    {
        public Vector3 Position { get; }
        public float YawRadians { get; }
        public Vector3 Size { get; }
        public Color WallColor { get; }
        public Color RoofColor { get; }

        public TerrainHouse(Vector3 position, float yawRadians, Vector3 size, Color wallColor, Color roofColor)
        {
            Position = position;
            YawRadians = yawRadians;
            Size = size;
            WallColor = wallColor;
            RoofColor = roofColor;
        }

        public TerrainInstance CreateWallInstance()
        {
            return new TerrainInstance(Position, Rotation, Size, WallColor);
        }

        public TerrainInstance CreateRoofInstance()
        {
            return new TerrainInstance(Position, Rotation, Size, RoofColor);
        }

        private Quaternion Rotation => Quaternion.AngleAxis(YawRadians * Mathf.Rad2Deg, Vector3.up);
    }

    public sealed class TerrainScatterResult
    {
        public IReadOnlyList<TerrainInstance> BroadleafTrees { get; }
        public IReadOnlyList<TerrainInstance> ConiferTrees { get; }
        public IReadOnlyList<TerrainInstance> Bushes { get; }
        public IReadOnlyList<TerrainHouse> Houses { get; }

        internal TerrainScatterResult(
            List<TerrainInstance> broadleafTrees,
            List<TerrainInstance> coniferTrees,
            List<TerrainInstance> bushes,
            List<TerrainHouse> houses)
        {
            BroadleafTrees = Array.AsReadOnly(broadleafTrees.ToArray());
            ConiferTrees = Array.AsReadOnly(coniferTrees.ToArray());
            Bushes = Array.AsReadOnly(bushes.ToArray());
            Houses = Array.AsReadOnly(houses.ToArray());
        }
    }
}
