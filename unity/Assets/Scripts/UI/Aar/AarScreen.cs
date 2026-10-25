using System;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Aar
{
    [DisallowMultipleComponent]
    public sealed class AarScreen : MonoBehaviour
    {
        private AarData _data = new AarData();
        private string _playerSide = "blue";
        private RectTransform _panel;

        public event Action ViewBattlefieldRequested;
        public event Action MenuRequested;

        public bool IsBuilt { get; private set; }
        public AarViewData CurrentView { get; private set; }
        public TextMeshProUGUI TitleLabel { get; private set; }
        public TextMeshProUGUI SummaryLabel { get; private set; }
        public TextMeshProUGUI LeftLabel { get; private set; }
        public TextMeshProUGUI TimelineLabel { get; private set; }
        public ScrollRect LeftScroll { get; private set; }
        public ScrollRect TimelineScroll { get; private set; }
        public Button ViewBattlefieldButton { get; private set; }
        public Button MenuButton { get; private set; }

        public static AarScreen Create(Transform parent, AarData data = null, string playerSide = "blue")
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("AarScreen", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var screen = root.AddComponent<AarScreen>();
            screen.Build();
            screen.SetData(data, playerSide);
            return screen;
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;
            UiKit.Stretch((RectTransform)transform);
            CanvasGroup canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            RectTransform dim = UiKit.CreatePanel(transform, "ModalDim", new Color(0f, 0f, 0f, 0.55f));
            UiKit.Stretch(dim);
            RectTransform center = UiKit.CreateRect(transform, "Center");
            UiKit.Stretch(center);
            _panel = UiKit.CreatePanel(center, "AarPanel");
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            UiKit.AddVerticalLayout(_panel, 10, 20);

            TitleLabel = UiKit.CreateLabel(_panel, string.Empty, 26, UiTheme.Accent);
            TitleLabel.name = "Title";
            LayoutElement titleLayout = TitleLabel.gameObject.AddComponent<LayoutElement>();
            titleLayout.minHeight = 36f;
            titleLayout.preferredHeight = 36f;
            SummaryLabel = UiKit.CreateLabel(_panel, string.Empty, 14, UiTheme.TextDim);
            SummaryLabel.name = "Summary";
            LayoutElement summaryLayout = SummaryLabel.gameObject.AddComponent<LayoutElement>();
            summaryLayout.minHeight = 24f;
            summaryLayout.preferredHeight = 24f;

            HorizontalLayoutGroup columns = UiKit.CreateHorizontalLayout(_panel, "Content", 18);
            columns.childForceExpandHeight = true;
            LayoutElement columnsLayout = columns.gameObject.AddComponent<LayoutElement>();
            columnsLayout.flexibleWidth = 1f;
            columnsLayout.flexibleHeight = 1f;

            LeftScroll = UiKit.CreateScrollView(columns.transform, "BattleStats");
            LayoutElement leftLayout = LeftScroll.gameObject.AddComponent<LayoutElement>();
            leftLayout.minWidth = 460f;
            leftLayout.preferredWidth = 460f;
            leftLayout.flexibleWidth = 0f;
            leftLayout.flexibleHeight = 1f;
            LeftLabel = CreateBodyLabel(LeftScroll.content, "Stats");

            VerticalLayoutGroup right = UiKit.CreateVerticalLayout(columns.transform, "TimelineColumn", 4);
            LayoutElement rightLayout = right.gameObject.AddComponent<LayoutElement>();
            rightLayout.minWidth = 0f;
            rightLayout.flexibleWidth = 1f;
            rightLayout.flexibleHeight = 1f;
            TextMeshProUGUI timelineTitle = UiKit.CreateLabel(right.transform, "교전 타임라인", 15, UiTheme.Accent);
            timelineTitle.name = "TimelineTitle";
            LayoutElement timelineTitleLayout = timelineTitle.gameObject.AddComponent<LayoutElement>();
            timelineTitleLayout.minHeight = 22f;
            timelineTitleLayout.preferredHeight = 22f;
            TimelineScroll = UiKit.CreateScrollView(right.transform, "Timeline");
            LayoutElement timelineLayout = TimelineScroll.gameObject.AddComponent<LayoutElement>();
            timelineLayout.minWidth = 0f;
            timelineLayout.minHeight = 420f;
            timelineLayout.flexibleWidth = 1f;
            timelineLayout.flexibleHeight = 1f;
            TimelineLabel = CreateBodyLabel(TimelineScroll.content, "TimelineText");

            HorizontalLayoutGroup navigation = UiKit.CreateHorizontalLayout(_panel, "Navigation", 8);
            LayoutElement navigationLayout = navigation.gameObject.AddComponent<LayoutElement>();
            navigationLayout.minHeight = 36f;
            navigationLayout.preferredHeight = 36f;
            ViewBattlefieldButton = UiKit.CreateButton(navigation.transform, "전장 보기", RequestViewBattlefield, 140f);
            ViewBattlefieldButton.name = "ViewBattlefield";
            MenuButton = UiKit.CreateButton(navigation.transform, "메뉴로", RequestMenu, 140f);
            MenuButton.name = "Menu";
            UpdatePanelSize();
            SetData(_data, _playerSide);
            UiKit.EnsureEventSystem();
        }

        public void SetData(AarData data, string playerSide = "blue")
        {
            _data = data ?? new AarData();
            _playerSide = string.IsNullOrEmpty(playerSide) ? "blue" : playerSide;
            CurrentView = AarViewModel.Build(_data, _playerSide);
            if (!IsBuilt)
                return;
            TitleLabel.text = CurrentView.Title;
            TitleLabel.color = CurrentView.TitleColor;
            SummaryLabel.text = CurrentView.Summary;
            LeftLabel.text = CurrentView.LeftText;
            TimelineLabel.text = CurrentView.TimelineText;
            ResetScroll(LeftScroll);
            ResetScroll(TimelineScroll);
        }

        public void Open()
        {
            Build();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UpdatePanelSize();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void RequestViewBattlefield()
        {
            ViewBattlefieldRequested?.Invoke();
        }

        public void RequestMenu()
        {
            MenuRequested?.Invoke();
        }

        private void Awake()
        {
            Build();
        }

        private void LateUpdate()
        {
            UpdatePanelSize();
        }

        private static TextMeshProUGUI CreateBodyLabel(Transform parent, string name)
        {
            TextMeshProUGUI label = UiKit.CreateLabel(parent, string.Empty, 14, UiTheme.Text);
            label.name = name;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.lineSpacing = 4f;
            LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 0f;
            layout.flexibleWidth = 1f;
            return label;
        }

        private static void ResetScroll(ScrollRect scroll)
        {
            scroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }

        private void UpdatePanelSize()
        {
            if (_panel == null)
                return;
            RectTransform parent = _panel.parent as RectTransform;
            if (parent == null)
                return;
            Vector2 available = parent.rect.size;
            if (available.x <= 0f)
                available.x = 1600f;
            if (available.y <= 0f)
                available.y = 900f;
            _panel.sizeDelta = new Vector2(
                Mathf.Min(980f, Mathf.Max(0f, available.x - 40f)),
                Mathf.Min(640f, Mathf.Max(0f, available.y - 40f)));
        }
    }
}
