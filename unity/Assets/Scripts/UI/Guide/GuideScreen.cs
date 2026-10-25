using System;
using System.Collections.Generic;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Guide
{
    [DisallowMultipleComponent]
    public sealed class GuideScreen : MonoBehaviour
    {
        private readonly List<Button> _sectionButtons = new List<Button>();
        private RectTransform _panel;
        private int _openedFrame = -1;

        public event Action Opened;
        public event Action Closed;
        public event Action<int> SectionChanged;

        public static IReadOnlyList<GuideSection> Sections => GuideContent.Sections;
        public IReadOnlyList<Button> SectionButtons => _sectionButtons;
        public bool IsBuilt { get; private set; }
        public bool IsOpen { get; private set; }
        public int CurrentSectionIndex { get; private set; }
        public ScrollRect BodyScroll { get; private set; }
        public Button CloseButton { get; private set; }

        public static GuideScreen Create(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var root = new GameObject("GuideScreen", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.SetActive(false);
            return root.AddComponent<GuideScreen>();
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;

            UiKit.Stretch((RectTransform)transform);
            var canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            _panel = UiKit.CreateModal(transform, "Modal", new Vector2(1040f, 680f));
            var panelLayout = UiKit.AddVerticalLayout(_panel, UiTheme.SpaceM, UiTheme.SpaceXl);
            panelLayout.childForceExpandHeight = false;

            var header = UiKit.CreateHorizontalLayout(_panel, "Header", UiTheme.SpaceS);
            header.childForceExpandWidth = false;
            UiKit.SetSize(header, -1f, 36f, 1f);
            var title = UiKit.CreateLabel(header.transform, "가이드", UiTheme.FontTitle + 4f, UiTheme.Text);
            title.name = "Title";
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            UiKit.SetSize(title, 0f, -1f, 1f);
            CloseButton = UiKit.CreateCommandButton(header.transform, "닫기", "Esc", Close, UiButtonStyle.Ghost);
            UiKit.SetSize(CloseButton, 104f, UiTheme.ControlHeight);
            UiKit.CreateSeparator(_panel);

            var body = UiKit.CreateHorizontalLayout(_panel, "Body", 16);
            body.childForceExpandHeight = true;
            body.childForceExpandWidth = false;
            var bodyLayout = body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.flexibleWidth = 1f;
            bodyLayout.flexibleHeight = 1f;

            var toc = UiKit.CreateScrollView(body.transform, "Sections");
            var tocLayout = toc.gameObject.AddComponent<LayoutElement>();
            tocLayout.minWidth = 205f;
            tocLayout.preferredWidth = 205f;
            tocLayout.flexibleWidth = 0f;
            tocLayout.flexibleHeight = 1f;
            var tocContentLayout = toc.content.GetComponent<VerticalLayoutGroup>();
            tocContentLayout.spacing = 4f;
            tocContentLayout.padding = new RectOffset(2, 2, 2, 2);
            BuildSectionButtons(toc.content);

            BodyScroll = UiKit.CreateScrollView(body.transform, "BodyScroll");
            var scrollLayout = BodyScroll.gameObject.AddComponent<LayoutElement>();
            scrollLayout.minWidth = 0f;
            scrollLayout.flexibleWidth = 1f;
            scrollLayout.flexibleHeight = 1f;
            var bodyContentLayout = BodyScroll.content.GetComponent<VerticalLayoutGroup>();
            bodyContentLayout.spacing = 8f;
            bodyContentLayout.padding = new RectOffset(4, 4, 4, 4);

            UpdatePanelSize();
            UpdateSectionSelection();
            UiKit.EnsureEventSystem();
        }

        public void Open()
        {
            if (IsOpen)
                return;
            Build();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UpdatePanelSize();
            IsOpen = true;
            _openedFrame = Time.frameCount;
            ShowSection(CurrentSectionIndex);
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        public void ShowSection(int index)
        {
            CurrentSectionIndex = Mathf.Clamp(index, 0, Sections.Count - 1);
            if (!IsBuilt)
                return;

            RenderSection(Sections[CurrentSectionIndex]);
            UpdateSectionSelection();
            BodyScroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            BodyScroll.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            BodyScroll.verticalNormalizedPosition = 1f;
            SectionChanged?.Invoke(CurrentSectionIndex);
        }

        private void Awake()
        {
            Build();
        }

        private void Update()
        {
            if (!IsOpen || Time.frameCount == _openedFrame)
                return;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.slashKey.wasPressedThisFrame)
                Close();
        }

        private void LateUpdate()
        {
            if (IsOpen)
                UpdatePanelSize();
        }

        private void OnDestroy()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            Closed?.Invoke();
        }

        private void BuildSectionButtons(Transform parent)
        {
            for (var i = 0; i < Sections.Count; i++)
            {
                var sectionIndex = i;
                var button = UiKit.CreateButton(
                    parent,
                    Sections[i].Title,
                    () => ShowSection(sectionIndex),
                    180f);
                button.name = "Section_" + i;
                var label = UiKit.ButtonLabel(button);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.fontSize = UiTheme.FontBody;
                label.rectTransform.offsetMin = new Vector2(10f, 0f);
                label.rectTransform.offsetMax = new Vector2(-6f, 0f);
                UiKit.SetSize(button, 180f, 34f);
                _sectionButtons.Add(button);
            }

            for (var i = 0; i < _sectionButtons.Count; i++)
            {
                var navigation = _sectionButtons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = _sectionButtons[(i + _sectionButtons.Count - 1) % _sectionButtons.Count];
                navigation.selectOnDown = _sectionButtons[(i + 1) % _sectionButtons.Count];
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                _sectionButtons[i].navigation = navigation;
            }
        }

        private void RenderSection(GuideSection section)
        {
            ClearBody();
            foreach (var block in section.Blocks)
            {
                if (block.Kind == GuideBlockKind.Table)
                    CreateTable(block.Rows);
                else
                    CreateBodyLabel(BodyScroll.content, block.Text);
            }
        }

        private void CreateTable(IReadOnlyList<GuideRow> rows)
        {
            var table = UiKit.CreateCard(BodyScroll.content, "Table", UiTheme.PanelSunken);
            UiKit.AddVerticalLayout(table, 4, 4);
            var tableLayout = table.gameObject.AddComponent<LayoutElement>();
            tableLayout.flexibleWidth = 1f;

            for (var i = 0; i < rows.Count; i++)
            {
                var rowColor = i % 2 == 0 ? Color.clear : UiTheme.WithAlpha(UiTheme.PanelRaised, 0.7f);
                var row = UiKit.CreatePanel(table, "Row_" + i, rowColor);
                var rowLayoutGroup = UiKit.AddHorizontalLayout(row, 12, 8);
                rowLayoutGroup.childForceExpandHeight = true;
                var rowLayout = row.gameObject.AddComponent<LayoutElement>();
                rowLayout.flexibleWidth = 1f;

                var key = CreateBodyLabel(row, rows[i].Key);
                key.name = "Key";
                key.color = UiTheme.Accent;
                var keyLayout = key.gameObject.AddComponent<LayoutElement>();
                keyLayout.minWidth = 190f;
                keyLayout.preferredWidth = 190f;
                keyLayout.flexibleWidth = 0f;

                var description = CreateBodyLabel(row, rows[i].Description);
                description.name = "Description";
                var descriptionLayout = description.gameObject.AddComponent<LayoutElement>();
                descriptionLayout.minWidth = 0f;
                descriptionLayout.flexibleWidth = 1f;
            }
        }

        private TextMeshProUGUI CreateBodyLabel(Transform parent, string text)
        {
            var label = UiKit.CreateLabel(parent, text, UiTheme.FontBody + 1f, UiTheme.Text);
            label.name = "RichText";
            label.alignment = TextAlignmentOptions.TopLeft;
            label.lineSpacing = 12f;
            var layout = label.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 0f;
            layout.flexibleWidth = 1f;
            return label;
        }

        private void UpdateSectionSelection()
        {
            for (var i = 0; i < _sectionButtons.Count; i++)
                UiKit.SetSelected(_sectionButtons[i], i == CurrentSectionIndex, UiButtonStyle.Ghost);
        }

        private void UpdatePanelSize()
        {
            if (_panel == null)
                return;
            var parent = _panel.parent as RectTransform;
            if (parent == null)
                return;
            var available = parent.rect.size;
            if (available.x <= 0f)
                available.x = 1600f;
            if (available.y <= 0f)
                available.y = 900f;
            _panel.sizeDelta = new Vector2(
                Mathf.Min(1040f, Mathf.Max(0f, available.x - 40f)),
                Mathf.Min(680f, Mathf.Max(0f, available.y - 40f)));
        }

        private void ClearBody()
        {
            for (var i = BodyScroll.content.childCount - 1; i >= 0; i--)
            {
                var child = BodyScroll.content.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(child);
                else
                    UnityEngine.Object.DestroyImmediate(child);
            }
        }
    }
}
