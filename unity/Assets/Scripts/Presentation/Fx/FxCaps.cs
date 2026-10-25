using System;

namespace Echelon.Unity.Presentation.Fx
{
    public enum FxQuality
    {
        Auto,
        Desktop,
        Web
    }

    public struct FxCaps
    {
        public int SmokeParticles;
        public int FireParticles;
        public int BurningWrecks;
        public int ScorchMarks;
        public int FlashLights;
        public float SmokeScreenDensity;
        public float WreckSmokeScale;

        public static FxCaps Desktop
        {
            get
            {
                return new FxCaps
                {
                    SmokeParticles = 3200,
                    FireParticles = 1400,
                    BurningWrecks = 30,
                    ScorchMarks = 160,
                    FlashLights = 6,
                    SmokeScreenDensity = 1.0f,
                    WreckSmokeScale = 1.0f
                };
            }
        }

        public static FxCaps Web
        {
            get
            {
                return new FxCaps
                {
                    SmokeParticles = 1100,
                    FireParticles = 500,
                    BurningWrecks = 12,
                    ScorchMarks = 60,
                    FlashLights = 0,
                    SmokeScreenDensity = 0.6f,
                    WreckSmokeScale = 0.5f
                };
            }
        }

        public static FxCaps ForQuality(FxQuality quality)
        {
            if (quality == FxQuality.Desktop)
                return Desktop;
            if (quality == FxQuality.Web)
                return Web;
#if UNITY_WEBGL
            return Web;
#else
            return Desktop;
#endif
        }

        public void Validate()
        {
            if (SmokeParticles < 0) throw new ArgumentOutOfRangeException("SmokeParticles");
            if (FireParticles < 0) throw new ArgumentOutOfRangeException("FireParticles");
            if (BurningWrecks < 0) throw new ArgumentOutOfRangeException("BurningWrecks");
            if (ScorchMarks < 0) throw new ArgumentOutOfRangeException("ScorchMarks");
            if (FlashLights < 0) throw new ArgumentOutOfRangeException("FlashLights");
            if (SmokeScreenDensity < 0.0f || float.IsNaN(SmokeScreenDensity) || float.IsInfinity(SmokeScreenDensity))
                throw new ArgumentOutOfRangeException("SmokeScreenDensity");
            if (WreckSmokeScale < 0.0f || float.IsNaN(WreckSmokeScale) || float.IsInfinity(WreckSmokeScale))
                throw new ArgumentOutOfRangeException("WreckSmokeScale");
        }
    }
}
