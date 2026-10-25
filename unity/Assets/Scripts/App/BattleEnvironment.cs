using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Echelon.Unity.App
{
    /// <summary>
    /// 전장의 하늘·주변광·안개·태양과 후처리(톤매핑·블룸·색 보정·비네트)를 설정하고,
    /// 전투가 끝나면 원래 RenderSettings 로 되돌린다.
    /// </summary>
    internal sealed class BattleEnvironment
    {
        private static readonly Color SkyTint = new Color(0.36f, 0.52f, 0.72f);
        private static readonly Color SkyGround = new Color(0.25f, 0.27f, 0.23f);
        private static readonly Color FlatAmbient = new Color(0.62f, 0.66f, 0.66f);
        private static readonly Color FogColor = new Color(0.66f, 0.72f, 0.78f);
        private static readonly Color SunColor = new Color(1f, 0.95f, 0.86f);
        private const float AmbientIntensity = 0.75f;

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
        private readonly SphericalHarmonicsL2 _previousAmbientProbe;
        private Material _skybox;
        private VolumeProfile _postProcess;

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
            _previousAmbientProbe = RenderSettings.ambientProbe;
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
            RenderSettings.ambientIntensity = AmbientIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00006f;
            // 플레이어는 하늘을 바꿔도 주변광 프로브를 다시 계산하지 않는다(편집기만 자동). 그대로 두면 주변광이 0이다.
            // DynamicGI.UpdateEnvironment 는 GPU 되읽기와 반사 큐브맵 재생성을 해 웹에서 그리기 오류를 냈다(D-050).
            // 주변광 색으로 프로브를 CPU 에서 바로 채운다.
            RenderSettings.ambientProbe = AmbientProbe();

            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(parent, false);
            sunObject.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            environment._postProcess = CreatePostProcess(parent);
            return environment;
        }

        /// <summary>
        /// 전장 전체에 거는 후처리. 중립 톤매핑으로 밝은 하늘·포연이 하얗게 날아가지 않게 하고, 블룸은 포구 섬광·화염에만
        /// 걸리도록 문턱을 1 가까이 둔다. 대비·채도는 지형 색이 흐릿해 보이지 않을 만큼만 올린다.
        /// </summary>
        private static VolumeProfile CreatePostProcess(Transform parent)
        {
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "BattlePostProcess";
            profile.hideFlags = HideFlags.DontSave;
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            Bloom bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);
            ColorAdjustments color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(0.1f);
            color.contrast.Override(12f);
            color.saturation.Override(8f);
            Vignette vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);

            var volumeObject = new GameObject("PostProcess");
            volumeObject.transform.SetParent(parent, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            return profile;
        }

        private static SphericalHarmonicsL2 AmbientProbe()
        {
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(FlatAmbient.linear * AmbientIntensity);
            return probe;
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
            RenderSettings.ambientProbe = _previousAmbientProbe;
            DestroyOwned(_skybox);
            _skybox = null;
            DestroyOwned(_postProcess);
            _postProcess = null;
        }

        private static void DestroyOwned(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
