using Echelon.Unity.Presentation.Symbols;
using UnityEngine;

namespace Echelon.Unity.UI.Minimap
{
    public static class MinimapPresentation
    {
        public static bool ShouldDrawContact(MinimapContactSnapshot contact, string playerSide, bool fogEnabled)
        {
            return contact != null && contact.OnMap && contact.IsAlive && !contact.IsMounted
                && SymbolAggregation.IsContactVisible(contact.Side, contact.Contact, playerSide, fogEnabled);
        }

        public static Color ContactColor(MinimapContactSnapshot contact, string playerSide)
        {
            return SymbolStyle.ApplyContact(SymbolStyle.SideColor(contact.Side, playerSide), contact.Contact, true);
        }

        public static Color ControlPointColor(string ownerSide, string playerSide)
        {
            return string.IsNullOrEmpty(ownerSide)
                ? new Color(0.9f, 0.9f, 0.9f, 1f)
                : SymbolStyle.SideColor(ownerSide, playerSide);
        }
    }

    public static class MinimapTerrainPalette
    {
        public static Color BaseColor(MinimapTerrainType type)
        {
            switch (type)
            {
                case MinimapTerrainType.Open: return new Color(0.43f, 0.50f, 0.31f, 1f);
                case MinimapTerrainType.Road: return new Color(0.36f, 0.36f, 0.36f, 1f);
                case MinimapTerrainType.Forest: return new Color(0.20f, 0.30f, 0.16f, 1f);
                case MinimapTerrainType.Urban: return new Color(0.52f, 0.50f, 0.47f, 1f);
                case MinimapTerrainType.Water: return new Color(0.14f, 0.27f, 0.33f, 1f);
                default: return new Color(0.43f, 0.50f, 0.31f, 1f);
            }
        }

        public static Color Shade(MinimapTerrainSample sample)
        {
            Color color = BaseColor(sample.Type);
            if (sample.Height >= 40f)
            {
                float amount = Mathf.Clamp((sample.Height - 40f) / 120f, -0.3f, 0.3f);
                color.r += (1f - color.r) * amount;
                color.g += (1f - color.g) * amount;
                color.b += (1f - color.b) * amount;
            }
            else
            {
                float amount = Mathf.Clamp((40f - sample.Height) / 120f, 0f, 0.3f);
                color.r *= 1f - amount;
                color.g *= 1f - amount;
                color.b *= 1f - amount;
            }
            return color;
        }
    }
}
