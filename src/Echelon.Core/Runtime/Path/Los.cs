using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public static class Los
    {
        public const double EyeVehicleM = 2.5;
        public const double EyeFootM = 1.5;
        public const double TargetVehicleM = 2.0;
        public const double TargetFootM = 1.0;
        public const double MinStepM = 10.0;
        public const int MaxSamples = 48;
        public const double EdgeClearM = 30.0;
        public const double CoverBlockM = 60.0;
        public const double SmokeHeightM = 20.0;

        public sealed class Smoke
        {
            public SimVec2 Pos { get; set; }
            public double Radius { get; set; }
            public long UntilTick { get; set; }

            public Smoke()
            {
            }

            public Smoke(SimVec2 pos, double radius, long untilTick)
            {
                Pos = pos;
                Radius = radius;
                UntilTick = untilTick;
            }
        }

        public static double EyeHeight(Element element)
        {
            return (element.IsInfantry() ? EyeFootM : EyeVehicleM) + element.AltitudeM();
        }

        public static double TargetHeight(Element element)
        {
            return (element.IsInfantry() ? TargetFootM : TargetVehicleM) + element.AltitudeM();
        }

        public static bool Between(Terrain terrain, IEnumerable<Smoke> smokes, long tick, Element observer, Element target)
        {
            return Check(terrain, smokes, tick, observer.Pos, EyeHeight(observer), target.Pos, TargetHeight(target));
        }

        public static bool Check(Terrain terrain, IEnumerable<Smoke> smokes, long tick,
            SimVec2 a, double ah, SimVec2 b, double bh)
        {
            double distance = a.DistanceTo(b);
            if (distance < 1.0)
            {
                return true;
            }

            foreach (Smoke smoke in smokes)
            {
                if (smoke.UntilTick > tick &&
                    Geometry2D.GetClosestPointToSegment(smoke.Pos, a, b).DistanceTo(smoke.Pos) < smoke.Radius)
                {
                    return false;
                }
            }

            double za = terrain.GroundHeight(a) + ah;
            double zb = terrain.GroundHeight(b) + bh;
            int n = SimMath.Clamp((int)Math.Ceiling(distance / MinStepM), 2, MaxSamples);
            double step = distance / n;
            double coverRun = 0.0;
            for (int i = 1; i < n; i++)
            {
                double t = (double)i / n;
                SimVec2 p = a.Lerp(b, t);
                double lineHeight = SimMath.Lerp(za, zb, t);
                double ground = terrain.GroundHeight(p);
                if (ground > lineHeight)
                {
                    return false;
                }

                double along = t * distance;
                if (along < EdgeClearM || distance - along < EdgeClearM)
                {
                    continue;
                }

                double cover = terrain.CoverHeightAt(p);
                if (cover > 0.0 && ground + cover > lineHeight)
                {
                    coverRun += step;
                    if (coverRun > CoverBlockM)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
