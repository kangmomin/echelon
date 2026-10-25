using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    [TestFixture]
    [Category("M5")]
    public sealed class SupplyM5IntegrationTests
    {
        [Test]
        public void ResupplyRestoresEmptyPlatoonAndRepairsBesideTrucks()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.Create(8, false);
            TaskForce taskForce = rig.Battle.TaskForces["blue"];
            FiresSupportTestFixture.Teleport(rig, taskForce, new SimVec2(2000.0, 300.0));
            taskForce.EntryPos = new SimVec2(2000.0, 50.0);
            Platoon tanks = FiresSupportTestFixture.Platoon(rig, "us_tank_plt", "blue", new SimVec2(2000.0, 1200.0));
            Platoon trucks = FiresSupportTestFixture.Platoon(rig, "us_supply_section", "blue", new SimVec2(1800.0, 400.0));
            foreach (Element element in tanks.Elements)
            {
                foreach (string weaponId in new List<string>(element.Ammo.Keys))
                {
                    element.Ammo[weaponId] = 0;
                }
            }

            tanks.Elements[1].State = ElementState.DAMAGED;
            tanks.Elements[1].DamageKind = "firepower";
            Assert.That(SupplySystem.PlatoonNeed(tanks), Is.GreaterThan(30.0));
            rig.Battle.RunTicks(25);
            Order order = FiresSupportTestFixture.Move(rig, tanks, SimVec2.Zero, OrderType.RESUPPLY);
            Assert.That(order.State, Is.Not.EqualTo(OrderState.CANCELLED));
            rig.Battle.RunSeconds(60.0);
            Assert.That(tanks.Posture, Is.EqualTo("resupplying"));
            Assert.That(rig.Supply.Tasks.ContainsKey(trucks.Id), Is.True);
            double startingNeed = SupplySystem.PlatoonNeed(tanks);
            rig.Battle.RunSeconds(240.0);
            Assert.That(SupplySystem.PlatoonNeed(tanks), Is.LessThan(startingNeed));
            rig.Battle.RunSeconds(240.0);
            Assert.That(SupplySystem.PlatoonNeed(tanks), Is.EqualTo(0.0).Within(0.05));
            Assert.That(tanks.Posture, Is.EqualTo("idle"));
            Assert.That(tanks.Elements[0].Ammo["m256_apfsds"], Is.GreaterThan(10));
            Assert.That(SupplySystem.SectionSupply(trucks), Is.LessThan(SupplySystem.SectionCapacity(trucks)));
            Assert.That(tanks.Elements[1].State, Is.EqualTo(ElementState.OK));
        }

        [Test]
        public void EmptySectionReturnsRefillsAndAcceptsRequestAgain()
        {
            using SupportBattleRig rig = FiresSupportTestFixture.Create(18, true);
            TaskForce taskForce = rig.Battle.TaskForces["blue"];
            SimVec2 entry = new SimVec2(1800.0, 400.0);
            taskForce.EntryPos = entry;
            Platoon tanks = FiresSupportTestFixture.Platoon(rig, "us_tank_plt", "blue", entry);
            Platoon trucks = FiresSupportTestFixture.Platoon(rig, "us_supply_section", "blue", entry);
            foreach (Element element in tanks.Elements)
            {
                foreach (string weaponId in new List<string>(element.Ammo.Keys))
                {
                    element.Ammo[weaponId] = 0;
                }
            }

            for (int i = 0; i < trucks.Elements.Count; ++i)
            {
                trucks.Elements[i].SupplyLeft = i == 0 ? 1.0 : 0.0;
            }

            rig.Supply.Request(tanks);
            rig.Battle.StepTick();
            Assert.That(rig.Supply.Returning.ContainsKey(trucks.Id), Is.True);
            Assert.That(rig.Supply.Requests, Does.Contain(tanks.Id));
            Assert.That(rig.Supply.Tasks.ContainsKey(trucks.Id), Is.False);
            int guard = 0;
            while (rig.Supply.Returning.ContainsKey(trucks.Id) && guard < 2500)
            {
                rig.Battle.StepTick();
                ++guard;
            }

            Assert.That(rig.Supply.Returning.ContainsKey(trucks.Id), Is.False);
            Assert.That(SupplySystem.SectionSupply(trucks),
                Is.EqualTo(SupplySystem.SectionCapacity(trucks)).Within(0.01));
            rig.Battle.RunTicks(10);
            Assert.That(rig.Supply.Tasks.ContainsKey(trucks.Id), Is.True);
            List<SimEvent> events = FiresSupportTestFixture.Drain(rig);
            Assert.That(events.Exists(e => e.Type == "supply_returning" && (int)e.Payload["unit"] == trucks.Id), Is.True);
            Assert.That(events.Exists(e => e.Type == "supply_refilled" && (int)e.Payload["unit"] == trucks.Id), Is.True);
        }
    }
}
