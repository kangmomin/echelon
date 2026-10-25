using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Echelon.Core;
using Echelon.Unity.UI.Common;
using Echelon.Unity.UI.UnitInfo;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitInformation = Echelon.Unity.UI.UnitInfo.UnitInfo;

namespace Echelon.Unity.UI.Builder
{
    [DisallowMultipleComponent]
    public sealed class ForceBuilder : MonoBehaviour
    {
        private readonly List<TMP_Dropdown> _companyDropdowns = new List<TMP_Dropdown>();
        private readonly List<TMP_Dropdown> _loadoutDropdowns = new List<TMP_Dropdown>();
        private readonly List<TMP_Dropdown> _waveDropdowns = new List<TMP_Dropdown>();
        private readonly List<TMP_Dropdown> _moveDropdowns = new List<TMP_Dropdown>();
        private readonly List<Toggle> _organicToggles = new List<Toggle>();
        private readonly List<Button> _attachmentButtons = new List<Button>();
        private readonly List<string> _savedPaths = new List<string>();

        private RectTransform _list;
        private RectTransform _loadRow;
        private Image _budgetFill;
        private bool _wired;

        public ForceBuilderController Controller { get; private set; }
        public ForceSpec Current => Controller != null ? Controller.Current : null;
        public ForceSpec CurrentSpec => Current;
        public ForceSpec Spec => Current;
        public ForceRules.Report Report => Controller != null ? Controller.Report : null;
        public bool CanStart => Controller != null && Controller.CanStart;
        public bool IsBuilt { get; private set; }

        public TMP_Dropdown FactionDropdown { get; private set; }
        public TMP_InputField NameInput { get; private set; }
        public TMP_Dropdown LoadDropdown { get; private set; }
        public TMP_Dropdown EntryDropdown { get; private set; }
        public Button SaveButton { get; private set; }
        public Button LoadButton { get; private set; }
        public Button DefaultButton { get; private set; }
        public Button MinimalButton { get; private set; }
        public Button StartButton { get; private set; }
        public Button BackButton { get; private set; }
        public Button ArtilleryMinusButton { get; private set; }
        public Button ArtilleryPlusButton { get; private set; }
        public Button CasMinusButton { get; private set; }
        public Button CasPlusButton { get; private set; }
        public TextMeshProUGUI BudgetLabel { get; private set; }
        public TextMeshProUGUI SummaryText { get; private set; }
        public TextMeshProUGUI ToastText { get; private set; }
        public TextMeshProUGUI ArtilleryValue { get; private set; }
        public TextMeshProUGUI CasValue { get; private set; }
        public TextMeshProUGUI AttachmentLimitLabel { get; private set; }
        public UnitInfoPanel InfoPanel { get; private set; }
        public UnitInfoTooltip Tooltip { get; private set; }

        public IReadOnlyList<TMP_Dropdown> CompanyDropdowns => _companyDropdowns;
        public IReadOnlyList<TMP_Dropdown> LoadoutDropdowns => _loadoutDropdowns;
        public IReadOnlyList<TMP_Dropdown> WaveDropdowns => _waveDropdowns;
        public IReadOnlyList<TMP_Dropdown> MoveDropdowns => _moveDropdowns;
        public IReadOnlyList<Toggle> OrganicToggles => _organicToggles;
        public IReadOnlyList<Button> AttachmentButtons => _attachmentButtons;

        public event Action<ForceSpec> StartRequested;
        public event Action BackRequested;

        public static ForceBuilder Create(
            Transform parent,
            DataCatalog catalog,
            ScenarioDef scenario,
            IForceStore store,
            ForceSpec initial = null)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("ForceBuilder", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            ForceBuilder builder = root.AddComponent<ForceBuilder>();
            builder.Setup(catalog, scenario, store, initial);
            return builder;
        }

