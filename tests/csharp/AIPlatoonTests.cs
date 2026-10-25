using System.Linq;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class AIPlatoonTests
    {
        [Test]
        public void ContactMemoryUsesStrictFifteenSecondBoundary()
        {
            TaskForce taskForce = new TaskForce();
            Platoon platoon = new Platoon { Parent = taskForce, LastContactTick = 0, LastHitTick = -1000000 };
            Assert.That(PlatoonAI.InContact(platoon, 149), Is.True);
            Assert.That(PlatoonAI.InContact(platoon, 150), Is.False);
            platoon.LastHitTick = 100;
            Assert.That(PlatoonAI.InContact(platoon, 249), Is.True);
            Assert.That(PlatoonAI.InContact(platoon, 250), Is.False);
        }

        [Test]
        public void AttackMoveHaltsAndCannotHaltAgainBeforeAdvanceWindow()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            Platoon platoon = battle.NewOrganic(blue, "tank", new SimVec2(2000, 1000), "tank");
            platoon.Posture = "moving";
            platoon.Mission = Order.Make(OrderType.ATTACK_MOVE, platoon.Id);
            platoon.LastContactTick = 0;
            PlatoonAI ai = new PlatoonAI(battle);

            ai.Step(battle.World);
            Assert.That(platoon.ContactHaltUntil, Is.EqualTo(150));
            Assert.That(platoon.NextHaltTick, Is.EqualTo(270));
            battle.AdvanceTo(10);
            ai.Step(battle.World);
            Assert.That(platoon.ContactHaltUntil, Is.EqualTo(150));
            battle.AdvanceTo(270);
            platoon.LastContactTick = 270;
            ai.Step(battle.World);
            Assert.That(platoon.ContactHaltUntil, Is.EqualTo(420));
            Assert.That(platoon.NextHaltTick, Is.EqualTo(540));
        }

        [Test]
        public void CompanyPropagatesChildContactAndUsesAttackHalt()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            Company company = battle.NewCompany(blue, new SimVec2(2000, 1000));
            company.Posture = "moving";
            company.Mission = Order.Make(OrderType.ATTACK_MOVE, company.Id);
            company.Platoons[0].LastContactTick = 20;
            company.Platoons[0].LastHitTick = 30;

            new PlatoonAI(battle).Step(battle.World);
            Assert.That(company.LastContactTick, Is.EqualTo(20));
            Assert.That(company.LastHitTick, Is.EqualTo(30));
            Assert.That(company.ContactHaltUntil, Is.EqualTo(150));
            Assert.That(company.NextHaltTick, Is.EqualTo(270));
        }

        [Test]
        public void MountedInfantryDismountsEvenWhenCompanyControlsReactions()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            Company company = battle.NewCompany(blue, new SimVec2(2000, 1000));
            Platoon mounted = battle.NewPlatoon(company, "mech", new SimVec2(2000, 1000), "ifv", 0);
            Element vehicle = battle.AddElement(mounted, "ifv");
            Element squad = battle.AddElement(mounted, "inf_squad");
            mounted.Mounted = true;
            squad.MountedIn = vehicle;
            vehicle.Passengers.Add(squad);
            mounted.LastContactTick = 0;
            company.Platoons.Add(mounted);
            battle.RegisterTree(blue);

            new PlatoonAI(battle).Step(battle.World);
            Assert.That(mounted.Mounted, Is.False);
            Assert.That(squad.MountedIn, Is.Null);
            Assert.That(battle.Dismounted, Is.EqualTo(new[] { mounted }));
            Assert.That(battle.SelfOrders, Is.Empty);
        }

        [Test]
        public void IdlePlatoonDispersesAwayFromShellAndHonorsCooldown()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            Platoon platoon = battle.NewOrganic(blue, "tank", new SimVec2(2000, 2000), "tank");
            platoon.LastShelledTick = 0;
            platoon.LastShellPos = new SimVec2(1900, 2000);
            PlatoonAI ai = new PlatoonAI(battle);

            ai.Step(battle.World);
            Assert.That(battle.SelfOrders.Count, Is.EqualTo(1));
            Assert.That(battle.SelfOrders[0].Type, Is.EqualTo(OrderType.MOVE));
            Assert.That(battle.SelfOrders[0].GetPos().X, Is.EqualTo(2250).Within(0.001));
            Assert.That(platoon.NextDisperseTick, Is.EqualTo(600));
            Assert.That(battle.World.Events.Drain().Single().Type, Is.EqualTo("disperse"));
            battle.AdvanceTo(10);
            platoon.LastShelledTick = 10;
            ai.Step(battle.World);
            Assert.That(battle.SelfOrders.Count, Is.EqualTo(1));
            battle.AdvanceTo(600);
            platoon.LastShelledTick = 600;
            ai.Step(battle.World);
            Assert.That(battle.SelfOrders.Count, Is.EqualTo(2));
        }

        [Test]
        public void WeakPlatoonWithdrawsOnceButHqDoesNotAutoWithdraw()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            Platoon weak = battle.NewOrganic(blue, "tank", new SimVec2(2000, 2000), "tank", 4);
            Platoon hq = battle.NewOrganic(blue, "hq", new SimVec2(2100, 2000), "cp", 4);
            hq.IsHqSection = true;
            foreach (Platoon platoon in new[] { weak, hq })
            {
                for (int i = 0; i < 3; ++i)
                {
                    platoon.Elements[i].State = ElementState.DESTROYED;
                }
            }
            PlatoonAI ai = new PlatoonAI(battle);

            ai.Step(battle.World);
            Assert.That(weak.AutoWithdrawn, Is.True);
            Assert.That(hq.AutoWithdrawn, Is.False);
            Assert.That(battle.SelfOrders.Count, Is.EqualTo(1));
            Assert.That(battle.SelfOrders[0].TargetUnitId, Is.EqualTo(weak.Id));
            Assert.That(battle.World.Events.Drain().Single().Type, Is.EqualTo("auto_withdraw"));
            battle.AdvanceTo(10);
            ai.Step(battle.World);
            Assert.That(battle.SelfOrders.Count, Is.EqualTo(1));
        }

        [Test]
        public void RoutedPlatoonDetachesAndWithdrawsTowardRear()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            Platoon routed = battle.NewOrganic(blue, "tank", new SimVec2(2000, 2000), "tank");
            routed.Routed = true;

            new PlatoonAI(battle).Step(battle.World);
            Assert.That(routed.Detached, Is.True);
            Assert.That(battle.SelfOrders.Count, Is.EqualTo(1));
            Assert.That(battle.SelfOrders[0].Type, Is.EqualTo(OrderType.WITHDRAW));
            Assert.That(battle.SelfOrders[0].GetPos().Y, Is.EqualTo(1200).Within(0.001));
        }

        [Test]
        public void BreakContactMovesAwayFromNearestLiveFreshContact()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            TaskForce red = battle.NewTaskForce("red");
            Platoon platoon = battle.NewOrganic(blue, "tank", new SimVec2(2000, 2000), "tank");
            Platoon enemy = battle.NewOrganic(red, "tank", new SimVec2(2200, 2000), "tank");
            platoon.Sop.OnContact = "break_contact";
            platoon.Posture = "moving";
            platoon.Mission = Order.Make(OrderType.MOVE, platoon.Id);
            platoon.LastContactTick = 0;
            platoon.Contacts[enemy.Elements[0].Id] = 0;

            new PlatoonAI(battle).Step(battle.World);
            Assert.That(battle.SelfOrders.Count, Is.EqualTo(1));
            Assert.That(battle.SelfOrders[0].Type, Is.EqualTo(OrderType.WITHDRAW));
            Assert.That(battle.SelfOrders[0].GetPos().X, Is.EqualTo(1600).Within(0.001));
        }

        [Test]
        public void AssaultRequiresLiveFreshContact()
        {
            AIPortTestBattle battle = new AIPortTestBattle();
            TaskForce blue = battle.NewTaskForce("blue");
            TaskForce red = battle.NewTaskForce("red");
            Platoon platoon = battle.NewOrganic(blue, "infantry", new SimVec2(2000, 2000), "inf_squad");
            Platoon enemy = battle.NewOrganic(red, "tank", new SimVec2(2300, 2000), "tank");
            platoon.Sop.OnContact = "assault";
            platoon.LastContactTick = 0;
            platoon.Contacts[enemy.Elements[0].Id] = 0;
            PlatoonAI ai = new PlatoonAI(battle);

            ai.Step(battle.World);
            Assert.That(battle.SelfOrders.Count, Is.EqualTo(1));
            Assert.That(battle.SelfOrders[0].Type, Is.EqualTo(OrderType.ATTACK_MOVE));
            Assert.That(battle.SelfOrders[0].GetPos(), Is.EqualTo(enemy.Elements[0].Pos));
            enemy.Elements[0].State = ElementState.DESTROYED;
            battle.SelfOrders.Clear();
            battle.AdvanceTo(10);
            ai.Step(battle.World);
            Assert.That(battle.SelfOrders, Is.Empty);
        }
    }
}
