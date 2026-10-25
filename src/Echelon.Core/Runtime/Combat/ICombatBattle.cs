using System.Collections.Generic;

namespace Echelon.Core
{
    public interface ICombatBattle : ICommandBattle
    {
        SpatialHash<Element> Spatial { get; }
        List<Los.Smoke> Smokes { get; }
        CommsNet Comms { get; }

        Element? GetElement(int elementId);
        List<Element> ElementsNear(SimVec2 position, double radiusM);
    }

    public interface ICombatMorale : IOrderMorale
    {
        void OnElementLost(Element element);
        void OnElementDamaged(Element element);
        void OnCasualties(Platoon? platoon, int men);
    }

    public interface ICombatEngineers : IOrderEngineers
    {
        double PositionFactor(Element element);
    }
}
