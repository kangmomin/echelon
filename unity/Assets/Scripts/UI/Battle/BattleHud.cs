using System;
using System.Collections.Generic;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Battle
{
    [DisallowMultipleComponent]
    public sealed class BattleHud : MonoBehaviour
    {
        private readonly Dictionary<BattleCommandType, Button> _orderButtons =
            new Dictionary<BattleCommandType, Button>();
        private readonly Dictionary<string, Button> _fireButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> _formationButtons = new Dictionary<string, Button>();
        private readonly List<BattleUnitView> _selection = new List<BattleUnitView>();
        private IReadOnlyList<BattleFormationOption> _platoonFormations =
            BattleFormationCatalog.DefaultsFor(BattleUnitLevel.Platoon);
        private IReadOnlyList<BattleFormationOption> _companyFormations =
            BattleFormationCatalog.DefaultsFor(BattleUnitLevel.Company);
        private RectTransform _formationRow;
        private RectTransform _specialRow;
        private BattleHudState _state = new BattleHudState();

        public event Action<BattleCommandType> OrderModeSelected;
        public event Action<BattleCommandIntent> ImmediateOrderRequested;
        public event Action<string> FormationSelected;
        public event Action<BattleFireAsset, bool> FireModeSelected;
        public event Action AltitudeToggleRequested;
        public event Action PauseToggleRequested;
        public event Action<int> SpeedChangeRequested;
        public event Action GuideRequested;

        public bool IsBuilt { get; private set; }
        public BattleCommandType OrderMode { get; private set; } = BattleCommandType.Move;
        public BattleFireAsset? FireAsset { get; private set; }
        public bool FireSmoke { get; private set; }
        public IReadOnlyList<BattleUnitView> Selection => _selection;
        public TextMeshProUGUI TopLabel { get; private set; }
        public TextMeshProUGUI SelectionLabel { get; private set; }
        public TextMeshProUGUI FireLabel { get; private set; }
        public RectTransform CommandPanel { get; private set; }
        public RectTransform FirePanel { get; private set; }
        public Button PauseButton { get; private set; }

        public static BattleHud Create(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("BattleHud", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var hud = root.AddComponent<BattleHud>();
            hud.Build();
            return hud;
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;
            UiKit.Stretch((RectTransform)transform);
            BuildTopBar();
            BuildSelectionPanel();
            BuildCommandPanel();
            BuildFirePanel();
            SetOrderMode(BattleCommandType.Move);
            SetSelection(null);
            SetFireSupport(new BattleFireSupportState());
            Render(_state);
            UiKit.EnsureEventSystem();
        }

        public void Render(BattleHudState state)
        {
            _state = state ?? new BattleHudState();
            if (!IsBuilt)
                return;
            TopLabel.text = BattleUiText.TopBar(_state);
            TextMeshProUGUI pauseLabel = PauseButton.GetComponentInChildren<TextMeshProUGUI>();
            pauseLabel.text = _state.Paused && !_state.Deploying ? "계속 [Space]" : "일시정지 [Space]";
            PauseButton.interactable = !_state.Deploying;
        }

        public void Bind(
            BattleHudState state,
            IReadOnlyList<BattleUnitView> selection,
            BattleFireSupportState fireSupport)
        {
            Render(state);
            SetSelection(selection);
            SetFireSupport(fireSupport);
        }

        public void SetSelection(IReadOnlyList<BattleUnitView> units)
        {
            _selection.Clear();
            if (units != null)
            {
                foreach (BattleUnitView unit in units)
                {
                    if (unit != null)
                        _selection.Add(unit);
                }
            }
            if (!IsBuilt)
                return;
            SelectionLabel.text = BattleUiText.Selection(_selection);
            CommandPanel.gameObject.SetActive(_selection.Count > 0);
            RebuildFormationButtons();
            UpdateSpecialRow();
        }

        public void SetFormationOptions(BattleUnitLevel level, IReadOnlyList<BattleFormationOption> options)
        {
            if (level == BattleUnitLevel.Company)
                _companyFormations = options ?? Array.Empty<BattleFormationOption>();
            else
                _platoonFormations = options ?? Array.Empty<BattleFormationOption>();
            if (IsBuilt)
                RebuildFormationButtons();
        }

        public void SetFireSupport(BattleFireSupportState state)
        {
            if (IsBuilt)
                FireLabel.text = BattleUiText.FireSupport(state);
        }

        public void SetOrderMode(BattleCommandType type)
        {
            OrderMode = type;
            if (!IsBuilt)
                return;
            foreach (KeyValuePair<BattleCommandType, Button> pair in _orderButtons)
                SetSelectedVisual(pair.Value, pair.Key == type);
        }

        public void RequestOrderMode(BattleCommandType type)
        {
            SetOrderMode(type);
            OrderModeSelected?.Invoke(type);
        }

        public void RequestImmediateOrder(BattleCommandIntent intent)
        {
            if (intent == null)
                return;
            ImmediateOrderRequested?.Invoke(new BattleCommandIntent
            {
                Type = intent.Type,
                FormationId = intent.FormationId,
                Technique = intent.Technique,
                RulesOfEngagement = intent.RulesOfEngagement,
                Altitude = intent.Altitude
            });
        }

        public void RequestFormation(string formationId)
        {
            if (!string.IsNullOrEmpty(formationId))
                FormationSelected?.Invoke(formationId);
        }

        public void SetFireMode(BattleFireAsset? asset, bool smoke)
        {
            FireAsset = asset;
            FireSmoke = asset.HasValue && smoke;
            if (!IsBuilt)
                return;
            foreach (KeyValuePair<string, Button> pair in _fireButtons)
                SetSelectedVisual(pair.Value, asset.HasValue && pair.Key == FireKey(asset.Value, FireSmoke));
        }

        public void RequestFireMode(BattleFireAsset asset, bool smoke)
        {
            SetFireMode(asset, smoke);
            FireModeSelected?.Invoke(asset, FireSmoke);
        }

        public void ClearFireMode()
        {
            SetFireMode(null, false);
        }

        public void CycleFireMode(bool smoke)
        {
            if (FireAsset == BattleFireAsset.Mortar && FireSmoke == smoke)
                SetFireMode(BattleFireAsset.Artillery, smoke);
            else if (FireAsset == BattleFireAsset.Artillery && FireSmoke == smoke)
                ClearFireMode();
            else
                SetFireMode(BattleFireAsset.Mortar, smoke);
        }

        public void RequestAltitudeToggle()
        {
            AltitudeToggleRequested?.Invoke();
        }

        public void RequestPauseToggle()
        {
            if (!_state.Deploying)
                PauseToggleRequested?.Invoke();
        }

        public void RequestSpeedChange(int direction)
        {
            if (direction != 0)
                SpeedChangeRequested?.Invoke(Math.Sign(direction));
        }

        public Button GetOrderButton(BattleCommandType type)
        {
            return _orderButtons.TryGetValue(type, out Button button) ? button : null;
        }

        public Button GetFormationButton(string formationId)
        {
            return _formationButtons.TryGetValue(formationId ?? string.Empty, out Button button) ? button : null;
        }

        public Button GetFireButton(BattleFireAsset asset, bool smoke)
        {
            return _fireButtons.TryGetValue(FireKey(asset, smoke), out Button button) ? button : null;
        }

        private void Awake()
        {
            Build();
        }

        private void BuildTopBar()
        {
            RectTransform panel = CreatePanel(
                "TopBar",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -6f),
                new Vector2(1120f, 82f));
            UiKit.AddVerticalLayout(panel, 3, 7);
            TopLabel = UiKit.CreateLabel(panel, string.Empty, 15, UiTheme.Text);
            TopLabel.name = "Status";
            TopLabel.alignment = TextAlignmentOptions.Center;
            TopLabel.textWrappingMode = TextWrappingModes.NoWrap;
            LayoutElement statusLayout = TopLabel.gameObject.AddComponent<LayoutElement>();
            statusLayout.minHeight = 25f;
            statusLayout.preferredHeight = 25f;
            statusLayout.flexibleWidth = 1f;

            HorizontalLayoutGroup controls = UiKit.CreateHorizontalLayout(panel, "TimeControls", 5);
            controls.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement controlsLayout = controls.gameObject.AddComponent<LayoutElement>();
            controlsLayout.minHeight = 32f;
            controlsLayout.preferredHeight = 32f;
            Button slower = UiKit.CreateButton(controls.transform, "느리게 [-]", () => RequestSpeedChange(-1), 120f);
            slower.name = "SpeedDown";
            PauseButton = UiKit.CreateButton(controls.transform, "일시정지 [Space]", RequestPauseToggle, 150f);
            PauseButton.name = "Pause";
            Button faster = UiKit.CreateButton(controls.transform, "빠르게 [+]", () => RequestSpeedChange(1), 120f);
            faster.name = "SpeedUp";
        }

        private void BuildSelectionPanel()
        {
            RectTransform panel = CreatePanel(
                "SelectionPanel",
                Vector2.zero,
                Vector2.zero,
                new Vector2(8f, 8f),
                new Vector2(360f, 242f));
            UiKit.AddVerticalLayout(panel, 0, 8);
            SelectionLabel = UiKit.CreateLabel(panel, string.Empty, 13, UiTheme.Text);
            SelectionLabel.name = "SelectionStatus";
            SelectionLabel.alignment = TextAlignmentOptions.TopLeft;
            SelectionLabel.lineSpacing = 2f;
            LayoutElement layout = SelectionLabel.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;
        }

        private void BuildCommandPanel()
        {
            CommandPanel = CreatePanel(
                "CommandPanel",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(10f, 8f),
                new Vector2(820f, 218f));
            UiKit.AddVerticalLayout(CommandPanel, 4, 6);

            HorizontalLayoutGroup orders = CreateRow(CommandPanel, "Orders", 36f);
            AddOrderButton(orders.transform, BattleCommandType.Move, "이동 [M]");
            AddOrderButton(orders.transform, BattleCommandType.FastMove, "급속 이동 [F]");
            AddOrderButton(orders.transform, BattleCommandType.AttackMove, "공격 이동 [Q]");
            AddOrderButton(orders.transform, BattleCommandType.Seize, "확보 [Y]");
            AddOrderButton(orders.transform, BattleCommandType.Defend, "방어 [J]");
            AddOrderButton(orders.transform, BattleCommandType.Withdraw, "철수 [B]");
            Button halt = CreateFlexibleButton(orders.transform, "정지·취소 [E]", () =>
                RequestImmediateOrder(new BattleCommandIntent(BattleCommandType.Halt)));
            halt.name = "Command_Halt";

            _formationRow = CreateRow(CommandPanel, "Formations", 36f).GetComponent<RectTransform>();
            _specialRow = CreateRow(CommandPanel, "Special", 36f).GetComponent<RectTransform>();
            AddOrderButton(_specialRow, BattleCommandType.LayMines, "지뢰 매설 [N]");
            AddOrderButton(_specialRow, BattleCommandType.ClearMines, "지뢰 제거 [I]");
            AddOrderButton(_specialRow, BattleCommandType.Fortify, "진지 구축 [O]");
            Button altitude = CreateFlexibleButton(_specialRow, "고도 전환 [V]", RequestAltitudeToggle);
            altitude.name = "Command_Altitude";

            RectTransform misc = UiKit.CreateRect(CommandPanel, "ImmediateCommands");
            LayoutElement miscLayout = misc.gameObject.AddComponent<LayoutElement>();
            miscLayout.minHeight = 68f;
            miscLayout.preferredHeight = 68f;
            var grid = misc.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(157f, 32f);
            grid.spacing = new Vector2(3f, 3f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.MiddleCenter;
            AddMiscButton(misc, "Command_Traveling", "일반 이동", BattleCommandIntent.Sop(technique: BattleSop.Traveling));
            AddMiscButton(misc, "Command_Overwatch", "경계 이동", BattleCommandIntent.Sop(technique: BattleSop.Overwatch));
            AddMiscButton(misc, "Command_Bounding", "교대 약진", BattleCommandIntent.Sop(technique: BattleSop.Bounding));
            AddMiscButton(misc, "Command_HoldFire", "사격 중지 [H]", BattleCommandIntent.Sop(rulesOfEngagement: BattleSop.HoldFire));
            AddMiscButton(misc, "Command_FireAtWill", "사격 자유", BattleCommandIntent.Sop(rulesOfEngagement: BattleSop.FireAtWill));
            AddMiscButton(misc, "Command_Resupply", "보급 [K]", new BattleCommandIntent(BattleCommandType.Resupply));
            AddMiscButton(misc, "Command_Mount", "탑승", new BattleCommandIntent(BattleCommandType.Mount));
            AddMiscButton(misc, "Command_Dismount", "하차 [U]", new BattleCommandIntent(BattleCommandType.Dismount));
            AddMiscButton(misc, "Command_Detach", "분리", new BattleCommandIntent(BattleCommandType.Detach));
            AddMiscButton(misc, "Command_Rejoin", "복귀", new BattleCommandIntent(BattleCommandType.Rejoin));
        }

        private void BuildFirePanel()
        {
            FirePanel = CreatePanel(
                "FireSupportPanel",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-8f, 8f),
                new Vector2(310f, 230f));
            UiKit.AddVerticalLayout(FirePanel, 4, 7);
            TextMeshProUGUI title = UiKit.CreateLabel(
                FirePanel,
                "화력 지원  (좌클릭: 표적, 우클릭: 취소)",
                13,
                UiTheme.Text);
            title.name = "Title";
            title.textWrappingMode = TextWrappingModes.Normal;
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minHeight = 34f;
            titleLayout.preferredHeight = 34f;

            RectTransform gridObject = UiKit.CreateRect(FirePanel, "FireButtons");
            LayoutElement gridLayout = gridObject.gameObject.AddComponent<LayoutElement>();
            gridLayout.minHeight = 104f;
            gridLayout.preferredHeight = 104f;
            var grid = gridObject.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(143f, 32f);
            grid.spacing = new Vector2(4f, 3f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            AddFireButton(gridObject, BattleFireAsset.Mortar, false, "박격포 [T]");
            AddFireButton(gridObject, BattleFireAsset.Mortar, true, "박격포 연막 [G]");
            AddFireButton(gridObject, BattleFireAsset.Artillery, false, "포병 [T T]");
            AddFireButton(gridObject, BattleFireAsset.Artillery, true, "포병 연막 [G G]");
            AddFireButton(gridObject, BattleFireAsset.Cas, false, "근접항공지원 [P]");

            FireLabel = UiKit.CreateLabel(FirePanel, string.Empty, 12, UiTheme.Text);
            FireLabel.name = "FireMissions";
            FireLabel.alignment = TextAlignmentOptions.TopLeft;
            LayoutElement fireLayout = FireLabel.gameObject.AddComponent<LayoutElement>();
            fireLayout.flexibleWidth = 1f;
            fireLayout.flexibleHeight = 1f;
        }

        private void AddOrderButton(Transform parent, BattleCommandType type, string text)
        {
            Button button = CreateFlexibleButton(parent, text, () => RequestOrderMode(type));
            button.name = "Order_" + type;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 12f;
            _orderButtons[type] = button;
        }

        private void AddMiscButton(Transform parent, string name, string text, BattleCommandIntent intent)
        {
            Button button = UiKit.CreateButton(parent, text, () => RequestImmediateOrder(intent));
            button.name = name;
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = 0f;
            layout.minHeight = 32f;
            layout.preferredHeight = 32f;
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 12f;
        }

        private void AddFireButton(Transform parent, BattleFireAsset asset, bool smoke, string text)
        {
            Button button = UiKit.CreateButton(parent, text, () => RequestFireMode(asset, smoke));
            button.name = "Fire_" + asset + (smoke ? "_Smoke" : string.Empty);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = 0f;
            layout.minHeight = 32f;
            layout.preferredHeight = 32f;
            button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 12f;
            _fireButtons[FireKey(asset, smoke)] = button;
        }

        private void RebuildFormationButtons()
        {
            if (_formationRow == null)
                return;
            for (int i = _formationRow.childCount - 1; i >= 0; i--)
                DestroyChild(_formationRow.GetChild(i).gameObject);
            _formationButtons.Clear();
            if (_selection.Count == 0)
                return;

            TextMeshProUGUI title = UiKit.CreateLabel(_formationRow, "대형:", 13, UiTheme.TextDim);
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minWidth = 42f;
            titleLayout.preferredWidth = 42f;
            BattleUnitView selected = _selection[0];
            IReadOnlyList<BattleFormationOption> options = selected.Level == BattleUnitLevel.Company
                ? _companyFormations
                : _platoonFormations;
            for (int i = 0; i < options.Count; i++)
            {
                BattleFormationOption option = options[i];
                if (option == null || string.IsNullOrEmpty(option.Id))
                    continue;
                string formationId = option.Id;
                Button button = CreateFlexibleButton(
                    _formationRow,
                    string.Format("{0} [Alt+{1}]", option.DisplayName, i + 1),
                    () => RequestFormation(formationId));
                button.name = "Formation_" + formationId;
                button.GetComponentInChildren<TextMeshProUGUI>().fontSize = 11f;
                SetSelectedVisual(button, selected.FormationId == formationId);
                _formationButtons[formationId] = button;
            }
        }

        private void UpdateSpecialRow()
        {
            bool engineer = false;
            bool aviation = false;
            foreach (BattleUnitView unit in _selection)
            {
                engineer |= unit.Kind == "engineer";
                aviation |= unit.Kind == "aviation";
            }
            foreach (BattleCommandType type in new[]
                     {
                         BattleCommandType.LayMines,
                         BattleCommandType.ClearMines,
                         BattleCommandType.Fortify
                     })
            {
                _orderButtons[type].gameObject.SetActive(engineer);
            }
            Transform altitude = _specialRow.Find("Command_Altitude");
            if (altitude != null)
                altitude.gameObject.SetActive(aviation);
            _specialRow.gameObject.SetActive(engineer || aviation);
        }

        private RectTransform CreatePanel(
            string name,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            RectTransform panel = UiKit.CreatePanel(transform, name, UiTheme.Panel);
            panel.anchorMin = anchor;
            panel.anchorMax = anchor;
            panel.pivot = pivot;
            panel.anchoredPosition = position;
            panel.sizeDelta = size;
            return panel;
        }

        private static HorizontalLayoutGroup CreateRow(Transform parent, string name, float height)
        {
            HorizontalLayoutGroup row = UiKit.CreateHorizontalLayout(parent, name, 3);
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleWidth = 1f;
            return row;
        }

        private static Button CreateFlexibleButton(Transform parent, string text, Action action)
        {
            Button button = UiKit.CreateButton(parent, text, () => action());
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = 0f;
            layout.flexibleWidth = 1f;
            layout.minHeight = 32f;
            layout.preferredHeight = 32f;
            return button;
        }

        private static void SetSelectedVisual(Button button, bool selected)
        {
            UiKit.SetSelected(button, selected);
        }

        private static string FireKey(BattleFireAsset asset, bool smoke)
        {
            return (int)asset + "_" + smoke;
        }

        private static void DestroyChild(GameObject child)
        {
            child.SetActive(false);
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}
