using System;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
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

        public void Build()
        {
            if (IsBuilt)
                return;
            IsBuilt = true;

            UiKit.Stretch((RectTransform)transform);
            UiKit.CreateBackground(transform);
            var center = UiKit.CreateRect(transform, "Center");
            UiKit.Stretch(center);

            var panel = UiKit.CreatePanel(center, "MenuPanel");
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = Vector2.zero;
            UiKit.AddVerticalLayout(panel, 12, 28);
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = UiKit.CreateLabel(panel, "ECHELON", 40, UiTheme.Accent);
            title.name = "Title";
            title.alignment = TextAlignmentOptions.Center;
            var subtitle = UiKit.CreateLabel(
                panel,
                "제대 지휘 현대전 RTT — 프로토타입",
                15,
                UiTheme.TextDim);
            subtitle.name = "Subtitle";
            subtitle.alignment = TextAlignmentOptions.Center;
            UiKit.CreateSeparator(panel);

            TutorialButton = CreateMenuButton(panel, "튜토리얼  (처음이라면, 약 10분)", RequestTutorial);
            SkirmishButton = CreateMenuButton(panel, "스커미시  (편성 → 배치 → 전투)", RequestSkirmish);
            SandboxButton = CreateMenuButton(panel, "샌드박스  (편성 없이 바로 전투)", RequestSandbox);
            GuideButton = CreateMenuButton(panel, "가이드", RequestGuide);
            QuitButton = CreateMenuButton(panel, "종료", RequestQuit);
            SetVerticalNavigation(
                TutorialButton,
                SkirmishButton,
                SandboxButton,
                GuideButton,
                QuitButton);
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

        private static Button CreateMenuButton(Transform parent, string text, Action action)
        {
            var button = UiKit.CreateButton(parent, text, () => action(), 340f);
            var layout = button.GetComponent<LayoutElement>();
            layout.minHeight = 42f;
            layout.preferredHeight = 42f;
            return button;
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
