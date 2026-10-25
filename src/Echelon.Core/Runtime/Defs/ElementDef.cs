using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class ElementDef
    {
        public static readonly string[] Classes =
        {
            "tank", "ifv", "apc", "recon", "mortar", "aa", "cp", "truck", "inf_squad", "fo", "ew",
            "heli", "eng", "at"
        };

        public static readonly string[] Factions = { "US", "RU" };
        public static readonly string[] ArmorFacings = { "front", "side", "rear", "top" };

        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Faction { get; set; } = "";
        public string ElementClass { get; set; } = "";
        public double Hp { get; set; }
        public Dictionary<string, double> Armor { get; } = new Dictionary<string, double>();
        public double Size { get; set; }
        public double Stealth { get; set; }
        public double OpticsM { get; set; }
        public double SpeedRoadKmh { get; set; }
        public double SpeedOffroadKmh { get; set; }
        public List<string> Weapons { get; } = new List<string>();
        public Dictionary<string, int> Ammo { get; } = new Dictionary<string, int>();
        public double RadioRangeM { get; set; }
        public int TransportCapacity { get; set; }
        public int Crew { get; set; }
        public int Cost { get; set; }
        public int SupplyFull { get; set; }
        public int SupplyCapacity { get; set; }
        public List<string> Loadouts { get; } = new List<string>();

        public static ElementDef FromJObject(JObject data, DefReader reader)
        {
            ElementDef element = new ElementDef();
            element.Id = reader.ReqString(data, "id");
            element.DisplayName = reader.ReqString(data, "name");
            element.Faction = reader.ReqString(data, "faction");
            reader.CheckOneOf(element.Faction, Factions, "faction");
            element.ElementClass = reader.ReqString(data, "class");
            reader.CheckOneOf(element.ElementClass, Classes, "class");
            element.Hp = reader.ReqNumber(data, "hp");
            reader.CheckRange(element.Hp, 1.0, 10000.0, "hp");

            JObject armor = reader.ReqObject(data, "armor");
            foreach (string facing in ArmorFacings)
            {
                double value = reader.ReqNumber(armor, facing);
                reader.CheckRange(value, 0.0, 5000.0, "armor." + facing);
                element.Armor[facing] = value;
            }

            element.Size = reader.ReqNumber(data, "size");
            reader.CheckRange(element.Size, 0.1, 5.0, "size");
            element.Stealth = reader.ReqNumber(data, "stealth");
            reader.CheckRange(element.Stealth, 0.0, 1.0, "stealth");
            element.OpticsM = reader.ReqNumber(data, "optics_m");
            reader.CheckRange(element.OpticsM, 0.0, 10000.0, "optics_m");

            JObject speed = reader.ReqObject(data, "speed_kmh");
            element.SpeedRoadKmh = reader.ReqNumber(speed, "road");
            element.SpeedOffroadKmh = reader.ReqNumber(speed, "offroad");
            reader.CheckRange(element.SpeedRoadKmh, 0.0, 350.0, "speed_kmh.road");
            reader.CheckRange(element.SpeedOffroadKmh, 0.0, 350.0, "speed_kmh.offroad");

            element.Weapons.AddRange(reader.ReqStringArray(data, "weapons"));
            JObject ammo = reader.ReqObject(data, "ammo");
            foreach (KeyValuePair<string, JToken?> pair in ammo)
            {
                string weaponId = pair.Key;
                if (!element.Weapons.Contains(weaponId))
                {
                    reader.Error("ammo 의 '" + weaponId + "' 가 weapons 목록에 없음");
                }

                int rounds = reader.AsInt(pair.Value, "ammo." + weaponId);
                if (rounds < 0)
                {
                    reader.Error("ammo." + weaponId + " 는 0 이상이어야 함");
                }

                element.Ammo[weaponId] = rounds;
            }

            element.RadioRangeM = reader.ReqNumber(data, "radio_range_m");
            reader.CheckRange(element.RadioRangeM, 0.0, 50000.0, "radio_range_m");
            element.TransportCapacity = reader.OptInt(data, "transport_capacity");
            element.Crew = reader.ReqInt(data, "crew");
            element.Cost = reader.ReqInt(data, "cost");
            reader.CheckRange(element.Cost, 0, 10000, "cost");
            element.SupplyFull = reader.ReqInt(data, "supply_full");
            element.SupplyCapacity = reader.OptInt(data, "supply_capacity");
            element.Loadouts.AddRange(reader.OptStringArray(data, "loadouts"));
            return element;
        }

        public double GetArmor(string facing)
        {
            return Armor.TryGetValue(facing, out double value) ? value : 0.0;
        }
    }
}
