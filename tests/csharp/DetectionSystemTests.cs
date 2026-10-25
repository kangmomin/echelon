using System;
using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class DetectionSystemTests
    {
        [Test]
        public void ProbabilityRewardsMovementFiringTrackingAndProximity()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon blue = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(2600.0, 3300.0));
            Platoon red = CombatTestSupport.Platoon(rig.Battle, "ru_tank_plt", "red",
                new SimVec2(3400.0, 3300.0), Math.PI);
            Element observer = blue.Elements[0];
            Element target = red.Elements[0];
            target.StationaryTicks = 100;
            const long tick = 1000;
            double baseline = rig.Detection.Probability(observer, target, 1000.0, tick);
            target.Velocity = new SimVec2(5.0, 0.0);
            double moving = rig.Detection.Probability(observer, target, 1000.0, tick);
            target.Velocity = SimVec2.Zero;
            target.LastFiredTick = 990;
            double firing = rig.Detection.Probability(observer, target, 1000.0, tick);
            target.LastFiredTick = -100000;
            double far = rig.Detection.Probability(observer, target, 2200.0, tick);
            rig.Detection.Seen["blue"][target.Id] = tick;
            double tracked = rig.Detection.Probability(observer, target, 1000.0, tick);

            Assert.That(moving, Is.GreaterThan(baseline));
            Assert.That(firing, Is.GreaterThan(moving));
            Assert.That(far, Is.LessThan(baseline));
            Assert.That(tracked, Is.GreaterThan(baseline));
            Assert.That(rig.Detection.Probability(observer, target, 100000.0, tick), Is.Zero);
        }

        [Test]
        public void ProbabilityAppliesForestConcealmentAndHighAltitudeVisibility()
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
            double open = openRig.Detection.Probability(openBlue.Elements[0], openRed.Elements[0], 1000.0, 100);
            double concealed = forestRig.Detection.Probability(forestBlue.Elements[0], forestRed.Elements[0], 1000.0, 100);
            Assert.That(concealed, Is.LessThan(open));

            Platoon aviation = CombatTestSupport.Platoon(openRig.Battle, "ru_aviation_sec", "red",
                new SimVec2(3000.0, 1000.0));
            Element helicopter = aviation.Elements[0];
            aviation.Sop.Altitude = "low";
            double low = openRig.Detection.Probability(openBlue.Elements[0], helicopter, 2000.0, 100);
            aviation.Sop.Altitude = "high";
            double high = openRig.Detection.Probability(openBlue.Elements[0], helicopter, 2000.0, 100);
            Assert.That(high, Is.GreaterThan(low));
            Assert.That(helicopter.AltitudeM(), Is.EqualTo(Element.AltitudeHighM));
        }

        [Test]
        public void ScanUsesElementPhaseAndNeverPassesNearestTwentyCandidates()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Battle battle = rig.Battle;
            Platoon observerPlatoon = CombatTestSupport.Platoon(battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Element observer = observerPlatoon.Elements[0];
            for (int i = 1; i < observerPlatoon.Elements.Count; ++i)
            {
                observerPlatoon.Elements[i].State = ElementState.DESTROYED;
            }

            observer.Pos = new SimVec2(1000.0, 1000.0);
            observer.HasMoveTarget = false;
            List<Element> targets = new List<Element>();
            for (int platoonIndex = 0; platoonIndex < 6; ++platoonIndex)
            {
                Platoon red = CombatTestSupport.Platoon(battle, "ru_tank_plt", "red",
                    new SimVec2(2000.0, 2000.0), Math.PI);
                foreach (Element element in red.Elements)
                {
                    int index = targets.Count;
                    element.Pos = new SimVec2(1050.0 + index * 50.0, 1000.0);
                    element.HasMoveTarget = false;
                    targets.Add(element);
                }
            }

            foreach (Element element in battle.Elements)
            {
                foreach (string weaponId in element.Def.Weapons)
                {
                    element.Ammo[weaponId] = 0;
                }
            }

            battle.RebuildSpatial();
            SimWorld clock = new SimWorld();
            long firstSeen = -1;
            for (int i = 0; i < 200 && firstSeen < 0; ++i)
            {
                long tick = clock.Tick;
                rig.Detection.Step(clock);
                if (observerPlatoon.Contacts.Count > 0)
                {
                    firstSeen = tick;
                }

                clock.StepTick();
            }

            for (int i = 0; i < 200; ++i)
            {
                rig.Detection.Step(clock);
                clock.StepTick();
            }

            Assert.That(firstSeen, Is.GreaterThanOrEqualTo(0));
            Assert.That((observer.Id + firstSeen) % DetectionSystem.ScanInterval, Is.Zero);
            Assert.That(observerPlatoon.Contacts.Count, Is.GreaterThan(0));
            for (int i = DetectionSystem.MaxCandidates; i < targets.Count; ++i)
            {
                Assert.That(observerPlatoon.Contacts.ContainsKey(targets[i].Id), Is.False,
                    "candidate beyond the nearest twenty entered the contact list");
            }
        }

        [Test]
        public void ReportsAndContactsExpireOnFiftiethTickMaintenance()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon blue = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            TaskForce taskForce = rig.Battle.TaskForces["blue"];
            taskForce.KnownEnemies[999] = new Dictionary<string, object>
            {
                ["pos"] = new SimVec2(2000.0, 2000.0),
                ["tick"] = -1801L,
                ["class"] = "tank"
            };
            blue.Contacts[999] = -301;
            rig.Detection.Step(rig.Battle.World);
            Assert.That(taskForce.KnownEnemies.ContainsKey(999), Is.False);
            Assert.That(blue.Contacts.ContainsKey(999), Is.False);
        }

        [Test]
        public void ReportDeliveryAndExpirationUseInsertionOrderAfterReinsertion()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            var pendingBySide = (Dictionary<string, InsertionOrderedDictionary<int, ContactReport>>)
                typeof(DetectionSystem).GetField("_pending", flags)!.GetValue(rig.Detection)!;
            InsertionOrderedDictionary<int, ContactReport> pending = pendingBySide["blue"];
            pending.Add(76, Report());
            pending.Add(84, Report());
            Assert.That(pending.Remove(76), Is.True);
            pending.Add(76, Report());
            Assert.That(pending.Keys, Is.EqualTo(new[] { 84, 76 }));

            Invoke("ProcessReports", 0L);
            TaskForce taskForce = rig.Battle.TaskForces["blue"];
            Assert.That(rig.Detection.ReportsDelivered, Is.EqualTo(2));
            Assert.That(pending, Is.Empty);
            Assert.That(taskForce.KnownEnemies.Keys, Is.EqualTo(new[] { 84, 76 }));

            taskForce.KnownEnemies[84]["tick"] = 0L;
            taskForce.KnownEnemies[76]["tick"] = 2000L;
            Invoke("Expire", 2000L);
            Assert.That(taskForce.KnownEnemies.Keys, Is.EqualTo(new[] { 76 }));
            taskForce.KnownEnemies.Add(84, Known(2000L));
            Assert.That(taskForce.KnownEnemies.Keys, Is.EqualTo(new[] { 76, 84 }));

            void Invoke(string method, long tick)
            {
                typeof(DetectionSystem).GetMethod(method, flags)!.Invoke(rig.Detection, new object[] { tick });
            }

            static ContactReport Report()
            {
                return new ContactReport
                {
                    Position = SimVec2.Zero,
                    ElementClass = "tank",
                    Tick = 0L,
                    PlatoonId = -1,
                    DeliverTick = 0L
                };
            }

            static Dictionary<string, object> Known(long tick)
            {
                return new Dictionary<string, object>
                {
                    ["pos"] = SimVec2.Zero,
                    ["tick"] = tick,
                    ["class"] = "tank"
                };
            }
        }

        [Test]
        public void ContactsRetainInsertionOrderAfterExpirationAndRediscovery()
        {
            var platoon = new Platoon();
            platoon.Contacts[84] = 1;
            platoon.Contacts[76] = 2;
            Assert.That(platoon.Contacts.Remove(84), Is.True);
            platoon.Contacts[84] = 3;

            Assert.That(platoon.Contacts.Keys, Is.EqualTo(new[] { 76, 84 }));
        }

        [Test]
        public void ConfidenceAndSeenRecentlyPreserveBoundaryRules()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Dictionary<string, object> info = new Dictionary<string, object> { ["tick"] = 100L };
            Assert.That(DetectionSystem.Confidence(info, 150), Is.EqualTo(0));
            Assert.That(DetectionSystem.Confidence(info, 151), Is.EqualTo(1));
            Assert.That(DetectionSystem.Confidence(info, 400), Is.EqualTo(1));
            Assert.That(DetectionSystem.Confidence(info, 401), Is.EqualTo(2));
            rig.Detection.Seen["blue"][42] = 100;
            Assert.That(rig.Detection.SeenRecently("blue", 42, 200), Is.True);
            Assert.That(rig.Detection.SeenRecently("blue", 42, 201), Is.False);
            Assert.That(rig.Detection.SeenRecently("unknown", 42, 200), Is.False);
        }
    }
}
