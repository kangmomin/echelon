using System;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.MainMenu
{
    /// <summary>메인 메뉴: 격자 배경 위 왼쪽 정렬 제목과, 제목·설명 두 줄짜리 항목 버튼.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        private const float ItemWidth = 440f;
        private const int GridSize = 40;
        private static Texture2D _grid;

        public event Action TutorialRequested;
        public event Action SkirmishRequested;
        public event Action SandboxRequested;
        public event Action GuideRequested;
        public event Action QuitRequested;

        public Button TutorialButton { get; private set; }
        public Button SkirmishButton { get; private set; }
        public Button SandboxButton { get; private set; }
        public Button GuideButton { get; private set; }
        public Button QuitButton { get; private set; }
        public bool IsBuilt { get; private set; }

        public static MainMenu Create(Transform parent)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var root = new GameObject("MainMenu", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var menu = root.AddComponent<MainMenu>();
            menu.Build();
            return menu;
        }

        /// <summary>메뉴 항목의 설명 줄. 제목 아래 흐린 글자.</summary>
        public static string Description(Button item)
        {
            Transform label = item.transform.Find("Content/Description");
            return label != null ? label.GetComponent<TextMeshProUGUI>().text : string.Empty;
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;

            UiKit.Stretch((RectTransform)transform);
            UiKit.CreateBackground(transform);
            CreateGrid(transform);

            RectTransform column = UiKit.CreateRect(transform, "Column");
            column.anchorMin = new Vector2(0f, 0.5f);
            column.anchorMax = new Vector2(0f, 0.5f);
            column.pivot = new Vector2(0f, 0.5f);
            column.anchoredPosition = new Vector2(140f, 0f);
            column.sizeDelta = new Vector2(ItemWidth, 0f);
            VerticalLayoutGroup layout = UiKit.AddVerticalLayout(column, 8);
            var fitter = column.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI kicker = UiKit.CreateHeading(column, "제대 지휘 현대전 RTT");
            kicker.color = UiTheme.Accent;
            TextMeshProUGUI title = UiKit.CreateLabel(column, "ECHELON", 72f, UiTheme.Text);
            title.name = "Title";
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 4f;
            RectTransform rule = UiKit.CreatePanel(column, "Rule", UiTheme.Accent);
            UiKit.SetSize(rule, 64f, 3f, 0f);
            layout.childForceExpandWidth = false;
            TextMeshProUGUI subtitle = UiKit.CreateLabel(
                column,
                "대대장 자리에서 중대·소대를 지휘한다. 명령은 지휘망을 타고 늦게 도착한다.",
                UiTheme.FontBody,
                UiTheme.TextDim);
            subtitle.name = "Subtitle";
            UiKit.SetSize(subtitle, ItemWidth);
            UiKit.SetSize(UiKit.CreateRect(column, "Gap"), -1f, 20f);

            TutorialButton = CreateItem(column, "튜토리얼", "처음이라면 여기부터 — 약 10분, 14단계", RequestTutorial, true);
            SkirmishButton = CreateItem(column, "스커미시", "편성 → 배치 → 전투. 교차로 FOX, 40분", RequestSkirmish);
            SandboxButton = CreateItem(column, "샌드박스", "편성 없이 바로 전투", RequestSandbox);
            GuideButton = CreateItem(column, "가이드", "조작, 제대와 지휘 지연, 대형, 승리 조건", RequestGuide);
            QuitButton = CreateItem(column, "종료", string.Empty, RequestQuit);
            UiKit.SetButtonStyle(QuitButton, UiButtonStyle.Ghost);
            UiKit.ButtonLabel(QuitButton).color = UiTheme.TextDim;
            SetVerticalNavigation(TutorialButton, SkirmishButton, SandboxButton, GuideButton, QuitButton);

            TextMeshProUGUI footer = UiKit.CreateLabel(transform, "프로토타입 · / 가이드", UiTheme.FontCaption, UiTheme.TextMuted);
            footer.name = "Footer";
            footer.alignment = TextAlignmentOptions.BottomLeft;
            UiKit.Anchor(footer.rectTransform, Vector2.zero, new Vector2(140f, 32f), new Vector2(400f, 20f));
            UiKit.EnsureEventSystem();
        }

        public void RequestTutorial()
        {
            TutorialRequested?.Invoke();
        }

        public void RequestSkirmish()
        {
            SkirmishRequested?.Invoke();
        }

        public void RequestSandbox()
        {
            SandboxRequested?.Invoke();
        }

        public void RequestGuide()
        {
            GuideRequested?.Invoke();
        }

        public void RequestQuit()
        {
            QuitRequested?.Invoke();
        }

        private void Awake()
        {
            Build();
        }

        private static Button CreateItem(Transform parent, string title, string description, Action action, bool primary = false)
        {
            Button button = UiKit.CreateButton(parent, title, () => action(), ItemWidth);
            float height = string.IsNullOrEmpty(description) ? 40f : 58f;
            UiKit.SetSize(button, ItemWidth, height);
            if (primary)
                UiKit.AddBorder((RectTransform)button.transform, UiTheme.WithAlpha(UiTheme.Accent, 0.6f));

            // 제목과 설명을 위아래로 쌓는다. 제목 라벨(Text)은 UiKit 버튼 스타일이 색을 관리한다.
            TextMeshProUGUI titleLabel = UiKit.ButtonLabel(button);
            RectTransform content = UiKit.CreateRect(button.transform, "Content");
            UiKit.Stretch(content);
            content.offsetMin = new Vector2(18f, 8f);
            content.offsetMax = new Vector2(-18f, -8f);
            VerticalLayoutGroup stack = UiKit.AddVerticalLayout(content, 2);
            stack.childAlignment = TextAnchor.MiddleLeft;
            titleLabel.transform.SetParent(content, false);
            titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
            titleLabel.fontSize = UiTheme.FontHeading;
            titleLabel.fontStyle = FontStyles.Bold;
            // 높이를 레이아웃에 맡긴 채 잘라내기(굵은 글자는 말줄임표가 없어 TMP 가 바꾼다)가 되면
            // 첫 배치에서 높이 0으로 잘려 사라진다. 높이를 정하고 넘침을 허용한다.
            titleLabel.overflowMode = TextOverflowModes.Overflow;
            UiKit.SetSize(titleLabel, -1f, 22f);
            if (primary)
                titleLabel.color = UiTheme.Accent;
            if (!string.IsNullOrEmpty(description))
            {
                TextMeshProUGUI detail = UiKit.CreateLabel(content, description, UiTheme.FontSmall, UiTheme.TextDim);
                detail.name = "Description";
                detail.textWrappingMode = TextWrappingModes.NoWrap;
                UiKit.SetSize(detail, -1f, 17f);
            }
            return button;
        }

        /// <summary>40px 간격의 옅은 격자(작전 지도 느낌). 텍스처 하나를 만들어 두고 반복한다.</summary>
        private static void CreateGrid(Transform parent)
        {
            RectTransform rect = UiKit.CreateRect(parent, "Grid");
            UiKit.Stretch(rect);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = GridTexture();
            image.uvRect = new Rect(0f, 0f, 1600f / GridSize, 900f / GridSize);
            image.raycastTarget = false;
        }

        private static Texture2D GridTexture()
        {
            if (_grid != null)
                return _grid;
            const int size = GridSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "MenuGrid",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = x == 0 || y == 0 ? new Color32(255, 255, 255, 10) : new Color32(0, 0, 0, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _grid = texture;
            return _grid;
        }

        private static void SetVerticalNavigation(params Button[] buttons)
        {
            for (var i = 0; i < buttons.Length; i++)
            {
                var navigation = buttons[i].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length];
                navigation.selectOnDown = buttons[(i + 1) % buttons.Length];
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                buttons[i].navigation = navigation;
            }
        }
    }
}
