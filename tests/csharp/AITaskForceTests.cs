using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class AITaskForceTests
    {
        [Test]
        public void EvaluationUsesOffsetConfiguredSidesAndBattleState()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            AddPoint(battle, "alpha", new SimVec2(2000, 2000));
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            Company company = battle.NewCompany(blue, new SimVec2(2000, 500));
            TaskForceAI ai = new TaskForceAI(battle);
            ai.Sides.Add("blue");

            ai.Step(battle.World);
            Assert.That(battle.IssuedOrders, Is.Empty);
            battle.AdvanceTo(TaskForceAI.EvalOffset);
            ai.Step(battle.World);
            Assert.That(battle.IssuedOrders, Is.Not.Empty);
            Assert.That(ai.Assignment.ContainsKey(company.Id), Is.True);

            battle.IssuedOrders.Clear();
            battle.Deploying = true;
            battle.AdvanceTo(TaskForceAI.EvalOffset + TaskForceAI.EvalInterval);
            ai.Step(battle.World);
            Assert.That(battle.IssuedOrders, Is.Empty);
            battle.Deploying = false;
            battle.SetResult(new Dictionary<string, object> { ["winner"] = "red" });
            ai.Step(battle.World);
            Assert.That(battle.IssuedOrders, Is.Empty);
        }

        [Test]
        public void GreedyAssignmentSplitsPointsAndKeepsStableAssignments()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            AddPoint(battle, "first", new SimVec2(2000, 2000));
            AddPoint(battle, "second", new SimVec2(2000, 2000));
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            Company first = battle.NewCompany(blue, new SimVec2(2000, 500));
            Company second = battle.NewCompany(blue, new SimVec2(2000, 500));
            TaskForceAI ai = new TaskForceAI(battle);

            ai.Evaluate(blue);
            Assert.That(ai.Assignment[first.Id], Is.EqualTo("first"));
            Assert.That(ai.Assignment[second.Id], Is.EqualTo("second"));
            Assert.That(battle.IssuedOrders.Count, Is.EqualTo(2));
            foreach (Company company in new[] { first, second })
            {
                company.Mission = battle.IssuedOrders[company == first ? 0 : 1];
                company.Posture = "moving";
            }

            ai.Evaluate(blue);
            Assert.That(ai.Assignment[first.Id], Is.EqualTo("first"));
            Assert.That(ai.Assignment[second.Id], Is.EqualTo("second"));
            Assert.That(battle.IssuedOrders.Count, Is.EqualTo(2));
            Assert.That(ai.StateHash(), Is.EqualTo(ai.StateHash()));
        }

        [Test]
        public void KnownEnemyValueDiscouragesSuicidalFrontalAssignment()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            AddPoint(battle, "danger", new SimVec2(2000, 1800));
            AddPoint(battle, "safe", new SimVec2(2000, 2800));
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            Company company = battle.NewCompany(blue, new SimVec2(2000, 1000));
            for (int i = 0; i < 10; ++i)
            {
                blue.KnownEnemies[1000 + i] = Contact(new SimVec2(2000, 1800), 0, "tank");
            }

            TaskForceAI ai = new TaskForceAI(battle);
            ai.Evaluate(blue);
            Assert.That(ai.Assignment[company.Id], Is.EqualTo("safe"));
        }

        [Test]
        public void WeakCompanyBecomesReserveAndWithdrawsOnlyOnce()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            AddPoint(battle, "alpha", new SimVec2(2000, 2000));
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            Company weak = battle.NewCompany(blue, new SimVec2(2000, 500));
            weak.Platoons[0].Elements[0].State = ElementState.DESTROYED;
            weak.Platoons[0].Elements[1].State = ElementState.DESTROYED;
            TaskForceAI ai = new TaskForceAI(battle);

            ai.Evaluate(blue);
            Assert.That(ai.Assignment[weak.Id], Is.EqualTo("reserve"));
            Assert.That(battle.IssuedOrders.Count, Is.EqualTo(1));
            Assert.That(battle.IssuedOrders[0].Type, Is.EqualTo(OrderType.WITHDRAW));
            Assert.That(battle.IssuedOrders[0].Issuer, Is.EqualTo("ai"));
            ai.Evaluate(blue);
            Assert.That(battle.IssuedOrders.Count, Is.EqualTo(1));
        }

        [Test]
        public void RoutedCompanyBecomesReserveEvenAtFullStrength()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            AddPoint(battle, "alpha", new SimVec2(2000, 2000));
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            Company routed = battle.NewCompany(blue, new SimVec2(2000, 500));
            routed.Routed = true;

            TaskForceAI ai = new TaskForceAI(battle);
            ai.Evaluate(blue);
            Assert.That(ai.Assignment[routed.Id], Is.EqualTo("reserve"));
        }

        [Test]
        public void OrganicRolesAdvanceAndLowAmmoPlatoonRequestsResupply()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            AddPoint(battle, "alpha", new SimVec2(2000, 3000), 2);
            battle.Scoring = new ScoringSystem(battle);
            battle.Catalog.Weapons["mortar"] = new WeaponDef
            {
                Id = "mortar",
                RangeMinM = 100,
                RangeMaxM = 2000,
                Indirect = true
            };
            TaskForce blue = battle.NewTaskForce("blue");
            battle.NewCompany(blue, new SimVec2(2000, 500));
            Platoon recon = battle.NewOrganic(blue, "recon", new SimVec2(1000, 500));
            Platoon fo = battle.NewOrganic(blue, "fo", new SimVec2(1000, 600), "fo");
            Platoon aviation = battle.NewOrganic(blue, "aviation", new SimVec2(1000, 700), "heli");
            Platoon mortar = battle.NewOrganic(blue, "mortar", new SimVec2(1000, 800), "mortar");
            battle.IndirectWeapons[mortar.Id] = "mortar";
            battle.AmmoRatioOverride = unit => unit == recon ? 0.2 : 1.0;

            new TaskForceAI(battle).Evaluate(blue);
            Assert.That(battle.IssuedOrders.Exists(order => order.TargetUnitId == recon.Id && order.Type == OrderType.MOVE), Is.True);
            Assert.That(battle.IssuedOrders.Exists(order => order.TargetUnitId == fo.Id && order.Type == OrderType.MOVE), Is.True);
            Assert.That(battle.IssuedOrders.Exists(order => order.TargetUnitId == aviation.Id && order.Type == OrderType.ATTACK_MOVE), Is.True);
            Assert.That(battle.IssuedOrders.Exists(order => order.TargetUnitId == mortar.Id && order.Type == OrderType.MOVE), Is.True);
            Assert.That(battle.IssuedOrders.Exists(order => order.TargetUnitId == recon.Id && order.Type == OrderType.RESUPPLY), Is.True);
        }

        [Test]
        public void ResupplySkipsContactSupplySectionsAndExistingResupply()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            AddPoint(battle, "alpha", new SimVec2(2000, 2000));
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            Company company = battle.NewCompany(blue, new SimVec2(2000, 500));
            Platoon platoon = company.Platoons[0];
            battle.AmmoRatioOverride = unit => unit == platoon ? 0.1 : 1.0;
            platoon.LastContactTick = 0;
            Platoon supply = battle.NewOrganic(blue, "supply", new SimVec2(1000, 500), "truck");

            new TaskForceAI(battle).Evaluate(blue);
            Assert.That(battle.IssuedOrders.Exists(order => order.Type == OrderType.RESUPPLY), Is.False);
            platoon.LastContactTick = -1000000;
            platoon.Posture = "resupplying";
            new TaskForceAI(battle).Evaluate(blue);
            Assert.That(battle.IssuedOrders.Exists(order => order.Type == OrderType.RESUPPLY), Is.False);
            Assert.That(supply.Posture, Is.EqualTo("idle"));
        }

        [Test]
        public void FireRequestsUseFreshClustersAssetRulesAndStrictCooldowns()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            AddPoint(battle, "alpha", new SimVec2(2000, 2000));
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            battle.NewCompany(blue, new SimVec2(2000, 500));
            blue.ArtilleryMissions = 2;
            blue.CasSorties = 2;
            blue.KnownEnemies[20] = Contact(new SimVec2(2000, 2000), 0, "tank");
            blue.KnownEnemies[21] = Contact(new SimVec2(2050, 2000), 0, "tank");
            blue.KnownEnemies[22] = Contact(new SimVec2(2000, 2050), 0, "ifv");
            blue.KnownEnemies[23] = Contact(new SimVec2(3500, 3500), -301, "tank");
            TaskForceAI ai = new TaskForceAI(battle);

            ai.Evaluate(blue);
            Assert.That(FireCount(battle, FireSupportAsset.Mortar), Is.EqualTo(1));
            Assert.That(FireCount(battle, FireSupportAsset.Artillery), Is.EqualTo(1));
            Assert.That(FireCount(battle, FireSupportAsset.Cas), Is.EqualTo(1));
            Assert.That(battle.FireRequests[0].Target, Is.EqualTo(new SimVec2(2000, 2000)));
            battle.ActiveFireMissions.Add("blue:" + FireSupportAsset.Mortar);

            ai.Evaluate(blue);
            Assert.That(FireCount(battle, FireSupportAsset.Mortar), Is.EqualTo(1));
            Assert.That(FireCount(battle, FireSupportAsset.Artillery), Is.EqualTo(1));
            Assert.That(FireCount(battle, FireSupportAsset.Cas), Is.EqualTo(1));

            battle.AdvanceTo(TaskForceAI.ArtilleryGapTicks);
            RefreshContacts(blue, battle.World.Tick);
            ai.Evaluate(blue);
            Assert.That(FireCount(battle, FireSupportAsset.Artillery), Is.EqualTo(1));
            battle.AdvanceTo(TaskForceAI.ArtilleryGapTicks + 1);
            RefreshContacts(blue, battle.World.Tick);
            ai.Evaluate(blue);
            Assert.That(FireCount(battle, FireSupportAsset.Artillery), Is.EqualTo(2));
            Assert.That(FireCount(battle, FireSupportAsset.Cas), Is.EqualTo(2));
        }

        [Test]
        public void FailedArtilleryRequestDoesNotStartCooldown()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            AddPoint(battle, "alpha", new SimVec2(2000, 2000));
            battle.Scoring = new ScoringSystem(battle);
            TaskForce blue = battle.NewTaskForce("blue");
            blue.ArtilleryMissions = 1;
            for (int i = 0; i < 3; ++i)
            {
                blue.KnownEnemies[i] = Contact(new SimVec2(2000 + i * 10, 2000), 0, "inf_squad");
            }
            battle.FireRequestSuccess[FireSupportAsset.Artillery] = false;
            TaskForceAI ai = new TaskForceAI(battle);

            ai.Evaluate(blue);
            ai.Evaluate(blue);
            Assert.That(FireCount(battle, FireSupportAsset.Artillery), Is.EqualTo(2));
        }

        private static Dictionary<string, object> Contact(SimVec2 position, long tick, string elementClass)
        {
            return new Dictionary<string, object>
            {
                ["pos"] = position,
                ["tick"] = tick,
                ["class"] = elementClass
            };
        }

        private static void RefreshContacts(TaskForce taskForce, long tick)
        {
            foreach (Dictionary<string, object> info in taskForce.KnownEnemies.Values)
            {
                info["tick"] = tick;
            }
        }

        private static int FireCount(AIPortTestBattle battle, int asset)
        {
            int count = 0;
            foreach (AIFireRequest request in battle.FireRequests)
            {
                if (request.Asset == asset)
                {
                    ++count;
                }
            }

            return count;
        }

        private static void AddPoint(AIPortTestBattle battle, string id, SimVec2 position, double vpPerMin = 1)
        {
            battle.Scenario.ControlPoints.Add(new ScenarioDef.ControlPoint
            {
                Id = id,
                DisplayName = id,
                Pos = position,
                RadiusM = 200,
                VpPerMin = vpPerMin
            });
        }
    }
}
