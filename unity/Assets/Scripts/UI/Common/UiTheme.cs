using UnityEngine;

namespace Echelon.Unity.UI.Common
{
    /// <summary>
    /// 디자인 토큰. 전술 지휘 화면 톤: 어두운 청회색 표면 위에 호박색 강조, 진영색은 NATO 관례(청/적).
    /// 화면 코드는 색·크기·간격을 직접 쓰지 않고 여기 값을 쓴다.
    /// </summary>
    public static class UiTheme
    {
        // 표면
        public static Color Background { get; } = Hex(0x0E1215);
        public static Color Panel { get; } = Hex(0x151B20, 0.96f);
        public static Color PanelRaised { get; } = Hex(0x1C242B);
        public static Color PanelSunken { get; } = Hex(0x0F1418, 0.9f);
        public static Color Scrim { get; } = new Color(0.02f, 0.03f, 0.04f, 0.66f);
        public static Color Border { get; } = Hex(0x2A3640);
        public static Color BorderStrong { get; } = Hex(0x3C4B57);
        public static Color Separator { get; } = Hex(0x2A3640);

        // 글자
        public static Color Text { get; } = Hex(0xE4E9EC);
        public static Color TextDim { get; } = Hex(0x97A3AC);
        public static Color TextMuted { get; } = Hex(0x66737D);
        public static Color TextOnAccent { get; } = Hex(0x15191C);

        // 강조·상태
        public static Color Accent { get; } = Hex(0xE3B04B);
        public static Color AccentHover { get; } = Hex(0xF0C266);
        public static Color AccentPressed { get; } = Hex(0xC4943A);
        public static Color AccentSoft { get; } = Hex(0xE3B04B, 0.18f);
        public static Color Ok { get; } = Hex(0x5BC27E);
        public static Color Warning { get; } = Hex(0xE3B04B);
        public static Color Error { get; } = Hex(0xE2584E);

        // 진영
        public static Color Friendly { get; } = Hex(0x4F9BDB);
        public static Color Hostile { get; } = Hex(0xE2584E);

        // 버튼 상태(ColorTint 전환에 쓰는 실제 색. 버튼 Image 자체는 흰색이어야 한다)
        public static Color ButtonNormal { get; } = Hex(0x222B33);
        public static Color ButtonHighlighted { get; } = Hex(0x2D3842);
        public static Color ButtonPressed { get; } = Hex(0x36322A);
        public static Color ButtonSelected { get; } = Hex(0xE3B04B, 0.22f);
        public static Color ButtonDisabled { get; } = Hex(0x1A2026, 0.7f);

        // 글자 크기(기준 해상도 1600×900)
        public const float FontDisplay = 40f;
        public const float FontTitle = 22f;
        public const float FontHeading = 16f;
        public const float FontBody = 14f;
        public const float FontSmall = 12.5f;
        public const float FontCaption = 11f;

        // 간격·모양
        public const int SpaceXs = 4;
        public const int SpaceS = 8;
        public const int SpaceM = 12;
        public const int SpaceL = 16;
        public const int SpaceXl = 24;
        public const float ControlHeight = 32f;
        public const float ControlHeightSmall = 26f;
        public const int CornerRadius = 4;

        /// <summary>전투력·탄약 비율(0~1) → 빨강·호박·초록.</summary>
        public static Color Ratio(float value)
        {
            return value >= 0.66f ? Ok : value >= 0.33f ? Warning : Error;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }
    }
}
