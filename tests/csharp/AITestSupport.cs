using System;
using System.Collections.Generic;
using Echelon.Core;
using Newtonsoft.Json.Linq;

namespace Echelon.Core.Tests
{
    internal readonly struct AIFireRequest
    {
        internal AIFireRequest(string side, int asset, SimVec2 target)
        {
            Side = side;
            Asset = asset;
            Target = target;
        }

        internal string Side { get; }
        internal int Asset { get; }
        internal SimVec2 Target { get; }
    }

    internal sealed class AIPortTestBattle : IAiBattle, IAiFireSupport, IReinforcementBattle, IScoringBattle, IBattleStatsBattle
    {
        private int _nextId = 1;
        private int _nextOrderId = 1;

        internal AIPortTestBattle()
        {
            World = new SimWorld(1);
            Terrain = Terrain.Build(new MapDef
            {
                SizeM = new SimVec2(4000, 4000),
                CellM = 100,
                BaseM = 0,
                AmplitudeM = 0,
                Frequency = 0.001,
                Octaves = 1
            });
            Scenario = new ScenarioDef
            {
                SizeM = Terrain.SizeM,
                TimeLimitMin = 60,
                CollapseThreshold = 0.25
            };
            Scenario.Sides["blue"] = new ScenarioDef.SideSetup
            {
                DeployZone = new MapRect(0, 0, 4000, 500)
            };
            Scenario.Sides["red"] = new ScenarioDef.SideSetup
            {
                DeployZone = new MapRect(0, 3500, 4000, 500)
            };
            Catalog = new DataCatalog();
        }

        public SimWorld World { get; }
        public Terrain Terrain { get; }
        public DataCatalog Catalog { get; }
        public ScenarioDef Scenario { get; }
        public IAiFireSupport Fires => this;
        public bool Deploying { get; set; }
        public bool HasResult => Result.Count > 0;
        public ScoringSystem? Scoring { get; set; }
        public Dictionary<string, object> Result { get; private set; } = new Dictionary<string, object>();
        public Dictionary<string, TaskForce> TaskForces { get; } = new Dictionary<string, TaskForce>();
        public Dictionary<int, Unit> Units { get; } = new Dictionary<int, Unit>();
        public Dictionary<int, Element> Elements { get; } = new Dictionary<int, Element>();
        public List<Order> IssuedOrders { get; } = new List<Order>();
        public List<Order> SelfOrders { get; } = new List<Order>();
        public List<AIFireRequest> FireRequests { get; } = new List<AIFireRequest>();
        public List<Platoon> PlacedPlatoons { get; } = new List<Platoon>();
        public List<Platoon> Dismounted { get; } = new List<Platoon>();
        public HashSet<string> ActiveFireMissions { get; } = new HashSet<string>();
        public Dictionary<int, bool> FireRequestSuccess { get; } = new Dictionary<int, bool>();
        public Dictionary<int, string> IndirectWeapons { get; } = new Dictionary<int, string>();
        public Func<Unit, double>? AmmoRatioOverride { get; set; }

        ScoringSystem IAiBattle.Scoring => Scoring ?? throw new InvalidOperationException("Scoring is not initialized.");
        ScoringSystem? IBattleStatsBattle.Scoring => Scoring;
        IReadOnlyDictionary<string, object> IBattleStatsBattle.Result => Result;

        public TaskForce? GetTaskForce(string side)
        {
            return TaskForces.TryGetValue(side, out TaskForce? value) ? value : null;
        }

        public Unit? GetUnit(int unitId)
        {
            return Units.TryGetValue(unitId, out Unit? value) ? value : null;
        }

        public Element? GetElement(int elementId)
        {
            return Elements.TryGetValue(elementId, out Element? value) ? value : null;
        }

        public IEnumerable<Element> ElementsNear(SimVec2 position, double radius)
        {
            List<Element> output = new List<Element>();
            double radiusSquared = radius * radius;
            foreach (Element element in Elements.Values)
            {
                if (element.IsActive() && element.Pos.DistanceSquaredTo(position) <= radiusSquared)
                {
                    output.Add(element);
                }
            }

            return output;
        }

        public Order IssueOrder(Order order)
        {
            order.Id = _nextOrderId++;
            order.TickIssued = World.Tick;
            IssuedOrders.Add(order);
            return order;
        }

        public Order SelfOrder(Unit unit, OrderType type, SimVec2 position, JObject? parameters = null)
        {
            Order order = Order.Make(type, unit.Id, parameters);
            order.Id = _nextOrderId++;
            order.TickIssued = World.Tick;
            order.Issuer = "self";
            order.SetPos(Terrain.ClampToMap(position, 20));
            SelfOrders.Add(order);
            return order;
        }

        public void SetMounted(Platoon platoon, bool mounted)
        {
            platoon.Mounted = mounted;
            if (!mounted)
            {
                Dismounted.Add(platoon);
                foreach (Element element in platoon.Elements)
                {
                    if (element.IsInfantry())
                    {
                        element.MountedIn = null;
                    }
                }
            }
        }

        public double AmmoRatio(Unit unit)
        {
            return AmmoRatioOverride != null ? AmmoRatioOverride(unit) : SupplySystem.AmmoRatio(unit);
        }

        public bool HasActiveMission(string side, int asset)
        {
            return ActiveFireMissions.Contains(FireKey(side, asset));
        }

