using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class ScoringSystemTests
    {
        [Test]
        public void OffMapReinforcementLossesDoNotWeakenCommittedForce()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            battle.NewCompany(blue, new SimVec2(2000, 500));
            Platoon pending = battle.NewOrganic(blue, "recon", new SimVec2(2000, 0));
            SetOffMap(pending, 1);
            battle.Scoring.Step(battle.World);
            double deployed = FullValue(blue.AllElements(), true);
            double ratio = battle.Scoring.ValueRatio("blue");
            Assert.That(battle.Scoring.CommittedValue["blue"], Is.EqualTo(deployed).Within(0.0001));

            foreach (Element element in pending.Elements)
            {
                element.State = ElementState.DESTROYED;
            }
            battle.AdvanceTo(10);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Result, Is.Empty);
            Assert.That(battle.Scoring.ForceValue("blue"), Is.EqualTo(deployed).Within(0.0001));
            Assert.That(battle.Scoring.ValueRatio("blue"), Is.EqualTo(ratio).Within(0.0001));
        }

        [Test]
        public void ArrivalExpandsBaselineOnceAndDamageOnlyChangesCurrentValue()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            battle.NewCompany(blue, new SimVec2(2000, 500));
            Platoon pending = battle.NewOrganic(blue, "recon", new SimVec2(2000, 0));
            SetOffMap(pending, 1);
            battle.Scoring.Step(battle.World);
            double baseline = battle.Scoring.CommittedValue["blue"];
            double arrivalValue = FullValue(pending.Elements, false);

            new ReinforcementSystem(battle).BringIn("blue", 1, battle.World.Tick);
            battle.AdvanceTo(1);
            battle.Scoring.Step(battle.World);
            double expectedBaseline = baseline + arrivalValue;
            Assert.That(battle.Scoring.CommittedValue["blue"], Is.EqualTo(expectedBaseline).Within(0.0001));
            Assert.That(battle.Scoring.ValueRatio("blue"), Is.EqualTo(1).Within(0.0001));

            Element damaged = pending.Elements[0];
            damaged.State = ElementState.DAMAGED;
            battle.AdvanceTo(2);
            battle.Scoring.Step(battle.World);
            double expectedRatio = (expectedBaseline - damaged.Def.Cost * 0.4) / expectedBaseline;
            Assert.That(battle.Scoring.ValueRatio("blue"), Is.EqualTo(expectedRatio).Within(0.0001));
            battle.AdvanceTo(12);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.CommittedValue["blue"], Is.EqualTo(expectedBaseline).Within(0.0001));
        }

        [Test]
        public void CaptureObjectiveVpContestationAndOwnershipTransferFollowOriginalOrder()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            ScenarioDef.ControlPoint point = AddPoint(battle, "fox", 1);
            battle.Scenario.Objectives.Add(new ScenarioDef.Objective
            {
                Id = "obj_fox",
                Side = "blue",
                ObjectiveType = "seize",
                Point = "fox",
                Minutes = 25,
                Vp = 30
            });
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            TaskForce red = battle.NewTaskForce("red");
            Platoon bluePlatoon = battle.NewOrganic(blue, "tank", point.Pos, "tank");
            battle.Scoring.Step(battle.World);

            ScoringSystem.PointState state = battle.Scoring.GetPoint("fox")!;
            Assert.That(state.Owner, Is.EqualTo("blue"));
            Assert.That(battle.Scoring.ObjectivesDone["obj_fox"], Is.True);
            Assert.That(battle.Scoring.Vp["blue"], Is.EqualTo(30.0 + 1.0 / 60.0).Within(0.0001));
            List<SimEvent> initialEvents = battle.World.Events.Drain();
            Assert.That(initialEvents[0].Type, Is.EqualTo("point_captured"));
            Assert.That(initialEvents[1].Type, Is.EqualTo("objective_done"));

            battle.AdvanceTo(10);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.Vp["blue"], Is.EqualTo(30.0 + 2.0 / 60.0).Within(0.0001));
            Platoon redPlatoon = battle.NewOrganic(red, "tank", point.Pos, "tank");
            battle.AdvanceTo(20);
            battle.Scoring.Step(battle.World);
            Assert.That(state.Contested, Is.True);
            Assert.That(state.Owner, Is.EqualTo("blue"));
            Assert.That(battle.Scoring.Vp["blue"], Is.EqualTo(30.0 + 3.0 / 60.0).Within(0.0001));
            Assert.That(battle.World.Events.Drain().Exists(value => value.Type == "objective_done"), Is.False);

            foreach (Element element in bluePlatoon.Elements)
            {
                element.Pos = new SimVec2(100, 100);
            }
            battle.AdvanceTo(30);
            battle.Scoring.Step(battle.World);
            Assert.That(state.Owner, Is.EqualTo("red"));
            Assert.That(state.Contested, Is.False);
            Assert.That(redPlatoon.OnMap, Is.True);
        }

        [Test]
        public void HoldObjectiveUsesAccumulatedOwnershipAndCompletesOnce()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            ScenarioDef.ControlPoint point = AddPoint(battle, "hold", 0);
            battle.Scenario.Objectives.Add(new ScenarioDef.Objective
            {
                Id = "hold_obj",
                Side = "blue",
                ObjectiveType = "hold",
                Point = "hold",
                Minutes = 0.05,
                Vp = 12
            });
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            battle.NewOrganic(blue, "tank", point.Pos, "tank");

            battle.Scoring.Step(battle.World);
            battle.AdvanceTo(10);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.ObjectivesDone.ContainsKey("hold_obj"), Is.False);
            battle.AdvanceTo(20);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.ObjectivesDone["hold_obj"], Is.True);
            Assert.That(battle.Scoring.Vp["blue"], Is.EqualTo(12).Within(0.0001));
            battle.AdvanceTo(30);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.Vp["blue"], Is.EqualTo(12).Within(0.0001));
        }

        [Test]
        public void SeizeObjectiveFailsPermanentlyAfterDeadline()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            AddPoint(battle, "late", 0);
            battle.Scenario.Objectives.Add(new ScenarioDef.Objective
            {
                Id = "late_obj",
                Side = "blue",
                ObjectiveType = "seize",
                Point = "late",
                Minutes = 0.01,
                Vp = 20
            });
            battle.Scoring = new ScoringSystem(battle);

            battle.Scoring.Step(battle.World);
            battle.AdvanceTo(10);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.ObjectivesDone["late_obj"], Is.False);
            Assert.That(battle.World.Events.Drain().Exists(value => value.Type == "objective_failed"), Is.True);
            battle.Scoring.GetPoint("late")!.Owner = "blue";
            battle.AdvanceTo(20);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.Vp["blue"], Is.Zero);
            Assert.That(battle.World.Events.Drain().Exists(value => value.Type == "objective_done"), Is.False);
        }

        [Test]
        public void MountedElementsDoNotCaptureUntilDismounted()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            ScenarioDef.ControlPoint point = AddPoint(battle, "dismount", 0);
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            Platoon infantry = battle.NewOrganic(blue, "infantry", point.Pos, "inf_squad");
            Element carrier = new Element { Def = infantry.Elements[0].Def };
            infantry.Elements[0].MountedIn = carrier;

            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.GetPoint("dismount")!.Owner, Is.Empty);
            infantry.Elements[0].MountedIn = null;
            battle.AdvanceTo(10);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.GetPoint("dismount")!.Owner, Is.EqualTo("blue"));
            infantry.Elements[0].OnMap = false;
            battle.AdvanceTo(20);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.GetPoint("dismount")!.Owner, Is.EqualTo("blue"));
        }

        [Test]
        public void CollapseSetsResultPausesAndEmitsEndAfterPauseChange()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            battle.Scoring = new ScoringSystem(battle);
            battle.NewTaskForce("blue");
            TaskForce red = battle.NewTaskForce("red");
            Company company = battle.NewCompany(red, new SimVec2(2000, 3500));
            battle.Scoring.Step(battle.World);
            foreach (Element element in company.AllElements())
            {
                element.State = ElementState.DESTROYED;
            }

            battle.AdvanceTo(10);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Result["winner"], Is.EqualTo("blue"));
            Assert.That(battle.Result["reason"], Is.EqualTo("collapse"));
            Assert.That(battle.Result["collapsed"], Is.EqualTo("red"));
            Assert.That(battle.World.Paused, Is.True);
            List<SimEvent> events = battle.World.Events.Drain();
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].Type, Is.EqualTo("paused_changed"));
            Assert.That(events[1].Type, Is.EqualTo("battle_ended"));
            Assert.That(events[1].Payload["winner"], Is.EqualTo("blue"));
            double vp = battle.Scoring.Vp["blue"];
            battle.AdvanceTo(20);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Scoring.Vp["blue"], Is.EqualTo(vp));
        }

        [Test]
        public void TimeEndingUsesVpAndSupportsDraw()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            battle.Scenario.TimeLimitMin = 0.01;
            battle.Scoring = new ScoringSystem(battle);
            battle.Scoring.Vp["blue"] = 4;
            battle.Scoring.Vp["red"] = 2;
            battle.Scoring.Step(battle.World);
            battle.AdvanceTo(10);
            battle.Scoring.Step(battle.World);
            Assert.That(battle.Result["reason"], Is.EqualTo("time"));
            Assert.That(battle.Result["winner"], Is.EqualTo("blue"));

            AIPortTestBattle draw = new AIPortTestBattle();
            draw.Scenario.TimeLimitMin = 0.01;
            draw.Scoring = new ScoringSystem(draw);
            draw.AdvanceTo(10);
            draw.Scoring.Step(draw.World);
            Assert.That(draw.Result["reason"], Is.EqualTo("time"));
            Assert.That(draw.Result["winner"], Is.EqualTo(""));
        }

        private static ScenarioDef.ControlPoint AddPoint(AIPortTestBattle battle, string id, double vpPerMin)
        {
            ScenarioDef.ControlPoint point = new ScenarioDef.ControlPoint
            {
                Id = id,
                DisplayName = id.ToUpperInvariant(),
                Pos = new SimVec2(2000, 2000),
                RadiusM = 200,
                VpPerMin = vpPerMin
            };
            battle.Scenario.ControlPoints.Add(point);
            return point;
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

        private static void SetOffMap(Unit unit, int wave)
        {
            unit.OnMap = false;
            unit.Phase = wave;
            foreach (Element element in unit.AllElements())
            {
                element.OnMap = false;
            }
        }
    }
}
