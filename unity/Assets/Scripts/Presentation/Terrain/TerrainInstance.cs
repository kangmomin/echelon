using System;
using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain
{
    public readonly struct TerrainInstance : IEquatable<TerrainInstance>
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
        public Color Color { get; }

        public TerrainInstance(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Color = color;
        }

        public Matrix4x4 ToMatrix()
        {
            return Matrix4x4.TRS(Position, Rotation, Scale);
        }

        public bool Equals(TerrainInstance other)
        {
            return Position.Equals(other.Position) && Rotation.Equals(other.Rotation)
                && Scale.Equals(other.Scale) && Color.Equals(other.Color);
        }

        public override bool Equals(object obj)
        {
            return obj is TerrainInstance other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Position.GetHashCode();
                hash = (hash * 397) ^ Rotation.GetHashCode();
                hash = (hash * 397) ^ Scale.GetHashCode();
                return (hash * 397) ^ Color.GetHashCode();
            }
        }
    }
}
