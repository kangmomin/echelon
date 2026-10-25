using System.IO;
using Echelon.Unity.Presentation.Terrain;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Echelon.Unity.Editor
{
    public static class EchelonAssetSetup
    {
        private const string SourceFont = "Assets/Fonts/NotoSansKR-Regular.otf";
        private const string FontAsset = "Assets/Resources/Fonts/NotoSansKR SDF.asset";

        [MenuItem("ECHELON/Setup Assets")]
        public static void SetupAll()
        {
            if (!Directory.Exists("Assets/TextMesh Pro/Resources"))
            {
                string editorRoot = Path.GetDirectoryName(EditorApplication.applicationPath);
                string essentials = Path.Combine(editorRoot, "Data/Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
                if (!File.Exists(essentials))
                    throw new FileNotFoundException("Missing TMP Essential Resources package", essentials);
                AssetDatabase.ImportPackage(essentials, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            Directory.CreateDirectory("Assets/Resources/Fonts");
            const string settingsPath = "Assets/Resources/TMP Settings.asset";
            const string importedSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            TMP_Settings importedSettings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(importedSettingsPath);
            if (importedSettings == null)
                throw new FileNotFoundException("Missing TMP settings", importedSettingsPath);
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TMP_Settings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            EditorUtility.CopySerialized(importedSettings, settings);
            TMP_Settings.LoadDefaultSettings();
            Font font = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
            if (font == null)
                throw new FileNotFoundException("Missing Korean source font", SourceFont);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAsset);
            if (fontAsset == null || fontAsset.atlasTextures == null ||
                fontAsset.atlasTextures.Length == 0 || fontAsset.atlasTextures[0] == null || fontAsset.material == null)
            {
                if (fontAsset != null)
                    AssetDatabase.DeleteAsset(FontAsset);
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    font, 64, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                if (fontAsset == null || fontAsset.atlasTextures[0] == null || fontAsset.material == null)
                    throw new InvalidDataException("Could not initialize Korean TMP font resources");
                AssetDatabase.CreateAsset(fontAsset, FontAsset);
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            fontAsset.name = "NotoSansKR SDF";
            TMP_Settings.defaultFontAsset = fontAsset;
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(fontAsset);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true)
            };
            EnsureShaderLibrary();
            AssetDatabase.SaveAssets();
            Debug.Log("[unity-setup] Korean font, terrain shaders and Boot scene ready");
        }

        private static void EnsureShaderLibrary()
        {
            const string path = "Assets/Resources/TerrainShaders.asset";
            TerrainShaderLibrary library = AssetDatabase.LoadAssetAtPath<TerrainShaderLibrary>(path);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<TerrainShaderLibrary>();
                AssetDatabase.CreateAsset(library, path);
            }
            library.Ground = LoadTerrainShader("TerrainGround");
            library.Road = LoadTerrainShader("TerrainRoad");
            library.Water = LoadTerrainShader("TerrainWater");
            library.Instances = LoadTerrainShader("TerrainInstances");
            library.AdditionalShaders = new[]
            {
                Shader.Find("Universal Render Pipeline/Lit"),
                Shader.Find("Universal Render Pipeline/Particles/Unlit"),
                Shader.Find("Universal Render Pipeline/Unlit"),
                Shader.Find("Skybox/Procedural"),
                Shader.Find("Unlit/Transparent"),
                Shader.Find("Sprites/Default")
            };
            foreach (Shader shader in library.AdditionalShaders)
            {
                if (shader == null)
                    throw new InvalidDataException("A built-in runtime shader could not be found");
            }
            EditorUtility.SetDirty(library);
        }

        private static Shader LoadTerrainShader(string name)
        {
            string path = "Assets/Shaders/Terrain/" + name + ".shader";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
                throw new FileNotFoundException("Missing terrain shader", path);
            return shader;
        }
    }
}
