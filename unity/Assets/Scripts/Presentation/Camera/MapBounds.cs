using System;
using UnityEngine;

namespace Echelon.Unity.Presentation
{
    [Serializable]
    public struct MapBounds
    {
        [SerializeField] private Vector2 min;
        [SerializeField] private Vector2 size;

        public Vector2 Min => min;
        public Vector2 Size => Vector2.Max(size, Vector2.zero);
        public Vector2 Max => min + Size;
        public Vector2 Center => (min + Max) * 0.5f;

        public MapBounds(Vector2 min, Vector2 size)
        {
            this.min = min;
            this.size = Vector2.Max(size, Vector2.zero);
        }

        public static MapBounds FromMinMax(Vector2 min, Vector2 max)
        {
            return new MapBounds(min, max - min);
        }

        public bool Contains(Vector2 simPosition)
        {
            Vector2 max = Max;
            return simPosition.x >= min.x && simPosition.y >= min.y
                && simPosition.x < max.x && simPosition.y < max.y;
        }

        public Vector2 Clamp(Vector2 simPosition, float margin = 0f)
        {
            margin = Mathf.Max(0f, margin);
            Vector2 max = Max;
            return new Vector2(
                ClampAxis(simPosition.x, min.x, max.x, margin),
                ClampAxis(simPosition.y, min.y, max.y, margin));
        }

        private static float ClampAxis(float value, float min, float max, float margin)
        {
            float lower = min + margin;
            float upper = max - margin;
            if (lower > upper)
                return (min + max) * 0.5f;
            return Mathf.Clamp(value, lower, upper);
        }
    }
}
