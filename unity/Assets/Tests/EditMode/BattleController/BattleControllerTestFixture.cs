using System;
using Echelon.Core;
using Echelon.Unity.App;
using Echelon.Unity.Platform;
using Echelon.Unity.UI.Common;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using CoreBattle = Echelon.Core.Battle;

namespace Echelon.Unity.Tests.BattleComposition
{
    public abstract class BattleControllerTestFixture
    {
        private EventSystem[] _existingEventSystems;
        private TMP_FontAsset _previousFont;
        private TMP_FontAsset _createdFont;
        private Material _createdMaterial;
        private Texture2D _createdAtlas;

        protected DataCatalog Catalog { get; private set; }
        protected CoreBattle Battle { get; set; }
        protected GameObject Root { get; private set; }
        protected BattleController Controller { get; private set; }

        [SetUp]
        public void SetUpCatalog()
        {
            _existingEventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            _previousFont = UiKit.DefaultFont;
            CreateTestFont();
            Catalog = DataLoader.LoadCatalog(new UnityCatalogSource());
            Assert.IsTrue(Catalog.IsValid(), string.Join("\n", Catalog.Errors));
        }

        [TearDown]
        public void TearDownBattle()
        {
            if (Controller != null)
                Controller.Dispose();
            else
            {
                if (Root != null)
                    UnityEngine.Object.DestroyImmediate(Root);
                Battle?.Dispose();
            }
            Controller = null;
            Root = null;
            Battle = null;

            EventSystem[] current = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (EventSystem eventSystem in current)
            {
                if (!Array.Exists(_existingEventSystems, existing => existing == eventSystem))
                    UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
            }
            UiKit.DefaultFont = _previousFont;
            if (_createdFont != null)
                UnityEngine.Object.DestroyImmediate(_createdFont);
            if (_createdMaterial != null)
                UnityEngine.Object.DestroyImmediate(_createdMaterial);
            if (_createdAtlas != null)
                UnityEngine.Object.DestroyImmediate(_createdAtlas);
            _createdFont = null;
            _createdMaterial = null;
            _createdAtlas = null;
        }

        private void CreateTestFont()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Regular.otf");
            Assert.NotNull(source);
            Assert.AreEqual(FontEngineError.Success, FontEngine.LoadFontFace(source, 64));
            _createdFont = ScriptableObject.CreateInstance<TMP_FontAsset>();
            _createdFont.name = "BattleControllerTestFont";
            _createdFont.faceInfo = FontEngine.GetFaceInfo();
            _createdFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            _createdAtlas = new Texture2D(512, 512, TextureFormat.Alpha8, false);
            _createdFont.atlasTextures = new[] { _createdAtlas };
            Shader shader = Shader.Find("TextMeshPro/Distance Field");
            Assert.NotNull(shader);
            _createdMaterial = new Material(shader) { mainTexture = _createdAtlas };
            _createdFont.material = _createdMaterial;
            typeof(TMP_Asset).GetField(
                "m_Version",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_createdFont, "1.1.0");
            _createdFont.ReadFontAssetDefinition();
            UiKit.DefaultFont = _createdFont;
        }

        protected CoreBattle CreateTutorialBattle()
        {
            return BattleSetup.Tutorial(Catalog);
        }

        protected void Compose(CoreBattle battle = null)
        {
            Battle = battle ?? CreateTutorialBattle();
            Root = new GameObject("BattleControllerTest");
            Controller = Root.AddComponent<BattleController>();
            Controller.Setup(Battle);
        }

        protected Platoon EnemyPlatoon()
        {
            return Battle.GetTaskForce(CoreBattle.EnemyOf(Battle.PlayerSide)).Organic[0];
        }

        protected static void Reveal(CoreBattle battle, Platoon platoon, SimVec2 position)
        {
            BattleSetup.RevealPlatoon(battle, platoon, position, Math.PI);
        }
    }
}
