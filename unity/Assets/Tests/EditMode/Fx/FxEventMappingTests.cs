using NUnit.Framework;
using UnityEngine;

namespace Echelon.Unity.Presentation.Fx.Tests
{
    public sealed class FxEventMappingTests
    {
        [Test]
        public void ShotMapsMuzzleDurationAndFactionColor()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps());
            effects.Enqueue(FxTestData.Shot(FxWeapon.KE, true));
            effects.Update(0.0f, 0);

            Assert.AreEqual(1, effects.ProjectileCount);
            FxProjectileState projectile = effects.GetProjectile(0);
            Assert.AreEqual(FxWeapon.KE, projectile.Weapon);
            Assert.AreEqual(0.1f, projectile.Duration, 0.0001f);
            Assert.AreEqual(0.25f, projectile.TailFraction);
            Assert.AreEqual(5.0f, projectile.Start.x, 0.0001f);
            Assert.AreEqual(2.9f, projectile.Start.y, 0.0001f);
            Assert.AreEqual(1.8f, projectile.End.y, 0.0001f);
            Assert.AreEqual(1.0f, projectile.Color.r);
            Assert.AreEqual(0.85f, projectile.Color.g, 0.0001f);
            Assert.AreEqual(0.45f, projectile.Color.b, 0.0001f);
        }

        [Test]
        public void MissUsesPresentationOffsetThenDirtImpact()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps());
            effects.Enqueue(FxTestData.Shot(FxWeapon.KE, false));
            effects.Update(0.0f, 0);
            Vector3 target = effects.GetProjectile(0).End;
            int muzzleSmoke = effects.SmokeParticleCount;

            Assert.Greater(target.x, 165.0f);
            effects.Update(0.1f, 1);
            Assert.AreEqual(0, effects.ProjectileCount);
            Assert.Greater(effects.SmokeParticleCount, muzzleSmoke);
        }

        [Test]
        public void HiddenShooterProducesOnlyImpact()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps());
            FxEvent shot = FxTestData.Shot(FxWeapon.KE, true);
            shot.SourceVisible = false;
            effects.Enqueue(shot);
            effects.Update(0.0f, 0);

            Assert.AreEqual(0, effects.ProjectileCount);
            Assert.AreEqual(11, effects.FireParticleCount);
            Assert.AreEqual(2, effects.SmokeParticleCount);
            Assert.AreEqual(0, effects.FlashLightSlotCount);
        }

        [Test]
        public void CompletelyHiddenShotIsIgnored()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps());
            FxEvent shot = FxTestData.Shot(FxWeapon.MG, false);
            shot.SourceVisible = false;
            shot.TargetVisible = false;
            effects.Enqueue(shot);
            effects.Update(0.0f, 0);

            Assert.AreEqual(0, effects.ProjectileCount);
            Assert.AreEqual(0, effects.SmokeParticleCount);
            Assert.AreEqual(0, effects.FireParticleCount);
        }

        [Test]
        public void ShellMapsAssetScaleAndSmokeVariant()
        {
            FxEffectSystem mortar = new FxEffectSystem(FxTestData.Caps());
            FxEvent shell = FxTestData.Event(FxEventType.Shell, 0, Vector3.zero);
            shell.Asset = 0;
            mortar.Enqueue(shell);
            mortar.Update(0.0f, 0);
            Assert.AreEqual(9.0f * 0.85f, mortar.GetScorchSlot(0).Radius, 0.0001f);

            FxEffectSystem smoke = new FxEffectSystem(FxTestData.Caps());
            FxEvent smokeShell = FxTestData.Event(FxEventType.Shell, 0, Vector3.zero);
            smokeShell.Smoke = true;
            smoke.Enqueue(smokeShell);
            smoke.Update(0.0f, 0);
            Assert.AreEqual(0, smoke.ActiveScorchCount);
            Assert.AreEqual(1, smoke.FireParticleCount);
            Assert.AreEqual(6, smoke.SmokeParticleCount);
        }

        [Test]
        public void SamUsesQuadraticPathAndSmokeTrail()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps());
            FxEvent sam = FxTestData.Event(FxEventType.SamLaunch, 0, Vector3.zero);
            sam.SourceId = 7;
            sam.TargetPosition = new Vector3(100.0f, 0.0f, 100.0f);
            effects.Enqueue(sam);
            effects.Update(0.0f, 0);
            effects.Update(0.5f, 1);

            FxProjectileState projectile = effects.GetProjectile(0);
            Assert.AreEqual(FxWeapon.SAM, projectile.Weapon);
            Assert.AreEqual(2.5f, projectile.Duration);
            Assert.IsFalse(projectile.ShowImpact);
            Assert.AreEqual(4.0f, projectile.Head.x, 0.0001f);
            Assert.AreEqual(4.0f, projectile.Head.z, 0.0001f);
            Assert.AreEqual(20.88f, projectile.Head.y, 0.0001f);
            Assert.Greater(effects.SmokeParticleCount, 4);
            effects.Update(0.01f, 1);
            Assert.Greater(effects.FireParticleCount, 0);
        }

        [Test]
        public void AtgmAddsArcAboveLinearTrajectory()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps());
            FxEvent atgm = FxTestData.Shot(FxWeapon.ATGM, true);
            effects.Enqueue(atgm);
            effects.Update(0.0f, 0);
            effects.Update(0.1f, 1);

            FxProjectileState projectile = effects.GetProjectile(0);
            float k = projectile.Age / projectile.Duration;
            float linearHeight = Vector3.Lerp(projectile.Start, projectile.End, k).y;
            Assert.AreEqual(FxWeapon.ATGM, projectile.Weapon);
            Assert.AreEqual(0.05f, projectile.TailFraction);
            Assert.Greater(projectile.Head.y, linearHeight);
            Assert.Greater(effects.SmokeParticleCount, 5);
        }

        [Test]
        public void IndirectFireHonorsVisibilityAndMortarScale()
        {
            FxEffectSystem hidden = new FxEffectSystem(FxTestData.Caps());
            FxEvent hiddenEvent = FxTestData.Event(FxEventType.IndirectFire, 0, Vector3.zero);
            hiddenEvent.SourceId = 1;
            hiddenEvent.SourceVisible = false;
            hidden.Enqueue(hiddenEvent);
            hidden.Update(0.0f, 0);
            Assert.AreEqual(0, hidden.FireParticleCount);

            FxEffectSystem mortar = new FxEffectSystem(FxTestData.Caps());
            FxEvent mortarEvent = FxTestData.Event(FxEventType.IndirectFire, 0, Vector3.zero);
            mortarEvent.SourceId = 1;
            mortarEvent.SourceIsMortar = true;
            mortar.Enqueue(mortarEvent);
            mortar.Update(0.0f, 0);
            Assert.AreEqual(1, mortar.FireParticleCount);
            Assert.AreEqual(10, mortar.SmokeParticleCount);
            Assert.AreEqual(1, mortar.FlashLightSlotCount);
        }

        [Test]
        public void StaleEventsOnlyLeaveAgedWreck()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps());
            effects.Enqueue(FxTestData.Event(FxEventType.ElementDestroyed, 69, Vector3.zero));
            effects.Update(0.0f, 100);

            Assert.AreEqual(1, effects.BurningWreckCount);
            Assert.AreEqual(3.1f, effects.GetWreck(0).Age, 0.0001f);
            Assert.AreEqual(0, effects.FireParticleCount);
            Assert.AreEqual(0, effects.SmokeParticleCount);

            FxEffectSystem staleShell = new FxEffectSystem(FxTestData.Caps());
            staleShell.Enqueue(FxTestData.Event(FxEventType.Shell, 69, Vector3.zero));
            staleShell.Update(0.0f, 100);
            Assert.AreEqual(0, staleShell.FireParticleCount);
            Assert.AreEqual(0, staleShell.ActiveScorchCount);
        }

        [Test]
        public void SmokeCirclesSeedAndRemovePersistentEmitters()
        {
            FxEffectSystem effects = new FxEffectSystem(FxTestData.Caps());
            effects.SetSmokeCircles(new[]
            {
                new FxSmoke { Id = 42, Position = Vector3.zero, Radius = 20.0f, UntilTick = 1000 }
            });
            effects.Update(0.01f, 0);

            Assert.AreEqual(1, effects.SmokeEmitterCount);
            Assert.AreEqual(12, effects.SmokeParticleCount);
            effects.SetSmokeCircles(null);
            effects.Update(0.01f, 1);
            Assert.AreEqual(0, effects.SmokeEmitterCount);
            Assert.Greater(effects.SmokeParticleCount, 0);
        }

        [Test]
        public void CasAndHelicopterExplosionsUseAirPositions()
        {
            FxEffectSystem cas = new FxEffectSystem(FxTestData.Caps());
            cas.Enqueue(FxTestData.Event(FxEventType.CasShotDown, 0, new Vector3(0.0f, 7.0f, 0.0f)));
            cas.Update(0.0f, 0);
            for (int i = 0; i < cas.FireParticleCount; i++)
                Assert.Greater(cas.GetParticle(FxParticleLayer.Fire, i).Position.y, 400.0f);

            FxEffectSystem helicopter = new FxEffectSystem(FxTestData.Caps());
            FxEvent destroyed = FxTestData.Event(FxEventType.ElementDestroyed, 0, Vector3.zero);
            destroyed.DestroyedIsHelicopter = true;
            destroyed.DestroyedHasPlatoon = true;
            destroyed.DestroyedAltitude = 90.0f;
            helicopter.Enqueue(destroyed);
            helicopter.Update(0.0f, 0);
            Assert.AreEqual(1, helicopter.BurningWreckCount);
            Assert.AreEqual(2.0f, helicopter.GetWreck(0).Position.y, 0.0001f);
            bool foundAirFire = false;
            for (int i = 0; i < helicopter.FireParticleCount; i++)
                foundAirFire |= helicopter.GetParticle(FxParticleLayer.Fire, i).Position.y > 80.0f;
            Assert.IsTrue(foundAirFire);
        }
    }
}
