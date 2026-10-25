using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class Sop
    {
        public static readonly string[] RoeValues = { "hold_fire", "fire_at_will", "designated" };
        public static readonly string[] ContactValues = { "halt_and_fire", "fire_and_continue", "assault", "break_contact" };
        public static readonly string[] TechniqueValues = { "traveling", "overwatch", "bounding" };
        public static readonly double[] WithdrawValues = { 0.4, 0.25, 0.0 };
        public static readonly string[] MountValues = { "dismount_on_contact", "stay_mounted", "stay_dismounted" };
        public static readonly string[] UnderFireValues = { "disperse", "hold" };
        public static readonly string[] AltitudeValues = { "low", "high" };

        public string Roe { get; set; } = "fire_at_will";
        public string OnContact { get; set; } = "fire_and_continue";
        public string Technique { get; set; } = "traveling";
        public double WithdrawAt { get; set; } = 0.4;
        public string MountPolicy { get; set; } = "dismount_on_contact";
        public string UnderFire { get; set; } = "disperse";
        public string Altitude { get; set; } = "low";

        public Sop Copy()
        {
            return new Sop
            {
                Roe = Roe,
                OnContact = OnContact,
                Technique = Technique,
                WithdrawAt = WithdrawAt,
                MountPolicy = MountPolicy,
                UnderFire = UnderFire,
                Altitude = Altitude
            };
        }

        public bool Apply(IEnumerable<KeyValuePair<string, object?>> changes)
        {
            bool ok = true;
            foreach (KeyValuePair<string, object?> pair in changes)
            {
                ok = Apply(pair.Key, pair.Value) && ok;
            }

            return ok;
        }

        public bool Apply(JObject changes)
        {
            bool ok = true;
            foreach (KeyValuePair<string, JToken?> pair in changes)
            {
                ok = Apply(pair.Key, PrimitiveValue(pair.Value)) && ok;
            }

            return ok;
        }

        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                ["roe"] = Roe,
                ["on_contact"] = OnContact,
                ["technique"] = Technique,
                ["withdraw_at"] = WithdrawAt,
                ["mount_policy"] = MountPolicy,
                ["under_fire"] = UnderFire,
                ["altitude"] = Altitude
            };
        }

        private bool Apply(string key, object? value)
        {
            switch (key)
            {
                case "roe":
                    if (!SetName(value, RoeValues, out string roe))
                    {
                        return false;
                    }

                    Roe = roe;
                    return true;
                case "on_contact":
                    if (!SetName(value, ContactValues, out string onContact))
                    {
                        return false;
                    }

                    OnContact = onContact;
                    return true;
                case "technique":
                    if (!SetName(value, TechniqueValues, out string technique))
                    {
                        return false;
                    }

                    Technique = technique;
                    return true;
                case "mount_policy":
                    if (!SetName(value, MountValues, out string mountPolicy))
                    {
                        return false;
                    }

                    MountPolicy = mountPolicy;
                    return true;
                case "under_fire":
                    if (!SetName(value, UnderFireValues, out string underFire))
                    {
                        return false;
                    }

                    UnderFire = underFire;
                    return true;
                case "altitude":
                    if (!SetName(value, AltitudeValues, out string altitude))
                    {
                        return false;
                    }

                    Altitude = altitude;
                    return true;
                case "withdraw_at":
                    if (!TryNumber(value, out double withdrawAt))
                    {
                        return false;
                    }

                    WithdrawAt = SimMath.Clamp(withdrawAt, 0.0, 1.0);
                    return true;
                default:
                    return false;
            }
        }

        private static bool SetName(object? value, string[] allowed, out string name)
        {
            name = value as string ?? "";
            return value is string && Array.IndexOf(allowed, name) >= 0;
        }

        private static object? PrimitiveValue(JToken? value)
        {
            if (value == null)
            {
                return null;
            }

            switch (value.Type)
            {
                case JTokenType.String:
                    return value.Value<string>();
                case JTokenType.Integer:
                    return value.Value<long>();
                case JTokenType.Float:
                    return value.Value<double>();
                default:
                    return value;
            }
        }

        private static bool TryNumber(object? value, out double number)
        {
            switch (value)
            {
                case double typed:
                    number = typed;
                    return true;
                case float typed:
                    number = typed;
                    return true;
                case long typed:
                    number = typed;
                    return true;
                case int typed:
                    number = typed;
                    return true;
                case short typed:
                    number = typed;
                    return true;
                case sbyte typed:
                    number = typed;
                    return true;
                case ulong typed:
                    number = typed;
                    return true;
                case uint typed:
                    number = typed;
                    return true;
                case ushort typed:
                    number = typed;
                    return true;
                case byte typed:
                    number = typed;
                    return true;
                case decimal typed:
                    number = (double)typed;
                    return true;
                default:
                    number = 0.0;
                    return false;
            }
        }
    }
}
