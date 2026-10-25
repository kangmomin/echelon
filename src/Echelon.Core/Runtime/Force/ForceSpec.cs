using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class ForceSpec
    {
        public const int FormatVersion = 1;
        public const int MaxWave = 2;
        public static readonly string[] WaveNames = { "초기 배치", "1차 증원 (T+10분)", "2차 증원 (T+20분)" };

        public string Name { get; set; } = "새 전투단";
        public string Faction { get; set; } = "US";
        public string Toe { get; set; } = "";
        public List<CompanySpec> Companies { get; } = new List<CompanySpec>();
        public List<PlatoonSpec> Organic { get; } = new List<PlatoonSpec>();
        public List<PlatoonSpec> Attachments { get; } = new List<PlatoonSpec>();
        public int ArtilleryMissions { get; set; }
        public int CasSorties { get; set; }
        public Dictionary<string, DeploymentPose> Deploy { get; } = new Dictionary<string, DeploymentPose>();
        public int EntryPoint { get; set; }

        public static ForceSpec FromToe(DataCatalog catalog, string toeId)
        {
            TOETemplate? toe = catalog.GetToe(toeId);
            ForceSpec spec = new ForceSpec();
            if (toe == null)
            {
                return spec;
            }

            spec.Toe = toe.Id;
            spec.Faction = toe.Faction;
            spec.Name = toe.DisplayName;
            foreach (TOETemplate.CompanySlot slot in toe.CompanySlots)
            {
                if (slot.Required && slot.Allowed.Count != 0)
                {
                    spec.Companies.Add(MakeCompany(catalog, slot.Id, slot.Allowed[0]));
                }
            }

            int minimumTank = toe.Constraints.TryGetValue("min_tank_co", out int minimum) ? minimum : 0;
            int tanks = 0;
            foreach (CompanySpec company in spec.Companies)
            {
                UnitTemplate? template = catalog.GetUnitTemplate(company.Template);
                if (template != null && template.Kind == "tank")
                {
                    ++tanks;
                }
            }

            for (int i = spec.Companies.Count - 1; i >= 0 && tanks < minimumTank; --i)
            {
                CompanySpec company = spec.Companies[i];
                UnitTemplate? current = catalog.GetUnitTemplate(company.Template);
                if (current != null && current.Kind == "tank")
                {
                    continue;
                }

                foreach (TOETemplate.CompanySlot slot in toe.CompanySlots)
                {
                    if (slot.Id != company.Slot)
                    {
                        continue;
                    }

                    foreach (string allowed in slot.Allowed)
                    {
                        UnitTemplate? template = catalog.GetUnitTemplate(allowed);
                        if (template != null && template.Kind == "tank")
                        {
                            spec.Companies[i] = MakeCompany(catalog, company.Slot, template.Id);
                            ++tanks;
                            break;
                        }
                    }
                }
            }

            foreach (string organic in toe.Organic)
            {
                spec.Organic.Add(new PlatoonSpec { Template = organic });
            }

            return spec;
        }

        public static CompanySpec MakeCompany(DataCatalog catalog, string slotId, string templateId)
        {
            CompanySpec company = new CompanySpec
            {
                Slot = slotId,
                Template = templateId
            };
            UnitTemplate? template = catalog.GetUnitTemplate(templateId);
            if (template != null)
            {
                foreach (string platoonId in template.Platoons)
                {
                    company.Platoons.Add(new PlatoonSpec { Template = platoonId, Home = slotId });
                }
            }

            return company;
        }

        public CompanySpec? GetCompany(string slotId)
        {
            foreach (CompanySpec company in Companies)
            {
                if (company.Slot == slotId)
                {
                    return company;
                }
            }

            return null;
        }

        public bool MovePlatoon(string fromSlot, int index, string toSlot)
        {
            CompanySpec? source = GetCompany(fromSlot);
            CompanySpec? destination = GetCompany(toSlot);
            if (source == null || destination == null || ReferenceEquals(source, destination) ||
                index < 0 || index >= source.Platoons.Count)
            {
                return false;
            }

            PlatoonSpec platoon = source.Platoons[index];
            source.Platoons.RemoveAt(index);
            destination.Platoons.Add(platoon);
            return true;
        }

        public ForceSpec DuplicateSpec()
        {
            return FromJObject(ToJObject());
        }

        public JObject ToJObject()
        {
            JArray companies = new JArray();
            foreach (CompanySpec company in Companies)
            {
                companies.Add(company.ToJObject());
            }

            JArray organic = new JArray();
            foreach (PlatoonSpec platoon in Organic)
            {
                organic.Add(platoon.ToJObject());
            }

            JArray attachments = new JArray();
            foreach (PlatoonSpec platoon in Attachments)
            {
                attachments.Add(platoon.ToJObject());
            }

            JObject deploy = new JObject();
            foreach (KeyValuePair<string, DeploymentPose> pair in Deploy)
            {
                DeploymentPose pose = pair.Value;
                deploy[pair.Key] = new JArray(pose.X, pose.North, pose.Heading);
            }

            return new JObject
            {
                ["format"] = FormatVersion,
                ["name"] = Name,
                ["faction"] = Faction,
                ["toe"] = Toe,
                ["companies"] = companies,
                ["organic"] = organic,
                ["attachments"] = attachments,
                ["support"] = new JObject
                {
                    ["artillery_missions"] = ArtilleryMissions,
                    ["cas_sorties"] = CasSorties
                },
                ["deploy"] = deploy,
                ["entry_point"] = EntryPoint
            };
        }

        public string ToJson()
        {
            return ToJObject().ToString(Formatting.Indented);
        }

        public static ForceSpec? FromJson(string text, DefReader? reader = null)
        {
            JToken token;
            try
            {
                token = JToken.Parse(text);
            }
            catch (JsonException exception)
            {
                reader?.Error("JSON 파싱 오류: " + exception.Message);
                return null;
            }

            if (!(token is JObject data))
            {
                reader?.Error("최상위가 객체({})여야 함");
                return null;
            }

            return FromJObject(data, reader);
        }

        public static ForceSpec FromJObject(JObject data, DefReader? reader = null)
        {
            ForceSpec spec = new ForceSpec();
            if (reader != null)
            {
                int format = reader.ReqInt(data, "format");
                if (format != FormatVersion)
                {
                    reader.Error("format 은 1 이어야 함");
                }
            }

            spec.Name = ReadString(data, "name", "전투단", reader);
            spec.Faction = ReadString(data, "faction", "US", reader);
            spec.Toe = ReadString(data, "toe", "", reader);
            ReadPlatoonLists(data, spec, reader);

            JObject support = ReadObject(data, "support", reader);
            spec.ArtilleryMissions = Math.Max(0, ReadInt(support, "artillery_missions", 0, reader));
            spec.CasSorties = Math.Max(0, ReadInt(support, "cas_sorties", 0, reader));

            JObject deploy = ReadObject(data, "deploy", reader);
            foreach (KeyValuePair<string, JToken?> pair in deploy)
            {
                if (!(pair.Value is JArray values))
                {
                    reader?.Error("deploy." + pair.Key + " 는 [x, north, heading] 배열이어야 함");
                    continue;
                }

                if (values.Count < 3)
                {
                    reader?.Error("deploy." + pair.Key + " 는 숫자 3개 이상이어야 함");
                    continue;
                }

                double x = ReadNumber(values[0], "deploy." + pair.Key + "[0]", reader);
                double north = ReadNumber(values[1], "deploy." + pair.Key + "[1]", reader);
                double heading = ReadNumber(values[2], "deploy." + pair.Key + "[2]", reader);
                spec.Deploy[pair.Key] = new DeploymentPose(x, north, heading);
            }

            spec.EntryPoint = Math.Max(0, ReadInt(data, "entry_point", 0, reader));
            return spec;
        }

        internal static string ReadString(JObject data, string key, string defaultValue, DefReader? reader)
        {
            if (reader != null)
            {
                return reader.OptString(data, key, defaultValue);
            }

            JToken? value = data[key];
            return value != null && value.Type == JTokenType.String ? value.Value<string>() ?? defaultValue : defaultValue;
        }

        internal static int ReadInt(JObject data, string key, int defaultValue, DefReader? reader)
        {
            if (reader != null)
            {
                return reader.OptInt(data, key, defaultValue);
            }

            return data.TryGetValue(key, out JToken? value) ? ToInt(value) : defaultValue;
        }

        internal static JArray ReadArray(JObject data, string key, DefReader? reader)
        {
            if (reader != null)
            {
                return reader.OptArray(data, key);
            }

            return data[key] as JArray ?? new JArray();
        }

        internal static JObject? ReadObjectItem(JToken item, string what, DefReader? reader)
        {
            if (item is JObject obj)
            {
                return obj;
            }

            reader?.Error("'" + what + "'의 원소는 객체({})여야 함");
            return null;
        }

        internal static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        internal static int ToInt(JToken? value)
        {
            if (!DefReader.IsNumber(value))
            {
                return 0;
            }

            double number = value!.Value<double>();
            if (number < int.MinValue || number > int.MaxValue)
            {
                return 0;
            }

            return (int)Math.Round(number, MidpointRounding.AwayFromZero);
        }

        private static void ReadPlatoonLists(JObject data, ForceSpec spec, DefReader? reader)
        {
            foreach (JToken item in ReadArray(data, "companies", reader))
            {
                JObject? company = ReadObjectItem(item, "companies", reader);
                if (company != null)
                {
                    spec.Companies.Add(CompanySpec.FromJObject(company, reader));
                }
            }

            ReadPlatoonList(data, "organic", spec.Organic, reader);
            ReadPlatoonList(data, "attachments", spec.Attachments, reader);
        }

        private static void ReadPlatoonList(JObject data, string key, List<PlatoonSpec> output, DefReader? reader)
        {
            foreach (JToken item in ReadArray(data, key, reader))
            {
                JObject? platoon = ReadObjectItem(item, key, reader);
                if (platoon != null)
                {
                    output.Add(PlatoonSpec.FromJObject(platoon, reader));
                }
            }
        }

        private static JObject ReadObject(JObject data, string key, DefReader? reader)
        {
            if (reader != null)
            {
                return reader.OptObject(data, key);
            }

            return data[key] as JObject ?? new JObject();
        }

        private static double ReadNumber(JToken value, string what, DefReader? reader)
        {
            if (reader != null)
            {
                return reader.AsNumber(value, what);
            }

            return DefReader.IsNumber(value) ? value.Value<double>() : 0.0;
        }
    }
}
