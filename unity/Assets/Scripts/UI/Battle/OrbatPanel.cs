using System;
using System.Collections.Generic;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Battle
{
    /// <summary>
    /// 편제 트리(ORBAT). 0.5초마다 모델을 다시 읽지만 행 GameObject 는 재사용하고 내용만 바꾼다.
    /// 클릭: 선택(Ctrl: 추가), 더블클릭: 카메라 이동, ▾/▸: 접기.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrbatPanel : MonoBehaviour
    {
        public const float RefreshSeconds = 0.5f;
        private const float RowHeight = 24f;
        private const float IndentPerLevel = 12f;

        private readonly Dictionary<int, OrbatNodeData> _nodes = new Dictionary<int, OrbatNodeData>();
        private readonly Dictionary<int, OrbatRow> _rows = new Dictionary<int, OrbatRow>();
        private readonly List<OrbatRow> _pool = new List<OrbatRow>();
        private readonly List<(OrbatNodeData Node, int Depth)> _visible = new List<(OrbatNodeData, int)>();
        private readonly Dictionary<int, bool> _expanded = new Dictionary<int, bool>();
        private readonly HashSet<int> _selected = new HashSet<int>();
        private readonly List<int> _selectionOrder = new List<int>();
        private float _refreshTimer;

        public event Action<OrbatSelectionIntent> SelectionRequested;
        public event Action<int> FocusRequested;

        public bool IsBuilt { get; private set; }
        public Func<OrbatNodeData> ModelSource { get; private set; }
        public OrbatNodeData Root { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public IReadOnlyList<int> SelectedUnitIds => _selectionOrder;
        public int VisibleRowCount => _rows.Count;

        public static OrbatPanel Create(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("OrbatPanel", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var panel = root.AddComponent<OrbatPanel>();
            panel.Build();
            return panel;
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;
            RectTransform rect = (RectTransform)transform;
            BattleHudLayout.PlaceOrbat(rect, 0f);
            Image background = gameObject.AddComponent<Image>();
            UiKit.SetRounded(background);
            background.color = UiTheme.Panel;
            background.raycastTarget = true;
            UiKit.AddBorder(rect);

            VerticalLayoutGroup layout = UiKit.AddVerticalLayout(rect, 6, UiTheme.SpaceS);
            layout.childForceExpandHeight = false;
            TextMeshProUGUI heading = UiKit.CreateHeading(transform, "편제 ORBAT");
            heading.margin = new Vector4(4f, 2f, 0f, 0f);
            Scroll = UiKit.CreateScrollView(transform, "OrbatTree");
            LayoutElement scrollLayout = Scroll.gameObject.AddComponent<LayoutElement>();
            scrollLayout.flexibleWidth = 1f;
            scrollLayout.flexibleHeight = 1f;
            VerticalLayoutGroup contentLayout = Scroll.content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 1f;
            UiKit.EnsureEventSystem();
        }

        public void Bind(Func<OrbatNodeData> modelSource)
        {
            ModelSource = modelSource;
            _refreshTimer = 0f;
            Refresh();
        }

        public void Refresh()
        {
            if (ModelSource != null)
                SetRoot(ModelSource());
        }

        public void SetRoot(OrbatNodeData root)
        {
            Root = root;
            if (!IsBuilt)
                return;
            _nodes.Clear();
            IndexNode(root);
            _visible.Clear();
            CollectVisible(root, 0);

            _rows.Clear();
            for (int i = 0; i < _visible.Count; i++)
            {
                if (i >= _pool.Count)
                    _pool.Add(CreateRow());
                OrbatRow row = _pool[i];
                BindRow(row, _visible[i].Node, _visible[i].Depth);
                _rows[row.UnitId] = row;
            }
            for (int i = _visible.Count; i < _pool.Count; i++)
                _pool[i].Root.gameObject.SetActive(false);
            // 패널은 내용 높이만큼만(남는 곳은 전장이 보이게), 화면을 넘으면 스크롤.
            BattleHudLayout.PlaceOrbat((RectTransform)transform, 46f + _visible.Count * (RowHeight + 1f));

            _selectionOrder.RemoveAll(id => !_nodes.ContainsKey(id));
            _selected.RemoveWhere(id => !_nodes.ContainsKey(id));
            UpdateSelectionVisuals();
        }

        public void ShowSelection(IEnumerable<int> unitIds)
        {
            _selected.Clear();
            _selectionOrder.Clear();
            if (unitIds != null)
            {
                foreach (int id in unitIds)
                {
                    if (_nodes.ContainsKey(id) && _selected.Add(id))
                        _selectionOrder.Add(id);
                }
            }
            UpdateSelectionVisuals();
        }

        public void SelectUnit(int unitId, bool additive)
        {
            if (!_nodes.TryGetValue(unitId, out OrbatNodeData node) ||
                node.Unit == null || node.Unit.Level == BattleUnitLevel.TaskForce)
                return;

            if (additive)
            {
                if (_selected.Add(unitId))
                    _selectionOrder.Add(unitId);
            }
            else
            {
                _selected.Clear();
                _selectionOrder.Clear();
                _selected.Add(unitId);
                _selectionOrder.Add(unitId);
            }
            UpdateSelectionVisuals();
            SelectionRequested?.Invoke(new OrbatSelectionIntent(unitId, additive, new List<int>(_selectionOrder)));
        }

        public void FocusUnit(int unitId)
        {
            if (_nodes.ContainsKey(unitId))
                FocusRequested?.Invoke(unitId);
        }

        public void ToggleExpanded(int unitId)
        {
            if (!_nodes.TryGetValue(unitId, out OrbatNodeData node) || node.Children.Count == 0)
                return;
            _expanded[unitId] = !_expanded.TryGetValue(unitId, out bool value) || !value;
            SetRoot(Root);
        }

        public Button GetRowButton(int unitId)
        {
            return _rows.TryGetValue(unitId, out OrbatRow row) ? row.NameButton : null;
        }

        public TextMeshProUGUI GetStatusLabel(int unitId)
        {
            return _rows.TryGetValue(unitId, out OrbatRow row) ? row.StatusLabel : null;
        }

        public TextMeshProUGUI GetStrengthLabel(int unitId)
        {
            return _rows.TryGetValue(unitId, out OrbatRow row) ? row.StrengthLabel : null;
        }

        public TextMeshProUGUI GetCommsLabel(int unitId)
        {
            return _rows.TryGetValue(unitId, out OrbatRow row) ? row.CommsLabel : null;
        }

        private void Awake()
        {
            Build();
        }

        private void Update()
        {
            if (ModelSource == null)
                return;
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer < RefreshSeconds)
                return;
            _refreshTimer = 0f;
            Refresh();
        }

        private void IndexNode(OrbatNodeData node)
        {
            if (node?.Unit == null || _nodes.ContainsKey(node.Unit.Id))
                return;
            _nodes.Add(node.Unit.Id, node);
            if (!_expanded.ContainsKey(node.Unit.Id))
                _expanded.Add(node.Unit.Id, true);
            foreach (OrbatNodeData child in node.Children)
                IndexNode(child);
        }

        private void CollectVisible(OrbatNodeData node, int depth)
        {
            if (node?.Unit == null || !_nodes.TryGetValue(node.Unit.Id, out OrbatNodeData indexed) || indexed != node)
                return;
            _visible.Add((node, depth));
            if (node.Children.Count == 0 || !_expanded[node.Unit.Id])
                return;
            foreach (OrbatNodeData child in node.Children)
                CollectVisible(child, depth + 1);
        }

        private OrbatRow CreateRow()
        {
            var row = new OrbatRow();
            row.Root = UiKit.CreatePanel(Scroll.content, "Row", Color.clear);
            row.Background = row.Root.GetComponent<Image>();
            UiKit.SetSize(row.Root, -1f, RowHeight, 1f);
            HorizontalLayoutGroup horizontal = UiKit.AddHorizontalLayout(row.Root, 4);
            horizontal.padding = new RectOffset(2, 6, 0, 0);
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            row.Indent = UiKit.SetSize(UiKit.CreateRect(row.Root, "Indent"), 0f);
            row.FoldButton = UiKit.CreateButton(row.Root, "▼", () => ToggleExpanded(row.UnitId), 18f, UiButtonStyle.Ghost);
            row.FoldButton.name = "Fold";
            UiKit.SetSize(row.FoldButton, 18f, RowHeight);
            TextMeshProUGUI foldLabel = UiKit.ButtonLabel(row.FoldButton);
            foldLabel.fontSize = 7f;
            foldLabel.color = UiTheme.TextDim;
            foldLabel.rectTransform.offsetMin = Vector2.zero;
            foldLabel.rectTransform.offsetMax = Vector2.zero;

            row.NameButton = UiKit.CreateButton(row.Root, string.Empty, () => SelectUnit(row.UnitId, ControlHeld()), 40f, UiButtonStyle.Ghost);
            row.NameButton.name = "Name";
            LayoutElement nameLayout = UiKit.SetSize(row.NameButton, 40f, RowHeight, 1f);
            nameLayout.preferredWidth = -1f;
            row.NameLabel = UiKit.ButtonLabel(row.NameButton);
            row.NameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            row.NameLabel.fontSize = UiTheme.FontSmall;
            row.NameLabel.richText = false;
            row.NameLabel.rectTransform.offsetMin = new Vector2(2f, 0f);
            row.NameLabel.rectTransform.offsetMax = new Vector2(-2f, 0f);
            row.DoubleClick = row.NameButton.gameObject.AddComponent<OrbatDoubleClick>();
            row.DoubleClick.Owner = this;

            row.StatusLabel = RowLabel(row.Root, "Status", UiTheme.FontCaption, UiTheme.TextMuted, -1f);
            row.StatusLabel.alignment = TextAlignmentOptions.MidlineRight;
            row.StrengthLabel = RowLabel(row.Root, "Strength", UiTheme.FontCaption, UiTheme.Ok, 36f);
            row.StrengthLabel.alignment = TextAlignmentOptions.MidlineRight;
            row.CommsLabel = RowLabel(row.Root, "Comms", UiTheme.FontCaption, UiTheme.Ok, 14f);
            row.CommsLabel.alignment = TextAlignmentOptions.Center;
            return row;
        }

        private static TextMeshProUGUI RowLabel(Transform parent, string name, float size, Color color, float width)
        {
            TextMeshProUGUI label = UiKit.CreateLabel(parent, string.Empty, size, color);
            label.name = name;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (width > 0f)
                UiKit.SetSize(label, width);
            return label;
        }

        private void BindRow(OrbatRow row, OrbatNodeData node, int depth)
        {
            BattleUnitView unit = node.Unit;
            row.UnitId = unit.Id;
            row.DoubleClick.UnitId = unit.Id;
            row.Root.gameObject.SetActive(true);
            row.Root.name = "Unit_" + unit.Id;
            float indent = depth * IndentPerLevel;
            row.Indent.minWidth = indent;
            row.Indent.preferredWidth = indent;

            bool folder = node.Children.Count > 0;
            UiKit.ButtonLabel(row.FoldButton).text = folder ? (_expanded[unit.Id] ? "▼" : "▶") : string.Empty;

            bool taskForce = unit.Level == BattleUnitLevel.TaskForce;
            row.NameLabel.text = unit.TreeName;
            UiKit.SetBold(row.NameLabel, unit.Level != BattleUnitLevel.Platoon);
            row.Dim = !unit.OnMap || (unit.Destroyed && !taskForce);
            row.StatusLabel.text = BattleUiText.OrbatStatus(unit);
            row.StrengthLabel.text = taskForce ? string.Empty : BattleUiText.Percent(unit.Strength);
            row.StrengthLabel.color = UiTheme.Ratio(unit.Strength);
            row.CommsLabel.text = taskForce ? string.Empty : BattleUiText.CommsIcon(unit.Comms);
            row.CommsLabel.color = BattleUiText.CommsColor(unit.Comms);
        }

        private void UpdateSelectionVisuals()
        {
            foreach (KeyValuePair<int, OrbatRow> pair in _rows)
            {
                bool selected = _selected.Contains(pair.Key);
                OrbatRow row = pair.Value;
                row.Background.color = selected ? UiTheme.AccentSoft : Color.clear;
                row.NameLabel.color = selected ? UiTheme.Accent : row.Dim ? UiTheme.TextMuted : UiTheme.Text;
            }
        }

        private static bool ControlHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        private sealed class OrbatRow
        {
            public int UnitId;
            public bool Dim;
            public RectTransform Root;
            public Image Background;
            public LayoutElement Indent;
            public Button FoldButton;
            public Button NameButton;
            public TextMeshProUGUI NameLabel;
            public TextMeshProUGUI StatusLabel;
            public TextMeshProUGUI StrengthLabel;
            public TextMeshProUGUI CommsLabel;
            public OrbatDoubleClick DoubleClick;
        }

        private sealed class OrbatDoubleClick : MonoBehaviour, IPointerClickHandler
        {
            public OrbatPanel Owner;
            public int UnitId;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
                    Owner.FocusUnit(UnitId);
            }
        }
    }
}
