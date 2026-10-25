using UnityEngine;

namespace Echelon.Unity.UI.Common
{
    public static class UiTheme
    {
        public static Color Background { get; } = new Color(0.09f, 0.10f, 0.12f);
        public static Color Panel { get; } = new Color(0.06f, 0.07f, 0.09f, 0.95f);
        public static Color Accent { get; } = new Color(0.95f, 0.80f, 0.35f);
        public static Color Text { get; } = new Color(0.93f, 0.93f, 0.95f);
        public static Color TextDim { get; } = new Color(0.65f, 0.67f, 0.72f);
        public static Color Ok { get; } = new Color(0.45f, 1.0f, 0.50f);
        public static Color Warning { get; } = new Color(1.0f, 0.80f, 0.30f);
        public static Color Error { get; } = new Color(1.0f, 0.42f, 0.38f);

        public static Color ButtonNormal { get; } = new Color(0.13f, 0.15f, 0.19f);
        public static Color ButtonHighlighted { get; } = new Color(0.22f, 0.24f, 0.29f);
        public static Color ButtonPressed { get; } = new Color(0.30f, 0.27f, 0.17f);
        public static Color ButtonSelected { get; } = new Color(0.95f, 0.80f, 0.35f, 0.24f);
        public static Color Separator { get; } = new Color(0.65f, 0.67f, 0.72f, 0.35f);
    }
}
