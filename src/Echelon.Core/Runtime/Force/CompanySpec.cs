using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class CompanySpec
    {
        public string Slot { get; set; } = "";
        public string Template { get; set; } = "";
        public List<PlatoonSpec> Platoons { get; } = new List<PlatoonSpec>();

        public int Wave()
        {
            int wave = ForceSpec.MaxWave;
            foreach (PlatoonSpec platoon in Platoons)
            {
                if (platoon.Wave < wave)
                {
                    wave = platoon.Wave;
                }
            }

            return Platoons.Count == 0 ? 0 : wave;
        }

        public JObject ToJObject()
        {
            JArray platoons = new JArray();
            foreach (PlatoonSpec platoon in Platoons)
            {
                platoons.Add(platoon.ToJObject());
            }

            return new JObject
            {
                ["slot"] = Slot,
                ["template"] = Template,
                ["platoons"] = platoons
            };
        }

        public static CompanySpec FromJObject(JObject data, DefReader? reader = null)
        {
            CompanySpec company = new CompanySpec
            {
                Slot = ForceSpec.ReadString(data, "slot", "", reader),
                Template = ForceSpec.ReadString(data, "template", "", reader)
            };
            foreach (JToken item in ForceSpec.ReadArray(data, "platoons", reader))
            {
                JObject? platoon = ForceSpec.ReadObjectItem(item, "platoons", reader);
                if (platoon != null)
                {
                    company.Platoons.Add(PlatoonSpec.FromJObject(platoon, reader));
                }
            }

            return company;
        }
    }
}
