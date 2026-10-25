using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class EchelonModelHierarchyTests
    {
        [Test]
        public void TaskForceChildrenUnitsAndElementsUseDeterministicHierarchyOrder()
        {
            TaskForce taskForce = new TaskForce();
            Platoon cp = NewPlatoon(NewElement());
            Company company = new Company { Parent = taskForce };
            Platoon hq = NewPlatoon(NewElement());
            hq.Parent = company;
            Platoon first = NewPlatoon(NewElement());
            first.Parent = company;
            Platoon second = NewPlatoon(NewElement());
            second.Parent = company;
            Platoon organic = NewPlatoon(NewElement());
            organic.Parent = taskForce;
            cp.Parent = taskForce;
            taskForce.CpSection = cp;
            taskForce.Companies.Add(company);
            taskForce.Organic.Add(organic);
            company.Hq = hq;
            company.Platoons.Add(first);
            company.Platoons.Add(second);

            Assert.That(taskForce.ChildrenUnits(), Is.EqualTo(new Unit[] { cp, company, organic }));
            Assert.That(company.ChildrenUnits(), Is.EqualTo(new Unit[] { hq, first, second }));
            Assert.That(taskForce.AllUnits(), Is.EqualTo(new Unit[] { taskForce, cp, company, hq, first, second, organic }));
            Assert.That(taskForce.AllPlatoons(), Is.EqualTo(new[] { cp, hq, first, second, organic }));
            Assert.That(taskForce.AllElements(), Is.EqualTo(new[]
            {
                cp.Elements[0], hq.Elements[0], first.Elements[0], second.Elements[0], organic.Elements[0]
            }));
            Assert.That(second.TaskForce(), Is.SameAs(taskForce));
            Assert.That(second.Company(), Is.SameAs(company));
            Assert.That(taskForce.TaskForce(), Is.SameAs(taskForce));
            Assert.That(taskForce.LevelName(), Is.EqualTo("대대 전투단"));
            Assert.That(company.LevelName(), Is.EqualTo("중대"));
            Assert.That(first.LevelName(), Is.EqualTo("소대"));
        }

        [Test]
        public void StrengthAliveActiveCentroidAndDestroyedFollowElementState()
        {
            Element first = NewElement();
            first.Pos = SimVec2.Zero;
            Element second = NewElement();
            second.Pos = new SimVec2(30.0, 0.0);
            second.State = ElementState.DAMAGED;
            Element third = NewElement();
            third.Pos = new SimVec2(90.0, 0.0);
            third.State = ElementState.DESTROYED;
            Platoon platoon = NewPlatoon(first, second, third);
            platoon.Guide.Pos = new SimVec2(7.0, 8.0);

            Assert.That(platoon.AllElements(), Is.SameAs(platoon.Elements));
            Assert.That(platoon.AliveElements(), Is.EqualTo(new[] { first, second }));
            Assert.That(platoon.ActiveElements(), Is.EqualTo(new[] { first, second }));
            Assert.That(platoon.Strength(), Is.EqualTo(1.6 / 3.0).Within(1e-9));
            Assert.That(platoon.IsCombatIneffective(), Is.False);
            Assert.That(platoon.Centroid(), Is.EqualTo(new SimVec2(15.0, 0.0)));
            second.OnMap = false;
            Assert.That(platoon.ActiveElements(), Is.EqualTo(new[] { first }));
            Assert.That(platoon.Centroid(), Is.EqualTo(SimVec2.Zero));
            first.OnMap = false;
            Assert.That(platoon.Centroid(), Is.EqualTo(platoon.Guide.Pos));
            Assert.That(platoon.IsDestroyed(), Is.False);
            second.State = ElementState.DESTROYED;
            Assert.That(platoon.Strength(), Is.EqualTo(1.0 / 3.0).Within(1e-9));
            Assert.That(platoon.IsCombatIneffective(), Is.True);
            first.State = ElementState.DESTROYED;
            Assert.That(platoon.IsDestroyed(), Is.True);
            Assert.That(new Platoon().IsDestroyed(), Is.True);
            Assert.That(new Unit().Strength(), Is.Zero);
        }

        [Test]
        public void CombatIneffectivenessIncludesExactlyHalfStrength()
        {
            Element alive = NewElement();
            Element destroyed = NewElement();
            destroyed.State = ElementState.DESTROYED;
            Platoon platoon = NewPlatoon(alive, destroyed);
            Assert.That(platoon.Strength(), Is.EqualTo(0.5));
            Assert.That(platoon.IsCombatIneffective(), Is.True);
            Assert.That(platoon.IsDestroyed(), Is.False);
        }

        [Test]
        public void DefaultDefendFacingUsesGodotPositiveNanBits()
        {
            double facing = new Unit().DefendFacing;
            Assert.That(double.IsNaN(facing), Is.True);
            Assert.That(System.BitConverter.DoubleToInt64Bits(facing),
                Is.EqualTo(unchecked((long)0x7ff8000000000000UL)));
        }

        [Test]
        public void LeadersParentFollowingLabelsSkillAndTimedStatesMatchContracts()
        {
            Element dead = NewElement();
            dead.State = ElementState.DESTROYED;
            Element alive = NewElement();
            Platoon platoon = NewPlatoon(dead, alive);
            TaskForce taskForce = new TaskForce { Commander = dead };
            Company company = new Company { Parent = taskForce, Commander = dead };
            platoon.Parent = company;
            Assert.That(taskForce.Leader(), Is.Null);
            Assert.That(company.Leader(), Is.Null);
            Assert.That(platoon.Leader(), Is.SameAs(alive));
            Assert.That(platoon.FollowsParent(), Is.True);
            platoon.Detached = true;
            Assert.That(platoon.FollowsParent(), Is.False);
            platoon.Detached = false;
            platoon.Routed = true;
            Assert.That(platoon.FollowsParent(), Is.False);
            platoon.Routed = false;
            platoon.Parent = taskForce;
            Assert.That(platoon.FollowsParent(), Is.False);
            Assert.That(platoon.Company(), Is.Null);

            Unit unit = new Unit
            {
                DisplayName = "Display",
                Skill = "elite",
                SuccessionDelayMult = 1.2,
                CommsState = Unit.Comms.Cut,
                ReformUntilTick = 11,
                SuccessionUntilTick = 12,
                RoutedUntilTick = 13
            };
            Assert.That(unit.Label(), Is.EqualTo("Display"));
            unit.ShortName = "Short";
            Assert.That(unit.Label(), Is.EqualTo("Short"));
            Assert.That(unit.CommsName(), Is.EqualTo("두절"));
            Assert.That(unit.SkillDelayFactor(), Is.EqualTo(0.78).Within(1e-9));
            unit.Skill = "unknown";
            Assert.That(unit.SkillDelayFactor(), Is.EqualTo(1.2));
            Assert.That(unit.IsReforming(10), Is.True);
            Assert.That(unit.IsReforming(11), Is.False);
            Assert.That(unit.IsLeaderless(11), Is.True);
            Assert.That(unit.IsLeaderless(12), Is.False);
            Assert.That(unit.IsRouted(12), Is.True);
            Assert.That(unit.IsRouted(13), Is.False);
            Assert.That(unit.FollowsParent(), Is.False);
        }

        [Test]
        public void CompanyFormationMembershipAndBaseSpeedUseOriginalOrderAndSlowestMember()
        {
            Company company = new Company();
            Platoon hq = NewPlatoon(NewElement(offroadKmh: 18.0));
            hq.Parent = company;
            Platoon first = NewPlatoon(NewElement(offroadKmh: 36.0));
            first.Parent = company;
            Platoon second = NewPlatoon(NewElement(offroadKmh: 72.0));
            second.Parent = company;
            Platoon detached = NewPlatoon(NewElement(offroadKmh: 3.6));
            detached.Parent = company;
            detached.Detached = true;
            Platoon offMap = NewPlatoon(NewElement(offroadKmh: 3.6));
            offMap.Parent = company;
            offMap.OnMap = false;
            Element destroyedElement = NewElement(offroadKmh: 3.6);
            destroyedElement.State = ElementState.DESTROYED;
            Platoon destroyed = NewPlatoon(destroyedElement);
            destroyed.Parent = company;
            Platoon wrongParent = NewPlatoon(NewElement(offroadKmh: 3.6));
            wrongParent.Parent = new Company();
            company.Hq = hq;
            company.Platoons = new List<Platoon> { first, second, detached, offMap, destroyed, wrongParent };

            Assert.That(company.FormationPlatoons(), Is.EqualTo(new[] { first, second }));
            Assert.That(company.BaseSpeed(), Is.EqualTo(5.0).Within(1e-9));
            hq.Elements[0].State = ElementState.DESTROYED;
            Assert.That(company.BaseSpeed(), Is.EqualTo(10.0).Within(1e-9));
            first.OnMap = false;
            Assert.That(company.BaseSpeed(), Is.EqualTo(20.0).Within(1e-9));
            Assert.That(new Company().BaseSpeed(), Is.Zero);
        }

        [Test]
        public void DismountedMechanizedPlatoonUsesSquadsAndTrailingVehicles()
        {
            Element vehicle = NewElement("ifv", 36.0, 100.0, 1);
            Element squad = NewElement("inf_squad", 18.0, 10.0);
            Platoon platoon = NewPlatoon(vehicle, squad);
            squad.MountedIn = vehicle;
            vehicle.Passengers.Add(squad);
            platoon.Mounted = true;
            Assert.That(platoon.HasSquads(), Is.True);
            Assert.That(platoon.HasTransport(), Is.True);
            Assert.That(platoon.Vehicles(), Is.EqualTo(new[] { vehicle }));
            Assert.That(platoon.Squads(), Is.EqualTo(new[] { squad }));
            Assert.That(platoon.FormationMembers(), Is.EqualTo(new[] { vehicle }));
            Assert.That(platoon.TrailingVehicles(), Is.Empty);
            Assert.That(platoon.BaseSpeed(), Is.EqualTo(10.0).Within(1e-9));

            platoon.Mounted = false;
            squad.MountedIn = null;
            vehicle.Passengers.Clear();
            Assert.That(platoon.FormationMembers(), Is.EqualTo(new[] { squad }));
            Assert.That(platoon.TrailingVehicles(), Is.EqualTo(new[] { vehicle }));
            Assert.That(platoon.BaseSpeed(), Is.EqualTo(5.0).Within(1e-9));
            squad.State = ElementState.DAMAGED;
            squad.DamageKind = "mobility";
            Assert.That(platoon.FormationMembers(), Is.Empty);
            Assert.That(platoon.TrailingVehicles(), Is.EqualTo(new[] { vehicle }));
            Assert.That(platoon.BaseSpeed(), Is.Zero);
            vehicle.OnMap = false;
            Assert.That(platoon.TrailingVehicles(), Is.Empty);
        }

        private static Element NewElement(string elementClass = "tank", double offroadKmh = 36.0,
            double hp = 100.0, int transportCapacity = 0)
        {
            return new Element
            {
                Def = new ElementDef
                {
                    ElementClass = elementClass,
                    DisplayName = elementClass,
                    Hp = hp,
                    SpeedOffroadKmh = offroadKmh,
                    SpeedRoadKmh = offroadKmh * 2.0,
                    TransportCapacity = transportCapacity
                },
                Hp = hp
            };
        }

        private static Platoon NewPlatoon(params Element[] elements)
        {
            Platoon platoon = new Platoon();
            for (int i = 0; i < elements.Length; ++i)
            {
                Element element = elements[i];
                element.Platoon = platoon;
                element.IndexInPlatoon = i;
                platoon.Elements.Add(element);
            }

            platoon.InitialElementCount = elements.Length;
            platoon.Commander = elements.Length > 0 ? elements[0] : null;
            return platoon;
        }
    }
}
