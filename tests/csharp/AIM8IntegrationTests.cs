using System;
using System.Reflection;
using Echelon.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    [Category("Integration")]
    public sealed class AIM8IntegrationTests
    {
        [Test]
        public void HelicopterFliesStraightOverRiver()
        {
            using Battle battle = TestFixture.Battle(3, true);
            Platoon helicopter = TestFixture.Platoon(battle, "us_aviation_sec", "blue", new SimVec2(2000, 700));
            Assert.That(OrderExecutor.PathMobility(helicopter), Is.EqualTo(Terrain.MobilityAir));
            TestFixture.Move(battle, helicopter, new SimVec2(2000, 2000));
            battle.RunSeconds(40);
            Element lead = helicopter.Elements[0];
            Assert.That(lead.Pos.Y, Is.GreaterThan(1800));
            Assert.That(Math.Abs(lead.Pos.X - 2000), Is.LessThan(250));
            Assert.That(lead.AltitudeM(), Is.GreaterThan(0));
        }

        [Test]
        public void AltitudeToggleChangesHeight()
        {
            using Battle battle = Flat();
            Platoon helicopter = TestFixture.Platoon(battle, "ru_aviation_sec", "red", new SimVec2(2000, 2000), Math.PI);
            Element element = helicopter.Elements[0];
            Assert.That(element.AltitudeM(), Is.EqualTo(Element.AltitudeLowM));
            Order order = Order.Make(OrderType.SET_SOP, helicopter.Id,
                new JObject { ["sop"] = new JObject { ["altitude"] = "high" } });
            battle.IssueOrder(order);
            Assert.That(element.AltitudeM(), Is.EqualTo(Element.AltitudeHighM));
        }

        [Test]
        public void TankCannotShootHelicopterButAaCan()
        {
            using Battle battle = Flat();
            Platoon tank = TestFixture.Platoon(battle, "us_tank_plt", "blue", new SimVec2(2000, 1000));
            Platoon aa = TestFixture.Platoon(battle, "us_ada_section", "blue", new SimVec2(1800, 1000));
            Platoon helicopter = TestFixture.Platoon(battle, "ru_aviation_sec", "red", new SimVec2(2000, 2500), Math.PI);
            Element tankElement = tank.Elements[0];
            Element helicopterElement = helicopter.Elements[0];
            Element aaElement = aa.Elements[0];
            double distance = tankElement.Pos.DistanceTo(helicopterElement.Pos);
            foreach (string weaponId in tankElement.Def.Weapons)
            {
                Assert.That(Usable(battle, tankElement, battle.Catalog.GetWeapon(weaponId), helicopterElement, distance),
                    Is.False, weaponId);
            }
            WeaponDef stinger = battle.Catalog.GetWeapon("stinger")!;
            Assert.That(Usable(battle, aaElement, stinger, helicopterElement,
                aaElement.Pos.DistanceTo(helicopterElement.Pos)), Is.True);
            Assert.That(Usable(battle, aaElement, stinger, tankElement, 100), Is.False);
        }

        [Test]
        public void AttackHelicoptersKillTanksFromStandoff()
        {
            using Battle battle = Flat();
            Platoon helicopter = TestFixture.Platoon(battle, "us_aviation_sec", "blue", new SimVec2(2000, 800));
            Platoon tanks = TestFixture.Platoon(battle, "ru_tank_plt", "red", new SimVec2(2000, 3400), Math.PI);
            battle.RunSeconds(120);
            int dead = 0;
            foreach (Element element in tanks.Elements)
            {
                if (!element.IsAlive())
                {
                    ++dead;
                }
            }
            Assert.That(dead, Is.GreaterThan(0));
            foreach (Element element in helicopter.Elements)
            {
                Assert.That(element.IsAlive(), Is.True);
            }
        }

        [Test]
        public void ShotDownHelicopterFallsToGround()
        {
            using Battle battle = Flat();
            Platoon helicopter = TestFixture.Platoon(battle, "ru_aviation_sec", "red", new SimVec2(2000, 2000), Math.PI);
            Element element = helicopter.Elements[0];
            battle.Combat.Destroy(element, null, battle.Tick());
            Assert.That(element.AltitudeM(), Is.Zero);
        }

        [Test]
        public void EngineersLayMinefieldThatHurtsEnemyVehicles()
        {
            using Battle battle = Flat();
            Platoon engineer = TestFixture.Platoon(battle, "us_engineer_plt", "blue", new SimVec2(2000, 1500));
            Assert.That(engineer.MinesLeft, Is.EqualTo(EngineerSystem.MinefieldsPerPlatoon));
            TestFixture.Move(battle, engineer, new SimVec2(2000, 1700), OrderType.LAY_MINES);
            battle.RunSeconds(90);
            EngineerSystem engineers = Engineers(battle);
            Assert.That(engineers.Minefields.Count, Is.EqualTo(1));
            Assert.That(engineer.MinesLeft, Is.EqualTo(EngineerSystem.MinefieldsPerPlatoon - 1));
            EngineerSystem.Minefield minefield = engineers.Minefields[0];
            Assert.That(minefield.Known.TryGetValue("red", out bool known) && known, Is.False);
            TestFixture.Teleport(battle, engineer, new SimVec2(600, 600));
            Platoon red = TestFixture.Platoon(battle, "ru_tank_plt", "red", minefield.Pos + new SimVec2(0, 400), Math.PI);
            red.Sop.Roe = "hold_fire";
            foreach (Element element in red.Elements)
            {
                element.Ammo.Clear();
            }
            TestFixture.Move(battle, red, minefield.Pos - new SimVec2(0, 400));
            battle.RunSeconds(90);
            Assert.That(engineers.MineStrikes, Is.GreaterThan(0));
            int hurt = 0;
            foreach (Element element in red.Elements)
            {
                if (element.State != ElementState.OK)
                {
                    ++hurt;
                }
            }
            Assert.That(hurt, Is.GreaterThan(0));
            bool redKnows = minefield.Known.TryGetValue("red", out bool detected) && detected;
            Assert.That(redKnows || !engineers.Minefields.Contains(minefield), Is.True);
        }

        [Test]
        public void EngineersClearMinefield()
        {
            using Battle battle = Flat();
            EngineerSystem.Minefield minefield = Engineers(battle).LayMinefield("red", new SimVec2(2000, 2000));
            Platoon engineer = TestFixture.Platoon(battle, "us_engineer_plt", "blue", new SimVec2(2000, 1600));
            TestFixture.Move(battle, engineer, new SimVec2(2000, 1880), OrderType.CLEAR_MINES);
            battle.RunSeconds(120);
            Assert.That(Engineers(battle).Minefields.Contains(minefield), Is.False);
        }

        [Test]
        public void OnlyEngineersCanLayMines()
        {
            using Battle battle = Flat();
            Platoon tanks = TestFixture.Platoon(battle, "us_tank_plt", "blue", new SimVec2(2000, 1000));
            Order order = TestFixture.Move(battle, tanks, new SimVec2(2000, 1700), OrderType.LAY_MINES);
            Assert.That(order.State, Is.EqualTo(OrderState.CANCELLED));
        }

        [Test]
        public void FortifiedPositionReducesHits()
        {
            using Battle battle = Flat();
            Platoon engineer = TestFixture.Platoon(battle, "us_engineer_plt", "blue", new SimVec2(2000, 1500));
            TestFixture.Move(battle, engineer, new SimVec2(2000, 1550), OrderType.FORTIFY);
            battle.RunSeconds(90);
            Assert.That(Engineers(battle).Positions.Count, Is.EqualTo(1));
            Platoon infantry = TestFixture.Platoon(battle, "us_light_inf_plt", "blue", new SimVec2(2000, 1500));
            battle.RunSeconds(5);
            Element squad = infantry.Elements[0];
            Assert.That(Engineers(battle).PositionFactor(squad), Is.EqualTo(EngineerSystem.PositionFactorValue).Within(0.001));
            TestFixture.Teleport(battle, infantry, new SimVec2(3000, 3000));
            battle.RunSeconds(5);
            Assert.That(Engineers(battle).PositionFactor(squad), Is.EqualTo(1).Within(0.001));
        }

        [Test]
        public void FootInfantryPlatoonWalks()
        {
            using Battle battle = Flat();
            Platoon infantry = TestFixture.Platoon(battle, "us_light_inf_plt", "blue", new SimVec2(2000, 1000));
            Assert.That(infantry.Mounted, Is.False);
            TestFixture.Move(battle, infantry, new SimVec2(2000, 1200));
            battle.RunSeconds(120);
            Assert.That(infantry.Elements[0].Pos.Y, Is.GreaterThan(1100));
        }

        [Test]
        public void WheeledCompanyIsFasterOnRoadThanMechanized()
        {
            DataCatalog catalog = TestFixture.Catalog();
            Assert.That(catalog.GetElement("us_m1126")!.SpeedRoadKmh,
                Is.GreaterThan(catalog.GetElement("us_m2a3")!.SpeedRoadKmh * 1.4));
            Assert.That(catalog.GetElement("ru_btr82a")!.SpeedRoadKmh,
                Is.GreaterThan(catalog.GetElement("ru_bmp3")!.SpeedRoadKmh * 1.3));
        }

        private static Battle Flat()
        {
            return TestFixture.BattleOn(TestFixture.FlatMap(), 3, true);
        }

        private static EngineerSystem Engineers(Battle battle)
        {
            return (EngineerSystem)battle.Engineers!;
        }

        private static bool Usable(Battle battle, Element shooter, WeaponDef? weapon, Element target, double distance)
        {
            MethodInfo method = typeof(CombatSystem).GetMethod("Usable", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (bool)method.Invoke(battle.Combat, new object?[] { shooter, weapon, target, distance })!;
        }
    }
}
