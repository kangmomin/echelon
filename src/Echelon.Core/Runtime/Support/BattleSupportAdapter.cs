using System;
using System.Collections.Generic;

namespace Echelon.Core
{
    public sealed class BattleSupportAdapter : IFireSupportBattle, ISupplyBattle, IEngineerBattle, ICombatBattle
    {
        private readonly Battle _battle;

        public BattleSupportAdapter(Battle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public CombatSystem? Combat { get; set; }
        public SimWorld World => _battle.World;
        public Terrain Terrain => _battle.Terrain;
        public DataCatalog Catalog => _battle.Catalog;
        public GridPathfinder Pathfinder => _battle.Pathfinder;
        public SpatialHash<Element> Spatial => _battle.Spatial;
        public CommsNet Comms => _battle.Comms;
        public IReadOnlyList<Unit> Units => _battle.Units;
        public IReadOnlyList<Element> Elements => _battle.Elements;
        public IReadOnlyDictionary<int, Order> OrdersById => _battle.OrdersById;
        public OrderExecutor Executor => _battle.Executor;
        public OrderSystem? OrderSystem => _battle.OrderSystem;
        public IOrderSupply Supply => _battle.Supply;
        public IOrderEngineers? Engineers => _battle.Engineers;
        public IOrderMorale? Morale => _battle.Morale;
        public List<Los.Smoke> Smokes => _battle.Smokes;

        IReadOnlyList<Element> IFireSupportBattle.Elements => _battle.Elements;
        IReadOnlyList<Element> ISupplyBattle.Elements => _battle.Elements;
        IList<Los.Smoke> IFireSupportBattle.Smokes => _battle.Smokes;

        public Unit? GetUnit(int unitId)
        {
            return _battle.GetUnit(unitId);
        }

        public Element? GetElement(int elementId)
        {
            return _battle.GetElement(elementId);
        }

        public TaskForce? GetTaskForce(string side)
        {
            return _battle.GetTaskForce(side);
        }

        public Order RegisterOrder(Order order)
        {
            return _battle.RegisterOrder(order);
        }

        public List<Element> ElementsNear(SimVec2 position, double radius)
        {
            return _battle.ElementsNear(position, radius);
        }

        IEnumerable<Element> ISupplyBattle.ElementsNear(SimVec2 position, double radius)
        {
            return _battle.ElementsNear(position, radius);
        }

        IEnumerable<Element> IEngineerBattle.ElementsNear(SimVec2 position, double radius)
        {
            return _battle.ElementsNear(position, radius);
        }

        public CommsLink LinkBetween(Unit fromUnit, Unit toUnit)
        {
            return _battle.Comms.LinkBetween(fromUnit, toUnit);
        }

        public bool TrySpendCommand(string side, double amount)
        {
            return _battle.OrderSystem == null || _battle.OrderSystem.TrySpend(side, amount);
        }

        public void SelfOrder(Unit unit, OrderType type, SimVec2 position)
        {
            _battle.SelfOrder(unit, type, position);
        }

        public void AddThreat(string side, SimVec2 position, double radius, double amount)
        {
            _battle.Pathfinder.AddThreat(side, position, radius, amount);
        }

        public void IndirectImpact(SimVec2 position, WeaponDef weapon, string firingSide, Element? source, long tick)
        {
            RequireCombat().IndirectImpact(position, weapon, firingSide, source, tick);
        }

        public void Destroy(Element element, Element? killer, long tick)
        {
            RequireCombat().Destroy(element, killer, tick);
        }

        public void OnCasualties(Platoon platoon, int men)
        {
            RequireMorale().OnCasualties(platoon, men);
        }

        public void OnElementDamaged(Element element)
        {
            RequireMorale().OnElementDamaged(element);
        }

        private CombatSystem RequireCombat()
        {
            return Combat ?? throw new InvalidOperationException("Assign BattleSupportAdapter.Combat before using support damage.");
        }

        private ICombatMorale RequireMorale()
        {
            return _battle.Morale as ICombatMorale ??
                throw new InvalidOperationException("Battle.Morale must implement ICombatMorale for engineer damage.");
        }
    }
}
