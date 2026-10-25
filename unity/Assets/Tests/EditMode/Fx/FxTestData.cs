using UnityEngine;

namespace Echelon.Unity.Presentation.Fx.Tests
{
    internal static class FxTestData
    {
        internal static FxCaps Caps(int smoke = 512, int fire = 256, int wrecks = 4, int scorch = 4, int lights = 6)
        {
            return new FxCaps
            {
                SmokeParticles = smoke,
                FireParticles = fire,
                BurningWrecks = wrecks,
                ScorchMarks = scorch,
                FlashLights = lights,
                SmokeScreenDensity = 1.0f,
                WreckSmokeScale = 1.0f
            };
        }

        internal static FxEvent Event(string type, int tick, Vector3 position)
        {
            return new FxEvent { Type = type, Tick = tick, Position = position };
        }

        internal static FxEvent Shot(string weapon, bool hit)
        {
            return new FxEvent
            {
                Type = FxEventType.Shot,
                Tick = 0,
                SourceId = 1,
                TargetId = 2,
                SourcePosition = Vector3.zero,
                TargetPosition = new Vector3(165.0f, 0.0f, 0.0f),
                Weapon = weapon,
                Hit = hit,
                SourceVisible = true,
                TargetVisible = true,
                SourceSide = "blue"
            };
        }
    }
}
