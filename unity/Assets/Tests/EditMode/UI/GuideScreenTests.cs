using System.Linq;
using Echelon.Unity.UI.Common;
using Echelon.Unity.UI.Guide;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Echelon.Unity.Tests.UI
{
    public sealed class GuideScreenTests
    {
        private Canvas _canvas;
        private EventSystem _existingEventSystem;
        private GuideScreen _guide;

        [SetUp]
        public void SetUp()
        {
            _existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
            _canvas = UiKit.CreateCanvas("GuideTestCanvas");
            _guide = GuideScreen.Create(_canvas.transform);
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
        public void OpenAndClose_RaiseOncePerTransition()
        {
            var opened = 0;
            var closed = 0;
            _guide.Opened += () => opened++;
            _guide.Closed += () => closed++;

            Assert.That(_guide.gameObject.activeSelf, Is.False);
            _guide.Open();
            _guide.Open();
            Assert.That(_guide.IsOpen, Is.True);
            Assert.That(_guide.gameObject.activeSelf, Is.True);
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(closed, Is.EqualTo(0));

            _guide.Close();
            _guide.Close();
            Assert.That(_guide.IsOpen, Is.False);
            Assert.That(_guide.gameObject.activeSelf, Is.False);
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(closed, Is.EqualTo(1));

            _guide.Open();
            _guide.Close();
            Assert.That(opened, Is.EqualTo(2));
            Assert.That(closed, Is.EqualTo(2));
        }

        [Test]
        public void SectionButtons_DisplayEverySection()
        {
            _guide.Open();
            Assert.That(_guide.SectionButtons.Count, Is.EqualTo(11));

            for (var i = 0; i < _guide.SectionButtons.Count; i++)
            {
                _guide.SectionButtons[i].onClick.Invoke();
                Assert.That(_guide.CurrentSectionIndex, Is.EqualTo(i));
                Assert.That(_guide.BodyScroll.content.childCount, Is.GreaterThan(0));
                Assert.That(BodyText(), Does.Contain(GuideContent.Sections[i].Blocks[0].Text));
            }
        }

        [Test]
        public void ShowSection_ClampsIndexAndResetsBodyToTop()
        {
            var changed = 0;
            _guide.SectionChanged += index => changed++;
            _guide.Open();

            _guide.ShowSection(-10);
            Assert.That(_guide.CurrentSectionIndex, Is.EqualTo(0));
            _guide.ShowSection(100);
            Assert.That(_guide.CurrentSectionIndex, Is.EqualTo(10));

            _guide.BodyScroll.verticalNormalizedPosition = 0f;
            _guide.ShowSection(3);
            Assert.That(_guide.CurrentSectionIndex, Is.EqualTo(3));
            Assert.That(_guide.BodyScroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
            Assert.That(changed, Is.EqualTo(4));
        }

        private string BodyText()
        {
            var labels = _guide.BodyScroll.content.GetComponentsInChildren<TextMeshProUGUI>(true);
            return string.Join("\n", labels.Select(label => label.text));
        }
    }
}
