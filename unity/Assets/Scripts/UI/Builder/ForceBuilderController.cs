using System;
using System.Collections.Generic;
using Echelon.Core;

namespace Echelon.Unity.UI.Builder
{
    public sealed class ForceBuilderController
    {
        public const int FallbackBudget = 2000;
        public static readonly string[] Factions = { "US", "RU" };

        private readonly DataCatalog _catalog;
        private readonly ScenarioDef _scenario;

        public ForceBuilderController(
            DataCatalog catalog,
            ScenarioDef scenario,
            IForceStore store,
            ForceSpec initial = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Store = store ?? throw new ArgumentNullException(nameof(store));
            ForceSpec selected = initial != null ? initial.DuplicateSpec() : _catalog.DefaultForce("US");
            if (selected == null)
                selected = CreateFallback("US");
            Current = selected ?? new ForceSpec { Faction = "US" };
        }

        public DataCatalog Catalog => _catalog;
        public ScenarioDef Scenario => _scenario;
        public IForceStore Store { get; }
        public ForceSpec Current { get; private set; }
        public ForceSpec CurrentSpec => Current;
        public ForceSpec Spec => Current;
        public ForceRules.Report Report => ForceRules.Evaluate(_catalog, Current, Budget);
        public bool CanStart => Report.IsValid();
        public string LastNotice { get; private set; } = string.Empty;
        public bool LastNoticeSucceeded { get; private set; }

        public int Budget
        {
            get
            {
                ScenarioDef.SideSetup side = SideForFaction(Current.Faction);
                return side != null ? side.Budget : FallbackBudget;
            }
        }

        public event Action Changed;
        public event Action<ForceSpec> StartRequested;
        public event Action BackRequested;
        public event Action<string, bool> Notice;

        public ScenarioDef.SideSetup SideForCurrentFaction()
        {
            return SideForFaction(Current.Faction);
        }

        public ScenarioDef.SideSetup SideForFaction(string faction)
        {
            foreach (ScenarioDef.SideSetup side in _scenario.Sides.Values)
            {
                if (side.Faction == faction)
                    return side;
            }
            return null;
        }

        public void SetName(string value)
        {
            Current.Name = value ?? string.Empty;
        }

        public bool SetFaction(string faction)
        {
            if (Array.IndexOf(Factions, faction) < 0 || Current.Faction == faction)
                return false;
            ForceSpec replacement = _catalog.DefaultForce(faction) ?? CreateFallback(faction);
            if (replacement == null)
                return false;
            Replace(replacement);
            return true;
        }

        public bool Replace(ForceSpec replacement)
        {
            if (replacement == null)
                return false;
            Current = replacement.DuplicateSpec();
            Changed?.Invoke();
            return true;
        }

        public bool UseDefaultForce()
        {
            ForceSpec replacement = _catalog.DefaultForce(Current.Faction) ?? CreateFallback(Current.Faction);
            return Replace(replacement);
        }

        public bool UseMinimalForce()
        {
            if (_catalog.GetToe(Current.Toe) == null)
                return false;
            return Replace(ForceSpec.FromToe(_catalog, Current.Toe));
        }

        public bool SetCompany(string slotId, string templateId)
        {
            TOETemplate toe = _catalog.GetToe(Current.Toe);
            TOETemplate.CompanySlot slot = FindCompanySlot(toe, slotId);
            if (slot == null)
                return false;
            templateId = templateId ?? string.Empty;
            if (templateId.Length == 0 && slot.Required)
                return false;
            if (templateId.Length != 0 && !slot.Allowed.Contains(templateId))
                return false;

            CompanySpec existing = Current.GetCompany(slotId);
            if ((existing == null && templateId.Length == 0) ||
                (existing != null && existing.Template == templateId))
                return false;

            if (templateId.Length == 0)
            {
                ReturnForeignPlatoons(existing);
                Current.Companies.Remove(existing);
            }
            else if (existing == null)
            {
                Current.Companies.Add(ForceSpec.MakeCompany(_catalog, slotId, templateId));
                SortCompanies(toe);
            }
            else
            {
                int index = Current.Companies.IndexOf(existing);
                Current.Companies[index] = ForceSpec.MakeCompany(_catalog, slotId, templateId);
                ReturnForeignPlatoons(existing);
            }

            Changed?.Invoke();
            return true;
        }

        public bool MovePlatoon(string fromSlot, int index, string toSlot)
        {
            if (!Current.MovePlatoon(fromSlot, index, toSlot))
                return false;
            Changed?.Invoke();
            return true;
        }

