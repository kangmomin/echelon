using System;
using System.Collections.Generic;
using Echelon.Core;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    [Category("Integration")]
    public sealed class AIM6IntegrationTests
    {
        [Test]
        public void SystemsUseDeterministicBattleOrder()
        {
            using Battle battle = TestFixture.Skirmish(Array.Empty<string>(), 1);
            List<SimSystem> systems = battle.World.GetSystems();
            Type[] expected =
            {
                typeof(CommsNet), typeof(SuccessionSystem), typeof(OrderSystem), typeof(ReinforcementSystem),
                typeof(TaskForceAI), typeof(PlatoonAI), typeof(MovementSystem), typeof(DetectionSystem),
                typeof(CombatSystem), typeof(FireSupportSystem), typeof(SupplySystem), typeof(EngineerSystem),
                typeof(MoraleSystem), typeof(ScoringSystem)
            };
            Assert.That(systems.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.That(systems[i].GetType(), Is.EqualTo(expected[i]), $"system {i}");
            }
        }

        [Test]
        public void AiVsAiGameFinishesWithResultAndAar()
        {
            using Battle battle = TestFixture.Skirmish(new[] { "blue", "red" }, 11);
            int limit = (int)Math.Round(battle.Scenario.TimeLimitMin * 60 * SimConst.TickRate,
                MidpointRounding.AwayFromZero) + 50;
            while (battle.Result.Count == 0 && battle.Tick() < limit)
            {
                battle.StepTick();
                if (battle.Tick() % 600 == 0)
                {
                    battle.World.Events.Drain();
                }
            }

            Assert.That(battle.Result, Is.Not.Empty);
            string reason = ResultString(battle, "reason");
            Assert.That(reason == "collapse" || reason == "time", Is.True, reason);
            Assert.That(battle.World.Paused, Is.True);
            Dictionary<string, object> aar = battle.Stats.Summary();
            Dictionary<string, object> sides = (Dictionary<string, object>)aar["sides"];
            Dictionary<string, object> blue = (Dictionary<string, object>)sides["blue"];
            Dictionary<string, object> red = (Dictionary<string, object>)sides["red"];
            Assert.That(blue["orders"], Is.GreaterThan(3));
            Assert.That(red["orders"], Is.GreaterThan(3));
            Assert.That(red["order_delay_mean"], Is.GreaterThan(5.0));
            Assert.That(((List<Dictionary<string, object>>)aar["timeline"]).Count, Is.GreaterThan(3));
            Assert.That((int)blue["losses_total"] + (int)red["losses_total"], Is.GreaterThan(0));
            Assert.That(JsonConvert.SerializeObject(aar).Length, Is.GreaterThan(100));
        }

        [Test]
        public void ControlPointCaptureVpAndObjective()
        {
            using Battle battle = TestFixture.BattleOn(TestFixture.FlatMap(), 1);
            ScoringSystem.PointState fox = battle.Scoring.GetPoint("cp_fox")!;
            Platoon blue = TestFixture.Platoon(battle, "us_tank_plt", "blue", fox.Def.Pos);
            ClearAmmo(blue);
            battle.RunSeconds(3);
            Assert.That(fox.Owner, Is.EqualTo("blue"));
            Assert.That(battle.Scoring.ObjectivesDone.TryGetValue("obj_fox", out bool done) && done, Is.True);
            double vpAfterObjective = battle.Scoring.Vp["blue"];
            Assert.That(vpAfterObjective, Is.EqualTo(30).Within(1));
            battle.RunSeconds(60);
            Assert.That(battle.Scoring.Vp["blue"] - vpAfterObjective, Is.EqualTo(1).Within(0.1));

            Platoon red = TestFixture.Platoon(battle, "ru_tank_plt", "red", fox.Def.Pos + new SimVec2(100, 100), Math.PI);
            ClearAmmo(red);
            ClearAmmo(blue);
            battle.RunSeconds(3);
            Assert.That(fox.Contested, Is.True);
            Assert.That(fox.Owner, Is.EqualTo("blue"));
            TestFixture.Teleport(battle, blue, new SimVec2(600, 600));
            battle.RunSeconds(3);
            Assert.That(fox.Owner, Is.EqualTo("red"));
        }

        [Test]
        public void CollapseEndsBattleImmediately()
        {
            using Battle battle = TestFixture.Skirmish(Array.Empty<string>(), 3);
            battle.RunTicks(5);
            List<Element> elements = battle.TaskForces["red"].AllElements();
            int count = (int)Math.Round(elements.Count * 0.9, MidpointRounding.AwayFromZero);
            for (int i = 0; i < count; ++i)
            {
                TestFixture.Kill(elements[i]);
            }
            battle.RunSeconds(2);
            Assert.That(ResultString(battle, "winner"), Is.EqualTo("blue"));
            Assert.That(ResultString(battle, "reason"), Is.EqualTo("collapse"));
            Assert.That(battle.World.Paused, Is.True);
        }

        [Test]
        public void OffMapReinforcementsDoNotWeakenFieldForce()
        {
            using Battle battle = TestFixture.Skirmish(Array.Empty<string>(), 6);
            battle.RunTicks(5);
            TaskForce blue = battle.TaskForces["blue"];
            List<Element> waiting = new List<Element>();
            foreach (Element element in blue.AllElements())
            {
                if (!element.OnMap)
                {
                    waiting.Add(element);
                }
            }
            Assert.That(waiting, Is.Not.Empty);
            double deployedValue = FullValue(blue.AllElements(), true);
            Assert.That(battle.Scoring.CommittedValue["blue"], Is.EqualTo(deployedValue).Within(0.01));
            double before = battle.Scoring.ValueRatio("blue");
            foreach (Element element in waiting)
            {
                TestFixture.Kill(element);
            }
            battle.RunSeconds(2);
            Assert.That(battle.Result, Is.Empty);
            Assert.That(battle.Scoring.ForceValue("blue"), Is.EqualTo(deployedValue).Within(0.01));
            Assert.That(battle.Scoring.ValueRatio("blue"), Is.EqualTo(before).Within(0.0001));
        }

        [Test]
        public void ReinforcementArrivalExpandsCommittedBaselineOnce()
        {
            using Battle battle = TestFixture.Skirmish(Array.Empty<string>(), 7);
            battle.RunTicks(5);
            TaskForce blue = battle.TaskForces["blue"];
            HashSet<int> pendingIds = new HashSet<int>();
            foreach (Element element in blue.AllElements())
            {
                if (!element.OnMap)
                {
                    pendingIds.Add(element.Id);
                }
            }
            double baseBefore = battle.Scoring.CommittedValue["blue"];
            battle.Reinforcements.BringIn("blue", 1, battle.Tick());
            List<Element> arrived = new List<Element>();
            foreach (Element element in blue.AllElements())
            {
                if (element.OnMap && pendingIds.Contains(element.Id))
                {
                    arrived.Add(element);
                }
            }
            Assert.That(arrived, Is.Not.Empty);
            double expectedBase = baseBefore + FullValue(arrived, false);
            battle.RunSeconds(2);
            Assert.That(battle.Scoring.CommittedValue["blue"], Is.EqualTo(expectedBase).Within(0.01));
            Assert.That(battle.Scoring.ValueRatio("blue"), Is.EqualTo(1).Within(0.001));
            Element? damaged = null;
            foreach (Element element in arrived)
            {
                if (!element.IsInfantry())
                {
                    damaged = element;
                    break;
                }
            }
            Assert.That(damaged, Is.Not.Null);
            damaged!.State = ElementState.DAMAGED;
            battle.RunSeconds(2);
            double expectedRatio = (expectedBase - damaged.Def.Cost * 0.4) / expectedBase;
            Assert.That(battle.Scoring.ValueRatio("blue"), Is.EqualTo(expectedRatio).Within(0.001));
            Assert.That(battle.Scoring.ValueRatio("blue"), Is.LessThan(1));
            battle.RunSeconds(2);
            Assert.That(battle.Scoring.CommittedValue["blue"], Is.EqualTo(expectedBase).Within(0.01));
        }

        [Test]
        public void TimeLimitDecidesByVp()
        {
            using Battle battle = TestFixture.BattleOn(TestFixture.FlatMap(), 2);
            double oldLimit = battle.Scenario.TimeLimitMin;
            battle.Scenario.TimeLimitMin = 1;
            ScoringSystem.PointState bravo = battle.Scoring.GetPoint("cp_bravo")!;
            Platoon red = TestFixture.Platoon(battle, "ru_tank_plt", "red", bravo.Def.Pos, Math.PI);
            ClearAmmo(red);
            battle.RunSeconds(65);
            battle.Scenario.TimeLimitMin = oldLimit;
            Assert.That(ResultString(battle, "reason"), Is.EqualTo("time"));
            Assert.That(ResultString(battle, "winner"), Is.EqualTo("red"));
        }

        [Test]
        public void ReinforcementsArriveAndJoinCompany()
        {
            using Battle battle = TestFixture.Skirmish(Array.Empty<string>(), 4);
            Assert.That(ReinforcementSystem.WaveTick(1), Is.EqualTo(6000));
            Assert.That(ReinforcementSystem.WaveTick(2), Is.EqualTo(12000));
            TaskForce blue = battle.TaskForces["blue"];
            Platoon a3 = blue.Companies[1].Platoons[2];
            Assert.That(a3.OnMap, Is.False);
            battle.Reinforcements.BringIn("blue", 1, battle.Tick());
            Assert.That(a3.OnMap, Is.True);
            foreach (Element element in a3.Elements)
            {
                Assert.That(element.IsActive(), Is.True);
            }
            double startDistance = a3.Centroid().DistanceTo(blue.Companies[1].Guide.Pos);
            battle.RunSeconds(60);
            double distance = a3.Centroid().DistanceTo(blue.Companies[1].Guide.Pos);
            Assert.That(distance, Is.LessThan(startDistance - 100));
            Company redCompany = battle.TaskForces["red"].Companies[2];
            Assert.That(redCompany.OnMap, Is.False);
            battle.Reinforcements.BringIn("red", 1, battle.Tick());
            Assert.That(redCompany.OnMap, Is.True);
            Assert.That(redCompany.Hq!.OnMap, Is.True);
        }

        [Test]
        public void TfAiAssignsPointsAndReservesWeakCompany()
        {
            using Battle battle = TestFixture.Skirmish(new[] { "red" }, 5);
            battle.RunTicks(TaskForceAI.EvalOffset + 1);
            TaskForce red = battle.TaskForces["red"];
            int assigned = 0;
            foreach (Company company in red.Companies)
            {
                if (company.OnMap)
                {
                    Assert.That(battle.TfAi.Assignment.ContainsKey(company.Id), Is.True, company.Label());
                    ++assigned;
                }
            }
            Assert.That(assigned, Is.GreaterThan(1));
            HashSet<string> distinct = new HashSet<string>(battle.TfAi.Assignment.Values);
            Assert.That(distinct.Count, Is.GreaterThan(1));
            Company weak = red.Companies[0];
            List<Element> elements = weak.AllElements();
            int count = (int)Math.Round(elements.Count * 0.6, MidpointRounding.AwayFromZero);
            for (int i = 0; i < count; ++i)
            {
                TestFixture.Kill(elements[i]);
            }
            battle.TfAi.Evaluate(red);
            Assert.That(battle.TfAi.Assignment[weak.Id], Is.EqualTo("reserve"));
        }

        private static string ResultString(Battle battle, string key)
        {
            return battle.Result.TryGetValue(key, out object? value) ? Convert.ToString(value) ?? "" : "";
        }

        private static double FullValue(IEnumerable<Element> elements, bool onMapOnly)
        {
            double total = 0;
            foreach (Element element in elements)
            {
                if (!onMapOnly || element.OnMap)
                {
                    total += element.Def.Cost;
                }
            }
            return total;
        }

        private static void ClearAmmo(Platoon platoon)
        {
            foreach (Element element in platoon.Elements)
            {
                element.Ammo.Clear();
            }
        }
    }
}
