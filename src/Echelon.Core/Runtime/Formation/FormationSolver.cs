using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public static class FormationSolver
    {
        public const double TrailSpacingM = 50.0;

        public static List<SimVec2> LocalOffsets(FormationDef def, int count, string terrain = "open")
        {
            List<SimVec2> output = new List<SimVec2>();
            if (count <= 0 || def.Slots.Count == 0)
            {
                return output;
            }

            List<SimVec2> baseSlots = PickSlots(def, count);
            double scale = def.SpacingScale.TryGetValue(terrain, out double value) ? value : 1.0;
            output.Capacity = count;
            for (int i = 0; i < count; i++)
            {
                output.Add(baseSlots[i] * scale);
            }

            return output;
        }

        public static List<SimVec2> WorldSlots(FormationDef def, int count, SimVec2 origin, double heading, string terrain = "open")
        {
            List<SimVec2> local = LocalOffsets(def, count, terrain);
            List<SimVec2> output = new List<SimVec2>(local.Count);
            for (int i = 0; i < local.Count; i++)
            {
                output.Add(LocalToWorld(local[i], origin, heading));
            }

            return output;
        }

        public static SimVec2 ForwardVector(double heading)
        {
            return new SimVec2(Math.Sin(heading), Math.Cos(heading));
        }

        public static SimVec2 RightVector(double heading)
        {
            return new SimVec2(Math.Cos(heading), -Math.Sin(heading));
        }

        public static SimVec2 LocalToWorld(SimVec2 local, SimVec2 origin, double heading)
        {
            return origin + RightVector(heading) * local.X + ForwardVector(heading) * local.Y;
        }

        public static SimVec2 WorldToLocal(SimVec2 world, SimVec2 origin, double heading)
        {
            SimVec2 relative = world - origin;
            return new SimVec2(relative.Dot(RightVector(heading)), relative.Dot(ForwardVector(heading)));
        }

        public static double HeadingFromVector(SimVec2 direction)
        {
            return Math.Atan2(direction.X, direction.Y);
        }

        private static List<SimVec2> PickSlots(FormationDef def, int count)
        {
            if (def.Slots.TryGetValue(count, out List<SimVec2>? exact))
            {
                return exact;
            }

            int best = -1;
            int largest = -1;
            foreach (int key in def.Slots.Keys)
            {
                if (key >= count && (best == -1 || key < best))
                {
                    best = key;
                }

                if (key > largest)
                {
                    largest = key;
                }
            }

            if (best != -1)
            {
                return def.Slots[best].GetRange(0, count);
            }

            List<SimVec2> points = new List<SimVec2>(def.Slots[largest]);
            SimVec2 last = points[points.Count - 1];
            while (points.Count < count)
            {
                last += new SimVec2(0.0f, -(float)TrailSpacingM);
                points.Add(last);
            }

            return points;
        }
    }
}