        public IReadOnlyDictionary<string, object> Request(string side, int asset, SimVec2 target)
        {
            FireRequests.Add(new AIFireRequest(side, asset, target));
            bool ok = !FireRequestSuccess.TryGetValue(asset, out bool configured) || configured;
            return new Dictionary<string, object> { ["ok"] = ok };
        }

        public string GetIndirectWeapon(Platoon platoon)
        {
            if (IndirectWeapons.TryGetValue(platoon.Id, out string? weaponId))
            {
                return weaponId;
            }

            foreach (Element element in platoon.Elements)
            {
                foreach (string id in element.Def.Weapons)
                {
                    WeaponDef? weapon = Catalog.GetWeapon(id);
                    if (weapon != null && weapon.Indirect)
                    {
                        return id;
                    }
                }
            }

            return "";
        }

        public void PlaceElements(Platoon platoon)
        {
            PlacedPlatoons.Add(platoon);
            foreach (Element element in platoon.Elements)
            {
                element.Pos = platoon.Guide.Pos;
                element.PrevPos = element.Pos;
                element.Heading = platoon.Guide.Heading;
            }
        }

        public void SetResult(Dictionary<string, object> result)
        {
            Result = result;
            World.SetPaused(true);
            World.Events.EmitEvent("battle_ended", World.Tick, new Dictionary<string, object>(result));
        }

        internal TaskForce NewTaskForce(string side)
        {
            TaskForce taskForce = new TaskForce
            {
                Id = NextId(),
                Side = side,
                DisplayName = side.ToUpperInvariant(),
                ShortName = "TF",
                EntryPos = side == "blue" ? new SimVec2(2000, 0) : new SimVec2(2000, 4000)
            };
            taskForce.Guide.Reset(taskForce.EntryPos, 0);
            Platoon cp = NewPlatoon(taskForce, "cp", taskForce.EntryPos, "cp", 1);
            cp.IsHqSection = true;
            taskForce.CpSection = cp;
            taskForce.Commander = cp.Elements[0];
            TaskForces[side] = taskForce;
            RegisterTree(taskForce);
            return taskForce;
        }

        internal Company NewCompany(TaskForce taskForce, SimVec2 position, int elementCount = 4, bool includeHq = false)
        {
            Company company = new Company
            {
                Id = NextId(),
                Side = taskForce.Side,
                Parent = taskForce,
                DisplayName = "Company " + taskForce.Companies.Count,
                ShortName = "C" + taskForce.Companies.Count
            };
            company.Guide.Reset(position, 0);
            if (includeHq)
            {
                company.Hq = NewPlatoon(company, "hq", position, "cp", 1);
                company.Hq.IsHqSection = true;
                company.Commander = company.Hq.Elements[0];
            }

            Platoon platoon = NewPlatoon(company, "tank", position, "tank", elementCount);
            company.Platoons.Add(platoon);
            company.InitialElementCount = company.AllElements().Count;
            taskForce.Companies.Add(company);
            RegisterTree(taskForce);
            return company;
        }

        internal Platoon NewOrganic(TaskForce taskForce, string kind, SimVec2 position, string elementClass = "recon", int count = 1)
        {
            Platoon platoon = NewPlatoon(taskForce, kind, position, elementClass, count);
            taskForce.Organic.Add(platoon);
            RegisterTree(taskForce);
            return platoon;
        }

        internal Platoon NewPlatoon(Unit parent, string kind, SimVec2 position, string elementClass, int count)
        {
            Platoon platoon = new Platoon
            {
                Id = NextId(),
                Side = parent.Side,
                Parent = parent,
                Kind = kind,
                DisplayName = kind + " " + parent.Id,
                ShortName = kind,
                Mounted = false
            };
            platoon.Guide.Reset(position, 0);
            for (int i = 0; i < count; ++i)
            {
                AddElement(platoon, elementClass);
            }

            platoon.Commander = platoon.Elements.Count > 0 ? platoon.Elements[0] : null;
            platoon.InitialElementCount = platoon.Elements.Count;
            return platoon;
        }

        internal Element AddElement(Platoon platoon, string elementClass, int cost = 100)
        {
            ElementDef definition = new ElementDef
            {
                Id = elementClass,
                DisplayName = elementClass,
                ElementClass = elementClass,
                Hp = 100,
                Cost = cost,
                SupplyFull = 100,
                SpeedOffroadKmh = 30,
                SpeedRoadKmh = 40,
                TransportCapacity = elementClass == "ifv" || elementClass == "apc" ? 3 : 0
            };
            definition.Weapons.Add("gun");
            definition.Ammo["gun"] = 10;
            Element element = new Element
            {
                Id = NextId(),
                Def = definition,
                Side = platoon.Side,
                Platoon = platoon,
                IndexInPlatoon = platoon.Elements.Count,
                Pos = platoon.Guide.Pos,
                PrevPos = platoon.Guide.Pos,
                Hp = 100
            };
            element.Ammo["gun"] = 10;
            platoon.Elements.Add(element);
            platoon.InitialElementCount = platoon.Elements.Count;
            Elements[element.Id] = element;
            return element;
        }

        internal void RegisterTree(TaskForce taskForce)
        {
            foreach (Unit unit in taskForce.AllUnits())
            {
                Units[unit.Id] = unit;
            }

            foreach (Element element in taskForce.AllElements())
            {
                Elements[element.Id] = element;
            }
        }

        internal void AdvanceTo(long tick)
        {
            while (World.Tick < tick)
            {
                World.StepTick();
            }
        }

        private int NextId()
        {
            return _nextId++;
        }

        private static string FireKey(string side, int asset)
        {
            return side + ":" + asset;
        }
    }
}
