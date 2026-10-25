using System.Collections.Generic;

namespace Echelon.Core
{
    public interface ISupplyBattle
    {
        SimWorld World { get; }
        IReadOnlyList<Element> Elements { get; }

        TaskForce? GetTaskForce(string side);
        Unit? GetUnit(int unitId);
        IEnumerable<Element> ElementsNear(SimVec2 position, double radius);
        void SelfOrder(Unit unit, OrderType type, SimVec2 position);
    }
}
