using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Echelon.Unity.Tests.BattleUi
{
    public abstract class BattleUiTestFixture
    {
        private EventSystem[] _existingEventSystems;
        protected GameObject CanvasObject { get; private set; }
        protected RectTransform CanvasRect { get; private set; }

        [SetUp]
        public void SetUpUi()
        {
            _existingEventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            CanvasObject = new GameObject(
                "BattleUiTestCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = CanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasRect = CanvasObject.GetComponent<RectTransform>();
            CanvasRect.sizeDelta = new Vector2(1600f, 900f);
        }

        [TearDown]
        public void TearDownUi()
        {
            if (CanvasObject != null)
                UnityEngine.Object.DestroyImmediate(CanvasObject);
            EventSystem[] current = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (EventSystem eventSystem in current)
            {
                if (!Array.Exists(_existingEventSystems, existing => existing == eventSystem))
                    UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        protected static Button FindButton(Component root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name)
                    return button;
            }
            Assert.Fail("Button not found: " + name);
            return null;
        }
    }
}
