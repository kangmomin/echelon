using System;
using System.Collections.Generic;
using Echelon.Core;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    internal sealed class SupportBattleRig : IDisposable
    {
        internal SupportBattleRig(Battle battle)
        {
            Battle = battle;
            Morale = (MoraleSystem)battle.Morale!;
            Detection = battle.Detection;
            Combat = battle.Combat;
            Fires = battle.Fires;
            Supply = (SupplySystem)battle.Supply;
            Engineers = (EngineerSystem)battle.Engineers!;
        }

        internal Battle Battle { get; }
        internal MoraleSystem Morale { get; }
        internal DetectionSystem Detection { get; }
        internal CombatSystem Combat { get; }
        internal FireSupportSystem Fires { get; }
        internal SupplySystem Supply { get; }
        internal EngineerSystem Engineers { get; }

        public void Dispose()
        {
            Battle.Dispose();
        }
    }

    internal static class FiresSupportTestFixture
    {
        internal static readonly SimVec2 Target = new SimVec2(2000.0, 2600.0);
        internal static DataCatalog Catalog => DataTestAssets.Catalog;

        internal static SupportBattleRig Create(long seed = 1, bool instant = true, MapDef? map = null,
            bool createTaskForces = true)
        {
            Battle battle = Battle.Create(Catalog, "skirmish_01", seed, instant, map ?? FlatMap());
            SupportBattleRig rig = new SupportBattleRig(battle);
            if (createTaskForces)
            {
                battle.Factory.CreateTaskForce("blue", Catalog.GetToe("us_armor_bn_tf")!,
                    new SimVec2(200.0, 2000.0), Math.PI / 2.0);
                battle.Factory.CreateTaskForce("red", Catalog.GetToe("ru_btg_bmp")!,
                    new SimVec2(3800.0, 3800.0), -Math.PI / 2.0);
            }

            return rig;
        }

        internal static MapDef FlatMap()
        {
            return new MapDef
            {
                Id = "test_flat",
                DisplayName = "평지",
                SizeM = new SimVec2(4000.0, 4000.0),
                CellM = 10.0,
                HeightSeed = 1,
                BaseM = 40.0,
                AmplitudeM = 0.0,
                Frequency = 0.001,
                Octaves = 1
            };
        }

        internal static Platoon Platoon(SupportBattleRig rig, string templateId, string side, SimVec2 position,
            double heading = 0.0)
        {
            TaskForce taskForce = rig.Battle.TaskForces[side];
            Platoon platoon = rig.Battle.Factory.SpawnPlatoon(Catalog.GetUnitTemplate(templateId)!, side, position,
                heading, taskForce);
            taskForce.Organic.Add(platoon);
            return platoon;
        }

        internal static Order Move(SupportBattleRig rig, Unit unit, SimVec2 position,
            OrderType type = OrderType.MOVE, double facing = double.NaN)
        {
            Order order = Order.Make(type, unit.Id);
            order.SetPos(position);
            if (!double.IsNaN(facing))
            {
                order.SetFacing(facing);
            }

            return rig.Battle.IssueOrder(order);
        }

        internal static void Teleport(SupportBattleRig rig, Unit unit, SimVec2 position)
        {
            unit.Guide.Reset(position, unit.Guide.Heading);
            if (unit is Platoon platoon)
            {
                rig.Battle.Factory.PlaceElements(platoon);
            }
            else if (unit is TaskForce taskForce && taskForce.CpSection != null)
            {
                taskForce.CpSection.Guide.Reset(position, taskForce.CpSection.Guide.Heading);
                rig.Battle.Factory.PlaceElements(taskForce.CpSection);
            }

            rig.Battle.RebuildSpatial();
        }

        internal static void Kill(Element element)
        {
            element.State = ElementState.DESTROYED;
            foreach (Element passenger in element.Passengers)
            {
                passenger.State = ElementState.DESTROYED;
            }
        }

        internal static SupportBattleRig FiresBattle(long seed, bool withForwardObserver)
        {
            SupportBattleRig rig = Create(seed, false);
            TaskForce taskForce = rig.Battle.TaskForces["blue"];
            Teleport(rig, taskForce, new SimVec2(2000.0, 300.0));
            Platoon(rig, "us_mortar_plt", "blue", new SimVec2(2000.0, 600.0));
            if (withForwardObserver)
            {
                Platoon(rig, "us_fo_team", "blue", new SimVec2(2000.0, 1300.0));
            }

            Platoon red = Platoon(rig, "ru_tank_plt", "red", Target, Math.PI);
            red.Sop.UnderFire = "hold";
            foreach (Element element in red.Elements)
            {
                element.Ammo.Clear();
            }

            rig.Battle.RunTicks(25);
            return rig;
        }

        internal static FireSupportSystem.Mission RequestedMission(Dictionary<string, object> result)
        {
            if (!result.TryGetValue("ok", out object? ok) || !(ok is bool succeeded) || !succeeded)
            {
                Assert.Fail("Fire request failed: " + (result.TryGetValue("error", out object? error) ? error : "unknown"));
            }

            return (FireSupportSystem.Mission)result["mission"];
        }

        internal static List<SimEvent> Drain(SupportBattleRig rig)
        {
            return rig.Battle.World.Events.Drain();
        }
    }
}
