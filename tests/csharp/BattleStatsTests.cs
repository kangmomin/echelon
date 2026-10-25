using System.Collections.Generic;
using Echelon.Core;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class BattleStatsTests
    {
        [Test]
        public void EventsAccumulateAllAarCountersAndSerializableSummary()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            battle.Scenario.ControlPoints.Add(new ScenarioDef.ControlPoint
            {
                Id = "fox",
                DisplayName = "FOX",
                Pos = new SimVec2(2000, 2000),
                RadiusM = 200,
                VpPerMin = 1
            });
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            TaskForce red = battle.NewTaskForce("red");
            Company blueCompany = battle.NewCompany(blue, new SimVec2(2000, 500));
            battle.NewCompany(red, new SimVec2(2000, 3500));
            Platoon blueInfantry = battle.NewOrganic(blue, "infantry", new SimVec2(2000, 500), "inf_squad");
            Element blueTank = blueCompany.Platoons[0].Elements[0];
            Element redTank = red.Companies[0].Platoons[0].Elements[0];
            battle.Scoring.Step(battle.World);
            battle.Scoring.Vp["blue"] = 8;
            battle.Scoring.GetPoint("fox")!.Owner = "blue";
            battle.Scoring.GetPoint("fox")!.HeldSec["blue"] = 42;
            BattleStats stats = new BattleStats(battle);

            Emit(battle, "element_destroyed", 10, "element", blueTank.Id, "side", "blue");
            Emit(battle, "element_destroyed", 11, "element", blueInfantry.Elements[0].Id, "side", "blue");
            Emit(battle, "element_damaged", 11, "side", "red");
            Emit(battle, "element_damaged", 12, "side", "red");
            Emit(battle, "order_received", 12, "side", "blue", "issuer", "player", "delay", 2.0);
            Emit(battle, "order_received", 13, "side", "blue", "issuer", "ai", "delay", 4.0);
            Emit(battle, "order_received", 14, "side", "blue", "issuer", "unit", "delay", 90.0);
            Emit(battle, "order_received", 14, "side", "blue", "issuer", "self", "delay", 80.0);
            Emit(battle, "fire_requested", 15, "side", "blue");
            Emit(battle, "fire_requested", 16, "side", "blue");
            Emit(battle, "comms_changed", 17, "side", "red", "state", (int)Unit.Comms.Cut);
            Emit(battle, "comms_changed", 18, "side", "red", "state", (int)Unit.Comms.Good);
            Emit(battle, "comms_changed", 19, "side", "red", "state", (int)Unit.Comms.Cut);
            Emit(battle, "resupplied", 20, "side", "blue");
            Emit(battle, "shot", 21, "from", -1);
            Emit(battle, "shot", 22, "from", blueTank.Id);
            Emit(battle, "shot", 23, "from", blueTank.Id);
            Emit(battle, "shot", 24, "from", redTank.Id);
            battle.AdvanceTo(50);
            battle.SetResult(new Dictionary<string, object>
            {
                ["winner"] = "blue",
                ["vp"] = new Dictionary<string, object> { ["blue"] = 8.0, ["red"] = 2.0 }
            });

            Dictionary<string, object> summary = stats.Summary();
            Dictionary<string, object> sides = (Dictionary<string, object>)summary["sides"];
            Dictionary<string, object> blueSummary = (Dictionary<string, object>)sides["blue"];
            Dictionary<string, object> redSummary = (Dictionary<string, object>)sides["red"];
            Dictionary<string, int> blueLosses = (Dictionary<string, int>)blueSummary["losses"];
            Assert.That(blueLosses["tank"], Is.EqualTo(1));
            Assert.That(blueLosses["inf_squad"], Is.EqualTo(1));
            Assert.That(blueSummary["losses_total"], Is.EqualTo(2));
            Assert.That(redSummary["damaged"], Is.EqualTo(2));
            Assert.That(blueSummary["elements"], Is.EqualTo(blue.AllElements().Count));
            Assert.That(blueSummary["orders"], Is.EqualTo(2));
            Assert.That(blueSummary["order_delay_mean"], Is.EqualTo(3).Within(0.0001));
            Assert.That(blueSummary["order_delay_max"], Is.EqualTo(4).Within(0.0001));
            Assert.That(blueSummary["fire_missions"], Is.EqualTo(2));
            Assert.That(redSummary["comms_cuts"], Is.EqualTo(2));
            Assert.That(blueSummary["resupplies"], Is.EqualTo(1));
            Assert.That(blueSummary["shots"], Is.EqualTo(2));
            Assert.That(redSummary["shots"], Is.EqualTo(1));
            Assert.That(blueSummary["vp"], Is.EqualTo(8));
            Assert.That(blueSummary["value_ratio"], Is.EqualTo(1).Within(0.0001));
            Assert.That(summary["duration_sec"], Is.EqualTo(5).Within(0.0001));
            Assert.That(summary["first_contact_sec"], Is.EqualTo(2.2).Within(0.0001));
            List<Dictionary<string, object>> points = (List<Dictionary<string, object>>)summary["points"];
            Assert.That(points[0]["owner"], Is.EqualTo("blue"));
            Assert.That(points[0]["held_blue_sec"], Is.EqualTo(42));
            string json = JsonConvert.SerializeObject(summary);
            Assert.That(json.Length, Is.GreaterThan(100));
            Assert.That(json, Does.Contain("order_delay_mean"));

            Dictionary<string, object> resultSnapshot = (Dictionary<string, object>)summary["result"];
            Dictionary<string, object> resultVp = (Dictionary<string, object>)resultSnapshot["vp"];
            Assert.That(resultVp["blue"], Is.EqualTo(8.0));
            ((Dictionary<string, object>)battle.Result["vp"])["blue"] = 999.0;
            Assert.That(resultVp["blue"], Is.EqualTo(8.0));
        }

        [Test]
        public void TimelineRetainsOnlyAarEventsWithOriginalDescriptionsAndOrder()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            Company company = battle.NewCompany(blue, new SimVec2(2000, 500));
            company.ShortName = "A";
            BattleStats stats = new BattleStats(battle);

            Emit(battle, "battle_started", 0);
            Emit(battle, "point_captured", 10, "side", "blue", "name", "FOX");
            Emit(battle, "objective_done", 20, "side", "red", "name", "BRAVO", "vp", 30);
            Emit(battle, "objective_failed", 30, "side", "blue", "name", "FOX");
            Emit(battle, "reinforcement_arrived", 40, "side", "blue", "unit", company.Id);
            Emit(battle, "routed", 50, "side", "blue", "unit", company.Id);
            Emit(battle, "commander_lost", 60, "side", "blue", "unit", company.Id);
            Emit(battle, "cas_shot_down", 70, "side", "red");
            Emit(battle, "auto_withdraw", 80, "side", "blue", "unit", company.Id);
            Emit(battle, "shot", 85, "from", -1);
            Emit(battle, "battle_ended", 90);

            Assert.That(stats.Timeline.Count, Is.EqualTo(10));
            string[] expected =
            {
                "전투 시작",
                "청군: FOX 점령",
                "적군: 임무 목표 달성 — BRAVO (+30)",
                "청군: 임무 목표 실패 — FOX",
                "청군: A 증원 도착",
                "청군: A 패주",
                "청군: A 지휘관 손실",
                "적군: 근접항공지원 격추됨",
                "청군: A 전투력 저하로 철수",
                "전투 종료"
            };
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.That(stats.Timeline[i]["text"], Is.EqualTo(expected[i]));
                Assert.That(stats.Timeline[i]["tick"], Is.EqualTo(i * 10));
            }
            Assert.That(BattleStats.SideLabel("blue"), Is.EqualTo("청군"));
            Assert.That(BattleStats.SideLabel("red"), Is.EqualTo("적군"));
            Assert.That(BattleStats.SideLabel("other"), Is.EqualTo("other"));

            Dictionary<string, object> summary = stats.Summary();
            List<Dictionary<string, object>> snapshot = (List<Dictionary<string, object>>)summary["timeline"];
            stats.Timeline[0]["text"] = "changed";
            Assert.That(snapshot[0]["text"], Is.EqualTo("전투 시작"));
        }

        [Test]
        public void EmptyAarUsesZeroDelayAndNegativeFirstContactSentinel()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            BattleStats stats = new BattleStats(battle);
            Dictionary<string, object> summary = stats.Summary();
            Dictionary<string, object> sides = (Dictionary<string, object>)summary["sides"];
            foreach (string side in new[] { "blue", "red" })
            {
                Dictionary<string, object> values = (Dictionary<string, object>)sides[side];
                Assert.That(values["order_delay_mean"], Is.Zero);
                Assert.That(values["order_delay_max"], Is.Zero);
                Assert.That(values["orders"], Is.Zero);
                Assert.That(values["value_ratio"], Is.EqualTo(1));
            }
            Assert.That(summary["first_contact_sec"], Is.EqualTo(-1.0));
            Assert.That((List<Dictionary<string, object>>)summary["timeline"], Is.Empty);
        }

        private static void Emit(AIPortTestBattle battle, string type, long tick, params object[] values)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>();
            for (int i = 0; i < values.Length; i += 2)
            {
                payload[(string)values[i]] = values[i + 1];
            }

            battle.World.Events.EmitEvent(type, tick, payload);
        }
    }
}
