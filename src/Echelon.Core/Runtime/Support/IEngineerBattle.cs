using System.Collections.Generic;

namespace Echelon.Core
{
    public interface IEngineerBattle
    {
        SimWorld World { get; }

        TaskForce? GetTaskForce(string side);
        IEnumerable<Element> ElementsNear(SimVec2 position, double radius);
        void AddThreat(string side, SimVec2 position, double radius, double amount);
        void Destroy(Element element, Element? killer, long tick);
        void OnCasualties(Platoon platoon, int men);
        void OnElementDamaged(Element element);
    }
}
