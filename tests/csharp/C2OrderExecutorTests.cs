using System.Collections.Generic;
using Echelon.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    public sealed class C2OrderExecutorTests
    {
        private readonly List<Battle> _battles = new List<Battle>();

        [TearDown]
        public void AfterEach()
        {
            foreach (Battle battle in _battles)
            {
                battle.Dispose();
            }

            _battles.Clear();
        }

        [Test]
        public void AttackRefusalRespectsMoraleAndSelfOrderExemption()
        {
            Battle battle = NewBattle();
            Company company = AddCompany(battle, "us_tank_co", "blue");
            company.Morale = OrderExecutor.ShakenMorale;
            Order refused = Order.Make(OrderType.ATTACK_MOVE, company.Id);
            refused.SetPos(company.Guide.Pos);
            battle.Executor.Execute(company, refused);
            Assert.That(refused.State, Is.EqualTo(OrderState.CANCELLED));

            Order self = Order.Make(OrderType.ATTACK_MOVE, company.Id);
            self.Issuer = "self";
            self.SetPos(company.Guide.Pos);
            battle.Executor.Execute(company, self);
            Assert.That(self.State, Is.EqualTo(OrderState.DONE));
        }

        [Test]
        public void AppendedMovementWaitsThenStartsAfterArrival()
        {
            Battle battle = NewBattle();
            Platoon platoon = AddPlatoon(battle, "us_tank_plt", "blue");
            Order first = Order.Make(OrderType.MOVE, platoon.Id);
            first.SetPos(new SimVec2(2300, 800));
            battle.Executor.Execute(platoon, first);
            Assert.That(first.State, Is.EqualTo(OrderState.EXECUTING));

            Order appended = Order.Make(OrderType.MOVE, platoon.Id, new JObject { ["append"] = true });
            appended.SetPos(new SimVec2(2400, 900));
            battle.Executor.Execute(platoon, appended);
            Assert.That(appended.State, Is.EqualTo(OrderState.RECEIVED));
            Assert.That(platoon.QueuedOrders, Is.EqualTo(new[] { appended }));

            battle.Executor.OnArrival(platoon);
            Assert.That(first.State, Is.EqualTo(OrderState.DONE));
            Assert.That(appended.State, Is.EqualTo(OrderState.EXECUTING));
            Assert.That(platoon.Mission, Is.SameAs(appended));
            Assert.That(platoon.QueuedOrders, Is.Empty);
        }

        [Test]
        public void RejoinRestoresCompanyControlAndClearsMissionState()
        {
            Battle battle = NewBattle();
            Platoon platoon = AddPlatoon(battle, "us_tank_plt", "blue");
            Order mission = Order.Make(OrderType.MOVE, platoon.Id);
            mission.State = OrderState.EXECUTING;
            platoon.Mission = mission;
            platoon.QueuedOrders.Add(Order.Make(OrderType.MOVE, platoon.Id));
            platoon.Detached = true;
            platoon.AwaitingFollow = true;
            platoon.Posture = "moving";
            platoon.Guide.SetPath(new List<SimVec2> { platoon.Guide.Pos, platoon.Guide.Pos + new SimVec2(100, 0) });

            Order rejoin = Order.Make(OrderType.REJOIN, platoon.Id);
            battle.Executor.Execute(platoon, rejoin);
            Assert.That(rejoin.State, Is.EqualTo(OrderState.DONE));
            Assert.That(mission.State, Is.EqualTo(OrderState.CANCELLED));
            Assert.That(platoon.Mission, Is.Null);
            Assert.That(platoon.QueuedOrders, Is.Empty);
            Assert.That(platoon.Detached, Is.False);
            Assert.That(platoon.AwaitingFollow, Is.False);
            Assert.That(platoon.Guide.IsMoving(), Is.False);
            Assert.That(platoon.Posture, Is.EqualTo("idle"));
        }

        [Test]
        public void SopRecursionAndFormationChangesUseOriginalScope()
        {
            Battle battle = NewBattle();
            Company company = AddCompany(battle, "us_tank_co", "blue");
            Order sop = Order.Make(OrderType.SET_SOP, company.Id, new JObject
            {
                ["sop"] = new JObject { ["technique"] = "bounding", ["withdraw_at"] = 0.25 }
            });
            battle.Executor.Execute(company, sop);
            Assert.That(sop.State, Is.EqualTo(OrderState.DONE));
            Assert.That(company.Sop.Technique, Is.EqualTo("bounding"));
            Assert.That(company.Sop.WithdrawAt, Is.EqualTo(0.25));
            foreach (Unit child in company.ChildrenUnits())
            {
                Assert.That(child.Sop.Technique, Is.EqualTo("bounding"));
            }

            Order nonRecursive = Order.Make(OrderType.SET_SOP, company.Id, new JObject
            {
                ["recursive"] = false,
                ["sop"] = new JObject { ["roe"] = "hold_fire" }
            });
            battle.Executor.Execute(company, nonRecursive);
            Assert.That(company.Sop.Roe, Is.EqualTo("hold_fire"));
            Assert.That(company.Platoons[0].Sop.Roe, Is.EqualTo("fire_at_will"));

            string hqFormation = company.Hq!.Formation!.Id;
            Order formation = Order.Make(OrderType.SET_FORMATION, company.Id, new JObject
            {
                ["formation"] = "co_line",
                ["child_formation"] = "line"
            });
            battle.Executor.Execute(company, formation);
            Assert.That(formation.State, Is.EqualTo(OrderState.DONE));
            Assert.That(company.Formation!.Id, Is.EqualTo("co_line"));
            FormationDef definition = DataTestAssets.Catalog.GetFormation("co_line")!;
            double seconds = definition.GetMod("transition_sec", 10.0) * definition.GetMod("control", 1.0) *
                company.SkillDelayFactor();
            Assert.That(company.ReformUntilTick, Is.EqualTo(OrderSystem.SecondsToTicks(seconds)));
            foreach (Platoon platoon in company.Platoons)
            {
                Assert.That(platoon.Formation!.Id, Is.EqualTo("line"));
            }

            Assert.That(company.Hq!.Formation!.Id, Is.EqualTo(hqFormation));
        }

        [Test]
        public void DismountAndMountRestorePassengerLinksAndPositions()
        {
            Battle battle = NewBattle();
            Platoon platoon = AddPlatoon(battle, "us_mech_plt", "blue");
            Order dismount = Order.Make(OrderType.DISMOUNT, platoon.Id);
            battle.Executor.Execute(platoon, dismount);
            Assert.That(dismount.State, Is.EqualTo(OrderState.DONE));
            Assert.That(platoon.Mounted, Is.False);
            Assert.That(platoon.ReformUntilTick, Is.EqualTo(10 * SimConst.TickRate));
            foreach (Element squad in platoon.Squads())
            {
                Assert.That(squad.MountedIn, Is.Null);
            }

            foreach (Element vehicle in platoon.Vehicles())
            {
                Assert.That(vehicle.Passengers, Is.Empty);
            }

            Order mount = Order.Make(OrderType.MOUNT, platoon.Id);
            battle.Executor.Execute(platoon, mount);
            Assert.That(mount.State, Is.EqualTo(OrderState.DONE));
            Assert.That(platoon.Mounted, Is.True);
            foreach (Element squad in platoon.Squads())
            {
                Assert.That(squad.MountedIn, Is.Not.Null);
                Assert.That(squad.MountedIn!.Passengers, Does.Contain(squad));
                Assert.That(squad.Pos, Is.EqualTo(squad.MountedIn.Pos));
            }
        }

        [Test]
        public void EngineerOrdersRejectNonEngineersAndMissingArrivalCapacity()
        {
            Battle battle = NewBattle();
            Platoon tank = AddPlatoon(battle, "us_tank_plt", "blue");
            Order refused = Order.Make(OrderType.LAY_MINES, tank.Id);
            battle.Executor.Execute(tank, refused);
            Assert.That(refused.State, Is.EqualTo(OrderState.CANCELLED));

            Platoon engineer = AddPlatoon(battle, "us_engineer_plt", "blue");
            engineer.MinesLeft = 0;
            Order noCapacity = Order.Make(OrderType.LAY_MINES, engineer.Id);
            noCapacity.SetPos(engineer.Guide.Pos);
            battle.Executor.Execute(engineer, noCapacity);
            Assert.That(noCapacity.State, Is.EqualTo(OrderState.CANCELLED));
            Assert.That(engineer.Mission, Is.Null);
            Assert.That(engineer.Posture, Is.EqualTo("idle"));
        }

        [Test]
        public void StartingMovementResetsDefendFacingToGodotPositiveNan()
        {
            Battle battle = NewBattle();
            Platoon platoon = AddPlatoon(battle, "us_tank_plt", "blue");
            platoon.DefendFacing = 0.5;
            Order order = Order.Make(OrderType.MOVE, platoon.Id);
            order.SetPos(platoon.Guide.Pos + new SimVec2(500.0, 0.0));

            battle.Executor.Execute(platoon, order);

            Assert.That(double.IsNaN(platoon.DefendFacing), Is.True);
            Assert.That(System.BitConverter.DoubleToInt64Bits(platoon.DefendFacing),
                Is.EqualTo(unchecked((long)0x7ff8000000000000UL)));
        }

        [Test]
        public void CallFireRemainsExecutingForItsOwningSupportSystem()
        {
            Battle battle = NewBattle();
            Company company = AddCompany(battle, "us_tank_co", "blue");
            Order callFire = Order.Make(OrderType.CALL_FIRE, company.Id);
            battle.Executor.Execute(company, callFire);
            Assert.That(callFire.State, Is.EqualTo(OrderState.EXECUTING));
        }

        private Battle NewBattle()
        {
            DataCatalog catalog = DataTestAssets.Catalog;
            Battle battle = Battle.Create(catalog, "skirmish_01", 3, true);
            battle.Factory.CreateTaskForce("blue", catalog.GetToe("us_armor_bn_tf")!, new SimVec2(2000, 150), 0.0);
            battle.Factory.CreateTaskForce("red", catalog.GetToe("ru_btg_bmp")!,
                new SimVec2(2000, 3850), System.Math.PI);
            _battles.Add(battle);
            return battle;
        }

        private static Company AddCompany(Battle battle, string templateId, string side)
        {
            TaskForce taskForce = battle.TaskForces[side];
            Company company = battle.Factory.SpawnCompany(DataTestAssets.Catalog.GetUnitTemplate(templateId)!,
                side, new SimVec2(2700, 450), 0.0, taskForce, taskForce.Companies.Count);
            taskForce.Companies.Add(company);
            return company;
        }

        private static Platoon AddPlatoon(Battle battle, string templateId, string side)
        {
            TaskForce taskForce = battle.TaskForces[side];
            Platoon platoon = battle.Factory.SpawnPlatoon(DataTestAssets.Catalog.GetUnitTemplate(templateId)!,
                side, new SimVec2(2700, 450), 0.0, taskForce);
            taskForce.Organic.Add(platoon);
            return platoon;
        }
    }
}
