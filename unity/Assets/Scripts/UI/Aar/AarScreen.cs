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

            _panel = UiKit.CreateModal(transform, "Modal", new Vector2(980f, 640f));
            VerticalLayoutGroup layout = UiKit.AddVerticalLayout(_panel, UiTheme.SpaceM, UiTheme.SpaceXl);
            layout.childForceExpandHeight = false;

            TitleLabel = UiKit.CreateLabel(_panel, string.Empty, UiTheme.FontTitle + 6f, UiTheme.Accent);
            TitleLabel.name = "Title";
            TitleLabel.fontStyle = FontStyles.Bold;
            UiKit.SetSize(TitleLabel, -1f, 34f);
            SummaryLabel = UiKit.CreateLabel(_panel, string.Empty, UiTheme.FontBody, UiTheme.TextDim);
            SummaryLabel.name = "Summary";
            UiKit.SetSize(SummaryLabel, -1f, 22f);
            UiKit.CreateSeparator(_panel);

            HorizontalLayoutGroup columns = UiKit.CreateHorizontalLayout(_panel, "Content", UiTheme.SpaceL);
            columns.childForceExpandHeight = true;
            columns.childForceExpandWidth = false;
            LayoutElement columnsLayout = columns.gameObject.AddComponent<LayoutElement>();
            columnsLayout.flexibleWidth = 1f;
            columnsLayout.flexibleHeight = 1f;

            LeftScroll = UiKit.CreateScrollView(columns.transform, "BattleStats");
            LayoutElement leftLayout = LeftScroll.gameObject.AddComponent<LayoutElement>();
            leftLayout.minWidth = 470f;
            leftLayout.preferredWidth = 470f;
            leftLayout.flexibleWidth = 0f;
            leftLayout.flexibleHeight = 1f;
            LeftLabel = CreateBodyLabel(LeftScroll.content, "Stats");

            RectTransform right = UiKit.CreatePanel(columns.transform, "TimelineColumn", UiTheme.PanelSunken);
            VerticalLayoutGroup rightStack = UiKit.AddVerticalLayout(right, 6, UiTheme.SpaceM);
            rightStack.childForceExpandHeight = false;
            LayoutElement rightLayout = right.gameObject.AddComponent<LayoutElement>();
            rightLayout.minWidth = 0f;
            rightLayout.flexibleWidth = 1f;
            rightLayout.flexibleHeight = 1f;
            TextMeshProUGUI timelineTitle = UiKit.CreateHeading(right, "교전 타임라인");
            timelineTitle.name = "TimelineTitle";
            TimelineScroll = UiKit.CreateScrollView(right, "Timeline");
            LayoutElement timelineLayout = TimelineScroll.gameObject.AddComponent<LayoutElement>();
            timelineLayout.minWidth = 0f;
            timelineLayout.flexibleWidth = 1f;
            timelineLayout.flexibleHeight = 1f;
            TimelineLabel = CreateBodyLabel(TimelineScroll.content, "TimelineText");
            TimelineLabel.fontSize = UiTheme.FontSmall;

            HorizontalLayoutGroup navigation = UiKit.CreateHorizontalLayout(_panel, "Navigation", UiTheme.SpaceS);
            navigation.childAlignment = TextAnchor.MiddleRight;
            navigation.childForceExpandWidth = false;
            UiKit.SetSize(navigation, -1f, UiTheme.ControlHeight + 4f);
            ViewBattlefieldButton = UiKit.CreateButton(navigation.transform, "전장 보기", RequestViewBattlefield, 140f);
            ViewBattlefieldButton.name = "ViewBattlefield";
            UiKit.SetSize(ViewBattlefieldButton, 140f, UiTheme.ControlHeight + 4f);
            MenuButton = UiKit.CreateButton(navigation.transform, "메뉴로", RequestMenu, 140f, UiButtonStyle.Primary);
            MenuButton.name = "Menu";
            UiKit.SetSize(MenuButton, 140f, UiTheme.ControlHeight + 4f);
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
            TextMeshProUGUI label = UiKit.CreateLabel(parent, string.Empty, UiTheme.FontBody, UiTheme.Text);
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