        public bool SetLoadout(PlatoonSpec platoon, string loadoutId)
        {
            if (!ContainsPlatoon(platoon))
                return false;
            UnitTemplate template = _catalog.GetUnitTemplate(platoon.Template);
            loadoutId = loadoutId ?? string.Empty;
            if (template == null || (loadoutId.Length != 0 && template.GetLoadout(loadoutId) == null) ||
                platoon.Loadout == loadoutId)
                return false;
            platoon.Loadout = loadoutId;
            Changed?.Invoke();
            return true;
        }

        public bool SetWave(PlatoonSpec platoon, int wave)
        {
            if (!ContainsPlatoon(platoon) || wave < 0 || wave > ForceSpec.MaxWave || platoon.Wave == wave)
                return false;
            platoon.Wave = wave;
            Changed?.Invoke();
            return true;
        }

        public bool SetOrganic(string templateId, bool included)
        {
            TOETemplate toe = _catalog.GetToe(Current.Toe);
            if (toe == null || !toe.Organic.Contains(templateId))
                return false;
            PlatoonSpec existing = Current.Organic.Find(platoon => platoon.Template == templateId);
            if (included == (existing != null))
                return false;
            if (included)
                Current.Organic.Add(new PlatoonSpec { Template = templateId });
            else
                Current.Organic.Remove(existing);
            Changed?.Invoke();
            return true;
        }

        public int AttachmentLimit()
        {
            TOETemplate toe = _catalog.GetToe(Current.Toe);
            if (toe == null)
                return 0;
            int total = 0;
            foreach (TOETemplate.AttachmentSlot slot in toe.AttachmentSlots)
                total += slot.MaxCount;
            return total;
        }

        public List<string> AllowedAttachments()
        {
            List<string> result = new List<string>();
            TOETemplate toe = _catalog.GetToe(Current.Toe);
            if (toe == null)
                return result;
            foreach (TOETemplate.AttachmentSlot slot in toe.AttachmentSlots)
            {
                foreach (string templateId in slot.Allowed)
                {
                    if (!result.Contains(templateId))
                        result.Add(templateId);
                }
            }
            return result;
        }

        public bool CanAddAttachment(string templateId)
        {
            TOETemplate toe = _catalog.GetToe(Current.Toe);
            if (toe == null || Current.Attachments.Count >= AttachmentLimit())
                return false;
            bool allowed = false;
            foreach (TOETemplate.AttachmentSlot slot in toe.AttachmentSlots)
            {
                if (slot.Allowed.Contains(templateId))
                {
                    allowed = true;
                    break;
                }
            }
            if (!allowed)
                return false;

            List<string> candidate = new List<string>(Current.Attachments.Count + 1);
            foreach (PlatoonSpec platoon in Current.Attachments)
                candidate.Add(platoon.Template);
            candidate.Add(templateId);
            return CanFitAttachments(toe, candidate);
        }

        public bool AddAttachment(string templateId)
        {
            if (!CanAddAttachment(templateId))
                return false;
            Current.Attachments.Add(new PlatoonSpec { Template = templateId });
            Changed?.Invoke();
            return true;
        }

        public bool RemoveAttachment(int index)
        {
            if (index < 0 || index >= Current.Attachments.Count)
                return false;
            Current.Attachments.RemoveAt(index);
            Changed?.Invoke();
            return true;
        }

        public bool RemoveAttachment(PlatoonSpec platoon)
        {
            return platoon != null && RemoveAttachment(Current.Attachments.IndexOf(platoon));
        }

        public bool SetArtilleryMissions(int count)
        {
            TOETemplate toe = _catalog.GetToe(Current.Toe);
            if (toe == null)
                return false;
            int value = Clamp(count, 0, toe.MaxArtillery);
            if (Current.ArtilleryMissions == value)
                return false;
            Current.ArtilleryMissions = value;
            Changed?.Invoke();
            return true;
        }

        public bool SetCasSorties(int count)
        {
            TOETemplate toe = _catalog.GetToe(Current.Toe);
            if (toe == null)
                return false;
            int value = Clamp(count, 0, toe.MaxCas);
            if (Current.CasSorties == value)
                return false;
            Current.CasSorties = value;
            Changed?.Invoke();
            return true;
        }

        public bool SetEntryPoint(int index)
        {
            ScenarioDef.SideSetup side = SideForCurrentFaction();
            if (side == null || side.EntryPoints.Count == 0)
                return false;
            int value = Clamp(index, 0, side.EntryPoints.Count - 1);
            if (Current.EntryPoint == value)
                return false;
            Current.EntryPoint = value;
            Changed?.Invoke();
            return true;
        }

