using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Presentation.Fx.Tests
{
    public sealed class FxRepeatabilityTests
    {
        [Test]
        public void IdenticalInputsRepeatParticlesPoolsAndRandomState()
        {
            FxEffectSystem first = new FxEffectSystem(FxCaps.Web);
            FxEffectSystem second = new FxEffectSystem(FxCaps.Web);
            first.GroundHeight = SampleGround;
            second.GroundHeight = SampleGround;
            Populate(first);
            Populate(second);

            for (int step = 0; step < 90; step++)
            {
                float dt = step == 0 ? 0.0f : 1.0f / 30.0f;
                first.Update(dt, step);
                second.Update(dt, step);
                AssertStateEqual(first, second);
            }
        }

        [Test]
        public void ZeroDeltaDoesNotAdvanceEventDrivenState()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps());
            effects.Enqueue(FxTestData.Shot(FxWeapon.ATGM, true));
            effects.Update(0.0f, 0);
            float projectileAge = effects.GetProjectile(0).Age;
            uint randomState = effects.PresentationRandomState;

            effects.Update(0.0f, 1);
            Assert.AreEqual(projectileAge, effects.GetProjectile(0).Age);
            Assert.AreEqual(randomState, effects.PresentationRandomState);
            for (int i = 0; i < effects.SmokeParticleCount; i++)
                Assert.AreEqual(0.0f, effects.GetParticle(FxParticleLayer.Smoke, i).Age);
        }

        private static void Populate(FxEffectSystem effects)
        {
            effects.Enqueue(FxTestData.Shot(FxWeapon.KE, false));
            effects.Enqueue(FxTestData.Shot(FxWeapon.ATGM, true));
            FxEvent sam = FxTestData.Event(FxEventType.SamLaunch, 0, new Vector3(20.0f, 0.0f, 0.0f));
            sam.SourceId = 9;
            sam.TargetPosition = new Vector3(200.0f, 0.0f, 100.0f);
            effects.Enqueue(sam);
            effects.Enqueue(FxTestData.Event(FxEventType.ElementDestroyed, 0, new Vector3(30.0f, 0.0f, 0.0f)));
            effects.Enqueue(FxTestData.Event(FxEventType.MineStrike, 0, new Vector3(40.0f, 0.0f, 0.0f)));
            effects.Enqueue(FxTestData.Event(FxEventType.CasShotDown, 0, new Vector3(50.0f, 0.0f, 0.0f)));
            FxEvent shell = FxTestData.Event(FxEventType.Shell, 0, new Vector3(60.0f, 0.0f, 0.0f));
            shell.Asset = 2;
            effects.Enqueue(shell);
            effects.SetSmokeCircles(new[]
            {
                new FxSmoke { Id = 4, Position = new Vector3(70.0f, 0.0f, 0.0f), Radius = 35.0f, UntilTick = 800 },
                new FxSmoke { Id = 8, Position = new Vector3(90.0f, 0.0f, 0.0f), Radius = 20.0f, UntilTick = 500 }
            });
        }

        private static float SampleGround(Vector3 position)
        {
            return Mathf.Sin(position.x * 0.013f) * 3.0f + Mathf.Cos(position.z * 0.017f) * 2.0f;
        }

        private static void AssertStateEqual(FxEffectSystem expected, FxEffectSystem actual)
        {
            Assert.AreEqual(expected.PresentationRandomState, actual.PresentationRandomState);
            Assert.AreEqual(expected.DroppedParticleCount, actual.DroppedParticleCount);
            Assert.AreEqual(expected.SmokeParticleCount, actual.SmokeParticleCount);
            Assert.AreEqual(expected.FireParticleCount, actual.FireParticleCount);
            Assert.AreEqual(expected.ProjectileCount, actual.ProjectileCount);
            Assert.AreEqual(expected.BurningWreckCount, actual.BurningWreckCount);
            Assert.AreEqual(expected.SmokeEmitterCount, actual.SmokeEmitterCount);
            Assert.AreEqual(expected.ActiveScorchCount, actual.ActiveScorchCount);
            Assert.AreEqual(expected.FlashLightSlotCount, actual.FlashLightSlotCount);
            AssertParticlesEqual(expected, actual, FxParticleLayer.Smoke);
            AssertParticlesEqual(expected, actual, FxParticleLayer.Fire);

            for (int i = 0; i < expected.ProjectileCount; i++)
            {
                FxProjectileState a = expected.GetProjectile(i);
                FxProjectileState b = actual.GetProjectile(i);
                AssertVectorEqual(a.Start, b.Start);
                AssertVectorEqual(a.End, b.End);
                AssertVectorEqual(a.Head, b.Head);
                AssertVectorEqual(a.Tail, b.Tail);
                Assert.AreEqual(a.Age, b.Age);
                Assert.AreEqual(a.Duration, b.Duration);
                Assert.AreEqual(a.TrailAccumulator, b.TrailAccumulator);
                Assert.AreEqual(a.Weapon, b.Weapon);
            }
            for (int i = 0; i < expected.BurningWreckCount; i++)
            {
                AssertVectorEqual(expected.GetWreck(i).Position, actual.GetWreck(i).Position);
                Assert.AreEqual(expected.GetWreck(i).Age, actual.GetWreck(i).Age);
            }
            for (int i = 0; i < expected.ScorchSlotCount; i++)
            {
                FxScorchState a = expected.GetScorchSlot(i);
                FxScorchState b = actual.GetScorchSlot(i);
                AssertVectorEqual(a.Position, b.Position);
                Assert.AreEqual(a.Radius, b.Radius);
                Assert.AreEqual(a.Rotation, b.Rotation);
                Assert.AreEqual(a.Seed, b.Seed);
                Assert.AreEqual(a.Age, b.Age);
            }
            for (int i = 0; i < expected.FlashLightSlotCount; i++)
            {
                FxFlashState a = expected.GetFlashLight(i);
                FxFlashState b = actual.GetFlashLight(i);
                Assert.AreEqual(a.Active, b.Active);
                AssertVectorEqual(a.Position, b.Position);
                Assert.AreEqual(a.Intensity, b.Intensity);
                Assert.AreEqual(a.Range, b.Range);
            }
        }

        private static void AssertParticlesEqual(FxEffectSystem expected, FxEffectSystem actual, FxParticleLayer layer)
        {
            int count = layer == FxParticleLayer.Smoke ? expected.SmokeParticleCount : expected.FireParticleCount;
            for (int i = 0; i < count; i++)
            {
                FxParticleState a = expected.GetParticle(layer, i);
                FxParticleState b = actual.GetParticle(layer, i);
                AssertVectorEqual(a.Position, b.Position);
                AssertVectorEqual(a.Velocity, b.Velocity);
                Assert.AreEqual(a.Color.r, b.Color.r);
                Assert.AreEqual(a.Color.g, b.Color.g);
                Assert.AreEqual(a.Color.b, b.Color.b);
                Assert.AreEqual(a.Color.a, b.Color.a);
                Assert.AreEqual(a.Size, b.Size);
                Assert.AreEqual(a.Rotation, b.Rotation);
                Assert.AreEqual(a.Seed, b.Seed);
                Assert.AreEqual(a.Age, b.Age);
                Assert.AreEqual(a.Life, b.Life);
            }
        }

        private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x);
            Assert.AreEqual(expected.y, actual.y);
            Assert.AreEqual(expected.z, actual.z);
        }
    }
}
