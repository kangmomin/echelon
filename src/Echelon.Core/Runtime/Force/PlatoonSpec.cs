using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class PlatoonSpec
    {
        public string Template { get; set; } = "";
        public string Loadout { get; set; } = "";
        public int Wave { get; set; }
        public string Home { get; set; } = "";

        public PlatoonSpec DuplicateSpec()
        {
            return new PlatoonSpec
            {
                Template = Template,
                Loadout = Loadout,
                Wave = Wave,
                Home = Home
            };
        }

        public JObject ToJObject()
        {
            JObject data = new JObject
            {
                ["template"] = Template,
                ["wave"] = Wave
            };
            if (Loadout != "")
            {
                data["loadout"] = Loadout;
            }

            if (Home != "")
            {
                data["home"] = Home;
            }

            return data;
        }

        public static PlatoonSpec FromJObject(JObject data, DefReader? reader = null)
        {
            PlatoonSpec platoon = new PlatoonSpec();
            platoon.Template = ForceSpec.ReadString(data, "template", "", reader);
            platoon.Loadout = ForceSpec.ReadString(data, "loadout", "", reader);
            platoon.Wave = ForceSpec.Clamp(ForceSpec.ReadInt(data, "wave", 0, reader), 0, ForceSpec.MaxWave);
            platoon.Home = ForceSpec.ReadString(data, "home", "", reader);
            return platoon;
        }
    }
}
