using UnityEngine;

namespace Echelon.Unity.Presentation.Symbols
{
    public static class SymbolStyle
    {
        public const float FrameWidth = 30f;
        public const float FrameHeight = 20f;
        public const float PickRadiusPixels = 18f;
        public const float StaleAlpha = 0.45f;
        public const float MinimapReportAlpha = 0.5f;

        public static readonly Color FriendlyFill = new Color(0.50f, 0.72f, 1.0f, 0.92f);
        public static readonly Color HostileFill = new Color(1.0f, 0.50f, 0.45f, 0.92f);
        public static readonly Color FrameColor = new Color(0.05f, 0.05f, 0.08f, 1f);
        public static readonly Color SelectColor = new Color(1.0f, 0.93f, 0.35f, 1f);
        public static readonly Color PathColor = new Color(1.0f, 1.0f, 1.0f, 0.75f);
        public static readonly Color PropagatingColor = new Color(1.0f, 0.8f, 0.3f, 0.9f);
        public static readonly Color DefendColor = new Color(0.6f, 1.0f, 0.6f, 0.8f);
        public static readonly Color NeutralColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        public static readonly Color DeadElementColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        public static readonly Color TextColor = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color PreviewColor = new Color(1.0f, 0.92f, 0.6f, 1f);

        public static bool IsHostile(string side, string playerSide)
        {
            return side != playerSide;
        }

        public static Color SideColor(string side, string playerSide)
        {
            return IsHostile(side, playerSide) ? HostileFill : FriendlyFill;
        }

        public static Color OwnerColor(string ownerSide, string playerSide)
        {
            return string.IsNullOrEmpty(ownerSide) ? NeutralColor : SideColor(ownerSide, playerSide);
        }

        public static Color ApplyContact(Color color, SymbolContactState contact, bool minimap = false)
        {
            switch (contact)
            {
                case SymbolContactState.Hidden:
                    color.a = 0f;
                    break;
                case SymbolContactState.Reported:
                    if (minimap)
                        color.a = MinimapReportAlpha;
                    break;
                case SymbolContactState.Stale:
                    color.a = minimap ? MinimapReportAlpha : StaleAlpha;
                    break;
            }
            return color;
        }

        public static Color StrengthColor(float strength)
        {
            float ratio = Mathf.Clamp01(strength);
            return new Color(1f - ratio, 0.4f + 0.5f * ratio, 0.2f, 1f);
        }

        public static Color CommsColor(SymbolCommsState state)
        {
            switch (state)
            {
                case SymbolCommsState.Good:
                    return new Color(0.4f, 1.0f, 0.4f, 1f);
                case SymbolCommsState.Poor:
                    return new Color(1.0f, 0.8f, 0.2f, 1f);
                default:
                    return new Color(1.0f, 0.3f, 0.3f, 1f);
            }
        }

        public static int CommsBars(SymbolCommsState state)
        {
            return 3 - Mathf.Clamp((int)state, 0, 3);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
