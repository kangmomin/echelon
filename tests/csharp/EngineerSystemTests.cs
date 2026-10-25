using System;
using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class EngineerSystemTests
    {
        [Test]
        public void EmptySystemIsOmittedFromWorldHashAndDoesNotDrawRng()
        {
            EngineerTestContext context = new EngineerTestContext(11);
            EngineerSystem engineers = new EngineerSystem(context);
            SimWorld withEngineers = new SimWorld(11);
            SimWorld withoutEngineers = new SimWorld(11);
            withEngineers.AddSystem(engineers);
            long rngState = context.World.Rng.GetState();
            withEngineers.StepTick();
            withoutEngineers.StepTick();
            Assert.That(engineers.OmitFromHash(), Is.True);
            Assert.That(withEngineers.StateHash(), Is.EqualTo(withoutEngineers.StateHash()));
            Assert.That(context.World.Rng.GetState(), Is.EqualTo(rngState));
            engineers.LayMinefield("blue", new SimVec2(10.0, 10.0));
            Assert.That(engineers.OmitFromHash(), Is.False);
            Assert.That(withEngineers.StateHash(), Is.Not.EqualTo(withoutEngineers.StateHash()));
        }

        [Test]
        public void RecentHitBlocksWorkForExactly100Ticks()
        {
            EngineerTestContext context = new EngineerTestContext(1);
            EngineerSystem engineers = new EngineerSystem(context);
            Platoon platoon = EngineerPlatoon(context);
            Order order = Order.Make(OrderType.LAY_MINES, platoon.Id);
            order.State = OrderState.EXECUTING;
            platoon.Mission = order;
            Assert.That(engineers.BeginTask(platoon, order), Is.True);
            platoon.LastHitTick = 0;
            context.World.AddSystem(engineers);
            context.World.StepTick();
            for (int i = 1; i < 99; ++i)
            {
                context.World.StepTick();
            }

            Assert.That(platoon.EngProgress, Is.EqualTo(0.0));
            context.World.StepTick();
            Assert.That(platoon.EngProgress, Is.EqualTo(0.0));
            context.World.StepTick();
            Assert.That(platoon.EngProgress, Is.EqualTo(0.5).Within(0.0001));
        }

        [Test]
        public void VehicleStrikeUsesOneStrikeDrawThenOneDestroyDraw()
        {
            int seed = SeedForChance(EngineerSystem.StrikePVehicle);
            EngineerTestContext context = new EngineerTestContext(seed);
            EngineerSystem engineers = new EngineerSystem(context);
            EngineerSystem.Minefield minefield = engineers.LayMinefield("blue", new SimVec2(2000.0, 2000.0));
            Platoon platoon = new Platoon { Id = 9, Side = "red" };
            Element vehicle = MovingElement("tank", "red", minefield.Pos, platoon);
            context.Nearby.Add(vehicle);
            SimRng expected = new SimRng(seed);
            Assert.That(expected.Chance(EngineerSystem.StrikePVehicle), Is.True);
            bool destroyed = expected.Chance(0.5);
            long expectedState = expected.GetState();
            engineers.Step(context.World);
            Assert.That(context.World.Rng.GetState(), Is.EqualTo(expectedState));
            Assert.That(minefield.Charges, Is.EqualTo(EngineerSystem.MinefieldCharges - 1));
            Assert.That(engineers.MineStrikes, Is.EqualTo(1));
            Assert.That(vehicle.State, destroyed ? Is.EqualTo(ElementState.DESTROYED) : Is.EqualTo(ElementState.DAMAGED));
            if (!destroyed)
            {
                Assert.That(vehicle.DamageKind, Is.EqualTo("mobility"));
                Assert.That(context.Damaged, Does.Contain(vehicle));
            }

            Assert.That(context.Threats.Count, Is.EqualTo(1));
            Assert.That(context.Threats[0].Radius, Is.EqualTo(90.0));
            Assert.That(context.Threats[0].Amount, Is.EqualTo(EngineerSystem.ThreatAmount));
            List<SimEvent> events = context.World.Events.Drain();
            Assert.That(events[0].Type, Is.EqualTo("minefield_found"));
            Assert.That(events[1].Type, Is.EqualTo("mine_strike"));
            Assert.That(events[1].Payload["element"], Is.EqualTo(vehicle.Id));
        }

        [Test]
        public void InfantryStrikeTakesTwoMenWithoutVehicleOutcomeDraw()
        {
            int seed = SeedForChance(EngineerSystem.StrikePFoot);
            EngineerTestContext context = new EngineerTestContext(seed);
            EngineerSystem engineers = new EngineerSystem(context);
            EngineerSystem.Minefield minefield = engineers.LayMinefield("blue", new SimVec2(2000.0, 2000.0));
            Platoon platoon = new Platoon { Id = 10, Side = "red" };
            Element infantry = MovingElement("inf_squad", "red", minefield.Pos, platoon);
            infantry.Hp = 3.0;
            context.Nearby.Add(infantry);
            SimRng expected = new SimRng(seed);
            Assert.That(expected.Chance(EngineerSystem.StrikePFoot), Is.True);
            long expectedState = expected.GetState();
            engineers.Step(context.World);
            Assert.That(context.World.Rng.GetState(), Is.EqualTo(expectedState));
            Assert.That(infantry.Hp, Is.EqualTo(1.0));
            Assert.That(context.Casualties.Count, Is.EqualTo(1));
            Assert.That(context.Casualties[0].Platoon, Is.SameAs(platoon));
            Assert.That(context.Casualties[0].Men, Is.EqualTo(2));
            Assert.That(context.Destroyed, Is.Empty);
        }

        [Test]
        public void NearbyEngineersDetectEnemyFieldAndRevealPathThreat()
        {
            EngineerTestContext context = new EngineerTestContext(4);
            EngineerSystem engineers = new EngineerSystem(context);
            Platoon platoon = EngineerPlatoon(context);
            platoon.Guide.Pos = new SimVec2(2000.0, 1800.0);
            EngineerSystem.Minefield minefield = engineers.LayMinefield("red", new SimVec2(2000.0, 2000.0));
            engineers.Step(context.World);
            Assert.That(minefield.Known["blue"], Is.True);
            Assert.That(context.Threats.Count, Is.EqualTo(1));
            Assert.That(context.Threats[0].Side, Is.EqualTo("blue"));
            Assert.That(context.Threats[0].Radius, Is.EqualTo(90.0));
            Assert.That(engineers.KnownMinefields("blue"), Does.Contain(minefield));
        }

        private static Platoon EngineerPlatoon(EngineerTestContext context)
        {
            TaskForce taskForce = new TaskForce { Id = 1, Side = "blue" };
            Platoon platoon = new Platoon { Id = 2, Side = "blue", Kind = "engineer", Parent = taskForce, MinesLeft = 2 };
            Element sapper = new Element
            {
                Id = 3,
                Side = "blue",
                Platoon = platoon,
                Def = new ElementDef { ElementClass = "inf_squad" }
            };
            platoon.Elements.Add(sapper);
            taskForce.Organic.Add(platoon);
            context.TaskForces["blue"] = taskForce;
            return platoon;
        }

        private static Element MovingElement(string elementClass, string side, SimVec2 position, Platoon platoon)
        {
            Element element = new Element
            {
                Id = 20,
                Side = side,
                Pos = position,
                Velocity = new SimVec2(0.0, 10.0),
                Platoon = platoon,
                Def = new ElementDef { ElementClass = elementClass, Hp = 10.0 }
            };
            platoon.Elements.Add(element);
            return element;
        }

        private static int SeedForChance(double probability)
        {
            for (int seed = 0; seed < 1000; ++seed)
            {
                if (new SimRng(seed).Chance(probability))
                {
                    return seed;
                }
            }

            throw new InvalidOperationException("No deterministic test seed found.");
        }

        private sealed class EngineerTestContext : IEngineerBattle
        {
            internal EngineerTestContext(long seed)
            {
                World = new SimWorld(seed);
            }

            internal Dictionary<string, TaskForce> TaskForces { get; } = new Dictionary<string, TaskForce>();
            internal List<Element> Nearby { get; } = new List<Element>();
            internal List<ThreatRecord> Threats { get; } = new List<ThreatRecord>();
            internal List<Element> Destroyed { get; } = new List<Element>();
            internal List<Element> Damaged { get; } = new List<Element>();
            internal List<CasualtyRecord> Casualties { get; } = new List<CasualtyRecord>();
            public SimWorld World { get; }

            public TaskForce? GetTaskForce(string side)
            {
                return TaskForces.TryGetValue(side, out TaskForce? taskForce) ? taskForce : null;
            }

            public IEnumerable<Element> ElementsNear(SimVec2 position, double radius)
            {
                return Nearby;
            }

            public void AddThreat(string side, SimVec2 position, double radius, double amount)
            {
                Threats.Add(new ThreatRecord(side, radius, amount));
            }

            public void Destroy(Element element, Element? killer, long tick)
            {
                element.State = ElementState.DESTROYED;
                Destroyed.Add(element);
            }

            public void OnCasualties(Platoon platoon, int men)
            {
                Casualties.Add(new CasualtyRecord(platoon, men));
            }

            public void OnElementDamaged(Element element)
            {
                Damaged.Add(element);
            }
        }

        private readonly struct ThreatRecord
        {
            internal ThreatRecord(string side, double radius, double amount)
            {
                Side = side;
                Radius = radius;
                Amount = amount;
            }

            internal string Side { get; }
            internal double Radius { get; }
            internal double Amount { get; }
        }

        private readonly struct CasualtyRecord
        {
            internal CasualtyRecord(Platoon platoon, int men)
            {
                Platoon = platoon;
                Men = men;
            }

            internal Platoon Platoon { get; }
            internal int Men { get; }
        }
    }
}
