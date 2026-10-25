using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echelon.Unity.Presentation.Terrain
{
    public sealed class TerrainVectorFeature
    {
        public string Name { get; }
        public ReadOnlyMemory<Vector2> Points { get; }
        public float WidthMeters { get; }

        public TerrainVectorFeature(string name, IEnumerable<Vector2> points, float widthMeters = 0f)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));

            Name = name ?? string.Empty;
            Points = Copy(points);
            WidthMeters = widthMeters;
        }

        private static ReadOnlyMemory<Vector2> Copy(IEnumerable<Vector2> source)
        {
            var copy = new List<Vector2>();
            foreach (Vector2 point in source)
                copy.Add(point);
            return copy.ToArray();
        }
    }
}
