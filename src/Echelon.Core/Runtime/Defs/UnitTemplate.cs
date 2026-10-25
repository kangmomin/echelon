using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class UnitTemplate
    {
        public static readonly string[] Echelons = { "platoon", "company" };
        public static readonly string[] Kinds =
        {
            "tank", "mech", "inf", "recon", "mortar", "supply", "hq", "cp", "fo", "aa", "ew", "engineer",
            "wheeled", "at", "sniper", "aviation", "artillery", "fixed_wing"
        };

        public static readonly string[] Skills = { "recruit", "regular", "veteran", "elite" };

        public sealed class Loadout
        {
            public string Id { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public Dictionary<string, string> Replace { get; } = new Dictionary<string, string>();
        }

        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Faction { get; set; } = "";
        public string Echelon { get; set; } = "";
        public string Kind { get; set; } = "";
        public List<string> Elements { get; } = new List<string>();
        public List<string> Passengers { get; } = new List<string>();
        public List<Loadout> Loadouts { get; } = new List<Loadout>();
        public string DefaultFormation { get; set; } = "";
        public List<string> HqElements { get; } = new List<string>();
        public List<string> Platoons { get; } = new List<string>();
        public string Skill { get; set; } = "regular";
        public int Cost { get; set; }
        public string Role { get; set; } = "";
        public List<string> Pros { get; } = new List<string>();
        public List<string> Cons { get; } = new List<string>();

        public static UnitTemplate FromJObject(JObject data, DefReader reader)
        {
            UnitTemplate template = new UnitTemplate();
            template.Id = reader.ReqString(data, "id");
            template.DisplayName = reader.ReqString(data, "name");
            template.Faction = reader.ReqString(data, "faction");
            reader.CheckOneOf(template.Faction, ElementDef.Factions, "faction");
            template.Echelon = reader.ReqString(data, "echelon");
            reader.CheckOneOf(template.Echelon, Echelons, "echelon");
            template.Kind = reader.ReqString(data, "kind");
            reader.CheckOneOf(template.Kind, Kinds, "kind");
            template.DefaultFormation = reader.OptString(data, "default_formation");
            template.Skill = reader.OptString(data, "skill", "regular");
            reader.CheckOneOf(template.Skill, Skills, "skill");
            template.Role = reader.OptString(data, "role");
            template.Pros.AddRange(reader.OptStringArray(data, "pros"));
            template.Cons.AddRange(reader.OptStringArray(data, "cons"));

            if (template.Echelon == "platoon")
            {
                Expand(reader.ReqArray(data, "elements"), reader, "elements", template.Elements);
                Expand(reader.OptArray(data, "passengers"), reader, "passengers", template.Passengers);
                if (template.Elements.Count == 0)
                {
                    reader.Error("elements 가 비어 있음");
                }

                JArray loadouts = reader.OptArray(data, "loadouts");
                for (int i = 0; i < loadouts.Count; ++i)
                {
                    JObject raw = reader.AsObject(loadouts[i], "loadouts[" + i + "]");
                    Loadout loadout = new Loadout();
                    loadout.Id = reader.ReqString(raw, "id");
                    loadout.DisplayName = reader.ReqString(raw, "name");
                    JObject replacements = reader.OptObject(raw, "replace");
                    foreach (KeyValuePair<string, JToken?> pair in replacements)
                    {
                        string what = string.Format(CultureInfo.InvariantCulture,
                            "loadouts[{0}].replace.{1}", i, pair.Key);
                        loadout.Replace[pair.Key] = reader.AsString(pair.Value, what);
                    }

                    template.Loadouts.Add(loadout);
                }
            }
            else
            {
                Expand(reader.ReqArray(data, "hq"), reader, "hq", template.HqElements);
                template.Platoons.AddRange(reader.ReqStringArray(data, "platoons"));
                if (template.Platoons.Count == 0)
                {
                    reader.Error("platoons 가 비어 있음");
                }
            }

            return template;
        }

        public Loadout? GetLoadout(string loadoutId)
        {
            foreach (Loadout loadout in Loadouts)
            {
                if (loadout.Id == loadoutId)
                {
                    return loadout;
                }
            }

            return null;
        }

        public List<string> ElementsFor(string loadoutId)
        {
            return ApplyLoadout(Elements, loadoutId);
        }

        public List<string> PassengersFor(string loadoutId)
        {
            return ApplyLoadout(Passengers, loadoutId);
        }

        private List<string> ApplyLoadout(List<string> source, string loadoutId)
        {
            Loadout? loadout = GetLoadout(loadoutId);
            List<string> result = new List<string>(source.Count);
            foreach (string elementId in source)
            {
                if (loadout != null && loadout.Replace.TryGetValue(elementId, out string? replacement))
                {
                    result.Add(replacement);
                }
                else
                {
                    result.Add(elementId);
                }
            }

            return result;
        }

        private static void Expand(JArray raw, DefReader reader, string what, List<string> output)
        {
            for (int i = 0; i < raw.Count; ++i)
            {
                string itemName = string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", what, i);
                JObject item = reader.AsObject(raw[i], itemName);
                string elementId = reader.ReqString(item, "element");
                int count = reader.OptInt(item, "count", 1);
                if (count < 1 || count > 12)
                {
                    reader.Error(itemName + ".count 는 1~12");
                    continue;
                }

                for (int n = 0; n < count; ++n)
                {
                    output.Add(elementId);
                }
            }
        }
    }
}
