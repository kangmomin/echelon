using System;
using System.Collections.Generic;
using System.Linq;

namespace Echelon.Core
{
    public static class ForceRules
    {
        public static readonly double[] WaveDiscount = { 0.0, 0.1, 0.2 };
        public const int MinPlatoons = 2;
        public const int MaxPlatoons = 4;
        public const double DutyCycle = 0.25;
        public static readonly string[] CompanyLetters = { "A", "B", "C", "D", "E", "F" };

        public sealed class CostLine
        {
            public string Label { get; }
            public int Cost { get; }

            public CostLine(string label, int cost)
            {
                Label = label;
                Cost = cost;
            }
        }

        public sealed class Report
        {
            public int Total { get; internal set; }
            public int Budget { get; internal set; }
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            public List<string> AdHoc { get; } = new List<string>();
            public List<CostLine> Lines { get; } = new List<CostLine>();
            public double SustainMin { get; internal set; }
            public int ElementCount { get; internal set; }

            public bool IsValid()
            {
                return Errors.Count == 0;
            }
        }

        public static int PlatoonBaseCost(DataCatalog catalog, PlatoonSpec platoon)
        {
            UnitTemplate? template = catalog.GetUnitTemplate(platoon.Template);
            if (template == null)
            {
                return 0;
            }

            int total = 0;
            foreach (string id in template.ElementsFor(platoon.Loadout))
            {
                total += catalog.GetElement(id)?.Cost ?? 0;
            }

            foreach (string id in template.PassengersFor(platoon.Loadout))
            {
                total += catalog.GetElement(id)?.Cost ?? 0;
            }

            return total;
        }

        public static int PlatoonCost(DataCatalog catalog, PlatoonSpec platoon)
        {
            int baseCost = PlatoonBaseCost(catalog, platoon);
            int wave = ForceSpec.Clamp(platoon.Wave, 0, ForceSpec.MaxWave);
            return (int)Math.Round(baseCost * (1.0 - WaveDiscount[wave]), MidpointRounding.AwayFromZero);
        }

        public static int CompanyHqCost(DataCatalog catalog, string templateId)
        {
            UnitTemplate? template = catalog.GetUnitTemplate(templateId);
            if (template == null)
            {
                return 0;
            }

            int total = 0;
            foreach (string id in template.HqElements)
            {
                total += catalog.GetElement(id)?.Cost ?? 0;
            }

            return total;
        }

        public static Report Evaluate(DataCatalog catalog, ForceSpec spec, int budget)
        {
            Report report = new Report { Budget = budget };
            TOETemplate? toe = catalog.GetToe(spec.Toe);
            if (toe == null)
            {
                report.Errors.Add("편제표 '" + spec.Toe + "' 가 없습니다.");
                return report;
            }

            if (toe.Faction != spec.Faction)
            {
                report.Errors.Add("편제표 진영(" + toe.Faction + ")과 전투단 진영(" + spec.Faction + ")이 다릅니다.");
            }

            ElementDef? cp = catalog.GetElement(toe.HqElement);
            int cpCost = (cp?.Cost ?? 0) * toe.HqCount;
            report.Lines.Add(new CostLine("대대 지휘소", cpCost));
            report.Total += cpCost;
            report.ElementCount += toe.HqCount;

            CheckCompanies(catalog, spec, toe, report);
            CheckList(catalog, spec.Organic, toe.Organic, "직할", report, -1);
            List<string> allowedAttachments = new List<string>();
            int maximumAttachments = 0;
            foreach (TOETemplate.AttachmentSlot slot in toe.AttachmentSlots)
            {
                allowedAttachments.AddRange(slot.Allowed);
                maximumAttachments += slot.MaxCount;
            }

            CheckList(catalog, spec.Attachments, allowedAttachments, "배속", report, maximumAttachments);
            foreach (TOETemplate.AttachmentSlot slot in toe.AttachmentSlots)
            {
                int count = spec.Attachments.Count(platoon => slot.Allowed.Contains(platoon.Template));
                if (count > slot.MaxCount)
                {
                    List<string> names = new List<string>();
                    foreach (string templateId in slot.Allowed)
                    {
                        UnitTemplate? template = catalog.GetUnitTemplate(templateId);
                        names.Add(template?.DisplayName ?? templateId);
                    }

                    report.Errors.Add("배속: " + string.Join(" / ", names) + " 은(는) 합쳐서 최대 " + slot.MaxCount + "개입니다.");
                }
            }

            if (spec.ArtilleryMissions > toe.MaxArtillery)
            {
                report.Errors.Add("포병 사격 임무는 최대 " + toe.MaxArtillery + "회입니다.");
            }

            if (spec.CasSorties > toe.MaxCas)
            {
                report.Errors.Add("근접항공지원은 최대 " + toe.MaxCas + "회입니다.");
            }

            int support = spec.ArtilleryMissions * toe.ArtilleryMissionCost + spec.CasSorties * toe.CasSortieCost;
            if (support > 0)
            {
                report.Lines.Add(new CostLine(
                    "지원 (포병 " + spec.ArtilleryMissions + "회, CAS " + spec.CasSorties + "회)", support));
                report.Total += support;
            }

            if (report.Total > budget)
            {
                report.Errors.Add("예산 초과: " + report.Total + " / " + budget + " 점");
            }

            report.SustainMin = SustainMinutes(catalog, spec);
            return report;
        }

