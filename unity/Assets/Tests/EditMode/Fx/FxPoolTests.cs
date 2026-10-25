using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Presentation.Fx.Tests
{
    public sealed class FxPoolTests
    {
        [Test]
        public void QualityPresetsMatchGodotCaps()
        {
            FxCaps desktop = FxCaps.Desktop;
            Assert.AreEqual(3200, desktop.SmokeParticles);
            Assert.AreEqual(1400, desktop.FireParticles);
            Assert.AreEqual(30, desktop.BurningWrecks);
            Assert.AreEqual(160, desktop.ScorchMarks);
            Assert.AreEqual(6, desktop.FlashLights);
            Assert.AreEqual(1.0f, desktop.SmokeScreenDensity);
            Assert.AreEqual(1.0f, desktop.WreckSmokeScale);

            FxCaps web = FxCaps.Web;
            Assert.AreEqual(1100, web.SmokeParticles);
            Assert.AreEqual(500, web.FireParticles);
            Assert.AreEqual(12, web.BurningWrecks);
            Assert.AreEqual(60, web.ScorchMarks);
            Assert.AreEqual(0, web.FlashLights);
            Assert.AreEqual(0.6f, web.SmokeScreenDensity);
            Assert.AreEqual(0.5f, web.WreckSmokeScale);
        }

        [Test]
        public void ParticlePoolsDropOverflowWithoutGrowing()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps(smoke: 5, fire: 3));
            effects.Enqueue(FxTestData.Event(FxEventType.Shell, 0, Vector3.zero));
            effects.Update(0.0f, 0);

            Assert.AreEqual(5, effects.SmokeParticleCount);
            Assert.AreEqual(3, effects.FireParticleCount);
            Assert.Greater(effects.DroppedParticleCount, 0);
        }

        [Test]
        public void WreckPoolKeepsNewestEvents()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps(wrecks: 2));
            for (int i = 0; i < 3; i++)
                effects.Enqueue(FxTestData.Event(FxEventType.ElementDestroyed, 0, new Vector3(i, 0.0f, 0.0f)));
            effects.Update(0.0f, 0);

            Assert.AreEqual(2, effects.BurningWreckCount);
            Assert.AreEqual(1.0f, effects.GetWreck(0).Position.x);
            Assert.AreEqual(2.0f, effects.GetWreck(1).Position.x);
        }

        [Test]
        public void ScorchRingOverwritesOldestSlot()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps(scorch: 2));
            for (int i = 1; i <= 3; i++)
                effects.Enqueue(FxTestData.Event(FxEventType.MineStrike, 0, new Vector3(i, 0.0f, 0.0f)));
            effects.Update(0.0f, 0);

            Assert.AreEqual(2, effects.ActiveScorchCount);
            Assert.AreEqual(3.0f, effects.GetScorchSlot(0).Position.x);
            Assert.AreEqual(2.0f, effects.GetScorchSlot(1).Position.x);
        }

        [Test]
        public void FlashLightsAreCappedAndWebDisablesThem()
        {
            FxEffectSystem desktop = new FxEffectSystem(FxCaps.Desktop);
            FxEffectSystem web = new FxEffectSystem(FxCaps.Web);
            for (int i = 0; i < 8; i++)
            {
                FxEvent fxEvent = FxTestData.Event(FxEventType.MineStrike, 0, new Vector3(i * 10.0f, 0.0f, 0.0f));
                desktop.Enqueue(fxEvent);
                web.Enqueue(fxEvent);
            }
            desktop.Update(0.0f, 0);
            web.Update(0.0f, 0);

            Assert.AreEqual(6, desktop.FlashLightSlotCount);
            Assert.AreEqual(6, desktop.ActiveFlashLightCount);
            Assert.AreEqual(0, web.FlashLightSlotCount);
            Assert.AreEqual(0, web.ActiveFlashLightCount);
        }

        [Test]
        public void ScorchFadesAndExpiresAtGodotLifetime()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps());
            effects.Enqueue(FxTestData.Event(FxEventType.MineStrike, 0, Vector3.zero));
            effects.Update(0.0f, 0);
            effects.Update(144.0f, 0);
            Assert.AreEqual(1.0f, effects.GetScorchSlot(0).Alpha);
            effects.Update(48.0f, 0);
            Assert.AreEqual(0.5f, effects.GetScorchSlot(0).Alpha, 0.0001f);
            effects.Update(48.0f, 0);
            Assert.AreEqual(0, effects.ActiveScorchCount);
        }
    }
}
