using System;
using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class MoraleSystemTests
    {
        [Test]
        public void SuppressionDecaysEveryTickWithLeaderRate()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon platoon = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Element led = platoon.Elements[0];
            Element unled = platoon.Elements[1];
            unled.Platoon = null;
            led.Suppression = 10.0;
            unled.Suppression = 10.0;
            rig.Morale.Step(rig.Battle.World);
            Assert.That(led.Suppression, Is.EqualTo(10.0 - MoraleSystem.SuppressionDecayLed * SimConst.TickDt)
                .Within(0.000001));
            Assert.That(unled.Suppression, Is.EqualTo(10.0 - MoraleSystem.SuppressionDecay * SimConst.TickDt)
                .Within(0.000001));
            led.State = ElementState.DESTROYED;
            led.Suppression = 10.0;
            rig.Morale.Step(rig.Battle.World);
            Assert.That(led.Suppression, Is.EqualTo(10.0));
        }

        [Test]
        public void MoraleUpdatesOnlyEveryTenthTickAndRespectsCommsFloor()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon platoon = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            SimWorld clock = new SimWorld();
            platoon.Morale = 40.0;
            platoon.CommsState = Unit.Comms.Cut;
            platoon.LastContactTick = 0;
            platoon.LastHitTick = 0;
            rig.Morale.Step(clock);
            Assert.That(platoon.Morale, Is.EqualTo(39.5).Within(0.000001));
            clock.StepTick();
            platoon.Morale = 40.0;
            rig.Morale.Step(clock);
            Assert.That(platoon.Morale, Is.EqualTo(40.0));
            while (clock.Tick < 10)
            {
                clock.StepTick();
            }

            platoon.Morale = 30.2;
            rig.Morale.Step(clock);
            Assert.That(platoon.Morale, Is.EqualTo(MoraleSystem.CommsCutFloor).Within(0.000001));
            platoon.CommsState = Unit.Comms.Good;
            platoon.Morale = 50.0;
            platoon.LastContactTick = -1000;
            platoon.LastHitTick = -1000;
            rig.Morale.Step(clock);
            Assert.That(platoon.Morale, Is.EqualTo(50.3).Within(0.000001));
        }

        [Test]
        public void CompanyMoraleAveragesOnlySurvivingLinePlatoons()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Company company = CombatTestSupport.Company(rig.Battle, "us_tank_co", "blue",
                new SimVec2(1000.0, 1000.0));
            Assert.That(company.Platoons.Count, Is.GreaterThanOrEqualTo(2));
            Platoon second = company.Platoons[1];
            foreach (Platoon platoon in company.Platoons)
            {
                platoon.Morale = 50.0;
                platoon.LastContactTick = 0;
                platoon.LastHitTick = 0;
            }

            second.Morale = 70.0;
            double expected = (50.0 * (company.Platoons.Count - 1) + 70.0) / company.Platoons.Count;
            rig.Morale.Step(rig.Battle.World);
            Assert.That(company.Morale, Is.EqualTo(expected).Within(0.000001));
            foreach (Element element in second.Elements)
            {
                element.State = ElementState.DESTROYED;
            }

            rig.Morale.Step(rig.Battle.World);
            Assert.That(company.Morale, Is.EqualTo(50.0).Within(0.000001));
        }

        [Test]
        public void RoutAndRallyEmitEventsAtExactIntervals()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Platoon platoon = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            SimWorld clock = new SimWorld();
            platoon.Morale = 15.0;
            platoon.LastContactTick = 0;
            platoon.LastHitTick = 0;
            rig.Battle.World.Events.Drain();
            clock.StepTick();
            rig.Morale.Step(clock);
            Assert.That(platoon.Routed, Is.False, "morale updates do not run off phase");
            while (clock.Tick < 10)
            {
                clock.StepTick();
            }

            rig.Morale.Step(clock);
            Assert.That(platoon.Routed, Is.True);
            Assert.That(platoon.RoutedUntilTick, Is.EqualTo(10 + MoraleSystem.RoutTicks));
            Assert.That(platoon.Posture, Is.EqualTo("routed"));
            while (clock.Tick < 10 + MoraleSystem.RoutTicks)
            {
                clock.StepTick();
            }

            rig.Morale.Step(clock);
            Assert.That(platoon.Routed, Is.False);
            Assert.That(platoon.Posture, Is.EqualTo("idle"));
            Assert.That(platoon.Morale, Is.GreaterThanOrEqualTo(MoraleSystem.AfterRout));
            List<SimEvent> events = rig.Battle.World.Events.Drain();
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].Type, Is.EqualTo("routed"));
            Assert.That(events[0].Tick, Is.EqualTo(10));
            Assert.That(events[1].Type, Is.EqualTo("rally"));
            Assert.That(events[1].Tick, Is.EqualTo(10 + MoraleSystem.RoutTicks));
        }

        [Test]
        public void CasualtyLossDamageCommanderAndKillHooksUseDocumentedAmounts()
        {
            using CombatTestRig rig = CombatTestSupport.Create();
            Company redCompany = CombatTestSupport.Company(rig.Battle, "ru_tank_co", "red",
                new SimVec2(2000.0, 2000.0), Math.PI);
            Platoon victimPlatoon = redCompany.Platoons[0];
            Platoon otherPlatoon = redCompany.Platoons[1];
            Platoon killerPlatoon = CombatTestSupport.Platoon(rig.Battle, "us_tank_plt", "blue",
                new SimVec2(1000.0, 1000.0));
            Element victim = victimPlatoon.Elements[0];
            Element killer = killerPlatoon.Elements[0];
            victim.KilledBy = killer.Id;
            killerPlatoon.Morale = 98.0;
            rig.Morale.OnElementLost(victim);
            Assert.That(victimPlatoon.Morale, Is.EqualTo(85.0).Within(0.000001));
            Assert.That(otherPlatoon.Morale, Is.EqualTo(97.0).Within(0.000001));
            Assert.That(killerPlatoon.Morale, Is.EqualTo(100.0).Within(0.000001));
            rig.Morale.OnElementDamaged(otherPlatoon.Elements[0]);
            Assert.That(otherPlatoon.Morale, Is.EqualTo(92.0).Within(0.000001));
            rig.Morale.OnCasualties(otherPlatoon, 2);
            Assert.That(otherPlatoon.Morale, Is.EqualTo(89.0).Within(0.000001));
            rig.Morale.OnCommanderLost(victimPlatoon);
            Assert.That(victimPlatoon.Morale, Is.EqualTo(75.0).Within(0.000001));
            rig.Morale.OnCommanderLost(redCompany);
            Assert.That(victimPlatoon.Morale, Is.EqualTo(65.0).Within(0.000001));
            Assert.That(otherPlatoon.Morale, Is.EqualTo(79.0).Within(0.000001));
            victimPlatoon.Morale = MoraleSystem.Shaken;
            Assert.That(MoraleSystem.IsShaken(victimPlatoon), Is.True);
            victimPlatoon.Morale = MoraleSystem.Shaken + 0.1;
            Assert.That(MoraleSystem.IsShaken(victimPlatoon), Is.False);
        }
    }
}