        public bool Save()
        {
            try
            {
                string path = Store.PathFor(Current.Name);
                Store.Save(Current, path);
                Notify("저장했습니다: " + DisplayNameForSaved(path), true);
                return true;
            }
            catch (Exception exception)
            {
                Notify("저장 실패: " + exception.Message, false);
                return false;
            }
        }

        public bool Save(string name)
        {
            SetName(name);
            return Save();
        }

        public IReadOnlyList<string> ListSaved()
        {
            try
            {
                return Store.ListSaved() ?? new List<string>();
            }
            catch (Exception exception)
            {
                Notify("저장 목록을 읽지 못했습니다: " + exception.Message, false);
                return new List<string>();
            }
        }

        public bool LoadSaved(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;
            try
            {
                ForceSpec loaded = Store.LoadSpec(fileName);
                if (loaded == null)
                {
                    Notify("불러오기 실패: " + DisplayNameForSaved(fileName), false);
                    return false;
                }
                Replace(loaded);
                Notify("불러왔습니다: " + DisplayNameForSaved(fileName), true);
                return true;
            }
            catch (Exception exception)
            {
                Notify("불러오기 실패: " + exception.Message, false);
                return false;
            }
        }

        public bool RequestStart()
        {
            if (!CanStart)
                return false;
            StartRequested?.Invoke(Current.DuplicateSpec());
            return true;
        }

        public void RequestBack()
        {
            BackRequested?.Invoke();
        }

        public static string DisplayNameForSaved(string path)
        {
            string value = path ?? string.Empty;
            value = value.Replace('\\', '/');
            int slash = value.LastIndexOf('/');
            if (slash >= 0)
                value = value.Substring(slash + 1);
            return value.EndsWith(".json", StringComparison.Ordinal)
                ? value.Substring(0, value.Length - 5)
                : value;
        }

        private ForceSpec CreateFallback(string faction)
        {
            foreach (TOETemplate toe in _catalog.ToeTemplates.Values)
            {
                if (toe.Faction == faction)
                    return ForceSpec.FromToe(_catalog, toe.Id);
            }
            return null;
        }

        private bool ContainsPlatoon(PlatoonSpec platoon)
        {
            return platoon != null && ForceRules.AllPlatoons(Current).Contains(platoon);
        }

        private void ReturnForeignPlatoons(CompanySpec company)
        {
            if (company == null)
                return;
            foreach (PlatoonSpec platoon in company.Platoons)
            {
                if (platoon.Home == company.Slot)
                    continue;
                CompanySpec home = Current.GetCompany(platoon.Home);
                if (home != null && !home.Platoons.Contains(platoon))
                    home.Platoons.Add(platoon);
            }
        }

        private void SortCompanies(TOETemplate toe)
        {
            List<CompanySpec> ordered = new List<CompanySpec>();
            foreach (TOETemplate.CompanySlot slot in toe.CompanySlots)
            {
                CompanySpec company = Current.GetCompany(slot.Id);
                if (company != null)
                    ordered.Add(company);
            }
            Current.Companies.Clear();
            Current.Companies.AddRange(ordered);
        }

        private void Notify(string message, bool succeeded)
        {
            LastNotice = message;
            LastNoticeSucceeded = succeeded;
            Notice?.Invoke(message, succeeded);
        }

        private static TOETemplate.CompanySlot FindCompanySlot(TOETemplate toe, string slotId)
        {
            if (toe == null)
                return null;
            foreach (TOETemplate.CompanySlot slot in toe.CompanySlots)
            {
                if (slot.Id == slotId)
                    return slot;
            }
            return null;
        }

        private static bool CanFitAttachments(TOETemplate toe, IList<string> templateIds)
        {
            return AssignAttachment(toe, templateIds, 0, new int[toe.AttachmentSlots.Count]);
        }

        private static bool AssignAttachment(
            TOETemplate toe,
            IList<string> templateIds,
            int index,
            int[] counts)
        {
            if (index == templateIds.Count)
                return true;
            string templateId = templateIds[index];
            for (int i = 0; i < toe.AttachmentSlots.Count; ++i)
            {
                TOETemplate.AttachmentSlot slot = toe.AttachmentSlots[i];
                if (!slot.Allowed.Contains(templateId) || counts[i] >= slot.MaxCount)
                    continue;
                ++counts[i];
                if (AssignAttachment(toe, templateIds, index + 1, counts))
                    return true;
                --counts[i];
            }
            return false;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
