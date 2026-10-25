using System.Collections.Generic;

namespace Echelon.Core
{
    public interface IFireSupportBattle
    {
        SimWorld World { get; }
        Terrain Terrain { get; }
        DataCatalog Catalog { get; }
        IReadOnlyList<Element> Elements { get; }
        IList<Los.Smoke> Smokes { get; }

        TaskForce? GetTaskForce(string side);
        Unit? GetUnit(int unitId);
        Element? GetElement(int elementId);
        CommsLink LinkBetween(Unit fromUnit, Unit toUnit);
        bool TrySpendCommand(string side, double amount);
        void IndirectImpact(SimVec2 position, WeaponDef weapon, string firingSide, Element? source, long tick);
    }
}
