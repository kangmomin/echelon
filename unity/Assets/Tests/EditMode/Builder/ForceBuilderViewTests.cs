using System.Linq;
using Echelon.Core;
using Echelon.Unity.UI.Builder;
using Echelon.Unity.UI.Common;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;

namespace Echelon.Unity.Tests.Builder
{
    public sealed class ForceBuilderViewTests
    {
        private GameObject _root;
        private ForceBuilder _builder;
        private MemoryForceStore _store;
        private EventSystem _existingEventSystem;
        private TMP_FontAsset _previousFont;
        private TMP_FontAsset _createdFont;
        private Material _createdMaterial;
        private Texture2D _createdAtlas;

        [OneTimeSetUp]
        public void SetUpFont()
        {
            _previousFont = UiKit.DefaultFont;
            if (_previousFont != null)
                return;
            Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Regular.otf");
            Assert.That(source, Is.Not.Null);
            Assert.That(FontEngine.LoadFontFace(source, 64), Is.EqualTo(FontEngineError.Success));
            _createdFont = ScriptableObject.CreateInstance<TMP_FontAsset>();
            _createdFont.name = "BuilderTestFont";
            _createdFont.faceInfo = FontEngine.GetFaceInfo();
            _createdFont.atlasPopulationMode = AtlasPopulationMode.Static;
            _createdAtlas = new Texture2D(1, 1, TextureFormat.Alpha8, false);
            _createdFont.atlasTextures = new[] { _createdAtlas };
            Shader shader = Shader.Find("UI/Default");
            Assert.That(shader, Is.Not.Null);
            _createdMaterial = new Material(shader);
            _createdMaterial.mainTexture = _createdAtlas;
            _createdFont.material = _createdMaterial;
            typeof(TMP_Asset).GetField("m_Version",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_createdFont, "1.1.0");
            _createdFont.ReadFontAssetDefinition();
            UiKit.DefaultFont = _createdFont;
        }

        [OneTimeTearDown]
        public void RestoreFont()
        {
            UiKit.DefaultFont = _previousFont;
            if (_createdFont != null)
                Object.DestroyImmediate(_createdFont);
            if (_createdMaterial != null)
                Object.DestroyImmediate(_createdMaterial);
            if (_createdAtlas != null)
                Object.DestroyImmediate(_createdAtlas);
        }

        [SetUp]
        public void SetUp()
        {
            _existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
            _store = new MemoryForceStore();
            _root = new GameObject("BuilderViewTests", typeof(RectTransform));
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1600f, 900f);
            _builder = ForceBuilder.Create(_root.transform, BuilderTestData.Catalog,
                BuilderTestData.Scenario, _store);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null && eventSystem != _existingEventSystem)
                Object.DestroyImmediate(eventSystem.gameObject);
        }

        [Test]
        public void RealCatalogBuildsAllControlsAndInformationCards()
        {
            TOETemplate toe = BuilderTestData.Catalog.GetToe(_builder.Current.Toe);
            Assert.That(_builder.IsBuilt, Is.True);
            Assert.That(_builder.FactionDropdown.options.Count, Is.EqualTo(2));
            Assert.That(_builder.CompanyDropdowns.Count, Is.EqualTo(toe.CompanySlots.Count));
            Assert.That(_builder.AttachmentButtons.Count, Is.EqualTo(_builder.Controller.AllowedAttachments().Count));
            Assert.That(_builder.OrganicToggles.Count, Is.EqualTo(toe.Organic.Count));
            Assert.That(_builder.LoadoutDropdowns, Is.Not.Empty);
            Assert.That(_builder.WaveDropdowns, Is.Not.Empty);
            Assert.That(_builder.EntryDropdown.options.Count,
                Is.EqualTo(BuilderTestData.Scenario.Sides["blue"].EntryPoints.Count));
            Assert.That(_builder.BudgetLabel.text, Does.Contain(_builder.Report.Total.ToString()));
            Assert.That(_builder.StartButton.interactable, Is.EqualTo(_builder.Report.IsValid()));

            _builder.ShowPlatoon("us_tank_plt", "tusk");
            StringAssert.Contains("TUSK", _builder.InfoPanel.CurrentText);
            StringAssert.Contains("장갑 정면", _builder.InfoPanel.CurrentText);
            _builder.ShowCompany("us_mech_co");
            StringAssert.Contains("기계화", _builder.InfoPanel.CurrentText);
            StringAssert.Contains("주 장비", _builder.InfoPanel.CurrentText);
        }

        [Test]
        public void DropdownAndPublicActionsRefreshLiveView()
        {
            _builder.FactionDropdown.value = 1;
            Assert.That(_builder.Current.Faction, Is.EqualTo("RU"));
            Assert.That(_builder.Current.Toe, Is.EqualTo("ru_btg_bmp"));
            Assert.That(_builder.EntryDropdown.options.Count,
                Is.EqualTo(BuilderTestData.Scenario.Sides["red"].EntryPoints.Count));
            Assert.That(_builder.EntryDropdown.value, Is.EqualTo(1));

            _builder.NameInput.text = "러시아 편성";
            Assert.That(_builder.Current.Name, Is.EqualTo("러시아 편성"));
            Assert.That(_builder.UseMinimalForce(), Is.True);
            Assert.That(_builder.ArtilleryValue.text, Is.EqualTo("0"));
            Assert.That(_builder.CasValue.text, Is.EqualTo("0"));
            Assert.That(_builder.SetArtilleryMissions(2), Is.True);
            Assert.That(_builder.ArtilleryValue.text, Is.EqualTo("2"));
            Assert.That(_builder.BudgetLabel.text, Does.Contain(_builder.Report.Total.ToString()));
            Assert.That(_builder.SetCompany("co_4", "ru_tank_co"), Is.True);
            Assert.That(_builder.Current.GetCompany("co_4"), Is.Not.Null);
            Assert.That(_builder.CompanyDropdowns.Count, Is.EqualTo(4));
        }

        [Test]
        public void ButtonsSaveLoadAndGuardStartCallbacks()
        {
            ForceSpec started = null;
            int starts = 0;
            int backs = 0;
            _builder.StartRequested += spec => { started = spec; ++starts; };
            _builder.BackRequested += () => ++backs;
            _builder.StartButton.onClick.Invoke();
            _builder.BackButton.onClick.Invoke();
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(backs, Is.EqualTo(1));
            Assert.That(started, Is.Not.SameAs(_builder.Current));
            Assert.That(started.ToJson(), Is.EqualTo(_builder.Current.ToJson()));

            _builder.NameInput.text = "UI 저장";
            _builder.SaveButton.onClick.Invoke();
            Assert.That(_store.ListSaved().Count, Is.EqualTo(1));
            Assert.That(_builder.LoadButton.interactable, Is.True);
            Assert.That(_builder.LoadDropdown.interactable, Is.True);
            string path = _store.ListSaved().Single();
            _builder.SelectFaction("RU");
            Assert.That(_builder.LoadSaved(path), Is.True);
            Assert.That(_builder.Current.Faction, Is.EqualTo("US"));

            _builder.SetArtilleryMissions(6);
            Assert.That(_builder.StartButton.interactable, Is.False);
            Assert.That(_builder.RequestStart(), Is.False);
            Assert.That(starts, Is.EqualTo(1));
        }
    }
}
