using System.Collections.Generic;

namespace Echelon.Unity.Presentation.Symbols
{
    public static class SymbolAggregation
    {
        public const float PlatoonLevelHeight = 450f;
        public const float CompanyLevelHeight = 2200f;

        public static SymbolAggregationLevel LevelForCameraHeight(float heightAboveGround)
        {
            if (heightAboveGround < PlatoonLevelHeight)
                return SymbolAggregationLevel.Element;
            if (heightAboveGround < CompanyLevelHeight)
                return SymbolAggregationLevel.Platoon;
            return SymbolAggregationLevel.Company;
        }

        public static bool IsUnitEligible(SymbolUnitSnapshot unit)
        {
            return unit != null && unit.OnMap && !unit.IsDestroyed && unit.Level != SymbolUnitLevel.TaskForce;
        }

        public static bool IsContactVisible(string side, SymbolContactState contact, string playerSide, bool fogEnabled)
        {
            return !fogEnabled || !SymbolStyle.IsHostile(side, playerSide) || contact != SymbolContactState.Hidden;
        }

        public static bool ShouldDrawUnitFrame(
            SymbolUnitSnapshot unit,
            SymbolAggregationLevel aggregationLevel,
            string playerSide,
            bool fogEnabled)
        {
            if (!IsUnitEligible(unit) || !IsContactVisible(unit.Side, unit.Contact, playerSide, fogEnabled))
                return false;
            if (aggregationLevel == SymbolAggregationLevel.Platoon)
                return unit.Level == SymbolUnitLevel.Platoon;
            if (aggregationLevel != SymbolAggregationLevel.Company)
                return false;
            return unit.Level == SymbolUnitLevel.Company
                || (unit.Level == SymbolUnitLevel.Platoon && !unit.FollowsParent);
        }

        public static bool ShouldDrawElements(
            SymbolUnitSnapshot unit,
            SymbolAggregationLevel aggregationLevel,
            string playerSide,
            bool fogEnabled)
        {
            if (aggregationLevel != SymbolAggregationLevel.Element || !IsUnitEligible(unit))
                return false;
            if (unit.Level != SymbolUnitLevel.Platoon)
                return false;
            return unit.Elements != null && unit.Elements.Count > 0;
        }

        public static bool ShouldDrawElement(
            SymbolUnitSnapshot unit,
            SymbolElementSnapshot element,
            string playerSide,
            bool fogEnabled)
        {
            return element != null && element.OnMap && !element.IsMounted
                && IsContactVisible(unit.Side, element.Contact, playerSide, fogEnabled);
        }

        public static bool IsSelected(SymbolUnitSnapshot unit, IReadOnlyList<string> selectedUnitIds)
        {
            if (unit == null || selectedUnitIds == null)
                return false;
            for (int i = 0; i < selectedUnitIds.Count; i++)
            {
                string selected = selectedUnitIds[i];
                if (selected == unit.Id || (!string.IsNullOrEmpty(unit.ParentId) && selected == unit.ParentId))
                    return true;
            }
            return false;
        }

        public static bool IsDirectlySelected(SymbolUnitSnapshot unit, IReadOnlyList<string> selectedUnitIds)
        {
            if (unit == null || selectedUnitIds == null)
                return false;
            for (int i = 0; i < selectedUnitIds.Count; i++)
            {
                if (selectedUnitIds[i] == unit.Id)
                    return true;
            }
            return false;
        }
    }
}
