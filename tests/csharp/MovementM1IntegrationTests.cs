using System;
using System.Collections.Generic;
using Echelon.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    [Category("M1")]
    public sealed class MovementM1IntegrationTests
    {
        private const double MaxStuckSec = 10.0;
        private static DataCatalog Catalog => DataTestAssets.Catalog;
        private readonly List<Battle> _battles = new List<Battle>();

        [TearDown]
        public void TearDown()
        {
            foreach (Battle b in _battles)
            {
                b.Dispose();
            }

            _battles.Clear();
        }

        [Test]
        public void PlatoonCrossesRiverRidgeAndForest()
        {
            Battle b = NewBattle();
            Platoon p = AddPlatoon(b, "us_tank_plt", "blue", new SimVec2(600.0, 300.0));
            SimVec2 goal = new SimVec2(1150.0, 2450.0);
            Order order = Move(b, p, goal);
            RunResult result = RunUntilArrived(b, p, 900.0);
            Assert.That(order.State, Is.EqualTo(OrderState.DONE), "order completion");
            Assert.That(p.Guide.Pos.DistanceTo(goal), Is.LessThan(5.0), "guide arrival");
            Assert.That(MaxSlotError(p), Is.LessThan(30.0), "all elements within 30 m of slots");
            Assert.That(result.StuckMax, Is.LessThan((int)Math.Round(MaxStuckSec * SimConst.TickRate)),
                "no element stuck for ten seconds");
            TestContext.Out.WriteLine("Platoon movement {0:F0} seconds", result.Ticks / 10.0);
        }

        [Test]
        public void LineFormationAlignsToFacing()
        {
            Battle b = NewBattle();
            Platoon p = AddPlatoon(b, "us_tank_plt", "blue", new SimVec2(2700.0, 3300.0));
            double facing = Math.PI / 2.0;
            Move(b, p, new SimVec2(3000.0, 3400.0), OrderType.DEFEND, facing,
                new JObject { ["formation"] = "line" });
            RunUntilArrived(b, p, 300.0);
            Assert.That(p.Formation!.Id, Is.EqualTo("line"));
            Assert.That(p.Posture, Is.EqualTo("defending"));
            foreach (Element e in p.FormationMembers())
            {
                SimVec2 local = FormationSolver.WorldToLocal(e.Pos, p.Guide.Pos, p.Guide.Heading);
                Assert.That(Math.Abs(local.Y), Is.LessThan(15.0), "line longitudinal deviation " + local);
                Assert.That(Math.Abs(SimMath.AngleDifference(e.Heading, facing)), Is.LessThan(0.35),
                    "element faces east");
            }
        }

        [Test]
        public void RoadMarchUsesColumn()
        {
            Battle b = NewBattle();
            Platoon p = AddPlatoon(b, "us_tank_plt", "blue", new SimVec2(3370.0, 60.0));
            Move(b, p, new SimVec2(3020.0, 680.0));
            RunResult result = RunUntilArrived(b, p, 300.0);
            Assert.That(result.ColumnSeen, Is.True, "road march switches to column");
        }

        [Test]
        public void CompanyCrossesBridgeInColumnAndReforms()
        {
            Battle b = NewBattle();
            Company c = AddCompany(b, "us_tank_co", "blue", new SimVec2(2700.0, 450.0));
            SimVec2 goal = new SimVec2(2350.0, 1900.0);
            Order order = Move(b, c, goal);
            RunResult result = RunUntilArrived(b, c, 1200.0);
            Assert.That(order.State, Is.EqualTo(OrderState.DONE), "company order completion");
            Assert.That(result.ColumnSeen, Is.True, "bridge causes column");
            foreach (Platoon p in c.Platoons)
            {
                Assert.That(p.Guide.Pos.DistanceTo(SlotOf(b, c, p)), Is.LessThan(60.0),
                    p.ShortName + " near company slot");
                Assert.That(MaxSlotError(p), Is.LessThan(40.0), p.ShortName + " platoon alignment");
                foreach (Element e in p.Elements)
                {
                    Assert.That(e.Pos.Y, Is.GreaterThan(1300.0), e.DisplayName() + " crossed river");
                }
            }

            Assert.That(result.StuckMax, Is.LessThan((int)Math.Round(MaxStuckSec * SimConst.TickRate)),
                "no stuck elements");
            TestContext.Out.WriteLine("Company movement {0:F0} seconds", result.Ticks / 10.0);
        }

        [Test]
        public void FormationChangeTakesTime()
        {
            Battle b = NewBattle();
            Platoon p = AddPlatoon(b, "us_tank_plt", "blue", new SimVec2(2700.0, 3300.0));
            Order order = Order.Make(OrderType.SET_FORMATION, p.Id, new JObject { ["formation"] = "line" });
            b.IssueOrder(order);
            Assert.That(p.Formation!.Id, Is.EqualTo("line"));
            Assert.That(p.IsReforming(b.World.Tick), Is.True, "transition starts");
            b.RunSeconds(14.0);
            Assert.That(p.IsReforming(b.World.Tick), Is.False, "10 seconds times control 1.3");
            b.RunSeconds(30.0);
            Assert.That(p.IsReforming(b.World.Tick), Is.False);
            Assert.That(MaxSlotError(p), Is.LessThan(10.0));
        }

        [Test]
        public void AutoDeployedHeadingKeepsGoldenMovement()
        {
            Battle b = TestFixture.Skirmish(new[] { "blue", "red" }, 1);
            _battles.Add(b);
            Element element = b.ElementsById[125];
            Assert.That(element.Heading, Is.EqualTo((double)(float)Math.PI));

            b.RunTicks(100);

            Assert.That(element.Pos, Is.EqualTo(new SimVec2(2919.0224609375f, 3999.0f)),
                "tests/golden/battle seed 1 tick 100");
        }

        [Test]
        public void SameSeedProducesSameResult()
        {
            List<long> hashes = new List<long>();
            for (int run = 0; run < 2; ++run)
            {
                Battle b = NewBattle(5);
                Company c = AddCompany(b, "us_tank_co", "blue", new SimVec2(2700.0, 450.0));
                Move(b, c, new SimVec2(2600.0, 900.0));
                b.RunSeconds(60.0);
                hashes.Add(b.World.StateHash());
            }

            Assert.That(hashes[1], Is.EqualTo(hashes[0]));
        }

        private static RunResult RunUntilArrived(Battle b, Unit unit, double maxSec)
        {
            int stuckMax = 0;
            bool columnSeen = false;
            int ticks = 0;
            int limit = (int)Math.Round(maxSec * SimConst.TickRate);
            while (ticks < limit)
            {
                b.StepTick();
                ++ticks;
                if (unit.ColumnMode)
                {
                    columnSeen = true;
                }

                if (unit.Guide.IsMoving() || unit.Guide.IsWaitingForPath())
                {
                    foreach (Element e in unit.AliveElements())
                    {
                        if (!e.IsMounted())
                        {
                            stuckMax = Math.Max(stuckMax, e.StuckTicks);
                        }
                    }
                }
                else if (unit.Mission == null || unit.Mission.IsFinished())
                {
                    break;
                }
            }

            b.RunSeconds(30.0);
            return new RunResult { Ticks = ticks, StuckMax = stuckMax, ColumnSeen = columnSeen };
        }

        private static double MaxSlotError(Platoon p)
        {
            double worst = 0.0;
            foreach (Element e in p.FormationMembers())
            {
                worst = Math.Max(worst, e.Pos.DistanceTo(e.MoveTarget));
            }

            return worst;
        }

        private static SimVec2 SlotOf(Battle b, Company c, Platoon p)
        {
            List<Platoon> platoons = c.FormationPlatoons();
            List<SimVec2> slots = FormationSolver.WorldSlots(c.Formation!, platoons.Count, c.Guide.Pos,
                c.Guide.Heading, b.Terrain.TypeNameAt(c.Guide.Pos));
            return slots[platoons.IndexOf(p)];
        }

        private Battle NewBattle(int seedValue = 1)
        {
            Battle b = Battle.Create(Catalog, "skirmish_01", seedValue, true);
            b.Factory.CreateTaskForce("blue", Catalog.GetToe("us_armor_bn_tf")!,
                new SimVec2(2000.0, 150.0), 0.0);
            b.Factory.CreateTaskForce("red", Catalog.GetToe("ru_btg_bmp")!,
                new SimVec2(2000.0, 3850.0), Math.PI);
            _battles.Add(b);
            return b;
        }

        private static Platoon AddPlatoon(Battle b, string templateId, string side, SimVec2 pos,
            double heading = 0.0)
        {
            TaskForce tf = b.TaskForces[side];
            Platoon p = b.Factory.SpawnPlatoon(Catalog.GetUnitTemplate(templateId)!, side, pos, heading, tf);
            tf.Organic.Add(p);
            return p;
        }

        private static Company AddCompany(Battle b, string templateId, string side, SimVec2 pos,
            double heading = 0.0)
        {
            TaskForce tf = b.TaskForces[side];
            Company c = b.Factory.SpawnCompany(Catalog.GetUnitTemplate(templateId)!, side, pos, heading, tf,
                tf.Companies.Count);
            tf.Companies.Add(c);
            return c;
        }

        private static Order Move(Battle b, Unit unit, SimVec2 pos, OrderType type = OrderType.MOVE,
            double facing = double.NaN, JObject? parameters = null)
        {
            Order order = Order.Make(type, unit.Id, parameters ?? new JObject());
            order.SetPos(pos);
            if (!double.IsNaN(facing))
            {
                order.SetFacing(facing);
            }

            return b.IssueOrder(order);
        }

        private struct RunResult
        {
            internal int Ticks { get; set; }
            internal int StuckMax { get; set; }
            internal bool ColumnSeen { get; set; }
        }
    }
}
