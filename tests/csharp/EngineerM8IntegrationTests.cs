using System;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    [Category("M8")]
    public sealed class EngineerM8IntegrationTests
    {
        [Test]
        public void EngineersLayMinefieldThatHurtsEnemyVehiclesAndAddsThreat()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.Create(3, true);
            Platoon engineers = FiresSupportTestFixture.Platoon(rig, "us_engineer_plt", "blue",
                new SimVec2(2000.0, 1500.0));
            Assert.That(engineers.MinesLeft, Is.EqualTo(EngineerSystem.MinefieldsPerPlatoon));
            FiresSupportTestFixture.Move(rig, engineers, new SimVec2(2000.0, 1700.0), OrderType.LAY_MINES);
            rig.Battle.RunSeconds(90.0);
            Assert.That(rig.Engineers.Minefields.Count, Is.EqualTo(1));
            Assert.That(engineers.MinesLeft, Is.EqualTo(EngineerSystem.MinefieldsPerPlatoon - 1));
            EngineerSystem.Minefield minefield = rig.Engineers.Minefields[0];
            Assert.That(Known(minefield, "red"), Is.False);
            FiresSupportTestFixture.Teleport(rig, engineers, new SimVec2(600.0, 600.0));
            Platoon red = FiresSupportTestFixture.Platoon(rig, "ru_tank_plt", "red",
                minefield.Pos + new SimVec2(0.0, 400.0), Math.PI);
            red.Sop.Roe = "hold_fire";
            foreach (Element element in red.Elements)
            {
                element.Ammo.Clear();
            }

            FiresSupportTestFixture.Move(rig, red, minefield.Pos - new SimVec2(0.0, 400.0));
            rig.Battle.RunSeconds(90.0);
            Assert.That(rig.Engineers.MineStrikes, Is.GreaterThan(0));
            int hurt = 0;
            foreach (Element element in red.Elements)
            {
                if (element.State != ElementState.OK)
                {
                    ++hurt;
                }
            }

            Assert.That(hurt, Is.GreaterThan(0));
            Assert.That(Known(minefield, "red") || !rig.Engineers.Minefields.Contains(minefield), Is.True);
            Assert.That(rig.Battle.Pathfinder.ThreatAt("red", minefield.Pos), Is.GreaterThan(0.0f));
        }

        [Test]
        public void EngineersClearMinefield()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.Create(3, true);
            EngineerSystem.Minefield minefield = rig.Engineers.LayMinefield("red", new SimVec2(2000.0, 2000.0));
            Platoon engineers = FiresSupportTestFixture.Platoon(rig, "us_engineer_plt", "blue",
                new SimVec2(2000.0, 1600.0));
            FiresSupportTestFixture.Move(rig, engineers, new SimVec2(2000.0, 1880.0), OrderType.CLEAR_MINES);
            rig.Battle.RunSeconds(120.0);
            Assert.That(rig.Engineers.Minefields, Does.Not.Contain(minefield),
                $"charges={minefield.Charges} task={engineers.EngTask} progress={engineers.EngProgress} " +
                $"pos={engineers.Guide.Pos} posture={engineers.Posture} mission={engineers.Mission?.State.ToString() ?? "none"}");
        }

        [Test]
        public void OnlyEngineersCanLayMines()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.Create(3, true);
            Platoon tanks = FiresSupportTestFixture.Platoon(rig, "us_tank_plt", "blue", new SimVec2(2000.0, 1500.0));
            Order order = FiresSupportTestFixture.Move(rig, tanks, new SimVec2(2000.0, 1700.0), OrderType.LAY_MINES);
            Assert.That(order.State, Is.EqualTo(OrderState.CANCELLED));
        }

        [Test]
        public void FortifiedPositionReducesHitsOnlyInsideAndAfterSettling()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.Create(3, true);
            Platoon engineers = FiresSupportTestFixture.Platoon(rig, "us_engineer_plt", "blue",
                new SimVec2(2000.0, 1500.0));
            FiresSupportTestFixture.Move(rig, engineers, new SimVec2(2000.0, 1550.0), OrderType.FORTIFY);
            rig.Battle.RunSeconds(90.0);
            Assert.That(rig.Engineers.Positions.Count, Is.EqualTo(1));
            Platoon infantry = FiresSupportTestFixture.Platoon(rig, "us_light_inf_plt", "blue",
                new SimVec2(2000.0, 1550.0));
            Element squad = infantry.Elements[0];
            squad.StationaryTicks = EngineerSystem.PositionSettleTicks - 1;
            Assert.That(rig.Engineers.PositionFactor(squad), Is.EqualTo(1.0));
            rig.Battle.RunSeconds(5.0);
            Assert.That(rig.Engineers.PositionFactor(squad), Is.EqualTo(EngineerSystem.PositionFactorValue).Within(0.001));
            FiresSupportTestFixture.Teleport(rig, infantry, new SimVec2(3000.0, 3000.0));
            rig.Battle.RunSeconds(5.0);
            Assert.That(rig.Engineers.PositionFactor(squad), Is.EqualTo(1.0).Within(0.001));
        }

        [Test]
        public void HelicoptersIgnoreMinesAndFortificationAtEitherAltitude()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.Create(3, true);
            SimVec2 position = new SimVec2(2000.0, 2000.0);
            EngineerSystem.Minefield minefield = rig.Engineers.LayMinefield("blue", position);
            Platoon helicopters = FiresSupportTestFixture.Platoon(rig, "ru_aviation_sec", "red", position, Math.PI);
            rig.Battle.RebuildSpatial();
            foreach (Element element in helicopters.Elements)
            {
                element.Velocity = new SimVec2(0.0, 10.0);
                element.StationaryTicks = 100;
                Assert.That(element.AltitudeM(), Is.EqualTo(Element.AltitudeLowM));
            }

            long rngState = rig.Battle.World.Rng.GetState();
            rig.Engineers.Step(rig.Battle.World);
            Assert.That(minefield.Charges, Is.EqualTo(EngineerSystem.MinefieldCharges));
            Assert.That(rig.Battle.World.Rng.GetState(), Is.EqualTo(rngState));
            rig.Engineers.Positions.Add(new EngineerSystem.FieldPosition { Pos = position, Side = "red" });
            foreach (Element element in helicopters.Elements)
            {
                Assert.That(rig.Engineers.PositionFactor(element), Is.EqualTo(1.0));
            }

            helicopters.Sop.Altitude = "high";
            Assert.That(helicopters.Elements[0].AltitudeM(), Is.EqualTo(Element.AltitudeHighM));
            rig.Engineers.Step(rig.Battle.World);
            Assert.That(minefield.Charges, Is.EqualTo(EngineerSystem.MinefieldCharges));
            Assert.That(rig.Battle.World.Rng.GetState(), Is.EqualTo(rngState));
            Assert.That(rig.Engineers.PositionFactor(helicopters.Elements[0]), Is.EqualTo(1.0));
        }

        private static bool Known(EngineerSystem.Minefield minefield, string side)
        {
            return minefield.Known.TryGetValue(side, out bool known) && known;
        }
    }
}
