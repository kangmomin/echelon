using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class WeaponDef
    {
        public static readonly string[] Types = { "KE", "HEAT", "ATGM", "HE", "MG", "AC", "SAM" };
        public static readonly string[] AllowedTargets = { "vehicle", "infantry", "air" };

        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string WeaponType { get; set; } = "";
        public double RangeMinM { get; set; }
        public double RangeMaxM { get; set; }
        public double Penetration { get; set; }
        public double PenFalloffPerKm { get; set; }
        public double Damage { get; set; }
        public double AccuracyBase { get; set; }
        public double AccuracyAtMaxRange { get; set; }
        public double RofRpm { get; set; }
        public double Suppression { get; set; }
        public bool FireOnMove { get; set; }
        public List<string> Targets { get; } = new List<string>();
        public double HeRadiusM { get; set; }
        public bool Indirect { get; set; }
        public bool TopAttack { get; set; }
        public int Burst { get; set; } = 1;

        public static WeaponDef FromJObject(JObject data, DefReader reader)
        {
            WeaponDef weapon = new WeaponDef();
            weapon.Id = reader.ReqString(data, "id");
            weapon.DisplayName = reader.OptString(data, "name", weapon.Id);
            weapon.WeaponType = reader.ReqString(data, "type");
            reader.CheckOneOf(weapon.WeaponType, Types, "type");

            JObject range = reader.ReqObject(data, "range_m");
            weapon.RangeMinM = reader.ReqNumber(range, "min");
            weapon.RangeMaxM = reader.ReqNumber(range, "max");
            if (weapon.RangeMaxM <= weapon.RangeMinM)
            {
                reader.Error(string.Format("range_m.max({0}) 는 range_m.min({1}) 보다 커야 함",
                    weapon.RangeMaxM, weapon.RangeMinM));
            }

            weapon.Penetration = reader.ReqNumber(data, "penetration");
            reader.CheckRange(weapon.Penetration, 0.0, 5000.0, "penetration");
            weapon.PenFalloffPerKm = reader.OptNumber(data, "pen_falloff_per_km");
            reader.CheckRange(weapon.PenFalloffPerKm, 0.0, 1.0, "pen_falloff_per_km");
            weapon.Damage = reader.ReqNumber(data, "damage");

            JObject accuracy = reader.ReqObject(data, "accuracy");
            weapon.AccuracyBase = reader.ReqNumber(accuracy, "base");
            weapon.AccuracyAtMaxRange = reader.ReqNumber(accuracy, "at_max_range");
            reader.CheckRange(weapon.AccuracyBase, 0.0, 1.0, "accuracy.base");
            reader.CheckRange(weapon.AccuracyAtMaxRange, 0.0, 1.0, "accuracy.at_max_range");

            weapon.RofRpm = reader.ReqNumber(data, "rof_rpm");
            reader.CheckRange(weapon.RofRpm, 0.1, 6000.0, "rof_rpm");
            weapon.Suppression = reader.ReqNumber(data, "suppression");
            reader.CheckRange(weapon.Suppression, 0.0, 100.0, "suppression");
            weapon.FireOnMove = reader.ReqBool(data, "fire_on_move");
            weapon.Targets.AddRange(reader.ReqStringArray(data, "targets"));
            foreach (string target in weapon.Targets)
            {
                reader.CheckOneOf(target, AllowedTargets, "targets");
            }

            weapon.HeRadiusM = reader.OptNumber(data, "he_radius_m");
            weapon.Indirect = reader.OptBool(data, "indirect");
            weapon.TopAttack = reader.OptBool(data, "top_attack");
            weapon.Burst = reader.OptInt(data, "burst", 1);
            if (weapon.Burst < 1)
            {
                reader.Error("burst 는 1 이상");
            }

            return weapon;
        }

        public double IntervalSec()
        {
            return 60.0 / RofRpm * Burst;
        }

        public double PenetrationAt(double distanceM)
        {
            return Penetration * Math.Max(0.0, 1.0 - PenFalloffPerKm * distanceM / 1000.0);
        }

        public double AccuracyAt(double distanceM)
        {
            double amount = Clamp((distanceM - RangeMinM) / Math.Max(RangeMaxM - RangeMinM, 1.0), 0.0, 1.0);
            return AccuracyBase + (AccuracyAtMaxRange - AccuracyBase) * amount;
        }

        public bool CanTarget(string kind)
        {
            return Targets.Contains(kind);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
