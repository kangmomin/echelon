using System;
using System.Collections.Generic;
using Echelon.Core;

namespace Echelon.Core.Tests
{
    internal sealed class FiresTestContext : IFireSupportBattle
    {
        private static readonly Lazy<Terrain> FlatTerrain = new Lazy<Terrain>(() =>
            Terrain.Build(FiresSupportTestFixture.FlatMap()));
        private readonly List<Element> _elements = new List<Element>();
        private readonly Dictionary<int, Element> _elementsById = new Dictionary<int, Element>();
        private readonly Dictionary<int, Unit> _units = new Dictionary<int, Unit>();
        private readonly Dictionary<string, TaskForce> _taskForces = new Dictionary<string, TaskForce>();
        private int _nextElementId = 1;
        private int _nextUnitId = 1;

        internal FiresTestContext(long seed = 1)
        {
            World = new SimWorld(seed);
        }

        public SimWorld World { get; }
        public Terrain Terrain => FlatTerrain.Value;
        public DataCatalog Catalog => FiresSupportTestFixture.Catalog;
        public IReadOnlyList<Element> Elements => _elements;
        public IList<Los.Smoke> Smokes { get; } = new List<Los.Smoke>();
        internal bool CommandAvailable { get; set; } = true;
        internal bool CommsAvailable { get; set; } = true;
        internal double CommsQuality { get; set; } = 1.0;
        internal int CommsRelays { get; set; }
        internal int SpendCalls { get; private set; }
        internal List<ImpactRecord> Impacts { get; } = new List<ImpactRecord>();

        public TaskForce? GetTaskForce(string side)
        {
            return _taskForces.TryGetValue(side, out TaskForce? taskForce) ? taskForce : null;
        }

        public Unit? GetUnit(int unitId)
        {
            return _units.TryGetValue(unitId, out Unit? unit) ? unit : null;
        }

        public Element? GetElement(int elementId)
        {
            return _elementsById.TryGetValue(elementId, out Element? element) ? element : null;
        }

        public CommsLink LinkBetween(Unit fromUnit, Unit toUnit)
        {
            return new CommsLink { Ok = CommsAvailable, Quality = CommsQuality, Relays = CommsRelays };
        }

        public bool TrySpendCommand(string side, double amount)
        {
            ++SpendCalls;
            return CommandAvailable;
        }

        public void IndirectImpact(SimVec2 position, WeaponDef weapon, string firingSide, Element? source, long tick)
        {
            Impacts.Add(new ImpactRecord(position, weapon, firingSide, source, tick));
        }

        internal TaskForce AddTaskForce(string side = "blue", string toeId = "us_armor_bn_tf")
        {
            TaskForce taskForce = new TaskForce
            {
                Id = _nextUnitId++,
                Side = side,
                Toe = Catalog.GetToe(toeId),
                Commander = new Element
                {
                    Id = _nextElementId++,
                    Side = side,
                    Def = new ElementDef { ElementClass = "cp", Hp = 100.0 }
                }
            };
            _taskForces[side] = taskForce;
            _units[taskForce.Id] = taskForce;
            return taskForce;
        }

        internal Platoon AddPlatoon(TaskForce? parent, string elementId, string kind, SimVec2 position, int count = 1)
        {
            Platoon platoon = new Platoon
            {
                Id = _nextUnitId++,
                Side = parent?.Side ?? "red",
                Parent = parent,
                Kind = kind
            };
            platoon.Guide.Reset(position, 0.0);
            ElementDef definition = Catalog.GetElement(elementId)!;
            for (int i = 0; i < count; ++i)
            {
                Element element = new Element
                {
                    Id = _nextElementId++,
                    Side = platoon.Side,
                    Def = definition,
                    Platoon = platoon,
                    IndexInPlatoon = i,
                    Pos = position,
                    PrevPos = position,
                    Hp = definition.Hp,
                    SupplyLeft = definition.SupplyCapacity
                };
                foreach (KeyValuePair<string, int> pair in definition.Ammo)
                {
                    element.Ammo[pair.Key] = pair.Value;
                }

                platoon.Elements.Add(element);
                _elements.Add(element);
                _elementsById[element.Id] = element;
            }

            platoon.Commander = platoon.Elements[0];
            platoon.InitialElementCount = count;
            parent?.Organic.Add(platoon);
            _units[platoon.Id] = platoon;
            return platoon;
        }

        internal readonly struct ImpactRecord
        {
            internal ImpactRecord(SimVec2 position, WeaponDef weapon, string firingSide, Element? source, long tick)
            {
                Position = position;
                Weapon = weapon;
                FiringSide = firingSide;
                Source = source;
                Tick = tick;
            }

            internal SimVec2 Position { get; }
            internal WeaponDef Weapon { get; }
            internal string FiringSide { get; }
            internal Element? Source { get; }
            internal long Tick { get; }
        }
    }
}
