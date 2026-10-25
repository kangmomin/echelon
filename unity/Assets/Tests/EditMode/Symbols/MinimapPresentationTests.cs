using Echelon.Unity.Presentation.Symbols;
using Echelon.Unity.UI.Minimap;
using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Tests.Symbols
{
    public sealed class MinimapPresentationTests
    {
        [Test]
        public void TerrainPalette_MatchesGodotColorsAndHeightShading()
        {
            AssertColor(MinimapTerrainPalette.Shade(new MinimapTerrainSample(MinimapTerrainType.Open, 40f)),
                new Color(0.43f, 0.50f, 0.31f, 1f));
            AssertColor(MinimapTerrainPalette.Shade(new MinimapTerrainSample(MinimapTerrainType.Forest, 40f)),
                new Color(0.20f, 0.30f, 0.16f, 1f));
            AssertColor(MinimapTerrainPalette.Shade(new MinimapTerrainSample(MinimapTerrainType.Open, 160f)),
                new Color(0.43f + 0.57f * 0.3f, 0.65f, 0.31f + 0.69f * 0.3f, 1f));
            AssertColor(MinimapTerrainPalette.Shade(new MinimapTerrainSample(MinimapTerrainType.Open, 0f)),
                new Color(0.43f * 0.7f, 0.35f, 0.31f * 0.7f, 1f));
        }

        [Test]
        public void Contacts_FilterHiddenMountedDeadAndOffMapElements()
        {
            var contact = new MinimapContactSnapshot { Side = "red", Contact = SymbolContactState.Hidden };
            Assert.That(MinimapPresentation.ShouldDrawContact(contact, "blue", true), Is.False);
            Assert.That(MinimapPresentation.ShouldDrawContact(contact, "blue", false), Is.True);
            contact.Contact = SymbolContactState.Stale;
            Assert.That(MinimapPresentation.ShouldDrawContact(contact, "blue", true), Is.True);
            Assert.That(MinimapPresentation.ContactColor(contact, "blue").a, Is.EqualTo(0.5f).Within(0.0001f));
            contact.IsAlive = false;
            Assert.That(MinimapPresentation.ShouldDrawContact(contact, "blue", true), Is.False);
            contact.IsAlive = true;
            contact.IsMounted = true;
            Assert.That(MinimapPresentation.ShouldDrawContact(contact, "blue", true), Is.False);
            contact.IsMounted = false;
            contact.OnMap = false;
            Assert.That(MinimapPresentation.ShouldDrawContact(contact, "blue", true), Is.False);
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }
    }

    public sealed class MinimapViewTests
    {
        private GameObject _gameObject;
        private MinimapView _view;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("MinimapViewTests", typeof(RectTransform));
            _view = _gameObject.AddComponent<MinimapView>();
            _view.SetState(new MinimapSnapshot { MapSize = new Vector2(4000f, 4000f) });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void Advance_RefreshesAtFourHertz()
        {
            int version = _view.RefreshVersion;
            _view.Advance(0.24f);
            Assert.That(_view.RefreshVersion, Is.EqualTo(version));
            _view.Advance(0.01f);
            Assert.That(_view.RefreshVersion, Is.EqualTo(version + 1));
        }

        [Test]
        public void ClickAndDrag_EmitClampedSimulationCoordinates()
        {
            Vector2 navigated = Vector2.zero;
            int calls = 0;
            _view.NavigationRequested += position =>
            {
                navigated = position;
                calls++;
            };

            Assert.That(_view.Click(Vector2.zero), Is.True);
            AssertVector(navigated, new Vector2(2000f, 2000f));
            Assert.That(_view.Drag(new Vector2(-500f, 500f)), Is.True);
            AssertVector(navigated, new Vector2(0f, 4000f));
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(_view.Click(new Vector2(500f, 500f)), Is.False);
            Assert.That(calls, Is.EqualTo(2));
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }
    }
}
