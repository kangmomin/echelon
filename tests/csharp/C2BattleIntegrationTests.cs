using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    [Category("M2")]
    public sealed class C2BattleIntegrationTests
    {
        private readonly List<ICommandBattle> _battles = new List<ICommandBattle>();

        [TearDown]
        public void AfterEach()
        {
            foreach (ICommandBattle battle in _battles)
            {
                DisposeBattle(battle);
            }

            _battles.Clear();
        }

        [Test]
        public void CompanyOrderReachesPlatoonsAfterAbout30Seconds()
        {
            ICommandBattle battle = C2Battle();
            Company company = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            RunTicks(battle, 25);
            Order order = Move(battle, company, new SimVec2(2600, 950), OrderType.SEIZE);
            Assert.That(order.State, Is.EqualTo(OrderState.PROPAGATING));
            Assert.That(order.Cost, Is.EqualTo(OrderSystem.CostMission));

            double moved = SecondsUntilMoving(battle, company.Platoons[0], 60.0);
            Assert.That(moved, Is.InRange(25.0, 35.0));
            double receivedDelay = (order.ReceivedTick - order.TickIssued) / (double)SimConst.TickRate;
            Assert.That(receivedDelay, Is.EqualTo(15.0).Within(0.5));
        }

        [Test]
        public void DirectPlatoonOrderDetachesAndCostsDouble()
        {
            ICommandBattle battle = C2Battle();
            Company company = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            RunTicks(battle, 25);
            Platoon platoon = company.Platoons[1];
            Order order = Move(battle, platoon, new SimVec2(2300, 800));
            Assert.That(order.Cost, Is.EqualTo(OrderSystem.CostBypass));

            double moved = SecondsUntilMoving(battle, platoon, 60.0);
            Assert.That(moved, Is.InRange(22.0, 30.0));
            Assert.That(platoon.Detached, Is.True);
            Assert.That(company.FormationPlatoons(), Does.Not.Contain(platoon));
        }

        [Test]
        public void CapacityLimitsOrdersPerMinute()
        {
            ICommandBattle battle = C2Battle();
            Company company = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            RunTicks(battle, 25);
            List<Order> orders = new List<Order>();
            for (int i = 0; i < 8; ++i)
            {
                orders.Add(Move(battle, company, new SimVec2(2600 + i * 10, 900)));
            }

            Assert.That(orders[5].State, Is.EqualTo(OrderState.PROPAGATING));
            Assert.That(orders[6].State, Is.EqualTo(OrderState.QUEUED));
            RunSeconds(battle, 11.0);
            Assert.That(orders[6].State, Is.Not.EqualTo(OrderState.QUEUED));
            Assert.That(orders[7].State, Is.EqualTo(OrderState.QUEUED));
        }

        [Test]
        public void CommsCutBlocksThenCancelsAfter120Seconds()
        {
            ICommandBattle battle = C2Battle();
            Company company = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            Platoon far = company.Platoons[2];
            Teleport(battle, far, new SimVec2(3500, 2600));
            RunTicks(battle, 25);
            Assert.That(far.CommsState, Is.EqualTo(Unit.Comms.Cut));

            Move(battle, company, new SimVec2(2600, 900));
            RunSeconds(battle, 20.0);
            Order? follow = FindFollow(battle, far);
            Assert.That(follow, Is.Not.Null);
            Assert.That(follow!.BlockedSinceTick, Is.GreaterThanOrEqualTo(0));
            Assert.That(far.AwaitingFollow, Is.True);
            RunSeconds(battle, 125.0);
            Assert.That(follow.State, Is.EqualTo(OrderState.CANCELLED));
        }

        [Test]
        public void RelayAddsDelay()
        {
            ICommandBattle battle = C2Battle();
            Company company = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            Platoon platoon = company.Platoons[0];
            Teleport(battle, platoon, new SimVec2(3550, 950));
            CommsNet comms = GetComms(battle);
            comms.Rebuild();
            CommsLink link = comms.LinkBetween(company, platoon);
            Assert.That(link.Ok, Is.True);
            Assert.That(link.Relays, Is.GreaterThan(0));
            CommsLink near = comms.LinkBetween(company, company.Platoons[1]);
            Assert.That(near.Relays, Is.EqualTo(0));
        }

        [Test]
        public void CompanyCommanderLossAndXoSuccession()
        {
            ICommandBattle battle = C2Battle();
            Company company = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            RunTicks(battle, 25);
            Element commander = company.Commander!;
            Element? xo = company.Xo;
            Kill(commander);
            RunTicks(battle, 15);
            Assert.That(company.IsLeaderless(battle.World.Tick), Is.True);

            Order order = Move(battle, company, new SimVec2(2600, 900));
            RunSeconds(battle, 30.0);
            Assert.That(order.State, Is.EqualTo(OrderState.PROPAGATING));
            RunSeconds(battle, 65.0);
            Assert.That(company.Commander, Is.SameAs(xo));
            Assert.That(company.IsLeaderless(battle.World.Tick), Is.False);
            RunSeconds(battle, 20.0);
            Assert.That(order.State, Is.Not.EqualTo(OrderState.PROPAGATING));
        }

        [Test]
        public void NoXoMeansSeniorPlatoonLeaderAndSlowerOrders()
        {
            ICommandBattle battle = C2Battle();
            Company company = CreateCompany(battle, "ru_tank_co", "red", new SimVec2(2000, 3300), System.Math.PI);
            RunTicks(battle, 25);
            Assert.That(company.Xo, Is.Null);
            Kill(company.Commander!);
            RunTicks(battle, 15);

            double untilSeconds = (company.SuccessionUntilTick - battle.World.Tick) / (double)SimConst.TickRate;
            Assert.That(untilSeconds, Is.InRange(110.0, 181.0));
            RunSeconds(battle, 185.0);
            Assert.That(company.Commander, Is.SameAs(company.Platoons[0].Leader()));
            Assert.That(company.SkillDelayFactor(), Is.EqualTo(1.5).Within(0.01));
        }

        [Test]
        public void TaskForceCpLossHalvesCapacity()
        {
            ICommandBattle battle = C2Battle();
            CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            Assert.That(battle.OrderSystem!.CapacityMax("blue"), Is.EqualTo(6.0).Within(0.01));
            Kill(battle.GetTaskForce("blue")!.CpSection!.Elements[0]);
            RunTicks(battle, 15);
            Assert.That(battle.OrderSystem!.CapacityMax("blue"), Is.EqualTo(3.0).Within(0.01));
        }

        [Test]
        public void DelayEstimateMatchesActual()
        {
            ICommandBattle battle = C2Battle();
            Company company = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            RunTicks(battle, 25);
            double estimate = battle.OrderSystem!.EstimateDelaySec(company, OrderType.MOVE, false);
            Order order = Move(battle, company, new SimVec2(2600, 900));
            RunSeconds(battle, 20.0);
            double actual = (order.ReceivedTick - order.TickIssued) / (double)SimConst.TickRate;
            Assert.That(estimate, Is.EqualTo(actual).Within(0.5));
        }

        [Test]
        public void C2BattleIsDeterministic()
        {
            List<long> hashes = new List<long>();
            for (int run = 0; run < 2; ++run)
            {
                ICommandBattle battle = C2Battle(9);
                Company company = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
                Kill(company.Commander!);
                Move(battle, company, new SimVec2(2600, 900));
                RunSeconds(battle, 120.0);
                hashes.Add(battle.World.StateHash());
            }

            Assert.That(hashes[1], Is.EqualTo(hashes[0]));
        }

        [Test]
        public void CutPlatoonRejoinsWhenCommsRestored()
        {
            ICommandBattle battle = C2Battle();
            Company company = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            Platoon far = company.Platoons[2];
            Teleport(battle, far, new SimVec2(3500, 2600));
            RunTicks(battle, 25);
            Move(battle, company, new SimVec2(3600, 2000));
            RunSeconds(battle, 40.0);
            Assert.That(far.AwaitingFollow, Is.True);
            RunSeconds(battle, 240.0);
            Assert.That(far.AwaitingFollow, Is.False);
        }

        [Test]
        public void BlockedHopCancelsOnTick1201NotTick1200()
        {
            ICommandBattle battle = C2Battle();
            Company company = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            Platoon far = company.Platoons[2];
            Teleport(battle, far, new SimVec2(3500, 2600));
            RunTicks(battle, 25);
            Move(battle, company, new SimVec2(2600, 900));
            RunSeconds(battle, 20.0);
            Order? follow = FindFollow(battle, far);
            Assert.That(follow, Is.Not.Null);
            Assert.That(follow!.BlockedSinceTick, Is.GreaterThanOrEqualTo(0));

            long ticksToBoundary = follow.BlockedSinceTick + OrderSystem.BlockTimeoutTicks + 1 - battle.World.Tick;
            RunTicks(battle, (int)ticksToBoundary);
            Assert.That(follow.State, Is.Not.EqualTo(OrderState.CANCELLED));
            RunTicks(battle, 1);
            Assert.That(follow.State, Is.EqualTo(OrderState.CANCELLED));
        }

        [Test]
        public void CommsSideUpdatesPreserveBlueRedPhaseOffsets()
        {
            ICommandBattle battle = C2Battle();
            Company blue = CreateCompany(battle, "us_tank_co", "blue", new SimVec2(2700, 450));
            Company red = CreateCompany(battle, "ru_tank_co", "red", new SimVec2(2000, 3300), System.Math.PI);
            Platoon blueFar = blue.Platoons[2];
            Platoon redFar = red.Platoons[2];
            RunTicks(battle, 25);
            Assert.That(blueFar.CommsState, Is.Not.EqualTo(Unit.Comms.Cut));
            Assert.That(redFar.CommsState, Is.Not.EqualTo(Unit.Comms.Cut));

            Teleport(battle, blueFar, new SimVec2(3500, 2600));
            Teleport(battle, redFar, new SimVec2(3500, 2600));
            RunTicks(battle, 6);
            Assert.That(redFar.CommsState, Is.EqualTo(Unit.Comms.Cut));
            Assert.That(blueFar.CommsState, Is.Not.EqualTo(Unit.Comms.Cut));
            RunTicks(battle, 10);
            Assert.That(blueFar.CommsState, Is.EqualTo(Unit.Comms.Cut));
        }

        private static ICommandBattle CreateBattle(int seed)
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            Battle battle = Battle.Create(catalog, "skirmish_01", seed, false);
            battle.Factory.CreateTaskForce("blue", catalog.GetToe("us_armor_bn_tf")!,
                new SimVec2(2000, 150), 0.0);
            battle.Factory.CreateTaskForce("red", catalog.GetToe("ru_btg_bmp")!,
                new SimVec2(2000, 3850), System.Math.PI);
            return battle;
        }

        private static Company CreateCompany(
            ICommandBattle battle,
            string templateId,
            string side,
            SimVec2 position,
            double heading = 0.0)
        {
            Battle concrete = (Battle)battle;
            TaskForce taskForce = concrete.TaskForces[side];
            Company company = concrete.Factory.SpawnCompany(DataTestAssets.Catalog.GetUnitTemplate(templateId)!,
                side, position, heading, taskForce, taskForce.Companies.Count);
            taskForce.Companies.Add(company);
            return company;
        }

        private static void Teleport(ICommandBattle battle, Unit unit, SimVec2 position)
        {
            Battle concrete = (Battle)battle;
            unit.Guide.Reset(position, unit.Guide.Heading);
            if (unit is Platoon platoon)
            {
                concrete.Factory.PlaceElements(platoon);
            }
        }

        private static CommsNet GetComms(ICommandBattle battle)
        {
            return ((Battle)battle).Comms;
        }

        private static void RunTicks(ICommandBattle battle, int count)
        {
            ((Battle)battle).RunTicks(count);
        }

        private static void DisposeBattle(ICommandBattle battle)
        {
            ((Battle)battle).Dispose();
        }

        private static void Kill(Element element)
        {
            element.State = ElementState.DESTROYED;
            foreach (Element passenger in element.Passengers)
            {
                passenger.State = ElementState.DESTROYED;
            }
        }

        private ICommandBattle C2Battle(int seed = 3)
        {
            ICommandBattle battle = CreateBattle(seed);
            _battles.Add(battle);
            return battle;
        }

        private static Order Move(
            ICommandBattle battle,
            Unit unit,
            SimVec2 position,
            OrderType type = OrderType.MOVE)
        {
            Order order = Order.Make(type, unit.Id);
            order.SetPos(position);
            return ((Battle)battle).IssueOrder(order);
        }

        private void RunSeconds(ICommandBattle battle, double seconds)
        {
            RunTicks(battle, (int)OrderSystem.SecondsToTicks(seconds));
        }

        private double SecondsUntilMoving(ICommandBattle battle, Unit unit, double limitSeconds)
        {
            long start = battle.World.Tick;
            long limit = OrderSystem.SecondsToTicks(limitSeconds);
            while (battle.World.Tick - start < limit)
            {
                RunTicks(battle, 1);
                foreach (Element element in unit.AliveElements())
                {
                    if (!element.IsMounted() && element.Velocity.Length() > 0.5)
                    {
                        return (battle.World.Tick - start) / (double)SimConst.TickRate;
                    }
                }
            }

            return -1.0;
        }

        private static Order? FindFollow(ICommandBattle battle, Platoon platoon)
        {
            Order? found = null;
            foreach (Order order in battle.OrdersById.Values)
            {
                if (order.Type == OrderType.FOLLOW && order.TargetUnitId == platoon.Id)
                {
                    found = order;
                }
            }

            return found;
        }
    }
}
