using System;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class CombatResolutionTests
    {
        [Test]
        public void InfantryHitConsumesHitThenCasualtyRngEvenForWholeNumberDamage()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon shooters = CombatTestSupport.Platoon(rig.Battle, "ru_tank_plt", "red",
                new SimVec2(1000.0, 1000.0), Math.PI);
            Platoon infantry = CombatTestSupport.Platoon(rig.Battle, "us_light_inf_plt", "blue",
                new SimVec2(1000.0, 1300.0));
            Element shooter = shooters.Elements[0];
            Element target = infantry.Elements[0];
            shooter.Pos = new SimVec2(1000.0, 1000.0);
            target.Pos = new SimVec2(1000.0, 1300.0);
            for (int i = 1; i < shooters.Elements.Count; ++i)
            {
                shooters.Elements[i].Pos = new SimVec2(100.0 + i * 20.0, 100.0);
            }

            foreach (Element element in shooters.Elements)
            {
                foreach (string weaponId in element.Def.Weapons)
                {
                    element.Ammo[weaponId] = 0;
                }
            }

            WeaponDef weapon = CombatTestSupport.Catalog.GetWeapon("pkt_coax")!;
            shooter.Ammo[weapon.Id] = 10;
            shooter.Reload[weapon.Id] = 0.0;
            shooters.Contacts[target.Id] = 0;
            shooter.TargetId = target.Id;
            const double distance = 300.0;
            double hitProbability = rig.Combat.HitChance(shooter, weapon, target, distance, 0);
            long seed = -1;
            for (long candidate = 1; candidate < 1000; ++candidate)
            {
                if (new SimRng(candidate).Chance(hitProbability))
                {
                    seed = candidate;
                    break;
                }
            }

            Assert.That(seed, Is.GreaterThan(0));
            rig.Battle.World.Rng.Seed = seed;
            long before = rig.Battle.World.Rng.GetState();
            SimRng expected = new SimRng(0);
            expected.SetState(before);
            Assert.That(expected.Chance(hitProbability), Is.True);
            double expectedCasualties = weapon.Damage * CombatSystem.InfantryCasualtyPerDamage;
            double fraction = expectedCasualties - Math.Floor(expectedCasualties);
            Assert.That(fraction, Is.Zero);
            expected.Chance(fraction);
            double hpBefore = target.Hp;

            rig.Combat.Step(rig.Battle.World);

            Assert.That(rig.Combat.ShotsFired, Is.EqualTo(1));
            Assert.That(rig.Combat.Hits, Is.EqualTo(1));
            Assert.That(target.Hp, Is.EqualTo(hpBefore - expectedCasualties).Within(0.000001));
            Assert.That(target.Suppression, Is.EqualTo(weapon.Suppression).Within(0.000001));
            Assert.That(rig.Battle.World.Rng.GetState(), Is.EqualTo(expected.GetState()),
                "infantry resolution must draw hit first and casualty fraction second");
        }

        [Test]
        public void IndirectImpactMarksShellingSuppressesOuterRingAndSkipsFriendlyElements()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon victim = CombatTestSupport.Platoon(rig.Battle, "ru_tank_plt", "red",
                new SimVec2(1000.0, 1000.0), Math.PI);
            Platoon outerInfantry = CombatTestSupport.Platoon(rig.Battle, "ru_light_inf_plt", "red",
                new SimVec2(1075.0, 1000.0), Math.PI);
            Platoon friendly = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Element vehicle = victim.Elements[0];
            Element infantry = outerInfantry.Elements[0];
            Element friend = friendly.Elements[0];
            vehicle.Pos = new SimVec2(1000.0, 1000.0);
            infantry.Pos = new SimVec2(1075.0, 1000.0);
            friend.Pos = new SimVec2(1000.0, 1000.0);
            double infantryHp = infantry.Hp;
            rig.Battle.RebuildSpatial();
            WeaponDef weapon = CombatTestSupport.Catalog.GetWeapon("m795_155mm")!;
            SimVec2 impact = new SimVec2(1000.0, 1000.0);

            rig.Combat.IndirectImpact(impact, weapon, "blue", null, 42);

            Assert.That(vehicle.Suppression, Is.EqualTo(weapon.Suppression).Within(0.000001));
            Assert.That(vehicle.LastHitTick, Is.EqualTo(42));
            Assert.That(victim.LastHitTick, Is.EqualTo(42));
            Assert.That(victim.LastShelledTick, Is.EqualTo(42));
            Assert.That(victim.LastShellPos, Is.EqualTo(impact));
            Assert.That(infantry.Suppression, Is.EqualTo(weapon.Suppression * 0.25).Within(0.000001));
            Assert.That(infantry.Hp, Is.EqualTo(infantryHp), "outer-ring suppression has no casualty roll");
            Assert.That(outerInfantry.LastShelledTick, Is.EqualTo(42));
            Assert.That(friend.Suppression, Is.Zero);
            Assert.That(friend.LastHitTick, Is.LessThan(0));
        }
    }
}
