using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Echelon.Core;

namespace Echelon.Unity.UI.UnitInfo
{
    public static class UnitInfo
    {
        public const string Accent = "#F2CC59";
        public const string Dim = "#A6A9B3";
        public const string Good = "#8CE99A";
        public const string Bad = "#FF8A80";

        public static readonly string[] RatingKeys = { "at", "inf", "armor", "mobility", "recon", "aa" };
        public static readonly string[] RatingNames = { "대전차", "대" + "보" + "병", "방호", "기동", "정찰·은밀", "대공" };

        private static readonly Dictionary<string, string> KindNames = new Dictionary<string, string>
        {
            ["tank"] = "기갑", ["mech"] = "기계화" + "보" + "병", ["inf"] = "보" + "병" + "(도보)", ["recon"] = "정찰",
            ["mortar"] = "박격포", ["supply"] = "보급", ["hq"] = "본부", ["cp"] = "지휘소",
            ["fo"] = "관측", ["aa"] = "방공", ["ew"] = "전자전", ["engineer"] = "공병",
            ["wheeled"] = "차륜형 " + "보" + "병", ["at"] = "대전차", ["sniper"] = "저격", ["aviation"] = "헬기",
            ["artillery"] = "포병", ["fixed_wing"] = "고정익 항공"
        };

        private static readonly Dictionary<string, string> ClassNames = new Dictionary<string, string>
        {
            ["tank"] = "전차", ["ifv"] = "보" + "병" + "전투차", ["apc"] = "장갑차", ["recon"] = "정찰차",
            ["mortar"] = "자주박격포", ["aa"] = "방공", ["cp"] = "지휘차", ["truck"] = "트럭",
            ["inf_squad"] = "보" + "병", ["fo"] = "관측", ["ew"] = "전자전", ["heli"] = "헬기",
            ["eng"] = "공병 장비", ["at"] = "대전차 차량", ["artillery"] = "자주포", ["plane"] = "고정익"
        };

        private static readonly Dictionary<string, string> WeaponTypeNames = new Dictionary<string, string>
        {
            ["KE"] = "철갑탄", ["HEAT"] = "성형작약", ["ATGM"] = "대전차 미사일", ["HE"] = "고폭",
            ["MG"] = "기관총·소총", ["AC"] = "기관포", ["SAM"] = "지대공 미사일",
            ["AAM"] = "공대공 미사일"
        };

        private static readonly Dictionary<string, string> TargetNames = new Dictionary<string, string>
        {
            ["vehicle"] = "차량", ["infantry"] = "보" + "병", ["air"] = "항공기"
        };

        public static string PlatoonCard(
            DataCatalog catalog,
            string templateId,
            string loadout = "",
            int cost = -1)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            UnitTemplate template = catalog.GetUnitTemplate(templateId);
            if (template == null)
                return string.Empty;
            string selectedLoadout = LoadoutOrDefault(template, loadout);
            List<string> elements = template.ElementsFor(selectedLoadout);
            List<string> passengers = template.PassengersFor(selectedLoadout);
            int actualCost = cost >= 0
                ? cost
                : ForceRules.PlatoonBaseCost(catalog, new PlatoonSpec { Template = templateId, Loadout = selectedLoadout });

            StringBuilder text = Header(template, actualCost);
            UnitTemplate.Loadout selected = template.GetLoadout(selectedLoadout);
            if (selected != null && template.Loadouts.Count > 1)
                text.Append("<color=").Append(Dim).Append(">장비 템플릿: ").Append(selected.DisplayName)
                    .AppendLine("</color>");
            AppendRole(text, template);
            AppendRatings(text, CalculateRatings(catalog, elements, passengers));
            text.AppendLine().AppendLine("<b>구성</b>");
            AppendElements(text, catalog, elements, string.Empty);
            if (passengers.Count != 0)
                AppendElements(text, catalog, passengers, "탑승 ");
            return text.ToString();
        }

        public static string CompanyCard(DataCatalog catalog, string templateId)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            UnitTemplate template = catalog.GetUnitTemplate(templateId);
            if (template == null)
                return string.Empty;

            List<string> all = new List<string>(template.HqElements);
            List<string> allPassengers = new List<string>();
            Dictionary<string, int> platoonCounts = new Dictionary<string, int>();
            List<string> platoonOrder = new List<string>();
            int cost = ForceRules.CompanyHqCost(catalog, templateId);
            foreach (string platoonId in template.Platoons)
            {
                UnitTemplate platoon = catalog.GetUnitTemplate(platoonId);
                if (platoon == null)
                    continue;
                all.AddRange(platoon.Elements);
                allPassengers.AddRange(platoon.Passengers);
                if (!platoonCounts.ContainsKey(platoonId))
                    platoonOrder.Add(platoonId);
                platoonCounts[platoonId] = platoonCounts.TryGetValue(platoonId, out int count) ? count + 1 : 1;
                cost += ForceRules.PlatoonBaseCost(catalog, new PlatoonSpec { Template = platoonId });
            }