        public void Setup(
            DataCatalog catalog,
            ScenarioDef scenario,
            IForceStore store,
            ForceSpec initial = null)
        {
            UnwireController();
            Controller = new ForceBuilderController(catalog, scenario, store, initial);
            if (IsBuilt)
            {
                WireController();
                RefreshLoadList();
                Rebuild();
            }
            else
            {
                Build();
            }
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            if (Controller == null)
                throw new InvalidOperationException("ForceBuilder.Setup must supply catalog, scenario, and force store before Build.");
            IsBuilt = true;
            UiKit.Stretch((RectTransform)transform);
            UiKit.CreateBackground(transform);

            HorizontalLayoutGroup root = UiKit.CreateHorizontalLayout(transform, "BuilderColumns", 12, 12);
            UiKit.Stretch(root.GetComponent<RectTransform>());
            root.childForceExpandWidth = false;
            root.childForceExpandHeight = true;

            RectTransform left = UiKit.CreatePanel(root.transform, "SummaryPanel");
            UiKit.AddVerticalLayout(left, 8, 12);
            LayoutElement leftLayout = left.gameObject.AddComponent<LayoutElement>();
            leftLayout.minWidth = 340f;
            leftLayout.preferredWidth = 340f;
            leftLayout.flexibleWidth = 0f;
            leftLayout.flexibleHeight = 1f;
            BuildSidePanel(left);

            RectTransform center = UiKit.CreatePanel(root.transform, "ForceListPanel");
            LayoutElement centerLayout = center.gameObject.AddComponent<LayoutElement>();
            centerLayout.minWidth = 420f;
            centerLayout.flexibleWidth = 1f;
            centerLayout.flexibleHeight = 1f;
            ScrollRect centerScroll = UiKit.CreateScrollView(center, "ForceListScroll");
            UiKit.Stretch(centerScroll.GetComponent<RectTransform>());
            VerticalLayoutGroup listLayout = centerScroll.content.GetComponent<VerticalLayoutGroup>();
            listLayout.spacing = 8f;
            listLayout.padding = new RectOffset(5, 5, 5, 5);
            _list = centerScroll.content;

            InfoPanel = UnitInfoPanel.Create(root.transform);
            LayoutElement infoLayout = InfoPanel.gameObject.AddComponent<LayoutElement>();
            infoLayout.minWidth = 400f;
            infoLayout.preferredWidth = 400f;
            infoLayout.flexibleWidth = 0f;
            infoLayout.flexibleHeight = 1f;

            Tooltip = UnitInfoTooltip.Create(transform);
            WireController();
            RefreshLoadList();
            Rebuild();
            UiKit.EnsureEventSystem();
        }

        public void Refresh()
        {
            Rebuild();
        }

        public bool SelectFaction(string faction)
        {
            return Controller.SetFaction(faction);
        }

        public bool SetCompany(string slotId, string templateId)
        {
            return Controller.SetCompany(slotId, templateId);
        }

        public bool MovePlatoon(string fromSlot, int index, string toSlot)
        {
            return Controller.MovePlatoon(fromSlot, index, toSlot);
        }

        public bool SetLoadout(PlatoonSpec platoon, string loadout)
        {
            return Controller.SetLoadout(platoon, loadout);
        }

        public bool SetWave(PlatoonSpec platoon, int wave)
        {
            return Controller.SetWave(platoon, wave);
        }

        public bool SetOrganic(string templateId, bool included)
        {
            return Controller.SetOrganic(templateId, included);
        }

        public bool AddAttachment(string templateId)
        {
            return Controller.AddAttachment(templateId);
        }

        public bool RemoveAttachment(int index)
        {
            return Controller.RemoveAttachment(index);
        }

        public bool SetArtilleryMissions(int count)
        {
            return Controller.SetArtilleryMissions(count);
        }

        public bool SetCasSorties(int count)
        {
            return Controller.SetCasSorties(count);
        }

        public bool SetEntryPoint(int index)
        {
            return Controller.SetEntryPoint(index);
        }

        public bool UseDefaultForce()
        {
            return Controller.UseDefaultForce();
        }

        public bool UseMinimalForce()
        {
            return Controller.UseMinimalForce();
        }

        public bool SaveCurrent()
        {
            bool saved = Controller.Save();
            RefreshLoadList();
            return saved;
        }

        public bool LoadSaved(string fileName)
        {
            bool loaded = Controller.LoadSaved(fileName);
            RefreshLoadList();
            return loaded;
        }

        public bool RequestStart()
        {
            return Controller.RequestStart();
        }

        public void RequestBack()
        {
            Controller.RequestBack();
        }

        public void ShowPlatoon(string templateId, string loadout = "", int cost = -1)
        {
            InfoPanel.ShowPlatoon(Controller.Catalog, templateId, loadout, cost);
        }

        public void ShowCompany(string templateId)
        {
            InfoPanel.ShowCompany(Controller.Catalog, templateId);
        }

        public void HideTooltip()
        {
            if (Tooltip != null)
                Tooltip.Hide();
        }

