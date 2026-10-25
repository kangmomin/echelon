using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public interface IDeploymentTerrain
    {
        bool IsPassable(SimVec2 point);
        SimVec2 NearestPassable(SimVec2 point);
    }

    public static class Deployment
    {
        public const double FrontInsetM = 180.0;
        public const double SupportDepthM = 480.0;
        public const double CpDepthM = 600.0;
        public const double EdgeMarginM = 150.0;
        public const string SupportSlotPrefix = "sup_";

        // 지원 중대 슬롯(보급·포병·방공·헬기·고정익)은 기동 중대 대열이 아니라 후방 지원 대열에 둔다(D-050).
        public static bool IsSupportSlot(string slot)
        {
            return slot.StartsWith(SupportSlotPrefix, StringComparison.Ordinal);
        }

        public static double Facing(MapRect zone, SimVec2 mapSize)
        {
            return zone.Center.Y < mapSize.Y * 0.5f ? 0.0 : Math.PI;
        }

        public static SimVec2 ZonePoint(MapRect zone, double heading, double t, double depth)
        {
            MapRect inner = zone.Grow(-EdgeMarginM);
            if (inner.Size.X <= 0.0f || inner.Size.Y <= 0.0f)
            {
                inner = zone;
            }

            double x = Lerp(inner.Position.X, inner.End.X, t);
            double y = Math.Abs(heading) < 0.00001
                ? inner.End.Y - depth
                : inner.Position.Y + depth;
            y = Clamp(y, inner.Position.Y, inner.End.Y);
            return new SimVec2(x, y);
        }

        public static void AutoFill(ForceSpec spec, MapRect zone, SimVec2 mapSize,
            IDeploymentTerrain? terrain = null)
        {
            double heading = Facing(zone, mapSize);
            if (!spec.Deploy.ContainsKey("cp"))
            {
                spec.Deploy["cp"] = Pose(ClampToZone(ZonePoint(zone, heading, 0.5, CpDepthM), zone, terrain), heading);
            }

            List<CompanySpec> firstWave = new List<CompanySpec>();
            foreach (CompanySpec company in spec.Companies)
            {
                if (company.Wave() == 0 && !IsSupportSlot(company.Slot))
                {
                    firstWave.Add(company);
                }
            }

            for (int i = 0; i < firstWave.Count; ++i)
            {
                string key = "co:" + firstWave[i].Slot;
                if (!spec.Deploy.ContainsKey(key))
                {
                    double t = (i + 1.0) / (firstWave.Count + 1.0);
                    spec.Deploy[key] = Pose(ClampToZone(ZonePoint(zone, heading, t, FrontInsetM), zone, terrain), heading);
                }
            }

            List<string> support = new List<string>();
            foreach (CompanySpec company in spec.Companies)
            {
                if (company.Wave() == 0 && IsSupportSlot(company.Slot))
                {
                    support.Add("co:" + company.Slot);
                }
            }

            for (int i = 0; i < spec.Organic.Count; ++i)
            {
                if (spec.Organic[i].Wave == 0)
                {
                    support.Add("org:" + i);
                }
            }

            for (int i = 0; i < spec.Attachments.Count; ++i)
            {
                if (spec.Attachments[i].Wave == 0)
                {
                    support.Add("att:" + i);
                }
            }

            for (int i = 0; i < support.Count; ++i)
            {
                if (!spec.Deploy.ContainsKey(support[i]))
                {
                    double t = (i + 1.0) / (support.Count + 1.0);
                    spec.Deploy[support[i]] = Pose(ClampToZone(ZonePoint(zone, heading, t, SupportDepthM), zone, terrain), heading);
                }
            }
        }

        public static bool IsValidPoint(SimVec2 point, MapRect zone, IDeploymentTerrain? terrain)
        {
            return zone.Contains(point) && (terrain == null || terrain.IsPassable(point));
        }

        public static SimVec2 ClampToZone(SimVec2 point, MapRect zone, IDeploymentTerrain? terrain = null)
        {
            MapRect inner = zone.Grow(-20.0);
            if (inner.Size.X <= 0.0f || inner.Size.Y <= 0.0f)
            {
                inner = zone;
            }

            SimVec2 result = new SimVec2(
                Clamp(point.X, inner.Position.X, inner.End.X),
                Clamp(point.Y, inner.Position.Y, inner.End.Y));
            if (terrain != null && !terrain.IsPassable(result))
            {
                result = terrain.NearestPassable(result);
            }

            return result;
        }

        private static DeploymentPose Pose(SimVec2 point, double heading)
        {
            return new DeploymentPose(point, heading);
        }

        private static double Lerp(double first, double second, double amount)
        {
            return first + (second - first) * amount;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
