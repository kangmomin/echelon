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
    [DisallowMultipleComponent]
    public sealed class OrbatPanel : MonoBehaviour
    {
        public const float RefreshSeconds = 0.5f;

        private readonly Dictionary<int, OrbatNodeData> _nodes = new Dictionary<int, OrbatNodeData>();
        private readonly Dictionary<int, OrbatRow> _rows = new Dictionary<int, OrbatRow>();
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
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(8f, -96f);
            rect.sizeDelta = new Vector2(322f, 420f);
            Image background = gameObject.AddComponent<Image>();
            background.color = UiTheme.Panel;
            background.raycastTarget = true;

            VerticalLayoutGroup layout = UiKit.AddVerticalLayout(rect, 0, 6);
            layout.childForceExpandHeight = true;
            Scroll = UiKit.CreateScrollView(transform, "OrbatTree");
            LayoutElement scrollLayout = Scroll.gameObject.AddComponent<LayoutElement>();
            scrollLayout.flexibleWidth = 1f;
            scrollLayout.flexibleHeight = 1f;
            VerticalLayoutGroup contentLayout = Scroll.content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 2f;
            contentLayout.padding = new RectOffset(0, 0, 0, 0);
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
            float scrollPosition = Scroll.verticalNormalizedPosition;
            for (int i = Scroll.content.childCount - 1; i >= 0; i--)
                DestroyChild(Scroll.content.GetChild(i).gameObject);
            _nodes.Clear();
            _rows.Clear();
            IndexNode(root);
            if (root != null)
                AddVisibleNode(root, 0);
            _selectionOrder.RemoveAll(id => !_nodes.ContainsKey(id));
            _selected.RemoveWhere(id => !_nodes.ContainsKey(id));
            UpdateSelectionVisuals();
            Canvas.ForceUpdateCanvases();
            Scroll.verticalNormalizedPosition = Mathf.Clamp01(scrollPosition);
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
            SelectionRequested?.Invoke(new OrbatSelectionIntent(
                unitId,
                additive,
                new List<int>(_selectionOrder)));
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
            bool expanded = !_expanded.TryGetValue(unitId, out bool value) || !value;
            _expanded[unitId] = expanded;
            SetRoot(Root);
        }

        public Button GetRowButton(int unitId)
        {
            return _rows.TryGetValue(unitId, out OrbatRow row) ? row.NameButton : null;
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

        private void AddVisibleNode(OrbatNodeData node, int depth)
        {
            if (node?.Unit == null || !_nodes.TryGetValue(node.Unit.Id, out OrbatNodeData indexed) || indexed != node)
                return;
            CreateRow(node, depth);
            if (node.Children.Count == 0 || !_expanded[node.Unit.Id])
                return;
            foreach (OrbatNodeData child in node.Children)
                AddVisibleNode(child, depth + 1);
        }

        private void CreateRow(OrbatNodeData node, int depth)
        {
            BattleUnitView unit = node.Unit;
            var row = new OrbatRow();
            RectTransform rect = UiKit.CreatePanel(Scroll.content, "Unit_" + unit.Id, Color.clear);
            LayoutElement rowLayout = rect.gameObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = 30f;
            rowLayout.preferredHeight = 30f;
            rowLayout.flexibleWidth = 1f;
            HorizontalLayoutGroup horizontal = UiKit.AddHorizontalLayout(rect, 2);
            horizontal.childAlignment = TextAnchor.MiddleCenter;
            row.Background = rect.GetComponent<Image>();

            if (depth > 0)
            {
                RectTransform indent = UiKit.CreateRect(rect, "Indent");
                LayoutElement indentLayout = indent.gameObject.AddComponent<LayoutElement>();
                indentLayout.minWidth = depth * 14f;
                indentLayout.preferredWidth = depth * 14f;
            }

            if (node.Children.Count > 0)
            {
                int id = unit.Id;
                Button fold = UiKit.CreateButton(rect, _expanded[id] ? "−" : "+", () => ToggleExpanded(id), 22f);
                fold.name = "Fold";
                LayoutElement foldLayout = fold.GetComponent<LayoutElement>();
                foldLayout.minWidth = 22f;
                foldLayout.preferredWidth = 22f;
                foldLayout.minHeight = 26f;
                foldLayout.preferredHeight = 26f;
                foldLayout.flexibleWidth = 0f;
                fold.GetComponentInChildren<TextMeshProUGUI>().fontSize = 13f;
            }
            else
            {
                RectTransform spacer = UiKit.CreateRect(rect, "FoldSpacer");
                LayoutElement spacerLayout = spacer.gameObject.AddComponent<LayoutElement>();
                spacerLayout.minWidth = 22f;
                spacerLayout.preferredWidth = 22f;
            }

            string status = BattleUiText.OrbatStatus(unit);
            string text = unit.TreeName + (string.IsNullOrEmpty(status) ? string.Empty : "  · " + status);
            int unitId = unit.Id;
            row.NameButton = UiKit.CreateButton(rect, text, () => SelectUnit(unitId, ControlHeld()));
            row.NameButton.name = "Name";
            LayoutElement nameLayout = row.NameButton.GetComponent<LayoutElement>();
            nameLayout.minWidth = 40f;
            nameLayout.flexibleWidth = 1f;
            nameLayout.minHeight = 28f;
            nameLayout.preferredHeight = 28f;
            TextMeshProUGUI nameLabel = row.NameButton.GetComponentInChildren<TextMeshProUGUI>();
            nameLabel.alignment = TextAlignmentOptions.Left;
            nameLabel.fontSize = 12f;
            nameLabel.richText = false;
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;
            bool dim = !unit.OnMap || (unit.Destroyed && unit.Level != BattleUnitLevel.TaskForce);
            nameLabel.color = dim ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.95f, 0.95f, 0.95f);
            ColorBlock nameColors = row.NameButton.colors;
            nameColors.normalColor = Color.clear;
            row.NameButton.colors = nameColors;
            row.NameButton.GetComponent<Image>().color = Color.clear;
            OrbatDoubleClick doubleClick = row.NameButton.gameObject.AddComponent<OrbatDoubleClick>();
            doubleClick.Owner = this;
            doubleClick.UnitId = unitId;

            row.StrengthLabel = UiKit.CreateLabel(
                rect,
                unit.Level == BattleUnitLevel.TaskForce
                    ? string.Empty
                    : Mathf.RoundToInt(unit.Strength * 100f) + "%",
                12,
                new Color(1f - unit.Strength, 0.35f + 0.6f * unit.Strength, 0.3f));
            row.StrengthLabel.name = "Strength";
            row.StrengthLabel.alignment = TextAlignmentOptions.Center;
            row.StrengthLabel.textWrappingMode = TextWrappingModes.NoWrap;
            LayoutElement strengthLayout = row.StrengthLabel.gameObject.AddComponent<LayoutElement>();
            strengthLayout.minWidth = 44f;
            strengthLayout.preferredWidth = 44f;

            row.CommsLabel = UiKit.CreateLabel(
                rect,
                unit.Level == BattleUnitLevel.TaskForce ? string.Empty : BattleUiText.CommsIcon(unit.Comms),
                15,
                BattleUiText.CommsColor(unit.Comms));
            row.CommsLabel.name = "Comms";
            row.CommsLabel.alignment = TextAlignmentOptions.Center;
            row.CommsLabel.textWrappingMode = TextWrappingModes.NoWrap;
            LayoutElement commsLayout = row.CommsLabel.gameObject.AddComponent<LayoutElement>();
            commsLayout.minWidth = 22f;
            commsLayout.preferredWidth = 22f;
            _rows.Add(unit.Id, row);
        }

        private void UpdateSelectionVisuals()
        {
            foreach (KeyValuePair<int, OrbatRow> pair in _rows)
            {
                bool selected = _selected.Contains(pair.Key);
                pair.Value.Background.color = selected ? UiTheme.ButtonSelected : Color.clear;
                TextMeshProUGUI label = pair.Value.NameButton.GetComponentInChildren<TextMeshProUGUI>();
                if (selected)
                    label.color = UiTheme.Accent;
                else
                {
                    BattleUnitView unit = _nodes[pair.Key].Unit;
                    bool dim = !unit.OnMap || (unit.Destroyed && unit.Level != BattleUnitLevel.TaskForce);
                    label.color = dim ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.95f, 0.95f, 0.95f);
                }
            }
        }

        private static bool ControlHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        private static void DestroyChild(GameObject child)
        {
            child.SetActive(false);
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        private sealed class OrbatRow
        {
            public Image Background;
            public Button NameButton;
            public TextMeshProUGUI StrengthLabel;
            public TextMeshProUGUI CommsLabel;
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