        public void RefreshLoadList()
        {
            if (_loadRow == null)
                return;
            string previous = LoadDropdown != null && LoadDropdown.value >= 0 && LoadDropdown.value < _savedPaths.Count
                ? _savedPaths[LoadDropdown.value]
                : string.Empty;
            if (LoadDropdown != null)
                DestroyObject(LoadDropdown.gameObject);
            _savedPaths.Clear();
            IReadOnlyList<string> saved = Controller.ListSaved();
            List<UiOption<string>> options = new List<UiOption<string>>();
            string selected = string.Empty;
            foreach (string path in saved)
            {
                _savedPaths.Add(path);
                options.Add(new UiOption<string>(ForceBuilderController.DisplayNameForSaved(path), path));
                if (path == previous)
                    selected = path;
            }
            if (options.Count == 0)
                options.Add(new UiOption<string>("(저장된 편성 없음)", string.Empty));
            else if (selected.Length == 0)
                selected = _savedPaths[0];
            LoadDropdown = UiKit.CreateOption(_loadRow, options, selected, null, 120f);
            LoadDropdown.name = "SavedForceDropdown";
            LoadDropdown.transform.SetSiblingIndex(0);
            LayoutElement dropdownLayout = LoadDropdown.GetComponent<LayoutElement>();
            dropdownLayout.flexibleWidth = 1f;
            LoadDropdown.interactable = _savedPaths.Count != 0;
            if (LoadButton != null)
                LoadButton.interactable = _savedPaths.Count != 0;
        }

