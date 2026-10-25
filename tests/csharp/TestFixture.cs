using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Echelon.Core.Tests
{
    internal static class TestFixture
    {
        private static readonly Lazy<DataCatalog> SharedCatalog = new Lazy<DataCatalog>(() =>
            DataLoader.LoadCatalog(Path.Combine(RepositoryRoot(), "data")));

        public static DataCatalog Catalog()
        {
            return SharedCatalog.Value;
        }

        public static string RepositoryRoot()
        {
            var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "data")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Could not find the repository data directory");
        }

        public static Battle Battle(int seedValue = 1, bool instant = true)
        {
            DataCatalog catalog = Catalog();
            var battle = Core.Battle.Create(catalog, "skirmish_01", seedValue, instant);
            battle.Factory.CreateTaskForce("blue", catalog.GetToe("us_armor_bn_tf")!, new SimVec2(2000.0, 150.0), 0.0);
            battle.Factory.CreateTaskForce("red", catalog.GetToe("ru_btg_bmp")!, new SimVec2(2000.0, 3850.0), Math.PI);
            return battle;
        }

        public static Battle Skirmish(string[] ai, int seedValue = -1)
        {
            DataCatalog catalog = Catalog();
            var battle = BattleSetup.Skirmish(catalog, catalog.DefaultForce("US")!, catalog.DefaultForce("RU")!,
                "skirmish_01", seedValue, ai);
            battle.BeginBattle();
            return battle;
        }

        public static Platoon Platoon(Core.Battle battle, string templateId, string side, SimVec2 pos, double heading = 0.0)
        {
            TaskForce taskForce = battle.TaskForces[side];
            var platoon = battle.Factory.SpawnPlatoon(battle.Catalog.GetUnitTemplate(templateId)!, side, pos, heading, taskForce);
            taskForce.Organic.Add(platoon);
            return platoon;
        }

        public static Company Company(Core.Battle battle, string templateId, string side, SimVec2 pos, double heading = 0.0)
        {
            TaskForce taskForce = battle.TaskForces[side];
            var company = battle.Factory.SpawnCompany(battle.Catalog.GetUnitTemplate(templateId)!, side, pos, heading,
                taskForce, taskForce.Companies.Count);
            taskForce.Companies.Add(company);
            return company;
        }

        public static Order Move(Core.Battle battle, Unit unit, SimVec2 pos, OrderType type = OrderType.MOVE,
            double facing = double.NaN, JObject? parameters = null)
        {
            var order = Order.Make(type, unit.Id, parameters == null ? new JObject() : (JObject)parameters.DeepClone());
            order.SetPos(pos);
            if (!double.IsNaN(facing))
                order.SetFacing(facing);
            return battle.IssueOrder(order);
        }

        public static void Kill(Element element)
        {
            element.State = ElementState.DESTROYED;
            foreach (Element passenger in element.Passengers)
                passenger.State = ElementState.DESTROYED;
        }

        public static void Teleport(Core.Battle battle, Unit unit, SimVec2 pos)
        {
            unit.Guide.Reset(pos, unit.Guide.Heading);
            if (unit is Platoon platoon)
                battle.Factory.PlaceElements(platoon);
        }

        public static double SecondsUntilMoving(Core.Battle battle, Unit unit, double limitSec)
        {
            long start = battle.Tick();
            int limit = (int)SimMath.Round(limitSec * SimConst.TickRate);
            while (battle.Tick() - start < limit)
            {
                battle.StepTick();
                foreach (Element element in unit.AliveElements())
                {
                    if (!element.IsMounted() && element.Velocity.Length() > 0.5)
                        return (battle.Tick() - start) / (double)SimConst.TickRate;
                }
            }
            return -1.0;
        }

        public static MapDef FlatMap(MapRect? forestRect = null)
        {
            var map = new MapDef
            {
                Id = "test_flat",
                DisplayName = "평지",
                SizeM = new SimVec2(4000.0f, 4000.0f),
                CellM = 10.0,
                HeightSeed = 1,
                BaseM = 40.0,
                AmplitudeM = 0.0,
                Frequency = 0.001,
                Octaves = 1,
            };
            if (forestRect.HasValue && forestRect.Value.Size.X > 0.0f && forestRect.Value.Size.Y > 0.0f)
            {
                MapRect rect = forestRect.Value;
                var feature = new MapDef.Feature { Name = "시험 숲" };
                feature.Points.Add(rect.Position);
                feature.Points.Add(new SimVec2(rect.End.X, rect.Position.Y));
                feature.Points.Add(rect.End);
                feature.Points.Add(new SimVec2(rect.Position.X, rect.End.Y));
                map.Forests.Add(feature);
            }
            return map;
        }

        public static Core.Battle BattleOn(MapDef map, int seedValue = 1, bool instant = true)
        {
            DataCatalog catalog = Catalog();
            var battle = Core.Battle.Create(catalog, "skirmish_01", seedValue, instant, map);
            battle.Factory.CreateTaskForce("blue", catalog.GetToe("us_armor_bn_tf")!, new SimVec2(200.0, 2000.0), Math.PI / 2.0);
            battle.Factory.CreateTaskForce("red", catalog.GetToe("ru_btg_bmp")!, new SimVec2(3800.0, 3800.0), -Math.PI / 2.0);
            return battle;
        }

        public static int Losses(Unit unit)
        {
            int count = 0;
            foreach (Element element in unit.AllElements())
            {
                if (!element.IsAlive())
                    count++;
            }
            return count;
        }
    }
}
