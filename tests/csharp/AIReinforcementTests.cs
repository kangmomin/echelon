using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class AIReinforcementTests
    {
        [Test]
        public void WaveScheduleMatchesTenAndTwentyMinutesAndClampsLookup()
        {
            Assert.That(ReinforcementSystem.WaveTick(0), Is.Zero);
            Assert.That(ReinforcementSystem.WaveTick(1), Is.EqualTo(6000));
            Assert.That(ReinforcementSystem.WaveTick(2), Is.EqualTo(12000));
            Assert.That(ReinforcementSystem.WaveTick(-1), Is.Zero);
            Assert.That(ReinforcementSystem.WaveTick(99), Is.EqualTo(12000));
        }

        [Test]
        public void ArrivalPlacesCompanyFirstThenColumnPlatoonsAndAnnouncesInOrder()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            Company company = battle.NewCompany(blue, new SimVec2(2000, 500), 2, true);
            Platoon organic = battle.NewOrganic(blue, "recon", new SimVec2(2000, 500));
            SetOffMap(company, 1);
            SetOffMap(organic, 1);
            ReinforcementSystem reinforcements = new ReinforcementSystem(battle);
            long initialHash = reinforcements.StateHash();

            battle.AdvanceTo(5990);
            reinforcements.Step(battle.World);
            Assert.That(company.OnMap, Is.False);
            battle.AdvanceTo(6000);
            reinforcements.Step(battle.World);

            Assert.That(reinforcements.ArrivedWaves["blue"], Is.EqualTo(1));
            Assert.That(reinforcements.ArrivedWaves["red"], Is.EqualTo(1));
            Assert.That(company.OnMap, Is.True);
            Assert.That(company.Hq!.OnMap, Is.True);
            Assert.That(company.Platoons[0].OnMap, Is.True);
            Assert.That(organic.OnMap, Is.True);
            Assert.That(company.Guide.Pos, Is.EqualTo(new SimVec2(2000, 120)));
            Assert.That(organic.Guide.Pos, Is.EqualTo(new SimVec2(2000, 390)));
            Assert.That(company.Platoons[0].FollowsParent(), Is.True);
            Assert.That(battle.PlacedPlatoons.Count, Is.EqualTo(3));
            Assert.That(battle.PlacedPlatoons[0], Is.SameAs(company.Hq));
            Assert.That(battle.PlacedPlatoons[1], Is.SameAs(company.Platoons[0]));
            Assert.That(battle.PlacedPlatoons[2], Is.SameAs(organic));
            foreach (Element element in company.AllElements())
            {
                Assert.That(element.IsActive(), Is.True);
            }
            foreach (Element element in organic.Elements)
            {
                Assert.That(element.IsActive(), Is.True);
            }

            List<SimEvent> events = battle.World.Events.Drain();
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].Type, Is.EqualTo("reinforcement_arrived"));
            Assert.That(events[0].Payload["unit"], Is.EqualTo(company.Id));
            Assert.That(events[1].Payload["unit"], Is.EqualTo(organic.Id));
            Assert.That(events[0].Tick, Is.EqualTo(6000));
            Assert.That(reinforcements.StateHash(), Is.Not.EqualTo(initialHash));

            reinforcements.Step(battle.World);
            Assert.That(battle.PlacedPlatoons.Count, Is.EqualTo(3));
            battle.AdvanceTo(12000);
            reinforcements.Step(battle.World);
            Assert.That(reinforcements.ArrivedWaves["blue"], Is.EqualTo(2));
        }

        [Test]
        public void DirectBringInDoesNotFalselyMarkScheduledWaveComplete()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            Platoon organic = battle.NewOrganic(blue, "recon", new SimVec2(2000, 500));
            SetOffMap(organic, 1);
            ReinforcementSystem reinforcements = new ReinforcementSystem(battle);

            reinforcements.BringIn("blue", 1, 25);
            Assert.That(organic.OnMap, Is.True);
            Assert.That(reinforcements.ArrivedWaves["blue"], Is.Zero);
            Assert.That(battle.World.Events.Drain()[0].Tick, Is.EqualTo(25));
        }

        private static void SetOffMap(Unit unit, int wave)
        {
            unit.OnMap = false;
            unit.Phase = wave;
            foreach (Element element in unit.AllElements())
            {
                element.OnMap = false;
            }

            foreach (Unit child in unit.ChildrenUnits())
            {
                SetOffMap(child, wave);
            }
        }
    }
}
