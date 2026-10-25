using System;
using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    [Category("M5")]
    public sealed class FiresM5IntegrationTests
    {
        [Test]
        public void ForwardObserverMakesMortarFasterAndTighter()
        {
            double observedMiss = 0.0;
            double observedDelay = 0.0;
            double unobservedMiss = 0.0;
            double unobservedDelay = 0.0;
            foreach (int seed in new[] { 1, 2, 3 })
            {
                MortarResult observed = MortarRun(seed, true);
                MortarResult unobserved = MortarRun(seed, false);
                Assert.That(observed.ObserverQuality, Is.EqualTo(0.0), "forward observer");
                Assert.That(unobserved.ObserverQuality, Is.EqualTo(2.0), "map fire");
                Assert.That(observed.ImpactCount, Is.GreaterThan(10.0), "effect rounds");
                observedMiss += observed.MeanMiss;
                unobservedMiss += unobserved.MeanMiss;
                observedDelay += observed.Delay;
                unobservedDelay += unobserved.Delay;
            }

            TestContext.Out.WriteLine(
                $"observer: miss {observedMiss / 3.0:F0} m delay {observedDelay / 3.0:F0} s; " +
                $"none: miss {unobservedMiss / 3.0:F0} m delay {unobservedDelay / 3.0:F0} s");
            Assert.That(observedMiss * 1.4, Is.LessThan(unobservedMiss));
            Assert.That(observedDelay + 20.0, Is.LessThan(unobservedDelay));
            Assert.That(observedDelay / 3.0, Is.InRange(40.0, 95.0));
        }

        [Test]
        public void ArtilleryNeedsPurchasedMissionsAndHurts()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.FiresBattle(4, true);
            TaskForce taskForce = rig.Battle.TaskForces["blue"];
            taskForce.ArtilleryMissions = 1;
            FireSupportSystem.Mission mission = FiresSupportTestFixture.RequestedMission(
                rig.Fires.Request("blue", FireSupportSystem.Asset.ARTILLERY, FiresSupportTestFixture.Target));
            Assert.That(mission.DelaySec, Is.InRange(110.0, 190.0));
            Dictionary<string, object> again = rig.Fires.Request(
                "blue", FireSupportSystem.Asset.ARTILLERY, FiresSupportTestFixture.Target);
            Assert.That(again["ok"], Is.EqualTo(false));
            rig.Battle.RunSeconds(mission.DelaySec + 120.0);
            Assert.That(mission.Phase, Is.EqualTo(FireSupportSystem.Phase.DONE));
            Platoon red = rig.Battle.TaskForces["red"].Organic[0];
            int hurt = 0;
            foreach (Element element in red.Elements)
            {
                if (element.State != ElementState.OK)
                {
                    ++hurt;
                }
            }

            Assert.That(hurt, Is.GreaterThan(0), "155 mm effect should damage or destroy tanks");
        }

        [Test]
        public void CasNeedsControllerAndFacesAirDefence()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.FiresBattle(5, false);
            TaskForce taskForce = rig.Battle.TaskForces["blue"];
            taskForce.CasSorties = 2;
            Dictionary<string, object> rejected = rig.Fires.Request(
                "blue", FireSupportSystem.Asset.CAS, FiresSupportTestFixture.Target);
            Assert.That(rejected["ok"], Is.EqualTo(false));
            FiresSupportTestFixture.Platoon(rig, "us_jtac_team", "blue", new SimVec2(2000.0, 1500.0));
            Platoon airDefence = FiresSupportTestFixture.Platoon(rig, "ru_ada_section", "red",
                FiresSupportTestFixture.Target + new SimVec2(0.0, 600.0), Math.PI);
            rig.Battle.RunTicks(20);
            FireSupportSystem.Mission mission = FiresSupportTestFixture.RequestedMission(
                rig.Fires.Request("blue", FireSupportSystem.Asset.CAS, FiresSupportTestFixture.Target));
            Assert.That(mission.ObserverQuality, Is.EqualTo(0));
            Assert.That(mission.DelaySec, Is.InRange(85.0, 160.0));
            int before = airDefence.Elements[0].Ammo["9m338_sam"];
            rig.Battle.RunSeconds(mission.DelaySec + 5.0);
            Assert.That(mission.Phase, Is.EqualTo(FireSupportSystem.Phase.DONE).Or.EqualTo(FireSupportSystem.Phase.CANCELLED));
            int after = airDefence.Elements[0].Ammo["9m338_sam"];
            Assert.That(after, Is.EqualTo(before - 1));
            Assert.That(taskForce.CasSorties, Is.EqualTo(1));
        }

        [Test]
        public void SmokeBlocksLineOfSight()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.FiresBattle(6, true);
            Element observer = rig.Battle.TaskForces["blue"].Organic[1].Elements[0];
            Assert.That(Los.Check(rig.Battle.Terrain, rig.Battle.Smokes, rig.Battle.Tick(), observer.Pos, 2.5,
                FiresSupportTestFixture.Target, 2.0), Is.True);
            SimVec2 target = FiresSupportTestFixture.Target.Lerp(observer.Pos, 0.15);
            FireSupportSystem.Mission mission = FiresSupportTestFixture.RequestedMission(
                rig.Fires.Request("blue", FireSupportSystem.Asset.MORTAR, target, true));
            rig.Battle.RunSeconds(mission.DelaySec + 70.0);
            Assert.That(rig.Battle.Smokes.Count, Is.GreaterThan(3));
            Assert.That(Los.Check(rig.Battle.Terrain, rig.Battle.Smokes, rig.Battle.Tick(), observer.Pos, 2.5,
                FiresSupportTestFixture.Target, 2.0), Is.False);
        }

        [Test]
        public void JammerSlowsFireRequests()
        {
            List<double> delays = new List<double>();
            foreach (bool jammed in new[] { false, true })
            {
                using SupportBattleRig rig = FiresSupportTestFixture.FiresBattle(7, true);
                if (jammed)
                {
                    FiresSupportTestFixture.Platoon(rig, "ru_ew_section", "red", new SimVec2(2000.0, 2100.0), Math.PI);
                    rig.Battle.RunTicks(25);
                }

                Platoon observer = rig.Battle.TaskForces["blue"].Organic[1];
                FireSupportSystem.Mission mission = FiresSupportTestFixture.RequestedMission(
                    rig.Fires.Request("blue", FireSupportSystem.Asset.MORTAR, FiresSupportTestFixture.Target));
                delays.Add(mission.DelaySec - FireSupportSystem.FdcSec[0]);
                if (jammed)
                {
                    Assert.That(observer.CommsState, Is.Not.EqualTo(Unit.Comms.Good));
                }
            }

            Assert.That(delays[1], Is.GreaterThan(delays[0] + 3.0));
        }

        [Test]
        public void UnderFireSopDisperses()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.FiresBattle(9, true);
            Platoon red = rig.Battle.TaskForces["red"].Organic[0];
            red.Sop.UnderFire = "disperse";
            SimVec2 start = red.Centroid();
            FireSupportSystem.Mission mission = FiresSupportTestFixture.RequestedMission(
                rig.Fires.Request("blue", FireSupportSystem.Asset.MORTAR, FiresSupportTestFixture.Target));
            rig.Battle.RunSeconds(mission.DelaySec + 60.0);
            Assert.That(red.Centroid().DistanceTo(start), Is.GreaterThan(120.0));
        }

        private static MortarResult MortarRun(long seed, bool withForwardObserver)
        {
            using SupportBattleRig rig = FiresSupportTestFixture.FiresBattle(seed, withForwardObserver);
            FireSupportSystem.Mission mission = FiresSupportTestFixture.RequestedMission(
                rig.Fires.Request("blue", FireSupportSystem.Asset.MORTAR, FiresSupportTestFixture.Target));
            List<SimVec2> impacts = new List<SimVec2>();
            int elapsed = 0;
            while (elapsed < 3000 && mission.Phase != FireSupportSystem.Phase.DONE)
            {
                rig.Battle.StepTick();
                ++elapsed;
                foreach (SimEvent simEvent in FiresSupportTestFixture.Drain(rig))
                {
                    if (simEvent.Type == "shell" && mission.Phase == FireSupportSystem.Phase.EFFECT)
                    {
                        impacts.Add((SimVec2)simEvent.Payload["pos"]);
                    }
                }
            }

            rig.Battle.RunTicks(200);
            foreach (SimEvent simEvent in FiresSupportTestFixture.Drain(rig))
            {
                if (simEvent.Type == "shell")
                {
                    impacts.Add((SimVec2)simEvent.Payload["pos"]);
                }
            }

            double miss = 0.0;
            foreach (SimVec2 impact in impacts)
            {
                miss += impact.DistanceTo(FiresSupportTestFixture.Target);
            }

            return new MortarResult(miss / Math.Max(1.0, impacts.Count), mission.DelaySec, impacts.Count,
                mission.ObserverQuality);
        }

        private readonly struct MortarResult
        {
            internal MortarResult(double meanMiss, double delay, double impactCount, double observerQuality)
            {
                MeanMiss = meanMiss;
                Delay = delay;
                ImpactCount = impactCount;
                ObserverQuality = observerQuality;
            }

            internal double MeanMiss { get; }
            internal double Delay { get; }
            internal double ImpactCount { get; }
            internal double ObserverQuality { get; }
        }
    }
}
