using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class SpatialHash<T>
    {
        private readonly Dictionary<SimVec2I, List<T>> _buckets = new Dictionary<SimVec2I, List<T>>();

        public SpatialHash(double cellSize = 100.0)
        {
            CellSize = cellSize;
        }

        public double CellSize { get; }

        public void Clear()
        {
            _buckets.Clear();
        }

        public SimVec2I KeyOf(SimVec2 pos)
        {
            return new SimVec2I(
                (int)Math.Floor(pos.X / CellSize),
                (int)Math.Floor(pos.Y / CellSize));
        }

        public void Insert(T item, SimVec2 pos)
        {
            SimVec2I key = KeyOf(pos);
            if (!_buckets.TryGetValue(key, out List<T>? bucket))
            {
                bucket = new List<T>();
                _buckets.Add(key, bucket);
            }

            bucket.Add(item);
        }

        public List<T> Query(SimVec2 pos, double radius)
        {
            List<T> output = new List<T>();
            SimVec2 offset = new SimVec2(radius, radius);
            SimVec2I k0 = KeyOf(pos - offset);
            SimVec2I k1 = KeyOf(pos + offset);
            for (int ky = k0.Y; ky <= k1.Y; ky++)
            {
                for (int kx = k0.X; kx <= k1.X; kx++)
                {
                    if (_buckets.TryGetValue(new SimVec2I(kx, ky), out List<T>? bucket))
                    {
                        output.AddRange(bucket);
                    }
                }
            }

            return output;
        }
    }
}
