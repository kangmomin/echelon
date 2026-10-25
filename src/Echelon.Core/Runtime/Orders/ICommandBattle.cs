using System.Collections.Generic;

namespace Echelon.Core
{
    public interface ICommandBattle
    {
        SimWorld World { get; }
        Terrain Terrain { get; }
        DataCatalog Catalog { get; }
        GridPathfinder Pathfinder { get; }
        IReadOnlyList<Unit> Units { get; }
        IReadOnlyList<Element> Elements { get; }
        IReadOnlyDictionary<int, Order> OrdersById { get; }
        OrderExecutor Executor { get; }
        OrderSystem? OrderSystem { get; }
        IOrderSupply Supply { get; }
        IOrderEngineers? Engineers { get; }
        IOrderMorale? Morale { get; }

        Unit? GetUnit(int unitId);
        TaskForce? GetTaskForce(string side);
        Order RegisterOrder(Order order);
    }

    public interface IOrderSupply
    {
        void Request(Platoon platoon);
    }

    public interface IOrderEngineers
    {
        bool BeginTask(Platoon platoon, Order order);
    }

    public interface IOrderMorale
    {
        void OnCommanderLost(Unit unit);
    }
}
