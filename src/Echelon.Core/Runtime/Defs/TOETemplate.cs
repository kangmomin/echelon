using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class TOETemplate
    {
        public sealed class CompanySlot
        {
            public string Id { get; set; } = "";
            public List<string> Allowed { get; } = new List<string>();
            public bool Required { get; set; }
        }

        public sealed class AttachmentSlot
        {
            public List<string> Allowed { get; } = new List<string>();
            public int MaxCount { get; set; }
        }

        public string Id { get; set; } = "";
        public string Faction { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string HqElement { get; set; } = "";
        public int HqCount { get; set; }
        public List<CompanySlot> CompanySlots { get; } = new List<CompanySlot>();
        public Dictionary<string, int> Constraints { get; } = new Dictionary<string, int>();
        public List<string> Organic { get; } = new List<string>();
        public List<AttachmentSlot> AttachmentSlots { get; } = new List<AttachmentSlot>();
        public double CommandCapacityPerMin { get; set; }
        public int MaxCompanies { get; set; }
        public int ArtilleryMissionCost { get; set; } = 30;
        public int MaxArtillery { get; set; } = 6;
        public int CasSortieCost { get; set; } = 70;
        public int MaxCas { get; set; } = 2;
        public string ArtilleryWeapon { get; set; } = "";
        public int BatteryGuns { get; set; } = 6;
        public int RoundsPerGun { get; set; } = 3;
        public string CasWeapon { get; set; } = "";
        public int CasBombs { get; set; } = 2;

        public static TOETemplate FromJObject(JObject data, DefReader reader)
        {
            TOETemplate toe = new TOETemplate();
            toe.Id = reader.ReqString(data, "id");
            toe.Faction = reader.ReqString(data, "faction");
            reader.CheckOneOf(toe.Faction, ElementDef.Factions, "faction");
            toe.DisplayName = reader.ReqString(data, "name");

            JObject hq = reader.ReqObject(data, "hq");
            toe.HqElement = reader.ReqString(hq, "element");
            toe.HqCount = reader.ReqInt(hq, "count");

            HashSet<string> seen = new HashSet<string>();
            JArray companySlots = reader.ReqArray(data, "company_slots");
            for (int i = 0; i < companySlots.Count; ++i)
            {
                JObject raw = reader.AsObject(companySlots[i], "company_slots[" + i + "]");
                CompanySlot slot = new CompanySlot();
                slot.Id = reader.ReqString(raw, "id");
                slot.Allowed.AddRange(reader.ReqStringArray(raw, "allowed"));
                slot.Required = reader.OptBool(raw, "required");
                if (slot.Allowed.Count == 0)
                {
                    reader.Error("company_slots[" + i + "].allowed 가 비어 있음");
                }

                if (!seen.Add(slot.Id))
                {
                    reader.Error("company_slots 의 id '" + slot.Id + "' 가 중복됨");
                }

                toe.CompanySlots.Add(slot);
            }

            JObject constraints = reader.OptObject(data, "constraints");
            foreach (KeyValuePair<string, JToken?> pair in constraints)
            {
                toe.Constraints[pair.Key] = reader.AsInt(pair.Value, "constraints." + pair.Key);
            }

            toe.Organic.AddRange(reader.OptStringArray(data, "organic"));
            JArray attachments = reader.OptArray(data, "attachment_slots");
            for (int i = 0; i < attachments.Count; ++i)
            {
                JObject raw = reader.AsObject(attachments[i], "attachment_slots[" + i + "]");
                AttachmentSlot slot = new AttachmentSlot();
                slot.Allowed.AddRange(reader.ReqStringArray(raw, "allowed"));
                slot.MaxCount = reader.ReqInt(raw, "max");
                toe.AttachmentSlots.Add(slot);
            }

            JObject support = reader.OptObject(data, "support");
            toe.ArtilleryMissionCost = reader.OptInt(support, "artillery_mission", 30);
            toe.MaxArtillery = reader.OptInt(support, "max_artillery", 6);
            toe.CasSortieCost = reader.OptInt(support, "cas_sortie", 70);
            toe.MaxCas = reader.OptInt(support, "max_cas", 2);
            toe.ArtilleryWeapon = reader.OptString(support, "artillery_weapon");
            toe.BatteryGuns = reader.OptInt(support, "battery_guns", 6);
            toe.RoundsPerGun = reader.OptInt(support, "rounds_per_gun", 3);
            toe.CasWeapon = reader.OptString(support, "cas_weapon");
            toe.CasBombs = reader.OptInt(support, "cas_bombs", 2);
            toe.CommandCapacityPerMin = reader.ReqNumber(data, "command_capacity_per_min");
            reader.CheckRange(toe.CommandCapacityPerMin, 1.0, 20.0, "command_capacity_per_min");
            toe.MaxCompanies = reader.ReqInt(data, "max_companies");
            if (toe.CompanySlots.Count > toe.MaxCompanies)
            {
                reader.Error(string.Format("company_slots 수({0})가 max_companies({1})를 넘음",
                    toe.CompanySlots.Count, toe.MaxCompanies));
            }

            return toe;
        }

        public List<string> ReferencedUnitTemplates()
        {
            List<string> result = new List<string>();
            foreach (CompanySlot slot in CompanySlots)
            {
                result.AddRange(slot.Allowed);
            }

            result.AddRange(Organic);
            foreach (AttachmentSlot slot in AttachmentSlots)
            {
                result.AddRange(slot.Allowed);
            }

            return result;
        }
    }
}
