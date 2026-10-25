using System;
using Echelon.Unity.UI.Battle;
using Echelon.Unity.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.UI.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialPanel : MonoBehaviour
    {
        private TutorialController _controller;
        private RectTransform _panel;

        public bool IsBuilt { get; private set; }
        public TextMeshProUGUI CountLabel { get; private set; }
        public TextMeshProUGUI TitleLabel { get; private set; }
        public TextMeshProUGUI BodyLabel { get; private set; }
        public Button NextButton { get; private set; }
        public Button SkipButton { get; private set; }
        public Button QuitButton { get; private set; }

        public static TutorialPanel Create(Transform parent, TutorialController controller)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));

            var root = new GameObject("TutorialPanel", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var panel = root.AddComponent<TutorialPanel>();
            panel._controller = controller;
            panel.Build();
            if (!controller.IsActive && !controller.HasExited)
                controller.Start();
            return panel;
        }

        public void Build()
        {
            if (IsBuilt)
                return;
            if (_controller == null)
                throw new InvalidOperationException("TutorialPanel.Create must supply a controller.");
            IsBuilt = true;

            UiKit.Stretch((RectTransform)transform);
            _panel = UiKit.CreatePanel(transform, "Tutorial", UiTheme.PanelRaised);
            UiKit.AddBorder(_panel, UiTheme.WithAlpha(UiTheme.Accent, 0.55f));
            UiKit.Anchor(_panel, new Vector2(0.5f, 1f), new Vector2(0f, BattleHudLayout.BelowTopBar), new Vector2(660f, 0f));
            UiKit.AddVerticalLayout(_panel, 6, UiTheme.SpaceL);
            var fitter = _panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = UiKit.CreateHorizontalLayout(_panel, "Header", 6);
            header.childForceExpandWidth = false;
            var headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.minHeight = 36f;
            headerLayout.preferredHeight = 36f;
            headerLayout.flexibleWidth = 1f;

            CountLabel = UiKit.CreateHeading(header.transform, string.Empty);
            CountLabel.name = "Count";
            CountLabel.alignment = TextAlignmentOptions.MidlineLeft;
            UiKit.SetSize(CountLabel, 104f);

            TitleLabel = UiKit.CreateLabel(header.transform, string.Empty, UiTheme.FontTitle - 2f, UiTheme.Accent);
            TitleLabel.fontStyle = FontStyles.Bold;
            TitleLabel.alignment = TextAlignmentOptions.MidlineLeft;
            TitleLabel.name = "Title";
            var titleLayout = TitleLabel.gameObject.AddComponent<LayoutElement>();
            titleLayout.minWidth = 0f;
            titleLayout.flexibleWidth = 1f;

            SkipButton = UiKit.CreateButton(header.transform, "건너뛰기", _controller.Skip, 84f, UiButtonStyle.Ghost);
            SkipButton.name = "Skip";
            QuitButton = UiKit.CreateButton(header.transform, "그만하기", _controller.Quit, 84f, UiButtonStyle.Danger);
            QuitButton.name = "Quit";

            BodyLabel = UiKit.CreateLabel(_panel, string.Empty, UiTheme.FontBody, UiTheme.Text);
            BodyLabel.name = "Body";
            BodyLabel.lineSpacing = 8f;
            var bodyLayout = BodyLabel.gameObject.AddComponent<LayoutElement>();
            bodyLayout.minWidth = 0f;
            bodyLayout.minHeight = 20f;
            bodyLayout.flexibleWidth = 1f;

            var footer = UiKit.CreateRect(_panel, "Footer");
            var footerLayoutGroup = UiKit.AddHorizontalLayout(footer);
            footerLayoutGroup.childAlignment = TextAnchor.MiddleRight;
            footerLayoutGroup.childForceExpandWidth = false;
            var footerLayout = footer.gameObject.AddComponent<LayoutElement>();
            footerLayout.minHeight = 36f;
            footerLayout.preferredHeight = 36f;
            footerLayout.flexibleWidth = 1f;
            NextButton = UiKit.CreateButton(footer, "다음", _controller.Next, 120f, UiButtonStyle.Primary);
            NextButton.name = "Next";

            _controller.StepEntered += Render;
            _controller.StepSatisfied += RenderSatisfied;
            _controller.Exited += Hide;
            UiKit.EnsureEventSystem();
            if (_controller.IsActive)
                Render(_controller.CurrentStep);
        }

        private void Update()
        {
            if (_controller != null && _controller.IsActive)
                _controller.Tick(Time.unscaledDeltaTime);
        }

        private void Render(TutorialStepDefinition step)
        {
            if (!IsBuilt)
                return;
            transform.SetAsLastSibling();
            CountLabel.text = "튜토리얼 " + (_controller.CurrentStepIndex + 1) + " / " + TutorialContent.StepCount;
            TitleLabel.text = step.Title;
            BodyLabel.text = step.Body;
            UiKit.ButtonLabel(NextButton).text = step.IsFinal ? "메뉴로" : "다음";
            NextButton.gameObject.SetActive(step.AllowsNext);
        }

        private void RenderSatisfied(TutorialStepId step)
        {
            if (IsBuilt && step == _controller.CurrentStepId)
                TitleLabel.text = _controller.CurrentStep.Title + "  ✓";
        }

        private void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_controller == null)
                return;
            _controller.StepEntered -= Render;
            _controller.StepSatisfied -= RenderSatisfied;
            _controller.Exited -= Hide;
        }
    }
}
