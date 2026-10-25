using System;
using System.Collections.Generic;
using Echelon.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class CombatM3IntegrationTests
    {
        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        private static readonly MapRect Forest = new MapRect(1500.0, 950.0, 1000.0, 400.0);

        [Test, Explicit("Ten-seed M3 statistical battle")]
        public void ColumnAttackTradesWorseThanLine()
        {
            int[] column = SumEngagements("column", false);
            int[] line = SumEngagements("line", false);
            double columnRatio = column[0] / Math.Max(1.0, (double)column[1]);
            double lineRatio = line[0] / Math.Max(1.0, (double)line[1]);
            Assert.That(lineRatio, Is.GreaterThan(columnRatio),
                $"line exchange ratio {lineRatio:F2} must exceed column ratio {columnRatio:F2}");
            Assert.That(column[1], Is.GreaterThan(line[1]));
        }

        [Test, Explicit("Ten-seed M3 statistical battle")]
        public void ForestEdgeHelpsDefender()
        {
            int[] open = SumEngagements("wedge", false);
            int[] forest = SumEngagements("wedge", true);
            Assert.That(forest[0], Is.LessThan(open[0]));
            Assert.That(forest[1], Is.GreaterThan(open[1]));
        }

        [Test]
        public void ContactReportsAreDelayed()
        {
            using CombatTestRig rig = CombatTestSupport.Create(2, CombatTestSupport.FlatMap());
            Battle battle = rig.Battle;
            Company company = CombatTestSupport.Company(battle, "us_tank_co", "blue",
                new SimVec2(600.0, 2000.0), Math.PI / 2.0);
            Platoon red = CombatTestSupport.Platoon(battle, "ru_tank_plt", "red",
                new SimVec2(2300.0, 2000.0), -Math.PI / 2.0);
            ClearAmmo(company.AllElements());
            ClearAmmo(red.AllElements());
            TaskForce taskForce = battle.TaskForces["blue"];
            long firstSeen = -1;
            long firstKnown = -1;
            for (int i = 0; i < 600; ++i)
            {
                battle.StepTick();
                if (firstSeen < 0 && rig.Detection.Seen["blue"].Count > 0)
                {
                    firstSeen = battle.World.Tick;
                }

                if (firstKnown < 0 && taskForce.KnownEnemies.Count > 0)
                {
                    firstKnown = battle.World.Tick;
                    break;
                }
            }

            Assert.That(firstSeen, Is.GreaterThan(0));
            Assert.That(firstKnown, Is.GreaterThan(0));
            double delaySeconds = (firstKnown - firstSeen) / 10.0;
            Assert.That(delaySeconds, Is.InRange(8.0, 25.0));
        }

        [Test]
        public void ContactReportsAreHeldWhenCommsAreCutAndResumeAfterRecovery()
        {
            using CombatTestRig rig = CombatTestSupport.Create(2, CombatTestSupport.FlatMap());
            Battle battle = rig.Battle;
            Company company = CombatTestSupport.Company(battle, "us_tank_co", "blue",
                new SimVec2(600.0, 1000.0), Math.PI / 2.0);
            Platoon cut = company.Platoons[2];
            CombatTestSupport.Teleport(battle, cut, new SimVec2(1200.0, 3300.0));
            Platoon red = CombatTestSupport.Platoon(battle, "ru_tank_plt", "red",
                new SimVec2(2600.0, 3300.0), -Math.PI / 2.0);
            ClearAmmo(battle.Elements);
            battle.RunSeconds(40.0);
            Assert.That(cut.CommsState, Is.EqualTo(Unit.Comms.Cut));
            Assert.That(rig.Detection.Seen["blue"], Is.Not.Empty);
            Assert.That(cut.Contacts, Is.Not.Empty);
            Assert.That(battle.TaskForces["blue"].KnownEnemies, Is.Empty);
            Assert.That(rig.Detection.PendingCount("blue"), Is.GreaterThan(0));
            CombatTestSupport.Teleport(battle, cut, new SimVec2(700.0, 1300.0));
            battle.RunSeconds(30.0);
            Assert.That(battle.TaskForces["blue"].KnownEnemies, Is.Not.Empty);
        }

        [Test]
        public void IfvKillHalvesPassengersAndDismountsSurvivors()
        {
            using CombatTestRig rig = CombatTestSupport.Create(1, CombatTestSupport.FlatMap());
            Platoon platoon = CombatTestSupport.Platoon(rig.Battle, "us_mech_plt", "blue",
                new SimVec2(2000.0, 2000.0));
            Element? ifv = null;
            foreach (Element vehicle in platoon.Vehicles())
            {
                if (vehicle.Passengers.Count > 0)
                {
                    ifv = vehicle;
                    break;
                }
            }

            Assert.That(ifv, Is.Not.Null);
            Element squad = ifv!.Passengers[0];
            double before = squad.Hp;
            rig.Combat.Destroy(ifv, null, rig.Battle.World.Tick);
            Assert.That(ifv.IsAlive(), Is.False);
            Assert.That(squad.IsMounted(), Is.False);
            Assert.That(squad.Hp, Is.EqualTo(Math.Floor(before * 0.5)));
            Assert.That(squad.Suppression, Is.GreaterThan(50.0));
            Assert.That(platoon.Mounted, Is.False);
        }

        [Test]
        public void EmptyAmmoPreventsBlueFireWhileEnemyContinuesFiring()
        {
            using CombatTestRig rig = CombatTestSupport.Create(1, CombatTestSupport.FlatMap());
            Battle battle = rig.Battle;
            Platoon blue = CombatTestSupport.Platoon(battle, "us_tank_plt", "blue",
                new SimVec2(2000.0, 1500.0));
            Platoon red = CombatTestSupport.Platoon(battle, "ru_tank_plt", "red",
                new SimVec2(2000.0, 3000.0), Math.PI);
            ClearAmmo(blue.AllElements());
            battle.RunSeconds(60.0);
            Assert.That(CombatTestSupport.Losses(red), Is.Zero);
            foreach (Element element in blue.Elements)
            {
                Assert.That(element.LastFiredTick, Is.LessThan(0));
            }

            Assert.That(rig.Combat.ShotsFired, Is.GreaterThan(0));
        }

        [Test]
        public void ShakenPlatoonRefusesAttackThenRoutsAndRallies()
        {
            using CombatTestRig rig = CombatTestSupport.Create(1, CombatTestSupport.FlatMap());
            Battle battle = rig.Battle;
            Platoon platoon = CombatTestSupport.Platoon(battle, "us_tank_plt", "blue",
                new SimVec2(2000.0, 2000.0));
            platoon.Morale = 35.0;
            Order attack = CombatTestSupport.Move(battle, platoon, new SimVec2(2000.0, 2600.0),
                OrderType.ATTACK_MOVE);
            battle.RunTicks(2);
            Assert.That(attack.State, Is.EqualTo(OrderState.CANCELLED));
            Order move = CombatTestSupport.Move(battle, platoon, new SimVec2(2100.0, 2000.0));
            battle.RunTicks(2);
            Assert.That(move.State, Is.Not.EqualTo(OrderState.CANCELLED));
            platoon.Morale = 15.0;
            battle.RunSeconds(3.0);
            Assert.That(platoon.Routed, Is.True);
            battle.RunSeconds(67.0);
            Assert.That(platoon.Routed, Is.False);
            Assert.That(platoon.Morale, Is.GreaterThanOrEqualTo(MoraleSystem.AfterRout));
        }

        [Test]
        public void RoutedPlatoonWithdrawsToRearWhenAiIsComposed()
        {
            using CombatTestRig rig = CombatTestSupport.Create(1, CombatTestSupport.FlatMap());
            if (!HasSystem<PlatoonAI>(rig.Battle))
            {
                Assert.Ignore("Battle does not yet compose PlatoonAI; routed rear withdrawal is not executable.");
            }

            Battle battle = rig.Battle;
            Platoon platoon = CombatTestSupport.Platoon(battle, "us_tank_plt", "blue",
                new SimVec2(2000.0, 2000.0));
            platoon.Morale = 15.0;
            battle.RunSeconds(3.0);
            Assert.That(platoon.Routed, Is.True);
            SimVec2 start = platoon.Centroid();
            battle.RunSeconds(40.0);
            Assert.That(start.DistanceTo(platoon.Centroid()), Is.GreaterThan(150.0));
            battle.RunSeconds(30.0);
            Assert.That(platoon.Routed, Is.False);
            Assert.That(platoon.Morale, Is.GreaterThanOrEqualTo(MoraleSystem.AfterRout));
        }

        private static int[] SumEngagements(string redFormation, bool forest)
        {
            int[] total = { 0, 0 };
            foreach (int seed in Seeds)
            {
                int[] result = Engage(redFormation, forest, seed);
                total[0] += result[0];
                total[1] += result[1];
            }

            TestContext.WriteLine($"{redFormation} forest={forest}: blue losses {total[0]}, red losses {total[1]}");
            return total;
        }

        private static int[] Engage(string redFormation, bool forest, long seed)
        {
            using CombatTestRig rig = CombatTestSupport.Create(seed,
                CombatTestSupport.FlatMap(forest ? Forest : (MapRect?)null));
            Battle battle = rig.Battle;
            Platoon blue = CombatTestSupport.Platoon(battle, "us_mech_plt", "blue",
                new SimVec2(2000.0, 1330.0));
            blue.Sop.MountPolicy = "stay_mounted";
            CombatTestSupport.Move(battle, blue, new SimVec2(2000.0, 1330.0), OrderType.DEFEND, 0.0,
                new JObject { ["formation"] = "line" });
            Platoon red = CombatTestSupport.Platoon(battle, "ru_tank_plt", "red",
                new SimVec2(2000.0, 3900.0), Math.PI);
            CombatTestSupport.Move(battle, red, new SimVec2(2000.0, 1600.0), OrderType.ATTACK_MOVE, Math.PI,
                new JObject { ["formation"] = redFormation });
            int tick = 0;
            while (tick < 4800 && !red.IsDestroyed() && !blue.IsDestroyed() && !red.Routed)
            {
                battle.StepTick();
                ++tick;
            }

            return new[] { CombatTestSupport.Losses(blue), CombatTestSupport.Losses(red) };
        }

        private static void ClearAmmo(IEnumerable<Element> elements)
        {
            foreach (Element element in elements)
            {
                foreach (string weaponId in element.Def.Weapons)
                {
                    element.Ammo[weaponId] = 0;
                }
            }
        }

        private static bool HasSystem<T>(Battle battle) where T : SimSystem
        {
            foreach (SimSystem system in battle.World.GetSystems())
            {
                if (system is T)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