        private void BuildSidePanel(Transform parent)
        {
            UiKit.CreateLabel(parent, "전투단 편성", 22, UiTheme.Accent);
            ScenarioDef scenario = CurrentScenario();
            UiKit.CreateLabel(parent, "시나리오: " + scenario.DisplayName, 13, UiTheme.TextDim);

            HorizontalLayoutGroup factionRow = CreateRow(parent, "FactionRow");
            CreateFixedLabel(factionRow.transform, "진영", 14, 52f);
            List<UiOption<string>> factions = new List<UiOption<string>>
            {
                new UiOption<string>("미 육군", "US"),
                new UiOption<string>("러시아 지상군", "RU")
            };
            FactionDropdown = UiKit.CreateOption(factionRow.transform, factions, Current.Faction,
                faction => SelectFaction(faction), 200f);
            FactionDropdown.name = "FactionDropdown";
            FactionDropdown.GetComponent<LayoutElement>().flexibleWidth = 1f;

            BudgetLabel = UiKit.CreateLabel(parent, string.Empty, 15, UiTheme.Text);
            BudgetLabel.name = "BudgetLabel";
            RectTransform budgetBackground = UiKit.CreatePanel(parent, "BudgetBar", UiTheme.ButtonNormal);
            LayoutElement barLayout = budgetBackground.gameObject.AddComponent<LayoutElement>();
            barLayout.minHeight = 10f;
            barLayout.preferredHeight = 10f;
            RectTransform fill = UiKit.CreatePanel(budgetBackground, "Fill", UiTheme.Accent);
            UiKit.Stretch(fill);
            fill.GetComponent<Image>().raycastTarget = false;
            _budgetFill = fill.GetComponent<Image>();

            ScrollRect summaryScroll = UiKit.CreateScrollView(parent, "SummaryScroll");
            LayoutElement summaryLayout = summaryScroll.gameObject.AddComponent<LayoutElement>();
            summaryLayout.minHeight = 130f;
            summaryLayout.flexibleWidth = 1f;
            summaryLayout.flexibleHeight = 1f;
            VerticalLayoutGroup summaryContent = summaryScroll.content.GetComponent<VerticalLayoutGroup>();
            summaryContent.padding = new RectOffset(3, 3, 3, 3);
            SummaryText = UiKit.CreateLabel(summaryScroll.content, string.Empty, 13, UiTheme.Text);
            SummaryText.name = "SummaryText";
            SummaryText.lineSpacing = 5f;
            SummaryText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            UiKit.CreateSeparator(parent);

            HorizontalLayoutGroup nameRow = CreateRow(parent, "NameRow");
            CreateFixedLabel(nameRow.transform, "이름", 14, 38f);
            NameInput = BuilderControls.CreateInput(nameRow.transform, Current.Name, value => Controller.SetName(value));
            SaveButton = UiKit.CreateButton(nameRow.transform, "저장", () => { SaveCurrent(); }, 56f);

            _loadRow = CreateRow(parent, "LoadRow").GetComponent<RectTransform>();
            LoadButton = UiKit.CreateButton(_loadRow, "불러오기", LoadSelected, 76f);

            HorizontalLayoutGroup presetRow = CreateRow(parent, "PresetRow");
            DefaultButton = UiKit.CreateButton(presetRow.transform, "기본 편성", () => { UseDefaultForce(); }, 130f);
            DefaultButton.GetComponent<LayoutElement>().flexibleWidth = 1f;
            MinimalButton = UiKit.CreateButton(presetRow.transform, "최소 편성", () => { UseMinimalForce(); }, 130f);
            MinimalButton.GetComponent<LayoutElement>().flexibleWidth = 1f;

            ToastText = UiKit.CreateLabel(parent, string.Empty, 12, UiTheme.Ok);
            ToastText.name = "Toast";
            ToastText.gameObject.AddComponent<LayoutElement>().minHeight = 30f;

            HorizontalLayoutGroup navigation = CreateRow(parent, "NavigationRow");
            BackButton = UiKit.CreateButton(navigation.transform, "← 메뉴", RequestBack, 100f);
            StartButton = UiKit.CreateButton(navigation.transform, "배치로 →", () => { RequestStart(); }, 180f);
            StartButton.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private void Rebuild()
        {
            if (!IsBuilt || _list == null || Controller == null)
                return;
            HideTooltip();
            NameInput.SetTextWithoutNotify(Current.Name ?? string.Empty);
            FactionDropdown.SetValueWithoutNotify(Current.Faction == "RU" ? 1 : 0);
            FactionDropdown.RefreshShownValue();
            ClearDynamicList();
            TOETemplate toe = Controller.Catalog.GetToe(Current.Toe);
            if (toe == null)
            {
                UiKit.CreateLabel(_list, "편제표 없음: " + Current.Toe, 14, UiTheme.Error);
            }
            else
            {
                UiKit.CreateLabel(_list,
                    toe.DisplayName + " — 중대 슬롯 " + toe.CompanySlots.Count + " (최대 " + toe.MaxCompanies +
                    "), 지휘 용량 분당 " + toe.CommandCapacityPerMin.ToString("0", CultureInfo.InvariantCulture),
                    15, UiTheme.Accent);
                foreach (TOETemplate.CompanySlot slot in toe.CompanySlots)
                    BuildCompanyBlock(toe, slot);
                SectionTitle("대대 직할");
                foreach (string organic in toe.Organic)
                    BuildOrganicRow(organic);
                SectionTitle("배속 자산");
                foreach (PlatoonSpec attachment in Current.Attachments)
                    CreatePlatoonRow(_list, attachment, string.Empty, true);
                BuildAttachmentAdders();
                SectionTitle("지원 구매");
                BuildSupportRows(toe);
                SectionTitle("증원 진입 지점");
                BuildEntryOptions();
            }
            RefreshSummary();
        }

        private void ClearDynamicList()
        {
            _companyDropdowns.Clear();
            _loadoutDropdowns.Clear();
            _waveDropdowns.Clear();
            _moveDropdowns.Clear();
            _organicToggles.Clear();
            _attachmentButtons.Clear();
            for (int i = _list.childCount - 1; i >= 0; --i)
                DestroyObject(_list.GetChild(i).gameObject);
        }

        private void BuildCompanyBlock(TOETemplate toe, TOETemplate.CompanySlot slot)
        {
            RectTransform box = UiKit.CreatePanel(_list, "Company_" + slot.Id);
            UiKit.AddVerticalLayout(box, 6, 8);
            box.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            CompanySpec company = Current.GetCompany(slot.Id);
            int companyIndex = company != null ? Current.Companies.IndexOf(company) : -1;
            HorizontalLayoutGroup header = CreateRow(box, "CompanyHeader");
            string letter = companyIndex >= 0 ? CompanyLetter(companyIndex) : "-";
            string title = letter + " 중대  [" + slot.Id + (slot.Required ? string.Empty : ", 선택") + "]";
            TextMeshProUGUI titleLabel = UiKit.CreateLabel(header.transform, title, 15, UiTheme.Text);
            titleLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            List<UiOption<string>> choices = new List<UiOption<string>>();
            List<string> templateIds = new List<string>();
            if (!slot.Required)
            {
                choices.Add(new UiOption<string>("(비움)", string.Empty));
                templateIds.Add(string.Empty);
            }
            foreach (string templateId in slot.Allowed)
            {
                UnitTemplate template = Controller.Catalog.GetUnitTemplate(templateId);
                choices.Add(new UiOption<string>(template != null ? template.DisplayName : templateId, templateId));
                templateIds.Add(templateId);
            }
            string selected = company != null ? company.Template : string.Empty;
            TMP_Dropdown dropdown = UiKit.CreateOption(header.transform, choices, selected,
                templateId => Controller.SetCompany(slot.Id, templateId), 210f);
            dropdown.name = "CompanyTemplate_" + slot.Id;
            _companyDropdowns.Add(dropdown);
            HookCompanyDropdown(dropdown, templateIds);

            if (company != null)
            {
                string companyTemplate = company.Template;
                AttachHover(box, UnitInformation.Tooltip(Controller.Catalog, companyTemplate),
                    () => ShowCompany(companyTemplate));
                int hqCost = ForceRules.CompanyHqCost(Controller.Catalog, company.Template);
                CreateFixedLabel(header.transform, "본부 " + hqCost + "점", 13, 86f, UiTheme.TextDim);
            }

            if (company == null)
                return;
            for (int i = 0; i < company.Platoons.Count; ++i)
            {
                PlatoonSpec platoon = company.Platoons[i];
                HorizontalLayoutGroup row = CreatePlatoonRow(box, platoon, letter + "/" + (i + 1), false);
                List<UiOption<string>> moves = new List<UiOption<string>>
                {
                    new UiOption<string>("이동…", string.Empty)
                };
                foreach (CompanySpec other in Current.Companies)
                {
                    if (ReferenceEquals(other, company))
                        continue;
                    int otherIndex = Current.Companies.IndexOf(other);
                    moves.Add(new UiOption<string>("→ " + CompanyLetter(otherIndex) + " 중대", other.Slot));
                }
                string fromSlot = company.Slot;
                int fromIndex = i;
                TMP_Dropdown move = UiKit.CreateOption(row.transform, moves, string.Empty, destination =>
                {
                    if (destination.Length != 0)
                        Controller.MovePlatoon(fromSlot, fromIndex, destination);
                }, 110f);
                move.name = "MovePlatoon";
                _moveDropdowns.Add(move);
            }
        }

        private HorizontalLayoutGroup CreatePlatoonRow(
            Transform parent,
            PlatoonSpec platoon,
            string tag,
            bool removable,
            string organicTemplate = null)
        {
            HorizontalLayoutGroup row = CreateRow(parent, "PlatoonRow", 6);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 36f;
            if (organicTemplate != null)
            {
                string templateId = organicTemplate;
                Toggle toggle = BuilderControls.CreateToggle(row.transform, platoon != null,
                    included => Controller.SetOrganic(templateId, included));
                toggle.name = "Organic_" + organicTemplate;
                _organicToggles.Add(toggle);
            }

            if (platoon == null)
            {
                UnitTemplate missing = Controller.Catalog.GetUnitTemplate(organicTemplate);
                string offText = (missing != null ? missing.DisplayName : organicTemplate) + "  (편성 안 함)";
                TextMeshProUGUI offLabel = UiKit.CreateLabel(row.transform, offText, 14, UiTheme.TextDim);
                offLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                AttachHover(offLabel, UnitInformation.Tooltip(Controller.Catalog, organicTemplate),
                    () => ShowPlatoon(organicTemplate));
                return row;
            }

            UnitTemplate template = Controller.Catalog.GetUnitTemplate(platoon.Template);
            string nameText = template != null ? template.DisplayName : platoon.Template;
            if (tag.Length != 0)
                nameText = tag + "  " + nameText;
            if (platoon.Home.Length != 0)
            {
                CompanySpec home = Current.GetCompany(platoon.Home);
                if (home != null && !home.Platoons.Contains(platoon))
                {
                    int homeIndex = Current.Companies.IndexOf(home);
                    nameText += "  (" + CompanyLetter(homeIndex) + " 중대에서 배속)";
                }
            }
            TextMeshProUGUI nameLabel = UiKit.CreateLabel(row.transform, nameText, 14, UiTheme.Text);
            nameLabel.name = "PlatoonName";
            nameLabel.enableWordWrapping = false;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement nameLayout = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLayout.minWidth = 190f;
            nameLayout.flexibleWidth = 1f;
            string currentTemplate = platoon.Template;
            string currentLoadout = platoon.Loadout;
            int rowCost = ForceRules.PlatoonCost(Controller.Catalog, platoon);
            AttachHover(nameLabel, UnitInformation.Tooltip(Controller.Catalog, currentTemplate, currentLoadout),
                () => ShowPlatoon(currentTemplate, currentLoadout, rowCost));

            if (template != null && template.Loadouts.Count != 0)
            {
                List<UiOption<string>> loadouts = new List<UiOption<string>>();
                List<string> loadoutIds = new List<string>();
                foreach (UnitTemplate.Loadout loadout in template.Loadouts)
                {
                    loadouts.Add(new UiOption<string>(loadout.DisplayName, loadout.Id));
                    loadoutIds.Add(loadout.Id);
                }
                string selectedLoadout = platoon.Loadout.Length != 0 ? platoon.Loadout : template.Loadouts[0].Id;
                TMP_Dropdown loadoutDropdown = UiKit.CreateOption(row.transform, loadouts, selectedLoadout,
                    loadout => Controller.SetLoadout(platoon, loadout), 150f);
                loadoutDropdown.name = "Loadout_" + platoon.Template;
                _loadoutDropdowns.Add(loadoutDropdown);
                HookPlatoonDropdown(loadoutDropdown, platoon.Template, loadoutIds);
            }
            else
            {
                RectTransform spacer = UiKit.CreateRect(row.transform, "NoLoadoutSpacer");
                LayoutElement spacerLayout = spacer.gameObject.AddComponent<LayoutElement>();
                spacerLayout.minWidth = 150f;
                spacerLayout.preferredWidth = 150f;
            }

            List<UiOption<int>> waves = new List<UiOption<int>>();
            for (int wave = 0; wave < ForceSpec.WaveNames.Length; ++wave)
                waves.Add(new UiOption<int>(ForceSpec.WaveNames[wave], wave));
            TMP_Dropdown waveDropdown = UiKit.CreateOption(row.transform, waves, platoon.Wave,
                wave => Controller.SetWave(platoon, wave), 170f);
            waveDropdown.name = "Wave";
            _waveDropdowns.Add(waveDropdown);
            CreateFixedLabel(row.transform, rowCost + "점", 14, 58f, UiTheme.TextDim);
            if (removable)
                UiKit.CreateButton(row.transform, "빼기", () => Controller.RemoveAttachment(platoon), 58f);
            return row;
        }

        private void BuildOrganicRow(string templateId)
        {
            PlatoonSpec platoon = Current.Organic.Find(item => item.Template == templateId);
            CreatePlatoonRow(_list, platoon, string.Empty, false, templateId);
        }

        private void BuildAttachmentAdders()
        {
            AttachmentLimitLabel = UiKit.CreateLabel(_list,
                "추가 (" + Current.Attachments.Count + "/" + Controller.AttachmentLimit() + "):",
                14, UiTheme.TextDim);
            List<string> allowed = Controller.AllowedAttachments();
            for (int i = 0; i < allowed.Count; i += 2)
            {
                HorizontalLayoutGroup row = CreateRow(_list, "AttachmentAdders", 6);
                for (int column = 0; column < 2 && i + column < allowed.Count; ++column)
                {
                    string templateId = allowed[i + column];
                    UnitTemplate template = Controller.Catalog.GetUnitTemplate(templateId);
                    Button button = UiKit.CreateButton(row.transform,
                        "+ " + (template != null ? template.DisplayName : templateId), null);
                    button.name = "Add_" + templateId;
                    button.onClick.AddListener(() => Controller.AddAttachment(templateId));
                    button.GetComponent<LayoutElement>().flexibleWidth = 1f;
                    button.interactable = Controller.CanAddAttachment(templateId);
                    AttachHover(button, UnitInformation.Tooltip(Controller.Catalog, templateId),
                        () => ShowPlatoon(templateId));
                    _attachmentButtons.Add(button);
                }
            }
        }

        private void BuildSupportRows(TOETemplate toe)
        {
            BuildSupportRow("지도 밖 포병 사격 임무", toe.ArtilleryMissionCost, toe.MaxArtillery,
                Current.ArtilleryMissions, true);
            BuildSupportRow("근접항공지원 출격", toe.CasSortieCost, toe.MaxCas,
                Current.CasSorties, false);
        }

        private void BuildSupportRow(string text, int unitCost, int maximum, int value, bool artillery)
        {
            HorizontalLayoutGroup row = CreateRow(parent: _list, name: artillery ? "ArtilleryRow" : "CasRow");
            TextMeshProUGUI label = UiKit.CreateLabel(row.transform,
                text + "  (" + unitCost + "점, 최대 " + maximum + ")", 14, UiTheme.Text);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Button minus = UiKit.CreateButton(row.transform, "−", () =>
            {
                if (artillery)
                    Controller.SetArtilleryMissions(Current.ArtilleryMissions - 1);
                else
                    Controller.SetCasSorties(Current.CasSorties - 1);
            }, 36f);
            TextMeshProUGUI count = CreateFixedLabel(row.transform, value.ToString(), 14, 38f);
            count.alignment = TextAlignmentOptions.Center;
            Button plus = UiKit.CreateButton(row.transform, "+", () =>
            {
                if (artillery)
                    Controller.SetArtilleryMissions(Current.ArtilleryMissions + 1);
                else
                    Controller.SetCasSorties(Current.CasSorties + 1);
            }, 36f);
            minus.interactable = value > 0;
            plus.interactable = value < maximum;
            if (artillery)
            {
                ArtilleryMinusButton = minus;
                ArtilleryPlusButton = plus;
                ArtilleryValue = count;
            }
            else
            {
                CasMinusButton = minus;
                CasPlusButton = plus;
                CasValue = count;
            }
        }

        private void BuildEntryOptions()
        {
            ScenarioDef.SideSetup side = Controller.SideForCurrentFaction();
            List<UiOption<int>> entries = new List<UiOption<int>>();
            if (side != null)
            {
                for (int i = 0; i < side.EntryPoints.Count; ++i)
                {
                    SimVec2 point = side.EntryPoints[i];
                    string label = "진입 " + (i + 1) + "  (" +
                                   (point.X / 1000.0f).ToString("0.0", CultureInfo.InvariantCulture) + " km, " +
                                   (point.Y / 1000.0f).ToString("0.0", CultureInfo.InvariantCulture) + " km)";
                    entries.Add(new UiOption<int>(label, i));
                }
            }
            if (entries.Count == 0)
                entries.Add(new UiOption<int>("진입 지점 없음", 0));
            int selected = side != null ? Math.Max(0, ForceRules.EntryPointIndex(side, Current)) : 0;
            EntryDropdown = UiKit.CreateOption(_list, entries, selected, index => Controller.SetEntryPoint(index), 260f);
            EntryDropdown.name = "ReinforcementEntry";
            EntryDropdown.interactable = side != null && side.EntryPoints.Count != 0;
        }

        private void RefreshSummary()
        {
            if (SummaryText == null)
                return;
            ForceRules.Report report = Controller.Report;
            int budget = Controller.Budget;
            BudgetLabel.text = "포인트  " + report.Total + " / " + budget;
            BudgetLabel.color = report.Total > budget ? UiTheme.Error : UiTheme.Text;
            float fill = budget > 0 ? Mathf.Clamp01(report.Total / (float)budget) : 0f;
            _budgetFill.rectTransform.anchorMax = new Vector2(fill, 1f);
            _budgetFill.color = report.Total > budget ? UiTheme.Error : UiTheme.Accent;

            StringBuilder text = new StringBuilder();
            text.Append("엘리먼트 ").Append(report.ElementCount).Append("개  ·  예상 지속 교전 약 <b>")
                .Append(Mathf.RoundToInt((float)report.SustainMin)).AppendLine("분</b>");
            if (report.Errors.Count == 0 && report.Warnings.Count == 0)
                text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(UiTheme.Ok))
                    .AppendLine(">편성 가능</color>");
            foreach (string error in report.Errors)
                text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(UiTheme.Error))
                    .Append(">✖ ").Append(error).AppendLine("</color>");
            foreach (string warning in report.Warnings)
                text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(UiTheme.Warning))
                    .Append(">▲ ").Append(warning).AppendLine("</color>");
            text.AppendLine().AppendLine("<b>포인트 내역</b>");
            foreach (ForceRules.CostLine line in report.Lines)
                text.Append(line.Label).Append("  <color=#").Append(ColorUtility.ToHtmlStringRGB(UiTheme.TextDim))
                    .Append(">").Append(line.Cost).AppendLine("</color>");
            text.AppendLine().Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(UiTheme.TextDim))
                .AppendLine(">증원: 1차(T+10분) 10% 할인, 2차(T+20분) 20% 할인.")
                .AppendLine("교차 배속: 소대 줄의 \"이동…\"으로 다른 중대에 붙인다.")
                .Append("중대당 소대 2~4개를 벗어나면 비편제(명령 지연 +20%).</color>");
            SummaryText.text = text.ToString();
            StartButton.interactable = report.IsValid();
        }

        private void HookCompanyDropdown(TMP_Dropdown dropdown, IList<string> templateIds)
        {
            BuilderDropdownHover.Attach(dropdown, index =>
            {
                string templateId = templateIds[index];
                if (templateId.Length == 0)
                    return;
                ShowCompany(templateId);
                Tooltip.Show(UnitInformation.Tooltip(Controller.Catalog, templateId), PointerPosition());
            }, HideTooltip);
        }

        private void HookPlatoonDropdown(
            TMP_Dropdown dropdown,
            string templateId,
            IList<string> loadoutIds)
        {
            BuilderDropdownHover.Attach(dropdown, index =>
            {
                ShowPlatoon(templateId, loadoutIds[index]);
                Tooltip.Show(UnitInformation.Tooltip(Controller.Catalog, templateId, loadoutIds[index]),
                    PointerPosition());
            }, HideTooltip);
        }

        private void AttachHover(Component component, string tooltip, Action showCard)
        {
            Graphic graphic = component.GetComponent<Graphic>();
            if (graphic != null)
                graphic.raycastTarget = true;
            BuilderHoverTarget hover = component.gameObject.AddComponent<BuilderHoverTarget>();
            hover.Configure(() =>
            {
                showCard();
                Tooltip.Show(tooltip, PointerPosition());
            }, HideTooltip);
        }

        private void LoadSelected()
        {
            if (LoadDropdown == null || LoadDropdown.value < 0 || LoadDropdown.value >= _savedPaths.Count)
                return;
            LoadSaved(_savedPaths[LoadDropdown.value]);
        }

        private void OnControllerChanged()
        {
            Rebuild();
        }

        private void OnNotice(string message, bool succeeded)
        {
            if (ToastText == null)
                return;
            ToastText.text = message;
            ToastText.color = succeeded ? UiTheme.Ok : UiTheme.Error;
        }

        private void WireController()
        {
            if (_wired || Controller == null)
                return;
            Controller.Changed += OnControllerChanged;
            Controller.Notice += OnNotice;
            Controller.StartRequested += OnControllerStartRequested;
            Controller.BackRequested += OnControllerBackRequested;
            _wired = true;
        }

        private void UnwireController()
        {
            if (!_wired || Controller == null)
                return;
            Controller.Changed -= OnControllerChanged;
            Controller.Notice -= OnNotice;
            Controller.StartRequested -= OnControllerStartRequested;
            Controller.BackRequested -= OnControllerBackRequested;
            _wired = false;
        }

        private void OnControllerStartRequested(ForceSpec spec)
        {
            StartRequested?.Invoke(spec);
        }

        private void OnControllerBackRequested()
        {
            BackRequested?.Invoke();
        }

        private ScenarioDef CurrentScenario()
        {
            return Controller.Scenario;
        }

        private HorizontalLayoutGroup CreateRow(Transform parent, string name, int spacing = 8)
        {
            HorizontalLayoutGroup row = UiKit.CreateHorizontalLayout(parent, name, spacing);
            row.childForceExpandWidth = false;
            return row;
        }

        private TextMeshProUGUI CreateFixedLabel(
            Transform parent,
            string text,
            int size,
            float width,
            Color? color = null)
        {
            TextMeshProUGUI label = UiKit.CreateLabel(parent, text, size, color ?? UiTheme.Text);
            LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            return label;
        }

        private void SectionTitle(string text)
        {
            TextMeshProUGUI label = UiKit.CreateLabel(_list, text, 16, UiTheme.Accent);
            label.gameObject.AddComponent<LayoutElement>().minHeight = 28f;
        }

        private static string CompanyLetter(int index)
        {
            return index >= 0
                ? ForceRules.CompanyLetters[index % ForceRules.CompanyLetters.Length]
                : "?";
        }

        private static Vector2 PointerPosition()
        {
            return Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private static void DestroyObject(GameObject target)
        {
            target.SetActive(false);
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        private void Awake()
        {
            if (Controller != null)
                Build();
        }

        private void OnDestroy()
        {
            UnwireController();
        }
    }
}
