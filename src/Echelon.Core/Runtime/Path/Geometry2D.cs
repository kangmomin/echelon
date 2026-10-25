using System.Collections.Generic;

namespace Echelon.Core
{
    public static class Geometry2D
    {
        public static SimVec2 GetClosestPointToSegment(SimVec2 point, SimVec2 segmentA, SimVec2 segmentB)
        {
            SimVec2 p = point - segmentA;
            SimVec2 n = segmentB - segmentA;
            float lengthSquared = n.X * n.X + n.Y * n.Y;
            if (lengthSquared < 1e-20f)
            {
                return segmentA;
            }

            float d = (n.X * p.X + n.Y * p.Y) / lengthSquared;
            if (d <= 0.0f)
            {
                return segmentA;
            }

            if (d >= 1.0f)
            {
                return segmentB;
            }

            return segmentA + n * d;
        }

        public static double GetDistanceToSegment(SimVec2 point, SimVec2 segmentA, SimVec2 segmentB)
        {
            return point.DistanceTo(GetClosestPointToSegment(point, segmentA, segmentB));
        }

        public static bool SegmentIntersectsSegment(SimVec2 fromA, SimVec2 toA, SimVec2 fromB, SimVec2 toB)
        {
            return SegmentIntersectsSegment(fromA, toA, fromB, toB, out _);
        }

        public static bool SegmentIntersectsSegment(SimVec2 fromA, SimVec2 toA, SimVec2 fromB, SimVec2 toB, out SimVec2 result)
        {
            result = SimVec2.Zero;
            SimVec2 b = toA - fromA;
            SimVec2 c = fromB - fromA;
            SimVec2 d = toB - fromA;

            float abLength = b.X * b.X + b.Y * b.Y;
            if (abLength <= 0.0f)
            {
                return false;
            }

            SimVec2 bn = b / abLength;
            c = new SimVec2(c.X * bn.X + c.Y * bn.Y, c.Y * bn.X - c.X * bn.Y);
            d = new SimVec2(d.X * bn.X + d.Y * bn.Y, d.Y * bn.X - d.X * bn.Y);

            if ((c.Y < -(float)SimMath.CmpEpsilon && d.Y < -(float)SimMath.CmpEpsilon) ||
                (c.Y > (float)SimMath.CmpEpsilon && d.Y > (float)SimMath.CmpEpsilon))
            {
                return false;
            }

            if (SimMath.IsEqualApprox(c.Y, d.Y))
            {
                return false;
            }

            float abPos = d.X + (c.X - d.X) * d.Y / (d.Y - c.Y);
            if (abPos < 0.0f || abPos > 1.0f)
            {
                return false;
            }

            result = fromA + b * abPos;
            return true;
        }

        public static bool IsPointInPolygon(SimVec2 point, IReadOnlyList<SimVec2> polygon)
        {
            int count = polygon.Count;
            if (count < 3)
            {
                return false;
            }

            SimVec2 furtherAway = new SimVec2(-1e20f, -1e20f);
            SimVec2 furtherAwayOpposite = new SimVec2(1e20f, 1e20f);
            for (int i = 0; i < count; i++)
            {
                furtherAway = furtherAway.Max(polygon[i]);
                furtherAwayOpposite = furtherAwayOpposite.Min(polygon[i]);
            }

            furtherAway += (furtherAway - furtherAwayOpposite) * new SimVec2(1.221313, 1.512312);
            int intersections = 0;
            for (int i = 0; i < count; i++)
            {
                SimVec2 v1 = polygon[i];
                SimVec2 v2 = polygon[(i + 1) % count];
                if (SegmentIntersectsSegment(v1, v2, point, furtherAway, out SimVec2 result))
                {
                    intersections++;
                    if (result.IsEqualApprox(point))
                    {
                        return true;
                    }
                }
            }

            return (intersections & 1) != 0;
        }

        public static bool IsSegmentIntersectingPolygon(SimVec2 from, SimVec2 to, IReadOnlyList<SimVec2> polygon)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                if (SegmentIntersectsSegment(from, to, polygon[i], polygon[(i + 1) % polygon.Count]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