            StringBuilder text = Header(template, cost);
            AppendRole(text, template);
            AppendRatings(text, CalculateRatings(catalog, all, allPassengers));
            text.AppendLine().Append("<b>구성</b>  <color=").Append(Dim).Append(">(차량 ")
                .Append(CountVehicles(catalog, all)).Append(", 분대 ")
                .Append(CountSquads(catalog, all, allPassengers)).AppendLine(")</color>");
            text.Append("• 중대 본부: ").AppendLine(SummaryList(catalog, template.HqElements));
            foreach (string platoonId in platoonOrder)
            {
                UnitTemplate platoon = catalog.GetUnitTemplate(platoonId);
                text.Append("• ").Append(platoon.DisplayName).Append(" × ").Append(platoonCounts[platoonId])
                    .Append(": ").Append(SummaryList(catalog, platoon.Elements));
                if (platoon.Passengers.Count != 0)
                    text.Append(" + 탑승 ").Append(SummaryList(catalog, platoon.Passengers));
                text.AppendLine();
            }

            if (platoonOrder.Count != 0)
            {
                // 소대가 여러 종류인 중대(헬기·고정익 등)는 종류마다 주 장비를 보인다.
                text.AppendLine().AppendLine("<b>주 장비</b>");
                foreach (string platoonId in platoonOrder)
                {
                    UnitTemplate platoon = catalog.GetUnitTemplate(platoonId);
                    string loadout = LoadoutOrDefault(platoon, string.Empty);
                    if (platoonOrder.Count > 1)
                        text.Append("<color=").Append(Dim).Append(">").Append(platoon.DisplayName).AppendLine("</color>");
                    AppendElements(text, catalog, platoon.ElementsFor(loadout), string.Empty);
                    List<string> passengers = platoon.PassengersFor(loadout);
                    if (passengers.Count != 0)
                        AppendElements(text, catalog, passengers, "탑승 ");
                }
            }
            return text.ToString();
        }

        public static string Tooltip(DataCatalog catalog, string templateId, string loadout = "")
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            UnitTemplate template = catalog.GetUnitTemplate(templateId);
            if (template == null)
                return string.Empty;

            List<string> elements;
            List<string> passengers;
            int cost;
            if (template.Echelon == "company")
            {
                elements = new List<string>(template.HqElements);
                passengers = new List<string>();
                cost = ForceRules.CompanyHqCost(catalog, templateId);
                foreach (string platoonId in template.Platoons)
                {
                    UnitTemplate platoon = catalog.GetUnitTemplate(platoonId);
                    if (platoon == null)
                        continue;
                    elements.AddRange(platoon.Elements);
                    passengers.AddRange(platoon.Passengers);
                    cost += ForceRules.PlatoonBaseCost(catalog, new PlatoonSpec { Template = platoonId });
                }
            }
            else
            {
                string selected = LoadoutOrDefault(template, loadout);
                elements = template.ElementsFor(selected);
                passengers = template.PassengersFor(selected);
                cost = ForceRules.PlatoonBaseCost(catalog,
                    new PlatoonSpec { Template = templateId, Loadout = selected });
            }

            StringBuilder text = new StringBuilder();
            text.Append(template.DisplayName).Append("  (").Append(KindName(template.Kind)).Append(", ")
                .Append(cost).AppendLine("점)");
            if (template.Role.Length != 0)
                text.AppendLine(template.Role);
            Dictionary<string, int> ratings = CalculateRatings(catalog, elements, passengers);
            List<string> parts = new List<string>();
            for (int i = 0; i < RatingKeys.Length; ++i)
            {
                int value = ratings.TryGetValue(RatingKeys[i], out int rating) ? rating : 0;
                parts.Add(RatingNames[i] + " " + new string('●', value) + new string('○', 5 - value));
            }
            text.AppendLine(string.Join("  ", parts.GetRange(0, 3)));
            text.AppendLine(string.Join("  ", parts.GetRange(3, 3)));
            foreach (string pro in template.Pros)
                text.Append("+ ").AppendLine(pro);
            foreach (string con in template.Cons)
                text.Append("- ").AppendLine(con);
            return text.ToString().TrimEnd('\r', '\n');
        }

        public static Dictionary<string, int> PlatoonRatings(
            DataCatalog catalog,
            string templateId,
            string loadout = "")
        {
            UnitTemplate template = catalog.GetUnitTemplate(templateId);
            if (template == null)
                return new Dictionary<string, int>();
            string selected = LoadoutOrDefault(template, loadout);
            return CalculateRatings(catalog, template.ElementsFor(selected), template.PassengersFor(selected));
        }

        public static Dictionary<string, int> CompanyRatings(DataCatalog catalog, string templateId)
        {
            UnitTemplate template = catalog.GetUnitTemplate(templateId);
            if (template == null)
                return new Dictionary<string, int>();
            List<string> elements = new List<string>(template.HqElements);
            List<string> passengers = new List<string>();
            foreach (string platoonId in template.Platoons)
            {
                UnitTemplate platoon = catalog.GetUnitTemplate(platoonId);
                if (platoon == null)
                    continue;
                elements.AddRange(platoon.Elements);
                passengers.AddRange(platoon.Passengers);
            }
            return CalculateRatings(catalog, elements, passengers);
        }

        public static Dictionary<string, int> CalculateRatings(
            DataCatalog catalog,
            IEnumerable<string> elements,
            IEnumerable<string> passengers)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (elements == null)
                throw new ArgumentNullException(nameof(elements));
            if (passengers == null)
                throw new ArgumentNullException(nameof(passengers));

            List<string> all = new List<string>(elements);
            HashSet<string> passengerSet = new HashSet<string>(passengers);
            all.AddRange(passengers);
            double at = 0.0;
            double infantry = 0.0;
            double armor = 0.0;
            double mobility = 9999.0;
            double recon = 0.0;
            double aa = 0.0;
            int count = 0;
            int vehicles = 0;

            foreach (string id in all)
            {
                ElementDef element = catalog.GetElement(id);
                if (element == null)
                    continue;
                ++count;
                double elementAt = 0.0;
                double elementInfantry = 0.0;
                double elementAa = 0.0;
                foreach (string weaponId in element.Weapons)
                {
                    WeaponDef weapon = catalog.GetWeapon(weaponId);
                    if (weapon == null || weapon.Indirect)
                        continue;
                    if (weapon.CanTarget("vehicle"))
                    {
                        double penetration = weapon.TopAttack
                            ? Math.Max(weapon.Penetration, 900.0)
                            : weapon.Penetration;
                        double value = Clamp(penetration / 900.0, 0.0, 1.25) *
                                       Clamp(weapon.RangeMaxM / 4000.0, 0.25, 1.0);
                        elementAt = Math.Max(elementAt, value);
                    }
                    if (weapon.CanTarget("infantry"))
                    {
                        double rate = Clamp(weapon.RofRpm * weapon.Damage / 1200.0, 0.1, 1.5);
                        elementInfantry += rate * (1.0 + weapon.HeRadiusM / 8.0) *
                                           Clamp(weapon.RangeMaxM / 1500.0, 0.3, 1.5);
                    }
                    if (weapon.CanTarget("air"))
                    {
                        double value = 0.4;
                        if (weapon.WeaponType == "SAM" || weapon.WeaponType == "AAM")
                            value = 1.0;
                        else if (element.ElementClass == "aa")
                            value = 0.8;
                        elementAa = Math.Max(elementAa, value);
                    }
                }

                at += elementAt;
                infantry += Math.Min(elementInfantry, 2.5);
                aa = Math.Max(aa, elementAa);
                recon = Math.Max(recon, element.OpticsM / 4500.0 * (0.6 + element.Stealth));
                if (element.ElementClass != "inf_squad")
                {
                    ++vehicles;
                    armor += element.GetArmor("front") / 900.0 * 0.7 +
                             element.GetArmor("side") / 350.0 * 0.3;
                }

                if (element.ElementClass == "plane")
                    mobility = Math.Min(mobility, 5.0);
                else if (element.ElementClass == "heli")
                    mobility = Math.Min(mobility, 2.5);
                else if (element.ElementClass == "inf_squad" && passengerSet.Contains(id))
                    continue;
                else
                    mobility = Math.Min(mobility,
                        element.SpeedRoadKmh / 100.0 * 0.5 + element.SpeedOffroadKmh / 50.0 * 0.5);
            }

            Dictionary<string, int> result = new Dictionary<string, int>();
            if (count == 0)
                return result;
            double sizeFactor = Clamp(count / 4.0, 0.7, 1.3);
            result["at"] = Stars(at / count * sizeFactor * 1.1);
            result["inf"] = Stars(infantry / count * sizeFactor * 0.4);
            result["armor"] = Stars(armor / Math.Max(vehicles, 1) * (vehicles / (double)count) +
                                    (vehicles < count ? 0.15 : 0.0));
            result["mobility"] = Stars(mobility < 9000.0 ? mobility : 0.0);
            result["recon"] = Stars(recon);
            result["aa"] = Stars(aa);
            return result;
        }

        public static string WeaponLine(WeaponDef weapon, int ammo)
        {
            if (weapon == null)
                throw new ArgumentNullException(nameof(weapon));
            List<string> targets = new List<string>();
            foreach (string target in weapon.Targets)
                targets.Add(TargetName(target));
            List<string> extra = new List<string>();
            if (weapon.TopAttack)
                extra.Add("상부 공격");
            if (weapon.FireOnMove)
                extra.Add("이동 사격");
            if (weapon.HeRadiusM > 0.0)
                extra.Add("폭발 " + Round(weapon.HeRadiusM) + " m");
            if (weapon.Indirect)
                extra.Add("간접");

            StringBuilder text = new StringBuilder();
            text.Append(weapon.DisplayName).Append(" <color=").Append(Dim).Append(">(")
                .Append(WeaponTypeName(weapon.WeaponType)).Append(")</color>  ")
                .Append(Number(weapon.RangeMaxM / 1000.0, "0.0")).Append(" km");
            if (weapon.Penetration >= 20.0)
                text.Append("  · 관통 ").Append(Round(weapon.Penetration));
            text.Append("  · ").Append(string.Join("/", targets));
            if (extra.Count != 0)
                text.Append("  · ").Append(string.Join(", ", extra));
            if (ammo > 0)
                text.Append("  · ").Append(ammo).Append("발");
            return text.ToString();
        }

        public static string LoadoutOrDefault(UnitTemplate template, string loadout)
        {
            if (!string.IsNullOrEmpty(loadout))
                return loadout;
            return template.Loadouts.Count != 0 ? template.Loadouts[0].Id : string.Empty;
        }

        public static string KindName(string kind)
        {
            return KindNames.TryGetValue(kind ?? string.Empty, out string name) ? name : kind ?? string.Empty;
        }

        public static string ClassName(string elementClass)
        {
            return ClassNames.TryGetValue(elementClass ?? string.Empty, out string name)
                ? name
                : elementClass ?? string.Empty;
        }

        public static string WeaponTypeName(string weaponType)
        {
            return WeaponTypeNames.TryGetValue(weaponType ?? string.Empty, out string name)
                ? name
                : weaponType ?? string.Empty;
        }

        public static string TargetName(string target)
        {
            return TargetNames.TryGetValue(target ?? string.Empty, out string name) ? name : target ?? string.Empty;
        }

        private static StringBuilder Header(UnitTemplate template, int cost)
        {
            StringBuilder text = new StringBuilder();
            text.Append("<size=19><color=").Append(Accent).Append("><b>").Append(template.DisplayName)
                .AppendLine("</b></color></size>");
            text.Append("<color=").Append(Dim).Append(">").Append(KindName(template.Kind)).Append(" · ")
                .Append(template.Echelon == "company" ? "중대" : "소대").Append(" · ").Append(cost)
                .AppendLine("점</color>");
            return text;
        }

        private static void AppendRole(StringBuilder text, UnitTemplate template)
        {
            if (template.Role.Length != 0)
                text.AppendLine().AppendLine(template.Role);
            if (template.Pros.Count == 0 && template.Cons.Count == 0)
                return;
            text.AppendLine();
            foreach (string pro in template.Pros)
                text.Append("<color=").Append(Good).Append(">▲</color> ").AppendLine(pro);
            foreach (string con in template.Cons)
                text.Append("<color=").Append(Bad).Append(">▼</color> ").AppendLine(con);
        }

        private static void AppendRatings(StringBuilder text, IReadOnlyDictionary<string, int> ratings)
        {
            text.AppendLine();
            for (int i = 0; i < RatingKeys.Length; i += 2)
            {
                for (int column = 0; column < 2; ++column)
                {
                    int index = i + column;
                    int value = ratings.TryGetValue(RatingKeys[index], out int rating) ? rating : 0;
                    if (column != 0)
                        text.Append("    ");
                    text.Append("<color=").Append(Dim).Append(">").Append(RatingNames[index])
                        .Append("</color>  <color=").Append(Accent).Append(">")
                        .Append(new string('■', value)).Append("</color><color=#4A4D55>")
                        .Append(new string('■', 5 - value)).Append("</color>");
                }
                text.AppendLine();
            }
        }

        private static void AppendElements(
            StringBuilder text,
            DataCatalog catalog,
            IEnumerable<string> ids,
            string prefix)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            List<string> order = new List<string>();
            foreach (string id in ids)
            {
                if (!counts.ContainsKey(id))
                    order.Add(id);
                counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;
            }

            foreach (string id in order)
            {
                ElementDef element = catalog.GetElement(id);
                if (element == null)
                    continue;
                text.AppendLine().Append("<b>").Append(prefix).Append(element.DisplayName).Append(" × ")
                    .Append(counts[id]).Append("</b>  <color=").Append(Dim).Append(">")
                    .Append(ClassName(element.ElementClass)).Append(", ").Append(element.Cost)
                    .AppendLine("점</color>");
                if (element.ElementClass == "inf_squad")
                {
                    text.Append("<color=").Append(Dim).Append(">인원 ").Append(Round(element.Hp))
                        .Append(" · 은폐 ").Append(Number(element.Stealth, "0.0"))
                        .Append(" · 관측 ").Append(Number(element.OpticsM / 1000.0, "0.0"))
                        .Append(" km · 도보 ").Append(Round(element.SpeedOffroadKmh)).AppendLine(" km/h</color>");
                }
                else
                {
                    text.Append("<color=").Append(Dim).Append(">장갑 정면 ")
                        .Append(Round(element.GetArmor("front"))).Append(" / 측면 ")
                        .Append(Round(element.GetArmor("side"))).Append(" / 후면 ")
                        .Append(Round(element.GetArmor("rear"))).Append(" / 상면 ")
                        .Append(Round(element.GetArmor("top"))).AppendLine();
                    text.Append("속도 도로 ").Append(Round(element.SpeedRoadKmh)).Append(" · 야지 ")
                        .Append(Round(element.SpeedOffroadKmh)).Append(" km/h · 관측 ")
                        .Append(Number(element.OpticsM / 1000.0, "0.0")).Append(" km · 은폐 ")
                        .Append(Number(element.Stealth, "0.00"));
                    if (element.TransportCapacity > 0)
                        text.Append(" · ").Append("보").Append("병 ").Append(element.TransportCapacity).Append("개 분대 탑승");
                    text.AppendLine("</color>");
                    if (element.ElementClass == "heli")
                        text.Append("<color=").Append(Good)
                            .AppendLine(">지형 무시 비행 · 저공 25 m / 고공 90 m(V) · 대공 무기만 맞힐 수 있음</color>");
                    if (element.ElementClass == "plane")
                        text.Append("<color=").Append(Good).Append(">지도 밖에서 출격 · 명령 즉시 · 연료 ")
                            .Append(Round(element.EnduranceSec)).Append("초 · 재무장 ").Append(Round(element.RearmSec))
                            .AppendLine("초 · 지대공·방공·항공기만 맞힐 수 있음</color>");
                }

                foreach (string weaponId in element.Weapons)
                {
                    WeaponDef weapon = catalog.GetWeapon(weaponId);
                    if (weapon == null)
                        continue;
                    int ammo = element.Ammo.TryGetValue(weapon.Id, out int rounds) ? rounds : 0;
                    text.Append("  · ").AppendLine(WeaponLine(weapon, ammo));
                }
            }
        }

        private static string SummaryList(DataCatalog catalog, IEnumerable<string> ids)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            List<string> order = new List<string>();
            foreach (string id in ids)
            {
                if (!counts.ContainsKey(id))
                    order.Add(id);
                counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;
            }
            List<string> parts = new List<string>();
            foreach (string id in order)
            {
                ElementDef element = catalog.GetElement(id);
                parts.Add((element != null ? element.DisplayName : id) + " " + counts[id]);
            }
            return string.Join(", ", parts);
        }

        private static int CountVehicles(DataCatalog catalog, IEnumerable<string> ids)
        {
            int count = 0;
            foreach (string id in ids)
            {
                ElementDef element = catalog.GetElement(id);
                if (element != null && element.ElementClass != "inf_squad")
                    ++count;
            }
            return count;
        }

        private static int CountSquads(DataCatalog catalog, IEnumerable<string> ids, ICollection<string> passengers)
        {
            int count = passengers.Count;
            foreach (string id in ids)
            {
                ElementDef element = catalog.GetElement(id);
                if (element != null && element.ElementClass == "inf_squad")
                    ++count;
            }
            return count;
        }

        private static int Stars(double value)
        {
            return Clamp(Round(value * 5.0), 0, 5);
        }

        private static int Round(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static string Number(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
