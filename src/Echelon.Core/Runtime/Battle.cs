using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public sealed class Battle : ICommandBattle, ICombatBattle, IDeploymentTerrain, IAiBattle, IAiFireSupport,
        IScoringBattle, IReinforcementBattle, IBattleStatsBattle, IDisposable
    {
        public static readonly string[] Sides = { "blue", "red" };

        public DataCatalog Catalog { get; private set; }
        public ScenarioDef Scenario { get; private set; }
        public MapDef Map { get; private set; }
        public Terrain Terrain { get; private set; }
        public GridPathfinder Pathfinder { get; private set; }
        public SimWorld World { get; private set; }
        public SpatialHash<Element> Spatial { get; } = new SpatialHash<Element>(100.0);
        public ForceFactory Factory { get; private set; }

        public List<Element> Elements { get; } = new List<Element>();
        public Dictionary<int, Element> ElementsById { get; } = new Dictionary<int, Element>();
        public List<Unit> Units { get; } = new List<Unit>();
        public Dictionary<int, Unit> UnitsById { get; } = new Dictionary<int, Unit>();
        public Dictionary<string, TaskForce> TaskForces { get; } = new Dictionary<string, TaskForce>();
        public Dictionary<int, Order> OrdersById { get; } = new Dictionary<int, Order>();

        IReadOnlyList<Element> ICommandBattle.Elements => Elements;
        IReadOnlyList<Unit> ICommandBattle.Units => Units;
        IReadOnlyDictionary<int, Order> ICommandBattle.OrdersById => OrdersById;
        IReadOnlyDictionary<string, object> IBattleStatsBattle.Result => Result;
        IAiFireSupport IAiBattle.Fires => this;

        public OrderExecutor Executor { get; private set; }
        public MovementSystem Movement { get; private set; }
        public CommsNet Comms { get; private set; }
        public SuccessionSystem Succession { get; private set; }
        public OrderSystem? OrderSystem { get; private set; }
        public ReinforcementSystem Reinforcements { get; private set; } = null!;
        public TaskForceAI TfAi { get; private set; } = null!;
        public PlatoonAI PlatoonAi { get; private set; } = null!;
        public DetectionSystem Detection { get; private set; } = null!;
        public CombatSystem Combat { get; private set; } = null!;
        public FireSupportSystem Fires { get; private set; } = null!;
        public IOrderSupply Supply { get; set; } = null!;
        public IOrderEngineers? Engineers { get; set; }
        public IOrderMorale? Morale { get; set; }
        public ScoringSystem Scoring { get; private set; } = null!;
        public BattleStats Stats { get; private set; } = null!;
        private BattleSupportAdapter _support = null!;

        public Dictionary<string, object> Result { get; } = new Dictionary<string, object>();
        public List<Los.Smoke> Smokes { get; } = new List<Los.Smoke>();
        public string PlayerSide { get; set; } = "blue";
        public bool Deploying { get; set; }
        public bool HasResult => Result.Count > 0;

        private int _nextElementId = 1;
        private int _nextUnitId = 1;
        private int _nextOrderId = 1;
        private bool _disposed;

        private Battle(DataCatalog catalog, ScenarioDef scenario, MapDef map, long seed)
        {
            Catalog = catalog;
            Scenario = scenario;
            Map = map;
            Terrain = Terrain.Build(map);
            Pathfinder = new GridPathfinder(Terrain);
            World = new SimWorld(seed);
            Factory = new ForceFactory(this);
            Executor = new OrderExecutor(this);
            Comms = new CommsNet(this);
            Succession = new SuccessionSystem(this);
            Movement = new MovementSystem(this);
        }

        public static Battle Create(DataCatalog catalog, string scenarioId, long seedOverride = -1,
            bool instantOrders = false, MapDef? mapOverride = null)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            ScenarioDef scenario = catalog.GetScenario(scenarioId) ??
                throw new ArgumentException("Scenario not found: " + scenarioId, nameof(scenarioId));
            MapDef map = mapOverride ?? catalog.GetMap(scenario.MapId) ??
                throw new ArgumentException("Map not found: " + scenario.MapId, nameof(mapOverride));
            var battle = new Battle(catalog, scenario, map, seedOverride < 0 ? scenario.SeedValue : seedOverride);
            battle.World.AddSystem(battle.Comms);
            battle.World.AddSystem(battle.Succession);
            if (!instantOrders)
            {
                battle.OrderSystem = new OrderSystem(battle, battle.Comms);
                battle.World.AddSystem(battle.OrderSystem);
            }
            battle.Reinforcements = new ReinforcementSystem(battle);
            battle.World.AddSystem(battle.Reinforcements);
            battle.TfAi = new TaskForceAI(battle);
            battle.World.AddSystem(battle.TfAi);
            battle.PlatoonAi = new PlatoonAI(battle);
            battle.World.AddSystem(battle.PlatoonAi);
            battle.World.AddSystem(battle.Movement);
            battle._support = new BattleSupportAdapter(battle);
            battle.Morale = new MoraleSystem(battle);
            battle.Detection = new DetectionSystem(battle._support);
            battle.World.AddSystem(battle.Detection);
            battle.Combat = new CombatSystem(battle._support, (ICombatMorale)battle.Morale);
            battle._support.Combat = battle.Combat;
            battle.World.AddSystem(battle.Combat);
            battle.Fires = new FireSupportSystem(battle._support);
            battle.World.AddSystem(battle.Fires);
            battle.Supply = new SupplySystem(battle._support);
            battle.World.AddSystem((SimSystem)battle.Supply);
            battle.Engineers = new EngineerSystem(battle._support);
            battle.World.AddSystem((SimSystem)battle.Engineers);
            battle.World.AddSystem((SimSystem)battle.Morale);
            battle.Scoring = new ScoringSystem(battle);
            battle.World.AddSystem(battle.Scoring);
            battle.Stats = new BattleStats(battle);
            return battle;
        }

        public void RegisterElement(Element element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            element.Id = _nextElementId++;
            Elements.Add(element);
            ElementsById[element.Id] = element;
        }

        public void RegisterUnit(Unit unit)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            unit.Id = _nextUnitId++;
            Units.Add(unit);
            UnitsById[unit.Id] = unit;
        }

        public Unit? GetUnit(int unitId)
        {
            return UnitsById.TryGetValue(unitId, out Unit? value) ? value : null;
        }

        public Element? GetElement(int elementId)
        {
            return ElementsById.TryGetValue(elementId, out Element? value) ? value : null;
        }

        public TaskForce? GetTaskForce(string side)
        {
            return TaskForces.TryGetValue(side, out TaskForce? value) ? value : null;
        }

        public Order? GetOrder(int orderId)
        {
            return OrdersById.TryGetValue(orderId, out Order? value) ? value : null;
        }

        public static string EnemyOf(string side)
        {
            return side == "blue" ? "red" : "blue";
        }

        public long Tick()
        {
            return World.Tick;
        }

        public Order IssueOrder(Order order)
        {
            RegisterOrder(order);
            if (OrderSystem != null)
            {
                OrderSystem.Submit(order);
            }
            else
            {
                Unit? unit = GetUnit(order.TargetUnitId);
                if (unit != null)
                {
                    order.State = OrderState.RECEIVED;
                    order.ReceivedTick = World.Tick;
                    Executor.Execute(unit, order);
                }
            }
            return order;
        }

        public Order RegisterOrder(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));
            order.Id = _nextOrderId++;
            order.TickIssued = World.Tick;
            OrdersById[order.Id] = order;
            return order;
        }

        public Order SelfOrder(Unit unit, OrderType type, SimVec2 pos, JObject? parameters = null)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            Order order = Order.Make(type, unit.Id, parameters ?? new JObject());
            order.Issuer = "self";
            order.SetPos(Terrain.ClampToMap(pos, 20.0));
            RegisterOrder(order);
            order.State = OrderState.RECEIVED;
            order.ReceivedTick = World.Tick;
            Executor.Execute(unit, order);
            return order;
        }

        public void BeginBattle()
        {
            Deploying = false;
            World.SetPaused(false);
            World.Events.EmitEvent("battle_started", World.Tick, new Dictionary<string, object>());
        }

        public bool DeployUnit(Unit? unit, SimVec2 pos, double heading)
        {
            if (!Deploying || unit == null || !unit.OnMap || !Scenario.Sides.TryGetValue(unit.Side, out ScenarioDef.SideSetup? setup))
                return false;
            if (unit is Platoon && unit.FollowsParent())
                unit = unit.Parent!;
            SimVec2 destination = Deployment.ClampToZone(pos, setup.DeployZone, this);
            Factory.Redeploy(unit, destination, heading);
            return true;
        }

        public void StepTick()
        {
            World.StepTick();
        }

        public void RunTicks(int count)
        {
            for (int i = 0; i < count; ++i)
                World.StepTick();
        }

        public void RunSeconds(double seconds)
        {
            RunTicks((int)SimMath.Round(seconds * SimConst.TickRate));
        }

        public List<Element> SideElements(string side)
        {
            var output = new List<Element>();
            foreach (Element element in Elements)
            {
                if (element.Side == side)
                    output.Add(element);
            }
            return output;
        }

        public List<Unit> SideUnits(string side)
        {
            var output = new List<Unit>();
            foreach (Unit unit in Units)
            {
                if (unit.Side == side)
                    output.Add(unit);
            }
            return output;
        }

        public List<Element> ElementsNear(SimVec2 pos, double radius)
        {
            var output = new List<Element>();
            double radiusSquared = radius * radius;
            foreach (Element element in Spatial.Query(pos, radius))
            {
                if (element.IsActive() && element.Pos.DistanceSquaredTo(pos) <= radiusSquared)
                    output.Add(element);
            }
            return output;
        }

        IEnumerable<Element> IScoringBattle.ElementsNear(SimVec2 position, double radius)
        {
            return ElementsNear(position, radius);
        }

        public void RebuildSpatial()
        {
            Spatial.Clear();
            foreach (Element element in Elements)
            {
                if (element.IsActive())
                    Spatial.Insert(element, element.Pos);
            }
        }

        public bool IsPassable(SimVec2 point)
        {
            return Terrain.IsPassable(point, Terrain.MobilityVehicle);
        }

        public SimVec2 NearestPassable(SimVec2 point)
        {
            return Terrain.NearestPassable(point, Terrain.MobilityVehicle);
        }

        public void SetMounted(Platoon platoon, bool mounted)
        {
            Executor.SetMounted(platoon, mounted);
        }

        public double AmmoRatio(Unit unit)
        {
            return SupplySystem.AmmoRatio(unit);
        }

        public bool HasActiveMission(string side, int asset)
        {
            foreach (FireSupportSystem.Mission mission in Fires.ActiveMissions(side))
            {
                if ((int)mission.Asset == asset)
                    return true;
            }
            return false;
        }

        public IReadOnlyDictionary<string, object> Request(string side, int asset, SimVec2 target)
        {
            return Fires.Request(side, (FireSupportSystem.Asset)asset, target);
        }

        public string GetIndirectWeapon(Platoon platoon)
        {
            return Fires.IndirectWeapon(platoon);
        }

        public void SetResult(Dictionary<string, object> result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            Result.Clear();
            foreach (KeyValuePair<string, object> pair in VictoryData.CopyDictionary(result))
                Result[pair.Key] = pair.Value;
            World.SetPaused(true);
            World.Events.EmitEvent("battle_ended", World.Tick, VictoryData.CopyDictionary(Result));
        }

        void IReinforcementBattle.PlaceElements(Platoon platoon)
        {
            Factory.PlaceElements(platoon);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (Unit unit in Units)
            {
                unit.Parent = null;
                unit.Commander = null;
                unit.Mission = null;
                unit.QueuedOrders.Clear();
                unit.Guide.Pending = null;
                if (unit is Platoon platoon)
                {
                    platoon.Elements.Clear();
                }
                else if (unit is Company company)
                {
                    company.Platoons.Clear();
                    company.Hq = null;
                    company.Xo = null;
                }
                else if (unit is TaskForce taskForce)
                {
                    taskForce.Companies.Clear();
                    taskForce.Organic.Clear();
                    taskForce.CpSection = null;
                }
            }
            foreach (Element element in Elements)
            {
                element.Platoon = null;
                element.MountedIn = null;
                element.Passengers.Clear();
                element.PathRequest = null;
            }
            Units.Clear();
            UnitsById.Clear();
            Elements.Clear();
            ElementsById.Clear();
            TaskForces.Clear();
            OrdersById.Clear();
            Spatial.Clear();
            World.Events.ClearListeners();
            World = new SimWorld(0);
            Smokes.Clear();
        }
    }
}
