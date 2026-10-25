using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class FiresFireSupportSystemTests
    {
        private static readonly SimVec2 Target = new SimVec2(2000.0, 2600.0);

        [Test]
        public void RejectedRequestsDoNotConsumeRngOrPurchases()
        {
            FiresTestContext context = new FiresTestContext(12);
            TaskForce taskForce = context.AddTaskForce();
            taskForce.CasSorties = 1;
            FireSupportSystem fires = new FireSupportSystem(context);
            long initialState = context.World.Rng.GetState();
            Dictionary<string, object> smoke = fires.Request("blue", FireSupportSystem.Asset.CAS, Target, true);
            Dictionary<string, object> noController = fires.Request("blue", FireSupportSystem.Asset.CAS, Target);
            Assert.That(smoke["ok"], Is.EqualTo(false));
            Assert.That(noController["ok"], Is.EqualTo(false));
            context.AddPlatoon(taskForce, "us_jtac", "fo", new SimVec2(2000.0, 1500.0));
            context.CommandAvailable = false;
            Dictionary<string, object> noCapacity = fires.Request("blue", FireSupportSystem.Asset.CAS, Target);
            Assert.That(noCapacity["ok"], Is.EqualTo(false));
            Assert.That(taskForce.CasSorties, Is.EqualTo(1));
            Assert.That(context.World.Rng.GetState(), Is.EqualTo(initialState));
            Assert.That(context.SpendCalls, Is.EqualTo(1));
            Assert.That(fires.Missions, Is.Empty);
        }

        [Test]
        public void RequestUsesOneDelayDrawAndEmitsOriginalPayload()
        {
            FiresTestContext context = new FiresTestContext(123);
            TaskForce taskForce = context.AddTaskForce();
            Platoon mortar = context.AddPlatoon(taskForce, "us_m1064", "mortar", new SimVec2(2000.0, 600.0), 4);
            Platoon observer = context.AddPlatoon(taskForce, "us_m7_fist", "fo", new SimVec2(2000.0, 1300.0));
            FireSupportSystem fires = new FireSupportSystem(context);
            long initialState = context.World.Rng.GetState();
            SimRng expected = new SimRng();
            expected.SetState(initialState);
            double randomDelay = expected.NextRange(0.0, FireSupportSystem.RandomSec[0]);
            FireSupportSystem.Mission mission = FiresSupportTestFixture.RequestedMission(
                fires.Request("blue", FireSupportSystem.Asset.MORTAR, Target));
            Assert.That(context.World.Rng.GetState(), Is.EqualTo(expected.GetState()));
            Assert.That(mission.DelaySec, Is.EqualTo(50.0 + randomDelay).Within(0.0001));
            Assert.That(mission.ReadyTick, Is.EqualTo((long)SimMath.Round(mission.DelaySec * SimConst.TickRate)));
            Assert.That(mission.ShooterUnitId, Is.EqualTo(mortar.Id));
            Assert.That(mission.ObserverId, Is.EqualTo(observer.Elements[0].Id));
            Assert.That(mission.ObserverQuality, Is.EqualTo(0));
            Assert.That(mission.RoundsLeft, Is.EqualTo(24));
            Assert.That(mission.Sigma, Is.EqualTo(35.0));
            List<SimEvent> events = context.World.Events.Drain();
            Assert.That(events.Count, Is.EqualTo(1));
            SimEvent requested = events[0];
            Assert.That(requested.Type, Is.EqualTo("fire_requested"));
            Assert.That(requested.Tick, Is.EqualTo(0));
            Assert.That(requested.Payload.Count, Is.EqualTo(8));
            Assert.That(requested.Payload["side"], Is.EqualTo("blue"));
            Assert.That(requested.Payload["mission"], Is.EqualTo(mission.Id));
            Assert.That(requested.Payload["asset"], Is.EqualTo((int)FireSupportSystem.Asset.MORTAR));
            Assert.That(requested.Payload["smoke"], Is.EqualTo(false));
            Assert.That(requested.Payload["pos"], Is.EqualTo(Target));
            Assert.That(requested.Payload["seconds"], Is.EqualTo(mission.DelaySec));
            Assert.That(requested.Payload["observer"], Is.EqualTo(observer.Elements[0].Id));
            Assert.That(requested.Payload["quality"], Is.EqualTo(0));
        }

        [Test]
        public void ObservedMortarAdjustsThenWaitsForCorrectionAndReload()
        {
            FiresTestContext context = new FiresTestContext(22);
            TaskForce taskForce = context.AddTaskForce();
            Platoon mortar = context.AddPlatoon(taskForce, "us_m1064", "mortar", new SimVec2(2000.0, 600.0), 4);
            context.AddPlatoon(taskForce, "us_m7_fist", "fo", new SimVec2(2000.0, 1300.0));
            FireSupportSystem fires = new FireSupportSystem(context);
            FireSupportSystem.Mission mission = FiresSupportTestFixture.RequestedMission(
                fires.Request("blue", FireSupportSystem.Asset.MORTAR, Target));
            context.World.AddSystem(fires);
            context.World.Events.Drain();
            mission.ReadyTick = 0;
            context.World.StepTick();
            Assert.That(mission.Phase, Is.EqualTo(FireSupportSystem.Phase.ADJUSTING));
            Assert.That(mission.RoundsFired, Is.EqualTo(1));
            Assert.That(mission.RoundsLeft, Is.EqualTo(23));
            Assert.That(mission.NextRoundTick, Is.EqualTo(150));
            Assert.That(mortar.Elements[0].Ammo["m120_mortar"], Is.EqualTo(68));
            Assert.That(mortar.Elements[0].LastFiredTick, Is.EqualTo(-1000000));
            Assert.That(fires.Shells.Count, Is.EqualTo(1));
            Assert.That(fires.Shells[0].LandTick, Is.EqualTo(80));
            Step(context.World, 79);
            Assert.That(fires.Shells.Count, Is.EqualTo(1));
            context.World.StepTick();
            Assert.That(fires.Shells, Is.Empty);
            Assert.That(mission.FirstImpact.IsFinite(), Is.True);
            Assert.That(context.Impacts.Count, Is.EqualTo(1));
            Step(context.World, 69);
            Assert.That(mission.Phase, Is.EqualTo(FireSupportSystem.Phase.ADJUSTING));
            context.World.StepTick();
            Assert.That(mission.Phase, Is.EqualTo(FireSupportSystem.Phase.EFFECT));
            Assert.That(mission.RoundsFired, Is.EqualTo(1));
            context.World.StepTick();
            Assert.That(mission.RoundsFired, Is.EqualTo(5));
            Assert.That(mission.RoundsLeft, Is.EqualTo(19));
            Assert.That(mission.NextRoundTick, Is.EqualTo(301));
            Assert.That(fires.Shells.Count, Is.EqualTo(4));
            foreach (Element element in mortar.Elements)
            {
                Assert.That(element.LastFiredTick, Is.EqualTo(151));
            }
        }

        [Test]
        public void ArtilleryScatterMatchesConsecutiveRngDrawsAndShellPayload()
        {
            FiresTestContext context = new FiresTestContext(31);
            TaskForce taskForce = context.AddTaskForce();
            taskForce.ArtilleryMissions = 1;
            FireSupportSystem fires = new FireSupportSystem(context);
            FireSupportSystem.Mission mission = FiresSupportTestFixture.RequestedMission(
                fires.Request("blue", FireSupportSystem.Asset.ARTILLERY, Target));
            context.World.AddSystem(fires);
            mission.ReadyTick = 0;
            context.World.StepTick();
            context.World.Events.Drain();
            Assert.That(mission.Phase, Is.EqualTo(FireSupportSystem.Phase.EFFECT));
            Assert.That(fires.Shells, Is.Empty);
            SimRng expected = new SimRng();
            expected.SetState(context.World.Rng.GetState());
            List<SimVec2> expectedPositions = new List<SimVec2>();
            for (int i = 0; i < 6; ++i)
            {
                expectedPositions.Add(context.Terrain.ClampToMap(Target + expected.NextScatter(mission.Sigma), 5.0));
            }

            context.World.StepTick();
            Assert.That(fires.Shells.Count, Is.EqualTo(6));
            Assert.That(context.World.Rng.GetState(), Is.EqualTo(expected.GetState()));
            for (int i = 0; i < 6; ++i)
            {
                Assert.That(fires.Shells[i].Pos, Is.EqualTo(expectedPositions[i]));
                Assert.That(fires.Shells[i].LandTick, Is.EqualTo(2));
                Assert.That(fires.Shells[i].SourceId, Is.EqualTo(-1));
            }

            Assert.That(mission.RoundsLeft, Is.EqualTo(12));
            Assert.That(mission.NextRoundTick, Is.EqualTo(151));
            context.World.StepTick();
            Assert.That(context.Impacts.Count, Is.EqualTo(6));
            Assert.That(mission.FirstImpact, Is.EqualTo(expectedPositions[0]));
            List<SimEvent> events = context.World.Events.Drain();
            Assert.That(events.Count, Is.EqualTo(6));
            for (int i = 0; i < 6; ++i)
            {
                Assert.That(events[i].Type, Is.EqualTo("shell"));
                Assert.That(events[i].Tick, Is.EqualTo(2));
                Assert.That(events[i].Payload.Count, Is.EqualTo(5));
                Assert.That(events[i].Payload["pos"], Is.EqualTo(expectedPositions[i]));
                Assert.That(events[i].Payload["smoke"], Is.EqualTo(false));
                Assert.That(events[i].Payload["asset"], Is.EqualTo((int)FireSupportSystem.Asset.ARTILLERY));
                Assert.That(events[i].Payload["side"], Is.EqualTo("blue"));
                Assert.That(events[i].Payload["radius"], Is.EqualTo(0.0));
                Assert.That(context.Impacts[i].Position, Is.EqualTo(expectedPositions[i]));
                Assert.That(context.Impacts[i].Source, Is.Null);
            }
        }

        [Test]
        public void ArtillerySmokeHasOriginalRadiusAndLifetime()
        {
            FiresTestContext context = new FiresTestContext(32);
            TaskForce taskForce = context.AddTaskForce();
            taskForce.ArtilleryMissions = 1;
            FireSupportSystem fires = new FireSupportSystem(context);
            FireSupportSystem.Mission mission = FiresSupportTestFixture.RequestedMission(
                fires.Request("blue", FireSupportSystem.Asset.ARTILLERY, Target, true));
            context.World.AddSystem(fires);
            mission.ReadyTick = 0;
            Step(context.World, 3);
            Assert.That(context.Smokes.Count, Is.EqualTo(6));
            Assert.That(context.Impacts, Is.Empty);
            foreach (Los.Smoke smoke in context.Smokes)
            {
                Assert.That(smoke.Radius, Is.EqualTo(50.0));
                Assert.That(smoke.UntilTick, Is.EqualTo(1502));
            }

            List<SimEvent> events = context.World.Events.Drain();
            Assert.That(events.FindAll(e => e.Type == "shell").Count, Is.EqualTo(6));
            Assert.That(events.FindAll(e => e.Type == "shell" && (double)e.Payload["radius"] == 50.0).Count, Is.EqualTo(6));
        }

        [Test]
        public void SmokePruningRunsOnlyOnSixHundredTickBoundary()
        {
            FiresTestContext context = new FiresTestContext(1);
            FireSupportSystem fires = new FireSupportSystem(context);
            context.Smokes.Add(new Los.Smoke(SimVec2.Zero, 35.0, 1));
            context.World.AddSystem(fires);
            Step(context.World, 600);
            Assert.That(context.Smokes.Count, Is.EqualTo(1));
            context.World.StepTick();
            Assert.That(context.Smokes, Is.Empty);
        }

        private static void Step(SimWorld world, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                world.StepTick();
            }
        }
    }
}
