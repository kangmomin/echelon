using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class FormationDef
    {
        public static readonly string[] Echelons = { "platoon", "company" };
        public static readonly string[] ModKeys =
        {
            "speed", "front_fire", "flank_security", "arty_vuln", "control", "transition_sec"
        };

        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Echelon { get; set; } = "";
        public Dictionary<int, List<SimVec2>> Slots { get; } = new Dictionary<int, List<SimVec2>>();
        public Dictionary<string, double> SpacingScale { get; } = new Dictionary<string, double>();
        public Dictionary<string, double> Mods { get; } = new Dictionary<string, double>();
        public bool AllowedMoving { get; set; } = true;

        public static FormationDef FromJObject(JObject data, DefReader reader)
        {
            FormationDef formation = new FormationDef();
            formation.Id = reader.ReqString(data, "id");
            formation.DisplayName = reader.OptString(data, "name", formation.Id);
            formation.Echelon = reader.ReqString(data, "echelon");
            reader.CheckOneOf(formation.Echelon, Echelons, "echelon");

            JObject slots = reader.ReqObject(data, "slots");
            if (slots.Count == 0)
            {
                reader.Error("slots 가 비어 있음");
            }

            foreach (KeyValuePair<string, JToken?> pair in slots)
            {
                if (!int.TryParse(pair.Key, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int count) || count <= 0)
                {
                    reader.Error("slots 의 키 '" + pair.Key + "' 는 양의 정수여야 함");
                    continue;
                }

                if (!(pair.Value is JArray points))
                {
                    reader.Error(string.Format(CultureInfo.InvariantCulture, "slots[{0}] 는 배열이어야 함", count));
                    continue;
                }

                if (points.Count != count)
                {
                    reader.Error(string.Format(CultureInfo.InvariantCulture,
                        "slots[{0}] 의 슬롯 수가 {1} 개임", count, points.Count));
                }

                List<SimVec2> slotPoints = new List<SimVec2>();
                for (int i = 0; i < points.Count; ++i)
                {
                    slotPoints.Add(reader.AsVec2(points[i], string.Format(CultureInfo.InvariantCulture,
                        "slots[{0}][{1}]", count, i)));
                }

                formation.Slots[count] = slotPoints;
            }

            JObject spacing = reader.OptObject(data, "spacing_scale");
            foreach (KeyValuePair<string, JToken?> pair in spacing)
            {
                double value = reader.AsNumber(pair.Value, "spacing_scale." + pair.Key);
                reader.CheckRange(value, 0.05, 5.0, "spacing_scale." + pair.Key);
                formation.SpacingScale[pair.Key] = value;
            }

            JObject mods = reader.ReqObject(data, "mods");
            foreach (string key in ModKeys)
            {
                formation.Mods[key] = reader.ReqNumber(mods, key);
            }

            foreach (KeyValuePair<string, JToken?> pair in mods)
            {
                if (!new List<string>(ModKeys).Contains(pair.Key))
                {
                    formation.Mods[pair.Key] = reader.AsNumber(pair.Value, "mods." + pair.Key);
                }
            }

            formation.AllowedMoving = reader.OptBool(data, "allowed_moving", true);
            return formation;
        }

        public double GetMod(string key, double defaultValue = 1.0)
        {
            return Mods.TryGetValue(key, out double value) ? value : defaultValue;
        }

        public int MaxDefinedCount()
        {
            int maximum = 0;
            foreach (int count in Slots.Keys)
            {
                if (count > maximum)
                {
                    maximum = count;
                }
            }

            return maximum;
        }
    }
}
