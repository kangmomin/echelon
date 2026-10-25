using UnityEngine;
using UnityEngine.Rendering;

namespace Echelon.Unity.App
{
    /// <summary>전장의 하늘·주변광·안개·태양을 설정하고, 전투가 끝나면 원래 RenderSettings 로 되돌린다.</summary>
    internal sealed class BattleEnvironment
    {
        private static readonly Color SkyTint = new Color(0.36f, 0.52f, 0.72f);
        private static readonly Color SkyGround = new Color(0.25f, 0.27f, 0.23f);
        private static readonly Color FlatAmbient = new Color(0.62f, 0.66f, 0.66f);
        private static readonly Color FogColor = new Color(0.66f, 0.72f, 0.78f);
        private static readonly Color SunColor = new Color(1f, 0.95f, 0.86f);

        private readonly Material _previousSkybox;
        private readonly AmbientMode _previousAmbientMode;
        private readonly Color _previousAmbientLight;
        private readonly Color _previousAmbientSky;
        private readonly Color _previousAmbientEquator;
        private readonly Color _previousAmbientGround;
        private readonly float _previousAmbientIntensity;
        private readonly bool _previousFog;
        private readonly Color _previousFogColor;
        private readonly FogMode _previousFogMode;
        private readonly float _previousFogDensity;
        private Material _skybox;

        private BattleEnvironment()
        {
            _previousSkybox = RenderSettings.skybox;
            _previousAmbientMode = RenderSettings.ambientMode;
            _previousAmbientLight = RenderSettings.ambientLight;
            _previousAmbientSky = RenderSettings.ambientSkyColor;
            _previousAmbientEquator = RenderSettings.ambientEquatorColor;
            _previousAmbientGround = RenderSettings.ambientGroundColor;
            _previousAmbientIntensity = RenderSettings.ambientIntensity;
            _previousFog = RenderSettings.fog;
            _previousFogColor = RenderSettings.fogColor;
            _previousFogMode = RenderSettings.fogMode;
            _previousFogDensity = RenderSettings.fogDensity;
        }

        public static Color BackgroundColor => SkyTint;

        /// <summary>현재 RenderSettings 를 기억한 뒤 전장 환경을 적용하고, 태양을 parent 아래에 만든다.</summary>
        public static BattleEnvironment Apply(Transform parent)
        {
            var environment = new BattleEnvironment();
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                environment._skybox = new Material(skyShader) { name = "BattleSky" };
                if (environment._skybox.HasProperty("_SkyTint"))
                    environment._skybox.SetColor("_SkyTint", SkyTint);
                if (environment._skybox.HasProperty("_GroundColor"))
                    environment._skybox.SetColor("_GroundColor", SkyGround);
                RenderSettings.skybox = environment._skybox;
                RenderSettings.ambientMode = AmbientMode.Skybox;
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = FlatAmbient;
            }
            RenderSettings.ambientIntensity = 0.75f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00006f;

            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(parent, false);
            sunObject.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            return environment;
        }

        public void Restore()
        {
            RenderSettings.skybox = _previousSkybox;
            RenderSettings.ambientMode = _previousAmbientMode;
            RenderSettings.ambientLight = _previousAmbientLight;
            RenderSettings.ambientSkyColor = _previousAmbientSky;
            RenderSettings.ambientEquatorColor = _previousAmbientEquator;
            RenderSettings.ambientGroundColor = _previousAmbientGround;
            RenderSettings.ambientIntensity = _previousAmbientIntensity;
            RenderSettings.fog = _previousFog;
            RenderSettings.fogColor = _previousFogColor;
            RenderSettings.fogMode = _previousFogMode;
            RenderSettings.fogDensity = _previousFogDensity;
            if (_skybox == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(_skybox);
            else
                Object.DestroyImmediate(_skybox);
            _skybox = null;
        }
    }
}
