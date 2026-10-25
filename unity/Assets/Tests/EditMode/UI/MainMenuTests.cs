using Echelon.Unity.UI.Common;
using Echelon.Unity.UI.MainMenu;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Echelon.Unity.Tests.UI
{
    public sealed class MainMenuTests
    {
        private Canvas _canvas;
        private EventSystem _existingEventSystem;
        private MainMenu _menu;

        [SetUp]
        public void SetUp()
        {
            _existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
            _canvas = UiKit.CreateCanvas("MainMenuTestCanvas");
            _menu = MainMenu.Create(_canvas.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvas != null)
                Object.DestroyImmediate(_canvas.gameObject);
            if (_existingEventSystem == null)
            {
                var eventSystem = Object.FindAnyObjectByType<EventSystem>();
                if (eventSystem != null)
                    Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        [Test]
        public void MenuButtons_RaiseMatchingActions()
        {
            var tutorial = 0;
            var skirmish = 0;
            var sandbox = 0;
            var guide = 0;
            var quit = 0;
            _menu.TutorialRequested += () => tutorial++;
            _menu.SkirmishRequested += () => skirmish++;
            _menu.SandboxRequested += () => sandbox++;
            _menu.GuideRequested += () => guide++;
            _menu.QuitRequested += () => quit++;

            _menu.TutorialButton.onClick.Invoke();
            _menu.SkirmishButton.onClick.Invoke();
            _menu.SandboxButton.onClick.Invoke();
            _menu.GuideButton.onClick.Invoke();
            _menu.QuitButton.onClick.Invoke();

            Assert.That(tutorial, Is.EqualTo(1));
            Assert.That(skirmish, Is.EqualTo(1));
            Assert.That(sandbox, Is.EqualTo(1));
            Assert.That(guide, Is.EqualTo(1));
            Assert.That(quit, Is.EqualTo(1));
        }

        [Test]
        public void MenuButtons_PreserveKoreanLabels()
        {
            Assert.That(ButtonText(_menu.TutorialButton), Is.EqualTo("튜토리얼  (처음이라면, 약 10분)"));
            Assert.That(ButtonText(_menu.SkirmishButton), Is.EqualTo("스커미시  (편성 → 배치 → 전투)"));
            Assert.That(ButtonText(_menu.SandboxButton), Is.EqualTo("샌드박스  (편성 없이 바로 전투)"));
            Assert.That(ButtonText(_menu.GuideButton), Is.EqualTo("가이드"));
            Assert.That(ButtonText(_menu.QuitButton), Is.EqualTo("종료"));
        }

        [Test]
        public void MenuButtons_HaveWrappedVerticalNavigation()
        {
            Assert.That(_menu.TutorialButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(_menu.TutorialButton.navigation.selectOnDown, Is.EqualTo(_menu.SkirmishButton));
            Assert.That(_menu.TutorialButton.navigation.selectOnUp, Is.EqualTo(_menu.QuitButton));
            Assert.That(_menu.QuitButton.navigation.selectOnDown, Is.EqualTo(_menu.TutorialButton));
        }

        [Test]
        public void Canvas_UsesSixteenHundredByNineHundredReferenceResolution()
        {
            var scaler = _canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1600f, 900f)));
        }

        private static string ButtonText(Button button)
        {
            return button.GetComponentInChildren<TextMeshProUGUI>().text;
        }
    }
}