        public static double SustainMinutes(DataCatalog catalog, ForceSpec spec)
        {
            double minutesSum = 0.0;
            int shooters = 0;
            int need = 0;
            int carried = 0;
            foreach (PlatoonSpec platoon in AllPlatoons(spec))
            {
                UnitTemplate? template = catalog.GetUnitTemplate(platoon.Template);
                if (template == null)
                {
                    continue;
                }

                List<string> ids = template.ElementsFor(platoon.Loadout);
                ids.AddRange(template.PassengersFor(platoon.Loadout));
                foreach (string id in ids)
                {
                    ElementDef? element = catalog.GetElement(id);
                    if (element == null)
                    {
                        continue;
                    }

                    carried += element.SupplyCapacity;
                    need += element.SupplyFull;
                    if (element.Weapons.Count == 0)
                    {
                        continue;
                    }

                    WeaponDef? weapon = catalog.GetWeapon(element.Weapons[0]);
                    int rounds = weapon != null && element.Ammo.TryGetValue(weapon.Id, out int ammo) ? ammo : 0;
                    if (weapon == null || rounds <= 0 || weapon.RofRpm <= 0.0)
                    {
                        continue;
                    }

                    minutesSum += rounds / (weapon.RofRpm * DutyCycle);
                    ++shooters;
                }
            }

            if (shooters == 0)
            {
                return 0.0;
            }

            double baseMinutes = minutesSum / shooters;
            double refills = need > 0 ? carried / (double)need : 0.0;
            return baseMinutes * (1.0 + refills);
        }

        public static List<PlatoonSpec> AllPlatoons(ForceSpec spec)
        {
            List<PlatoonSpec> result = new List<PlatoonSpec>();
            foreach (CompanySpec company in spec.Companies)
            {
                result.AddRange(company.Platoons);
            }

            result.AddRange(spec.Organic);
            result.AddRange(spec.Attachments);
            return result;
        }

        public static int EntryPointIndex(ScenarioDef.SideSetup setup, ForceSpec spec)
        {
            if (setup.EntryPoints.Count == 0)
            {
                return -1;
            }

            return ForceSpec.Clamp(spec.EntryPoint, 0, setup.EntryPoints.Count - 1);
        }

        public static SimVec2? ResolveEntryPoint(ScenarioDef.SideSetup setup, ForceSpec spec)
        {
            int index = EntryPointIndex(setup, spec);
            return index < 0 ? (SimVec2?)null : setup.EntryPoints[index];
        }

        private static void CheckCompanies(DataCatalog catalog, ForceSpec spec, TOETemplate toe, Report report)
        {
            if (spec.Companies.Count > toe.MaxCompanies)
            {
                report.Errors.Add("중대는 최대 " + toe.MaxCompanies + "개입니다.");
            }

            HashSet<string> used = new HashSet<string>();
            int templateTankCompanies = 0;
            int composedTankCompanies = 0;
            bool cross = false;
            foreach (CompanySpec company in spec.Companies)
            {
                TOETemplate.CompanySlot? slot = FindSlot(toe, company.Slot);
                UnitTemplate? template = catalog.GetUnitTemplate(company.Template);
                string label = Letter(spec, company) + " 중대";
                if (slot == null)
                {
                    report.Errors.Add(label + ": 편제표에 없는 슬롯 '" + company.Slot + "'");
                    continue;
                }

                if (!used.Add(company.Slot))
                {
                    report.Errors.Add("슬롯 '" + company.Slot + "' 가 두 번 쓰였습니다.");
                }

                if (template == null || template.Echelon != "company" || !slot.Allowed.Contains(company.Template))
                {
                    report.Errors.Add(label + ": '" + company.Template + "' 는 이 슬롯에 넣을 수 없습니다.");
                    continue;
                }

                if (template.Kind == "tank")
                {
                    ++templateTankCompanies;
                }

                int hqCost = CompanyHqCost(catalog, company.Template);
                report.Total += hqCost;
                report.ElementCount += template.HqElements.Count;
                int cost = hqCost;
                int tanks = 0;
                foreach (PlatoonSpec platoon in company.Platoons)
                {
                    CheckPlatoon(catalog, platoon, spec.Faction, label, report);
                    int platoonCost = PlatoonCost(catalog, platoon);
                    cost += platoonCost;
                    report.Total += platoonCost;
                    UnitTemplate? platoonTemplate = catalog.GetUnitTemplate(platoon.Template);
                    if (platoonTemplate != null && platoonTemplate.Kind == "tank")
                    {
                        ++tanks;
                    }

                    if (platoon.Home != company.Slot)
                    {
                        cross = true;
                    }
                }

                if (company.Platoons.Count == 0)
                {
                    report.Errors.Add(label + " 에 소대가 없습니다.");
                }
                else if (company.Platoons.Count < MinPlatoons || company.Platoons.Count > MaxPlatoons)
                {
                    report.Warnings.Add(label + ": 소대 " + company.Platoons.Count + "개 — 비편제(명령 지연 +20%, 재편 불가)");
                    report.AdHoc.Add(company.Slot);
                }

                if (company.Platoons.Count != 0 && tanks * 2 >= company.Platoons.Count)
                {
                    ++composedTankCompanies;
                }

                report.Lines.Add(new CostLine(label + " (" + template.DisplayName + ")", cost));
            }

            foreach (TOETemplate.CompanySlot slot in toe.CompanySlots)
            {
                if (slot.Required && !used.Contains(slot.Id))
                {
                    report.Errors.Add("필수 중대 슬롯 '" + slot.Id + "' 가 비어 있습니다.");
                }
            }

            int minimumTank = toe.Constraints.TryGetValue("min_tank_co", out int minimum) ? minimum : 0;
            if (templateTankCompanies < minimumTank)
            {
                report.Errors.Add("전차 중대가 최소 " + minimumTank + "개 필요합니다(현재 " + templateTankCompanies + ").");
            }
            else if (cross && composedTankCompanies < minimumTank)
            {
                report.Warnings.Add("교차 배속으로 전차 위주 중대가 " + composedTankCompanies + "개(최소 " + minimumTank + ") — 해당 중대 비편제");
                foreach (CompanySpec company in spec.Companies)
                {
                    if (InvolvedInCross(spec, company) && !report.AdHoc.Contains(company.Slot))
                    {
                        report.AdHoc.Add(company.Slot);
                    }
                }
            }
        }

