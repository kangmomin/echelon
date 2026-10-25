using System;
using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class CombatRulesTests
    {
        [Test]
        public void HitFacingUsesTargetFrontSideRearAndTop()
        {
            Element target = new Element
            {
                Pos = new SimVec2(1000.0, 1000.0),
                Heading = 0.0
            };
            Assert.That(CombatSystem.HitFacing(new SimVec2(1000.0, 2000.0), target, false), Is.EqualTo("front"));
            Assert.That(CombatSystem.HitFacing(new SimVec2(2000.0, 1000.0), target, false), Is.EqualTo("side"));
            Assert.That(CombatSystem.HitFacing(new SimVec2(1000.0, 0.0), target, false), Is.EqualTo("rear"));
            Assert.That(CombatSystem.HitFacing(new SimVec2(1000.0, 2000.0), target, true), Is.EqualTo("top"));
        }

        [Test]
        public void PenetrationClassesMatchRealArmorAndWeaponData()
        {
            DataCatalog catalog = CombatTestSupport.Catalog;
            ElementDef m1 = catalog.GetElement("us_m1a2_sepv3")!;
            WeaponDef t90Gun = catalog.GetWeapon("2a46m_apfsds")!;
            WeaponDef javelin = catalog.GetWeapon("javelin")!;
            WeaponDef coax = catalog.GetWeapon("pkt_coax")!;
            Assert.That(CombatSystem.ClassifyPenetration(t90Gun.PenetrationAt(2000.0), m1.GetArmor("front")),
                Is.EqualTo("bounce"));
            Assert.That(CombatSystem.ClassifyPenetration(t90Gun.PenetrationAt(2000.0), m1.GetArmor("side")),
                Is.EqualTo("overmatch"));
            Assert.That(CombatSystem.ClassifyPenetration(javelin.PenetrationAt(1500.0), m1.GetArmor("top")),
                Is.EqualTo("overmatch"));
            Assert.That(CombatSystem.ClassifyPenetration(coax.PenetrationAt(300.0), m1.GetArmor("rear")),
                Is.EqualTo("bounce"));
            Assert.That(CombatSystem.ClassifyPenetration(100.0, 100.0), Is.EqualTo("marginal"));
            Assert.That(CombatSystem.ClassifyPenetration(111.0, 100.0), Is.EqualTo("overmatch"));
            Assert.That(CombatSystem.ClassifyPenetration(110.0, 100.0), Is.EqualTo("marginal"));
            Assert.That(CombatSystem.ClassifyPenetration(89.0, 100.0), Is.EqualTo("bounce"));
        }

        [Test]
        public void LongRangeAccuracyFallsLinearlyBetweenOneAndThreeKilometers()
        {
            Assert.That(CombatSystem.LongRangeFactor(500.0), Is.EqualTo(1.0));
            Assert.That(CombatSystem.LongRangeFactor(1000.0), Is.EqualTo(1.0));
            Assert.That(CombatSystem.LongRangeFactor(2000.0), Is.EqualTo(0.7).Within(0.000001));
            Assert.That(CombatSystem.LongRangeFactor(3000.0), Is.EqualTo(0.4).Within(0.000001));
            Assert.That(CombatSystem.LongRangeFactor(4000.0), Is.EqualTo(0.4).Within(0.000001));
        }

        [Test]
        public void HitChanceAppliesSkillMovementSuppressionReformAndBounding()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon blue = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Platoon red = CombatTestSupport.Platoon(rig.Battle, "ru_tank_plt", "red",
                new SimVec2(1000.0, 2000.0), Math.PI);
            Element shooter = blue.Elements[0];
            Element target = red.Elements[0];
            WeaponDef weapon = CombatTestSupport.Catalog.GetWeapon("2a46m_apfsds")!;
            const long tick = 1000;
            double baseline = rig.Combat.HitChance(shooter, weapon, target, 1000.0, tick);

            blue.Skill = "veteran";
            Assert.That(rig.Combat.HitChance(shooter, weapon, target, 1000.0, tick), Is.GreaterThan(baseline));
            blue.Skill = "regular";
            shooter.Velocity = new SimVec2(5.0, 0.0);
            Assert.That(rig.Combat.HitChance(shooter, weapon, target, 1000.0, tick),
                Is.EqualTo(baseline * CombatSystem.MovingShooter).Within(0.000001));
            shooter.Velocity = SimVec2.Zero;
            target.Velocity = new SimVec2(5.0, 0.0);
            Assert.That(rig.Combat.HitChance(shooter, weapon, target, 1000.0, tick),
                Is.EqualTo(baseline * CombatSystem.MovingTarget).Within(0.000001));
            target.Velocity = SimVec2.Zero;
            shooter.Suppression = 50.0;
            Assert.That(rig.Combat.HitChance(shooter, weapon, target, 1000.0, tick),
                Is.EqualTo(baseline * CombatSystem.SuppressedAccuracy).Within(0.000001));
            shooter.Suppression = 0.0;
            blue.ReformUntilTick = tick + 1;
            Assert.That(rig.Combat.HitChance(shooter, weapon, target, 1000.0, tick),
                Is.EqualTo(baseline * CombatSystem.ReformingAccuracy).Within(0.000001));
            blue.ReformUntilTick = -1;
            blue.Sop.Technique = "bounding";
            Assert.That(rig.Combat.HitChance(shooter, weapon, target, 1000.0, tick),
                Is.EqualTo(baseline * CombatSystem.BoundingBonus).Within(0.000001));
        }

        [Test]
        public void HitChanceAppliesTerrainAndEngineerCoverButNotToAir()
        {
            MapRect forest = new MapRect(1500.0, 500.0, 1000.0, 1000.0);
            using CombatTestRig openRig = CombatTestSupport.Create();
            using CombatTestRig forestRig = CombatTestSupport.Create(1, CombatTestSupport.FlatMap(forest));
            Platoon openBlue = CombatTestSupport.Platoon(openRig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Platoon openRed = CombatTestSupport.Platoon(openRig.Battle, "ru_tank_plt", "red",
                new SimVec2(2000.0, 1000.0));
            Platoon forestBlue = CombatTestSupport.Platoon(forestRig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Platoon forestRed = CombatTestSupport.Platoon(forestRig.Battle, "ru_tank_plt", "red",
                new SimVec2(2000.0, 1000.0));
            WeaponDef weapon = CombatTestSupport.Catalog.GetWeapon("2a46m_apfsds")!;
            double open = openRig.Combat.HitChance(openBlue.Elements[0], weapon, openRed.Elements[0], 1000.0, 0);
            double covered = forestRig.Combat.HitChance(forestBlue.Elements[0], weapon, forestRed.Elements[0], 1000.0, 0);
            Assert.That(covered, Is.EqualTo(open * 0.75).Within(0.000001));
            openRig.Battle.Engineers = new CombatPositionEffects { Factor = 0.6 };
            Assert.That(openRig.Combat.HitChance(openBlue.Elements[0], weapon, openRed.Elements[0], 1000.0, 0),
                Is.EqualTo(open * 0.6).Within(0.000001));

            Platoon air = CombatTestSupport.Platoon(openRig.Battle, "ru_aviation_sec", "red",
                new SimVec2(2000.0, 1000.0));
            double airProbability = openRig.Combat.HitChance(openBlue.Elements[0], weapon, air.Elements[0], 1000.0, 0);
            Assert.That(airProbability, Is.GreaterThan(0.0));
        }

        [Test]
        public void TargetChoiceHonorsContactMemoryRoeAmmoAndSmokeLos()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon blue = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Platoon red = CombatTestSupport.Platoon(rig.Battle, "ru_tank_plt", "red",
                new SimVec2(1000.0, 1800.0), Math.PI);
            Element shooter = blue.Elements[0];
            Element target = red.Elements[0];
            shooter.Pos = new SimVec2(1000.0, 1000.0);
            target.Pos = new SimVec2(1000.0, 1800.0);
            for (int i = 1; i < blue.Elements.Count; ++i)
            {
                blue.Elements[i].Pos = new SimVec2(100.0 + i * 20.0, 100.0);
            }

            blue.Contacts[target.Id] = 100;
            Assert.That(rig.Combat.ChooseTarget(shooter, 100), Is.EqualTo(target.Id));
            blue.Sop.Roe = "hold_fire";
            Assert.That(rig.Combat.ChooseTarget(shooter, 100), Is.EqualTo(-1));
            blue.LastHitTick = 50;
            Assert.That(rig.Combat.ChooseTarget(shooter, 100), Is.EqualTo(target.Id));
            blue.Sop.Roe = "fire_at_will";
            blue.Contacts[target.Id] = -1;
            Assert.That(rig.Combat.ChooseTarget(shooter, 100), Is.EqualTo(-1));
            blue.Contacts[target.Id] = 100;
            rig.Battle.Smokes.Add(new Los.Smoke(new SimVec2(1000.0, 1400.0), 100.0, 200));
            Assert.That(rig.Combat.ChooseTarget(shooter, 100), Is.EqualTo(-1));
            rig.Battle.Smokes.Clear();
            foreach (string weaponId in shooter.Def.Weapons)
            {
                shooter.Ammo[weaponId] = 0;
            }

            Assert.That(rig.Combat.ChooseTarget(shooter, 100), Is.EqualTo(-1));
        }

        [Test]
        public void SamePlatoonVehiclesMaskOnlyInsideLineRangeAndWidth()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon blue = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Platoon red = CombatTestSupport.Platoon(rig.Battle, "ru_tank_plt", "red",
                new SimVec2(1000.0, 1400.0), Math.PI);
            Element shooter = blue.Elements[0];
            Element blocker = blue.Elements[1];
            Element target = red.Elements[0];
            shooter.Pos = new SimVec2(1000.0, 1000.0);
            blocker.Pos = new SimVec2(1000.0, 1050.0);
            target.Pos = new SimVec2(1000.0, 1400.0);
            Assert.That(rig.Combat.IsMasked(shooter, target), Is.True);
            blocker.Pos = new SimVec2(1050.0, 1000.0);
            Assert.That(rig.Combat.IsMasked(shooter, target), Is.False);
            blocker.Pos = new SimVec2(1000.0, 1300.0);
            target.Pos = new SimVec2(1000.0, 1600.0);
            Assert.That(rig.Combat.IsMasked(shooter, target), Is.False);
            blocker.Pos = new SimVec2(1008.0, 1050.0);
            target.Pos = new SimVec2(1000.0, 1400.0);
            Assert.That(rig.Combat.IsMasked(shooter, target), Is.False);
        }

        [Test]
        public void FireConsumesBurstSetsSuppressedReloadAndStopsWithoutAmmo()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon blue = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Platoon red = CombatTestSupport.Platoon(rig.Battle, "ru_tank_plt", "red",
                new SimVec2(1000.0, 1500.0), Math.PI);
            Element shooter = blue.Elements[0];
            Element target = red.Elements[0];
            shooter.Pos = new SimVec2(1000.0, 1000.0);
            target.Pos = new SimVec2(1000.0, 1500.0);
            shooter.Suppression = 50.0;
            for (int i = 0; i < blue.Elements.Count; ++i)
            {
                Element element = blue.Elements[i];
                if (i > 0)
                {
                    element.Pos = new SimVec2(100.0 + i * 20.0, 100.0);
                }

                foreach (string weaponId in element.Def.Weapons)
                {
                    element.Ammo[weaponId] = 0;
                }
            }

            WeaponDef weapon = CombatTestSupport.Catalog.GetWeapon(shooter.Def.Weapons[0])!;
            shooter.Ammo[weapon.Id] = 10;
            shooter.Reload[weapon.Id] = 0.0;
            blue.Contacts[target.Id] = 0;
            shooter.TargetId = target.Id;
            rig.Battle.World.Events.Drain();
            rig.Combat.Step(rig.Battle.World);

            Assert.That(rig.Combat.ShotsFired, Is.EqualTo(1));
            Assert.That(shooter.Ammo[weapon.Id], Is.EqualTo(10 - weapon.Burst));
            Assert.That(shooter.Reload[weapon.Id], Is.EqualTo(weapon.IntervalSec() * 1.25)
                .Within(0.000001));
            Assert.That(shooter.LastFiredTick, Is.EqualTo(0));
            List<SimEvent> events = rig.Battle.World.Events.Drain();
            SimEvent? shot = null;
            int shotCount = 0;
            foreach (SimEvent simEvent in events)
            {
                if (simEvent.Type == "shot")
                {
                    shot = simEvent;
                    ++shotCount;
                }
            }

            Assert.That(shotCount, Is.EqualTo(1));
            Assert.That(shot, Is.Not.Null);
            Assert.That(shot!.Payload["from"], Is.EqualTo(shooter.Id));
            Assert.That(shot.Payload["to"], Is.EqualTo(target.Id));
            Assert.That(shot.Payload["weapon"], Is.EqualTo(weapon.WeaponType));
            Assert.That(target.LastHitTick, Is.EqualTo(0));
            Assert.That(red.LastHitTick, Is.EqualTo(0));
            Assert.That(red.LastContactTick, Is.EqualTo(0));

            rig.Combat.Step(rig.Battle.World);
            Assert.That(rig.Combat.ShotsFired, Is.EqualTo(1), "reload must prevent an immediate second shot");
            shooter.Reload[weapon.Id] = 0.0;
            shooter.Ammo[weapon.Id] = 0;
            rig.Combat.Step(rig.Battle.World);
            Assert.That(rig.Combat.ShotsFired, Is.EqualTo(1), "an empty weapon cannot fire");
        }

        [Test]
        public void DamageAndDestroyEmitEventsAndUpdateMoraleAndStatistics()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon blue = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Platoon red = CombatTestSupport.Platoon(rig.Battle, "ru_tank_plt", "red",
                new SimVec2(1000.0, 1800.0), Math.PI);
            Element killer = blue.Elements[0];
            Element target = red.Elements[0];
            blue.Morale = 97.0;
            rig.Battle.World.Events.Drain();

            rig.Combat.Damage(target, null, 12);
            Assert.That(target.State, Is.EqualTo(ElementState.DAMAGED));
            Assert.That(target.DamageKind, Is.EqualTo("mobility").Or.EqualTo("firepower"));
            Assert.That(red.Morale, Is.EqualTo(95.0).Within(0.000001));
            SimEvent damaged = rig.Battle.World.Events.Drain()[0];
            Assert.That(damaged.Type, Is.EqualTo("element_damaged"));
            Assert.That(damaged.Payload["kind"], Is.EqualTo(target.DamageKind));

            target.State = ElementState.OK;
            rig.Combat.Destroy(target, killer, 25);
            Assert.That(target.State, Is.EqualTo(ElementState.DESTROYED));
            Assert.That(target.DestroyedTick, Is.EqualTo(25));
            Assert.That(target.KilledBy, Is.EqualTo(killer.Id));
            Assert.That(killer.Kills, Is.EqualTo(1));
            Assert.That(rig.Combat.Kills, Is.EqualTo(1));
            Assert.That(red.Morale, Is.EqualTo(80.0).Within(0.000001));
            Assert.That(blue.Morale, Is.EqualTo(100.0).Within(0.000001));
            SimEvent destroyed = rig.Battle.World.Events.Drain()[0];
            Assert.That(destroyed.Type, Is.EqualTo("element_destroyed"));
            Assert.That(destroyed.Payload["killer"], Is.EqualTo(killer.Id));
            Assert.That(destroyed.Payload["unit"], Is.EqualTo(red.Id));
            rig.Combat.Destroy(target, killer, 26);
            Assert.That(rig.Combat.Kills, Is.EqualTo(1), "destroy is idempotent");
        }
    }
}
