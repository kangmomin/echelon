using System;
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
            _panel = UiKit.CreatePanel(transform, "Tutorial");
            _panel.anchorMin = new Vector2(0.5f, 1f);
            _panel.anchorMax = new Vector2(0.5f, 1f);
            _panel.pivot = new Vector2(0.5f, 1f);
            _panel.anchoredPosition = new Vector2(0f, -46f);
            _panel.sizeDelta = new Vector2(660f, 0f);
            UiKit.AddVerticalLayout(_panel, 6, 12);
            var fitter = _panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = UiKit.CreateHorizontalLayout(_panel, "Header", 6);
            var headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.minHeight = 36f;
            headerLayout.preferredHeight = 36f;
            headerLayout.flexibleWidth = 1f;

            CountLabel = UiKit.CreateLabel(header.transform, string.Empty, 12, UiTheme.TextDim);
            CountLabel.name = "Count";
            var countLayout = CountLabel.gameObject.AddComponent<LayoutElement>();
            countLayout.minWidth = 112f;
            countLayout.preferredWidth = 112f;

            TitleLabel = UiKit.CreateLabel(header.transform, string.Empty, 18, UiTheme.Accent);
            TitleLabel.name = "Title";
            var titleLayout = TitleLabel.gameObject.AddComponent<LayoutElement>();
            titleLayout.minWidth = 0f;
            titleLayout.flexibleWidth = 1f;

            SkipButton = UiKit.CreateButton(header.transform, "Skip", _controller.Skip, 80f);
            SkipButton.name = "Skip";
            QuitButton = UiKit.CreateButton(header.transform, "Quit", _controller.Quit, 80f);
            QuitButton.name = "Quit";

            BodyLabel = UiKit.CreateLabel(_panel, string.Empty, 14, UiTheme.Text);
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
            NextButton = UiKit.CreateButton(footer, "Next", _controller.Next, 120f);
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
            CountLabel.text = "Tutorial " + (_controller.CurrentStepIndex + 1) + " / " + TutorialContent.StepCount;
            TitleLabel.text = step.Title;
            BodyLabel.text = step.Body;
            NextButton.GetComponentInChildren<TextMeshProUGUI>().text = step.IsFinal ? "Menu" : "Next";
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
