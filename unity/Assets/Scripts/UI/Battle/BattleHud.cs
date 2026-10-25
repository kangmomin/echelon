using System;
using System.Collections.Generic;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Battle
{
    /// <summary>
    /// 전투 HUD: 위쪽 상태 바(시간·배속·승점·전력·지휘 용량), 아래쪽 선택 부대 카드·명령 바·화력 지원.
    /// 명령은 이벤트로만 내보내고, 실제 지휘 상태는 BattleCommands 가 갖는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleHud : MonoBehaviour
    {
        private const float RowHeight = 28f;
        private const float RowLabelWidth = 52f;
        private const int TagSlots = 6;

        private readonly Dictionary<BattleCommandType, Button> _orderButtons = new Dictionary<BattleCommandType, Button>();
        private readonly Dictionary<string, Button> _fireButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> _formationButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> _sopButtons = new Dictionary<string, Button>();
        private readonly List<BattleUnitView> _selection = new List<BattleUnitView>();
        private readonly List<TextMeshProUGUI> _tags = new List<TextMeshProUGUI>();
        private IReadOnlyList<BattleFormationOption> _platoonFormations = BattleFormationCatalog.DefaultsFor(BattleUnitLevel.Platoon);
        private IReadOnlyList<BattleFormationOption> _companyFormations = BattleFormationCatalog.DefaultsFor(BattleUnitLevel.Company);
        private IReadOnlyList<BattleFormationOption> _builtFormations;
        private string _highlight = string.Empty;
        private BattleHudState _state = new BattleHudState();

        private TextMeshProUGUI _clock;
        private TextMeshProUGUI _speed;
        private TextMeshProUGUI _phase;
        private TextMeshProUGUI _playerScore;
        private TextMeshProUGUI _enemyScore;
        private TextMeshProUGUI _remaining;
        private TextMeshProUGUI _playerForce;
        private TextMeshProUGUI _enemyForce;
        private TextMeshProUGUI _capacity;
        private GameObject _scoreGroup;
        private GameObject _capacityGroup;

        private TextMeshProUGUI _unitTitle;
        private TextMeshProUGUI _unitLevel;
        private TextMeshProUGUI _unitSetup;
        private TextMeshProUGUI _unitActivity;
        private TextMeshProUGUI _unitHint;
        private TextMeshProUGUI _strengthValue;
        private TextMeshProUGUI _moraleValue;
        private TextMeshProUGUI _ammoValue;
        private UiBar _strengthBar;
        private UiBar _moraleBar;
        private UiBar _ammoBar;
        private RectTransform _unitDetails;
        private RectTransform _formationRow;
        private RectTransform _specialRow;
        private TextMeshProUGUI _fireHint;

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
        public RectTransform TopBar { get; private set; }
        public RectTransform UnitCard { get; private set; }
        public RectTransform CommandPanel { get; private set; }
        public RectTransform FirePanel { get; private set; }
        public TextMeshProUGUI FireLabel { get; private set; }
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
            BuildUnitCard();
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
            _clock.text = BattleUiText.Clock(Mathf.Max(0, (int)_state.SimulationSeconds));
            _speed.text = BattleUiText.Speed(_state.Speed);
            string phase = BattleUiText.Phase(_state);
            _phase.text = phase;
            _phase.transform.parent.gameObject.SetActive(phase.Length > 0);
            UiKit.ButtonLabel(PauseButton).text = _state.Paused && !_state.Deploying ? "▶" : "Ⅱ";
            PauseButton.interactable = !_state.Deploying;

            _scoreGroup.SetActive(_state.HasScoring);
            if (_state.HasScoring)
            {
                _playerScore.text = Mathf.RoundToInt(_state.PlayerVictoryPoints).ToString();
                _enemyScore.text = Mathf.RoundToInt(_state.EnemyVictoryPoints).ToString();
                _remaining.text = BattleUiText.Remaining(_state);
            }
            _playerForce.text = BattleUiText.Percent(_state.PlayerForceRatio);
            _enemyForce.text = BattleUiText.Percent(_state.EnemyForceRatio);
            string capacity = BattleUiText.Capacity(_state);
            _capacityGroup.SetActive(capacity.Length > 0);
            _capacity.text = capacity;
        }

        public void Bind(BattleHudState state, IReadOnlyList<BattleUnitView> selection, BattleFireSupportState fireSupport)
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
            RenderUnitCard();
            CommandPanel.gameObject.SetActive(_selection.Count > 0);
            if (_selection.Count == 0)
                return;
            BuildFormationButtons();
            UpdateSpecialRow();
            UpdateSetupHighlight();
        }

        public void SetFormationOptions(BattleUnitLevel level, IReadOnlyList<BattleFormationOption> options)
        {
            if (level == BattleUnitLevel.Company)
                _companyFormations = options ?? Array.Empty<BattleFormationOption>();
            else
                _platoonFormations = options ?? Array.Empty<BattleFormationOption>();
            _builtFormations = null;
            if (IsBuilt && _selection.Count > 0)
                BuildFormationButtons();
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
                UiKit.SetSelected(pair.Value, pair.Key == type);
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
                UiKit.SetSelected(pair.Value, asset.HasValue && pair.Key == FireKey(asset.Value, FireSmoke));
            _fireHint.gameObject.SetActive(asset.HasValue);
        }

        public void RequestFireMode(BattleFireAsset asset, bool smoke)
        {
            SetFireMode(asset, smoke);
            FireModeSelected?.Invoke(asset, FireSmoke);
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

        public void RequestGuide()
        {
            GuideRequested?.Invoke();
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

        // ── 상단 바 ─────────────────────────────────────────────

        private void BuildTopBar()
        {
            TopBar = UiKit.CreateRect(transform, "TopBar");
            TopBar.anchorMin = new Vector2(0f, 1f);
            TopBar.anchorMax = new Vector2(1f, 1f);
            TopBar.pivot = new Vector2(0.5f, 1f);
            TopBar.anchoredPosition = Vector2.zero;
            TopBar.sizeDelta = new Vector2(0f, BattleHudLayout.TopBarHeight);
            Image background = TopBar.gameObject.AddComponent<Image>();
            background.color = UiTheme.PanelRaised;
            RectTransform edge = UiKit.CreateRect(TopBar, "Edge");
            edge.anchorMin = Vector2.zero;
            edge.anchorMax = new Vector2(1f, 0f);
            edge.pivot = new Vector2(0.5f, 0f);
            edge.sizeDelta = new Vector2(0f, 1f);
            edge.gameObject.AddComponent<Image>().color = UiTheme.Border;

            HorizontalLayoutGroup row = UiKit.AddHorizontalLayout(TopBar, 6);
            row.padding = new RectOffset(UiTheme.SpaceS, UiTheme.SpaceS, 6, 6);
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;

            PauseButton = TimeButton("Pause", "Ⅱ", RequestPauseToggle, 36f);
            TimeButton("SpeedDown", "◀", () => RequestSpeedChange(-1), 28f);
            _speed = Value(TopBar, "1×", UiTheme.Text, false, 34f);
            _speed.alignment = TextAlignmentOptions.Center;
            TimeButton("SpeedUp", "▶", () => RequestSpeedChange(1), 28f);
            Gap(TopBar, 6f);
            _clock = Value(TopBar, "00:00", UiTheme.Text, true, 56f);
            _clock.fontSize = UiTheme.FontHeading;
            _phase = UiKit.CreateBadge(TopBar, string.Empty, UiTheme.Accent);
            Flexible(TopBar);

            _scoreGroup = Group(TopBar, "Score").gameObject;
            Caption(_scoreGroup.transform, "승점");
            _playerScore = Value(_scoreGroup.transform, "0", UiTheme.Friendly, true);
            Caption(_scoreGroup.transform, ":");
            _enemyScore = Value(_scoreGroup.transform, "0", UiTheme.Hostile, true);
            Gap(_scoreGroup.transform, 14f);
            Caption(_scoreGroup.transform, "남은 시간");
            _remaining = Value(_scoreGroup.transform, "00:00", UiTheme.Text, true);
            Flexible(TopBar);

            RectTransform force = Group(TopBar, "Force");
            Caption(force, "전력");
            _playerForce = Value(force, "100%", UiTheme.Friendly, true);
            Caption(force, ":");
            _enemyForce = Value(force, "100%", UiTheme.Hostile, true);
            Gap(TopBar, 10f);
            _capacityGroup = Group(TopBar, "Capacity").gameObject;
            Caption(_capacityGroup.transform, "지휘 용량");
            _capacity = Value(_capacityGroup.transform, string.Empty, UiTheme.Text, true);
            Gap(TopBar, 6f);
            Button guide = UiKit.CreateCommandButton(TopBar, "가이드", "/", RequestGuide, UiButtonStyle.Ghost);
            guide.name = "GuideButton";
            UiKit.SetSize(guide, 92f, 28f);
        }

        private Button TimeButton(string name, string text, Action action, float width)
        {
            Button button = UiKit.CreateButton(TopBar, text, () => action(), width);
            button.name = name;
            UiKit.SetSize(button, width, 28f);
            TextMeshProUGUI label = UiKit.ButtonLabel(button);
            label.fontSize = UiTheme.FontCaption;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static RectTransform Group(Transform parent, string name)
        {
            HorizontalLayoutGroup group = UiKit.CreateHorizontalLayout(parent, name, 5);
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            return (RectTransform)group.transform;
        }

        private static TextMeshProUGUI Caption(Transform parent, string text)
        {
            TextMeshProUGUI label = UiKit.CreateLabel(parent, text, UiTheme.FontSmall, UiTheme.TextDim);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static TextMeshProUGUI Value(Transform parent, string text, Color color, bool bold, float width = -1f)
        {
            TextMeshProUGUI label = UiKit.CreateLabel(parent, text, UiTheme.FontBody, color);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (bold)
                label.fontStyle = FontStyles.Bold;
            if (width > 0f)
                UiKit.SetSize(label, width);
            return label;
        }

        private static void Gap(Transform parent, float width)
        {
            UiKit.SetSize(UiKit.CreateRect(parent, "Gap"), width);
        }

        private static void Flexible(Transform parent)
        {
            UiKit.SetSize(UiKit.CreateRect(parent, "Flexible"), 0f, -1f, 1f);
        }

        // ── 선택 부대 카드 ───────────────────────────────────────

        private void BuildUnitCard()
        {
            UnitCard = UiKit.CreateCard(transform, "UnitCard");
            UiKit.Anchor(UnitCard, Vector2.zero, new Vector2(BattleHudLayout.Margin, BattleHudLayout.Margin),
                new Vector2(BattleHudLayout.UnitCardWidth, BattleHudLayout.BottomHeight));
            VerticalLayoutGroup layout = UiKit.AddVerticalLayout(UnitCard, 4, UiTheme.SpaceM);

            HorizontalLayoutGroup header = UiKit.CreateHorizontalLayout(UnitCard, "Header", 8);
            header.childForceExpandWidth = false;
            UiKit.SetSize(header, -1f, 24f);
            _unitTitle = UiKit.CreateLabel(header.transform, string.Empty, UiTheme.FontHeading, UiTheme.Text);
            _unitTitle.name = "Title";
            _unitTitle.alignment = TextAlignmentOptions.MidlineLeft;
            _unitTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _unitTitle.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.SetBold(_unitTitle, true);
            UiKit.SetSize(_unitTitle, 0f, -1f, 1f);
            _unitLevel = UiKit.CreateBadge(header.transform, string.Empty, UiTheme.Friendly);

            _unitHint = UiKit.CreateLabel(UnitCard, BattleUiText.NoSelectionHint, UiTheme.FontSmall, UiTheme.TextDim);
            _unitHint.name = "Hint";
            _unitHint.lineSpacing = 8f;

            _unitDetails = UiKit.CreateVerticalLayout(UnitCard, "Details", 4).GetComponent<RectTransform>();
            _unitSetup = DetailLine("Setup");
            _unitActivity = DetailLine("Activity");
            _strengthBar = StatRow("전투력", out _strengthValue);
            _moraleBar = StatRow("사기", out _moraleValue);
            _ammoBar = StatRow("탄약", out _ammoValue);
            HorizontalLayoutGroup tags = UiKit.CreateHorizontalLayout(_unitDetails, "Tags", 4);
            tags.childForceExpandWidth = false;
            UiKit.SetSize(tags, -1f, 18f);
            for (int i = 0; i < TagSlots; i++)
            {
                TextMeshProUGUI tag = UiKit.CreateBadge(tags.transform, string.Empty, UiTheme.TextDim);
                tag.transform.parent.gameObject.SetActive(false);
                _tags.Add(tag);
            }
        }

        private TextMeshProUGUI DetailLine(string name)
        {
            TextMeshProUGUI label = UiKit.CreateLabel(_unitDetails, string.Empty, UiTheme.FontSmall, UiTheme.TextDim);
            label.name = name;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private UiBar StatRow(string name, out TextMeshProUGUI value)
        {
            HorizontalLayoutGroup row = UiKit.CreateHorizontalLayout(_unitDetails, name, 8);
            row.childForceExpandWidth = false;
            row.childAlignment = TextAnchor.MiddleLeft;
            UiKit.SetSize(row, -1f, 15f);
            TextMeshProUGUI caption = UiKit.CreateLabel(row.transform, name, UiTheme.FontCaption, UiTheme.TextDim);
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            UiKit.SetSize(caption, 40f);
            RectTransform holder = UiKit.CreateRect(row.transform, "BarHolder");
            UiKit.SetSize(holder, 0f, 15f, 1f);
            UiBar bar = UiKit.CreateBar(holder, "Bar", 5f);
            bar.Root.anchorMin = new Vector2(0f, 0.5f);
            bar.Root.anchorMax = new Vector2(1f, 0.5f);
            bar.Root.sizeDelta = new Vector2(0f, 5f);
            value = UiKit.CreateLabel(row.transform, string.Empty, UiTheme.FontCaption, UiTheme.Text);
            value.alignment = TextAlignmentOptions.MidlineRight;
            UiKit.SetSize(value, 36f);
            return bar;
        }

        private void RenderUnitCard()
        {
            bool any = _selection.Count > 0;
            // 선택이 없으면 안내 두 줄만 보이게 카드를 접는다.
            UnitCard.sizeDelta = new Vector2(BattleHudLayout.UnitCardWidth, any ? BattleHudLayout.BottomHeight : 96f);
            _unitTitle.text = BattleUiText.SelectionTitle(_selection);
            _unitTitle.color = any ? UiTheme.Text : UiTheme.TextDim;
            _unitLevel.transform.parent.gameObject.SetActive(any);
            _unitHint.gameObject.SetActive(!any);
            _unitDetails.gameObject.SetActive(any);
            if (!any)
                return;
            BattleUnitView unit = _selection[0];
            _unitLevel.text = BattleUiText.LevelName(unit.Level);
            _unitSetup.text = BattleUiText.UnitSetup(unit);
            _unitActivity.text = BattleUiText.UnitActivity(unit);
            _strengthBar.SetValue(unit.Strength, UiTheme.Ratio(unit.Strength));
            _strengthValue.text = BattleUiText.Percent(unit.Strength);
            float morale = Mathf.Clamp01(unit.Morale / 100f);
            _moraleBar.SetValue(morale, UiTheme.Ratio(morale));
            _moraleValue.text = Mathf.RoundToInt(unit.Morale).ToString();
            _ammoBar.SetValue(unit.AmmoRatio, UiTheme.Ratio(unit.AmmoRatio));
            _ammoValue.text = BattleUiText.Percent(unit.AmmoRatio);
            List<(string Text, Color Color)> tags = BattleUiText.UnitTags(unit);
            for (int i = 0; i < _tags.Count; i++)
            {
                bool shown = i < tags.Count;
                _tags[i].transform.parent.gameObject.SetActive(shown);
                if (!shown)
                    continue;
                _tags[i].text = tags[i].Text;
                _tags[i].color = tags[i].Color;
                _tags[i].transform.parent.GetComponent<Image>().color = UiTheme.WithAlpha(tags[i].Color, 0.16f);
            }
        }

        // ── 명령 바 ─────────────────────────────────────────────

        private void BuildCommandPanel()
        {
            CommandPanel = UiKit.CreateCard(transform, "CommandPanel");
            CommandPanel.anchorMin = Vector2.zero;
            CommandPanel.anchorMax = new Vector2(1f, 0f);
            CommandPanel.pivot = new Vector2(0.5f, 0f);
            float left = BattleHudLayout.Margin * 2f + BattleHudLayout.UnitCardWidth;
            float right = BattleHudLayout.Margin * 2f + BattleHudLayout.FirePanelWidth;
            CommandPanel.offsetMin = new Vector2(left, BattleHudLayout.Margin);
            CommandPanel.offsetMax = new Vector2(-right, BattleHudLayout.Margin + BattleHudLayout.BottomHeight);
            UiKit.AddVerticalLayout(CommandPanel, 5, UiTheme.SpaceS);

            RectTransform orders = CommandRow("Orders", "명령");
            AddOrderButton(orders, BattleCommandType.Move, "이동", "M");
            AddOrderButton(orders, BattleCommandType.FastMove, "급속 이동", "F");
            AddOrderButton(orders, BattleCommandType.AttackMove, "공격 이동", "Q");
            AddOrderButton(orders, BattleCommandType.Seize, "확보", "Y");
            AddOrderButton(orders, BattleCommandType.Defend, "방어", "J");
            AddOrderButton(orders, BattleCommandType.Withdraw, "철수", "B");
            CommandButton(orders, "Command_Halt", "정지", "E", new BattleCommandIntent(BattleCommandType.Halt), UiButtonStyle.Danger);

            _formationRow = CommandRow("Formations", "대형 Alt");

            RectTransform movement = CommandRow("Movement", "기동");
            SopButton(movement, "Command_Traveling", "일반", "", BattleCommandIntent.Sop(technique: BattleSop.Traveling));
            SopButton(movement, "Command_Overwatch", "경계 이동", "", BattleCommandIntent.Sop(technique: BattleSop.Overwatch));
            SopButton(movement, "Command_Bounding", "교대 약진", "L", BattleCommandIntent.Sop(technique: BattleSop.Bounding));
            SopButton(movement, "Command_FireAtWill", "사격 자유", "", BattleCommandIntent.Sop(rulesOfEngagement: BattleSop.FireAtWill));
            SopButton(movement, "Command_HoldFire", "사격 중지", "H", BattleCommandIntent.Sop(rulesOfEngagement: BattleSop.HoldFire));

            RectTransform support = CommandRow("Support", "지원");
            CommandButton(support, "Command_Resupply", "보급", "K", new BattleCommandIntent(BattleCommandType.Resupply));
            CommandButton(support, "Command_Mount", "탑승", "", new BattleCommandIntent(BattleCommandType.Mount));
            CommandButton(support, "Command_Dismount", "하차", "U", new BattleCommandIntent(BattleCommandType.Dismount));
            CommandButton(support, "Command_Detach", "분리", "", new BattleCommandIntent(BattleCommandType.Detach));
            CommandButton(support, "Command_Rejoin", "복귀", "", new BattleCommandIntent(BattleCommandType.Rejoin));

            _specialRow = CommandRow("Special", "특수");
            AddOrderButton(_specialRow, BattleCommandType.LayMines, "지뢰 매설", "N");
            AddOrderButton(_specialRow, BattleCommandType.ClearMines, "지뢰 제거", "I");
            AddOrderButton(_specialRow, BattleCommandType.Fortify, "진지 구축", "O");
            Button altitude = UiKit.CreateCommandButton(_specialRow, "고도 전환", "V", RequestAltitudeToggle);
            altitude.name = "Command_Altitude";
            UiKit.SetSize(altitude, 0f, RowHeight, 1f);
            AddOrderButton(_specialRow, BattleCommandType.Airdrop, "보급 투하", "R");
            CommandButton(_specialRow, "Command_ReturnToBase", "귀환", "B", new BattleCommandIntent(BattleCommandType.Withdraw));
        }

        private RectTransform CommandRow(string name, string title)
        {
            HorizontalLayoutGroup row = UiKit.CreateHorizontalLayout(CommandPanel, name, 4);
            row.childForceExpandWidth = false;
            UiKit.SetSize(row, -1f, RowHeight);
            TextMeshProUGUI label = UiKit.CreateLabel(row.transform, title, UiTheme.FontCaption, UiTheme.TextMuted);
            label.name = "RowTitle";
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.SetSize(label, RowLabelWidth);
            return (RectTransform)row.transform;
        }

        private void AddOrderButton(RectTransform parent, BattleCommandType type, string text, string key)
        {
            Button button = UiKit.CreateCommandButton(parent, text, key, () => RequestOrderMode(type));
            button.name = "Order_" + type;
            UiKit.SetSize(button, 0f, RowHeight, 1f);
            UiKit.ButtonLabel(button).fontSize = UiTheme.FontSmall;
            _orderButtons[type] = button;
        }

        private Button CommandButton(
            RectTransform parent,
            string name,
            string text,
            string key,
            BattleCommandIntent intent,
            UiButtonStyle style = UiButtonStyle.Secondary)
        {
            Button button = UiKit.CreateCommandButton(parent, text, key, () => RequestImmediateOrder(intent), style);
            button.name = name;
            UiKit.SetSize(button, 0f, RowHeight, 1f);
            UiKit.ButtonLabel(button).fontSize = UiTheme.FontSmall;
            return button;
        }

        private void SopButton(RectTransform parent, string name, string text, string key, BattleCommandIntent intent)
        {
            _sopButtons[intent.Technique != null && intent.Technique.Length > 0 ? intent.Technique : intent.RulesOfEngagement] =
                CommandButton(parent, name, text, key, intent);
        }

        /// <summary>선택한 제대 종류(중대·소대)에 맞는 대형 버튼. 목록이 바뀔 때만 다시 만든다.</summary>
        private void BuildFormationButtons()
        {
            IReadOnlyList<BattleFormationOption> options = _selection[0].Level == BattleUnitLevel.Company
                ? _companyFormations
                : _platoonFormations;
            if (ReferenceEquals(options, _builtFormations))
                return;
            _builtFormations = options;
            _highlight = string.Empty;
            for (int i = _formationRow.childCount - 1; i >= 0; i--)
            {
                GameObject child = _formationRow.GetChild(i).gameObject;
                if (child.name != "RowTitle")
                    DestroyChild(child);
            }
            _formationButtons.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                BattleFormationOption option = options[i];
                if (option == null || string.IsNullOrEmpty(option.Id))
                    continue;
                string formationId = option.Id;
                Button button = UiKit.CreateCommandButton(
                    _formationRow,
                    ShortFormationName(option.DisplayName),
                    (i + 1).ToString(),
                    () => RequestFormation(formationId));
                button.name = "Formation_" + formationId;
                UiKit.SetSize(button, 0f, RowHeight, 1f);
                UiKit.ButtonLabel(button).fontSize = UiTheme.FontSmall;
                _formationButtons[formationId] = button;
            }
        }

        /// <summary>현재 대형·이동 방식·사격 통제를 버튼에 표시. 값이 바뀔 때만 다시 칠한다.</summary>
        private void UpdateSetupHighlight()
        {
            BattleUnitView unit = _selection[0];
            string highlight = unit.FormationId + "|" + unit.Technique + "|" + unit.RulesOfEngagement;
            if (highlight == _highlight)
                return;
            _highlight = highlight;
            foreach (KeyValuePair<string, Button> pair in _formationButtons)
                UiKit.SetSelected(pair.Value, pair.Key == unit.FormationId);
            foreach (KeyValuePair<string, Button> pair in _sopButtons)
                UiKit.SetSelected(pair.Value, pair.Key == unit.Technique || pair.Key == unit.RulesOfEngagement);
        }

        private void UpdateSpecialRow()
        {
            bool engineer = false;
            bool aviation = false;
            bool airWing = false;
            bool cargo = false;
            foreach (BattleUnitView unit in _selection)
            {
                engineer |= unit.Kind == "engineer";
                aviation |= unit.Kind == "aviation";
                airWing |= unit.Kind == "fixed_wing";
                cargo |= unit.CarriesCargo;
            }
            _orderButtons[BattleCommandType.LayMines].gameObject.SetActive(engineer);
            _orderButtons[BattleCommandType.ClearMines].gameObject.SetActive(engineer);
            _orderButtons[BattleCommandType.Fortify].gameObject.SetActive(engineer);
            _specialRow.Find("Command_Altitude").gameObject.SetActive(aviation);
            _orderButtons[BattleCommandType.Airdrop].gameObject.SetActive(cargo);
            _specialRow.Find("Command_ReturnToBase").gameObject.SetActive(airWing);
            _specialRow.gameObject.SetActive(engineer || aviation || airWing);
        }

        // ── 화력 지원 ───────────────────────────────────────────

        private void BuildFirePanel()
        {
            FirePanel = UiKit.CreateCard(transform, "FireSupportPanel");
            UiKit.Anchor(FirePanel, new Vector2(1f, 0f), new Vector2(-BattleHudLayout.Margin, BattleHudLayout.Margin),
                new Vector2(BattleHudLayout.FirePanelWidth, BattleHudLayout.BottomHeight));
            UiKit.AddVerticalLayout(FirePanel, 6, 10);

            HorizontalLayoutGroup header = UiKit.CreateHorizontalLayout(FirePanel, "Header", 6);
            header.childForceExpandWidth = false;
            UiKit.SetSize(header, -1f, 16f);
            UiKit.CreateHeading(header.transform, "화력 지원");
            Flexible(header.transform);
            _fireHint = UiKit.CreateLabel(header.transform, "좌클릭 표적 · 우클릭 취소", UiTheme.FontCaption, UiTheme.Accent);
            _fireHint.name = "FireHint";
            _fireHint.textWrappingMode = TextWrappingModes.NoWrap;
            _fireHint.gameObject.SetActive(false);

            RectTransform grid = UiKit.CreateRect(FirePanel, "FireButtons");
            UiKit.SetSize(grid, -1f, 26f * 3f + 4f * 2f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2((BattleHudLayout.FirePanelWidth - 20f - 4f) * 0.5f, 26f);
            layout.spacing = new Vector2(4f, 4f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            AddFireButton(grid, BattleFireAsset.Mortar, false, "박격포", "T");
            AddFireButton(grid, BattleFireAsset.Mortar, true, "박격포 연막", "G");
            AddFireButton(grid, BattleFireAsset.Artillery, false, "포병", "T T");
            AddFireButton(grid, BattleFireAsset.Artillery, true, "포병 연막", "G G");
            AddFireButton(grid, BattleFireAsset.Cas, false, "근접항공지원", "P");

            FireLabel = UiKit.CreateLabel(FirePanel, string.Empty, UiTheme.FontCaption, UiTheme.TextDim);
            FireLabel.name = "FireMissions";
            FireLabel.lineSpacing = 4f;
            FireLabel.overflowMode = TextOverflowModes.Truncate;
            UiKit.SetSize(FireLabel, -1f, -1f, 1f).flexibleHeight = 1f;
        }

        private void AddFireButton(Transform parent, BattleFireAsset asset, bool smoke, string text, string key)
        {
            Button button = UiKit.CreateCommandButton(parent, text, key, () => RequestFireMode(asset, smoke));
            button.name = "Fire_" + asset + (smoke ? "_Smoke" : string.Empty);
            UiKit.ButtonLabel(button).fontSize = UiTheme.FontSmall;
            _fireButtons[FireKey(asset, smoke)] = button;
        }

        private static string ShortFormationName(string name)
        {
            int paren = name.IndexOf(" (", StringComparison.Ordinal);
            return paren > 0 ? name.Substring(0, paren) : name;
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
