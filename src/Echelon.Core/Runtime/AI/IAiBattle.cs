using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Echelon.Core
{
    public static class FireSupportAsset
    {
        public const int Mortar = (int)FireSupportSystem.Asset.MORTAR;
        public const int Artillery = (int)FireSupportSystem.Asset.ARTILLERY;
        public const int Cas = (int)FireSupportSystem.Asset.CAS;
    }

    public interface IAiFireSupport
    {
        bool HasActiveMission(string side, int asset);
        IReadOnlyDictionary<string, object> Request(string side, int asset, SimVec2 target);
        string GetIndirectWeapon(Platoon platoon);
    }

    public interface IAiBattle
    {
        SimWorld World { get; }
        Terrain Terrain { get; }
        DataCatalog Catalog { get; }
        ScenarioDef Scenario { get; }
        ScoringSystem Scoring { get; }
        IAiFireSupport Fires { get; }
        bool Deploying { get; }
        bool HasResult { get; }

        TaskForce? GetTaskForce(string side);
        Element? GetElement(int elementId);
        Order IssueOrder(Order order);
        Order SelfOrder(Unit unit, OrderType type, SimVec2 position, JObject? parameters = null);
        void SetMounted(Platoon platoon, bool mounted);
        double AmmoRatio(Unit unit);
    }
}
