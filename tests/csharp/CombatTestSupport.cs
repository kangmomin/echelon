using System;
using System.Collections.Generic;
using Echelon.Core;
using Newtonsoft.Json.Linq;

namespace Echelon.Core.Tests
{
    internal sealed class CombatTestRig : IDisposable
    {
        internal CombatTestRig(Battle battle, ICombatBattle context, DetectionSystem detection,
            CombatSystem combat, MoraleSystem morale)
        {
            Battle = battle;
            Context = context;
            Detection = detection;
            Combat = combat;
            Morale = morale;
        }

        internal Battle Battle { get; }
        internal ICombatBattle Context { get; }
        internal DetectionSystem Detection { get; }
        internal CombatSystem Combat { get; }
        internal MoraleSystem Morale { get; }

        public void Dispose()
        {
            Battle.Dispose();
        }
    }

    internal static class CombatTestSupport
    {
        internal static DataCatalog Catalog => DataTestAssets.Catalog;

        internal static CombatTestRig Create(long seed = 1, MapDef? map = null)
        {
            Battle battle = Battle.Create(Catalog, "skirmish_01", seed, true, map);
            battle.Factory.CreateTaskForce("blue", Catalog.GetToe("us_armor_bn_tf")!,
                new SimVec2(200.0, 2000.0), Math.PI / 2.0);
            battle.Factory.CreateTaskForce("red", Catalog.GetToe("ru_btg_bmp")!,
                new SimVec2(3800.0, 3800.0), -Math.PI / 2.0);
            ICombatBattle context = new CombatBattleAdapter(battle);
            MoraleSystem? morale = FindSystem<MoraleSystem>(battle);
            DetectionSystem? detection = FindSystem<DetectionSystem>(battle);
            CombatSystem? combat = FindSystem<CombatSystem>(battle);
            if (morale == null)
            {
                morale = new MoraleSystem(context);
            }

            if (battle.Morale == null)
            {
                battle.Morale = morale;
            }

            if (detection == null)
            {
                detection = new DetectionSystem(context);
                battle.World.AddSystem(detection);
            }

            if (combat == null)
            {
                combat = new CombatSystem(context, morale);
                battle.World.AddSystem(combat);
            }

            if (FindSystem<MoraleSystem>(battle) == null)
            {
                battle.World.AddSystem(morale);
            }

            return new CombatTestRig(battle, context, detection, combat, morale);
        }

        internal static MapDef FlatMap(MapRect? forest = null)
        {
            MapDef map = new MapDef
            {
                Id = "combat_test_flat",
                DisplayName = "Combat test flat map",
                SizeM = new SimVec2(4000.0, 4000.0),
                CellM = 10.0,
                HeightSeed = 1,
                BaseM = 40.0,
                AmplitudeM = 0.0,
                Frequency = 0.001,
                Octaves = 1
            };
            if (forest.HasValue)
            {
                MapRect rectangle = forest.Value;
                MapDef.Feature feature = new MapDef.Feature { Name = "Test forest" };
                feature.Points.Add(rectangle.Position);
                feature.Points.Add(new SimVec2(rectangle.End.X, rectangle.Position.Y));
                feature.Points.Add(rectangle.End);
                feature.Points.Add(new SimVec2(rectangle.Position.X, rectangle.End.Y));
                map.Forests.Add(feature);
            }

            return map;
        }

        internal static Platoon Platoon(Battle battle, string templateId, string side, SimVec2 position,
            double heading = 0.0)
        {
            TaskForce taskForce = battle.TaskForces[side];
            Platoon platoon = battle.Factory.SpawnPlatoon(Catalog.GetUnitTemplate(templateId)!,
                side, position, heading, taskForce);
            taskForce.Organic.Add(platoon);
            return platoon;
        }

        internal static Company Company(Battle battle, string templateId, string side, SimVec2 position,
            double heading = 0.0)
        {
            TaskForce taskForce = battle.TaskForces[side];
            Company company = battle.Factory.SpawnCompany(Catalog.GetUnitTemplate(templateId)!,
                side, position, heading, taskForce, taskForce.Companies.Count);
            taskForce.Companies.Add(company);
            return company;
        }

        internal static Order Move(Battle battle, Unit unit, SimVec2 position,
            OrderType type = OrderType.MOVE, double facing = double.NaN, JObject? parameters = null)
        {
            Order order = Order.Make(type, unit.Id, parameters ?? new JObject());
            order.SetPos(position);
            if (!double.IsNaN(facing))
            {
                order.SetFacing(facing);
            }

            return battle.IssueOrder(order);
        }

        internal static void Teleport(Battle battle, Unit unit, SimVec2 position)
        {
            unit.Guide.Reset(position, unit.Guide.Heading);
            if (unit is Platoon platoon)
            {
                battle.Factory.PlaceElements(platoon);
            }
        }

        internal static int Losses(Unit unit)
        {
            int losses = 0;
            foreach (Element element in unit.AllElements())
            {
                if (!element.IsAlive())
                {
                    ++losses;
                }
            }

            return losses;
        }

        private static T? FindSystem<T>(Battle battle) where T : SimSystem
        {
            foreach (SimSystem system in battle.World.GetSystems())
            {
                if (system is T match)
                {
                    return match;
                }
            }

            return null;
        }
    }

    internal sealed class CombatBattleAdapter : ICombatBattle
    {
        private readonly Battle _battle;

        internal CombatBattleAdapter(Battle battle)
        {
            _battle = battle;
        }

        public SimWorld World => _battle.World;
        public Terrain Terrain => _battle.Terrain;
        public DataCatalog Catalog => _battle.Catalog;
        public GridPathfinder Pathfinder => _battle.Pathfinder;
        public IReadOnlyList<Unit> Units => _battle.Units;
        public IReadOnlyList<Element> Elements => _battle.Elements;
        public IReadOnlyDictionary<int, Order> OrdersById => _battle.OrdersById;
        public OrderExecutor Executor => _battle.Executor;
        public OrderSystem? OrderSystem => _battle.OrderSystem;
        public IOrderSupply Supply => _battle.Supply!;
        public IOrderEngineers? Engineers => _battle.Engineers;
        public IOrderMorale? Morale => _battle.Morale;
        public IOrderAir? Air => _battle.Air;
        public SpatialHash<Element> Spatial => _battle.Spatial;
        public List<Los.Smoke> Smokes => _battle.Smokes;
        public CommsNet Comms => _battle.Comms;

        public Unit? GetUnit(int unitId)
        {
            return _battle.GetUnit(unitId);
        }

        public TaskForce? GetTaskForce(string side)
        {
            return _battle.GetTaskForce(side);
        }

        public Order RegisterOrder(Order order)
        {
            return _battle.RegisterOrder(order);
        }

        public Element? GetElement(int elementId)
        {
            return _battle.GetElement(elementId);
        }

        public List<Element> ElementsNear(SimVec2 position, double radiusM)
        {
            return _battle.ElementsNear(position, radiusM);
        }
    }

    internal sealed class CombatPositionEffects : ICombatEngineers
    {
        internal double Factor { get; set; } = 0.6;

        public bool BeginTask(Platoon platoon, Order order)
        {
            return false;
        }

        public double PositionFactor(Element element)
        {
            return Factor;
        }
    }
}