        private static bool InvolvedInCross(ForceSpec spec, CompanySpec company)
        {
            foreach (PlatoonSpec platoon in company.Platoons)
            {
                if (platoon.Home != company.Slot)
                {
                    return true;
                }
            }

            foreach (CompanySpec other in spec.Companies)
            {
                if (ReferenceEquals(other, company))
                {
                    continue;
                }

                foreach (PlatoonSpec platoon in other.Platoons)
                {
                    if (platoon.Home == company.Slot)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void CheckList(DataCatalog catalog, List<PlatoonSpec> platoons, List<string> allowed,
            string what, Report report, int maximumCount)
        {
            if (maximumCount >= 0 && platoons.Count > maximumCount)
            {
                report.Errors.Add(what + " 자산은 최대 " + maximumCount + "개입니다.");
            }

            HashSet<string> seen = new HashSet<string>();
            foreach (PlatoonSpec platoon in platoons)
            {
                UnitTemplate? template = catalog.GetUnitTemplate(platoon.Template);
                if (!allowed.Contains(platoon.Template))
                {
                    report.Errors.Add(what + ": '" + platoon.Template + "' 는 이 편제표에서 쓸 수 없습니다.");
                    continue;
                }

                if (maximumCount < 0 && seen.Contains(platoon.Template))
                {
                    report.Errors.Add(what + ": '" + platoon.Template + "' 가 중복됐습니다.");
                }

                seen.Add(platoon.Template);
                CheckPlatoon(catalog, platoon, template?.Faction ?? "", what, report);
                int cost = PlatoonCost(catalog, platoon);
                report.Total += cost;
                report.Lines.Add(new CostLine(what + ": " + (template?.DisplayName ?? platoon.Template), cost));
            }
        }

        private static void CheckPlatoon(DataCatalog catalog, PlatoonSpec platoon, string faction,
            string where, Report report)
        {
            UnitTemplate? template = catalog.GetUnitTemplate(platoon.Template);
            if (template == null || template.Echelon != "platoon")
            {
                report.Errors.Add(where + ": 소대 템플릿 '" + platoon.Template + "' 가 없습니다.");
                return;
            }

            if (template.Faction != faction)
            {
                report.Errors.Add(where + ": '" + template.DisplayName + "' 는 다른 진영 부대입니다.");
            }

            if (platoon.Loadout != "" && template.GetLoadout(platoon.Loadout) == null)
            {
                report.Errors.Add(where + ": '" + template.DisplayName + "' 에 장비 템플릿 '" + platoon.Loadout + "' 가 없습니다.");
            }

            if (platoon.Wave < 0 || platoon.Wave > ForceSpec.MaxWave)
            {
                report.Errors.Add(where + ": 증원 단계 " + platoon.Wave + " 는 0~2 여야 합니다.");
            }

            report.ElementCount += template.ElementsFor(platoon.Loadout).Count + template.PassengersFor(platoon.Loadout).Count;
        }

        private static TOETemplate.CompanySlot? FindSlot(TOETemplate toe, string slotId)
        {
            foreach (TOETemplate.CompanySlot slot in toe.CompanySlots)
            {
                if (slot.Id == slotId)
                {
                    return slot;
                }
            }

            return null;
        }

        private static string Letter(ForceSpec spec, CompanySpec company)
        {
            int index = spec.Companies.IndexOf(company);
            return index >= 0 ? CompanyLetters[index % CompanyLetters.Length] : "?";
        }
    }
}
