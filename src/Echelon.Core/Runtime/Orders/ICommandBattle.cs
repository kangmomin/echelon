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
        IOrderAir? Air { get; }

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

    public interface IOrderAir
    {
        bool Launch(Platoon flight, SimVec2 toward);
        void ReturnToBase(Platoon flight, string reason);
        bool IsReturning(Platoon flight);
    }

    public interface IOrderMorale
    {
        void OnCommanderLost(Unit unit);
    }
}
