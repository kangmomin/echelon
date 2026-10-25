using UnityEngine;

namespace Echelon.Unity.UI.Common
{
    /// <summary>
    /// 모서리가 둥근 9-슬라이스 스프라이트를 코드로 만든다(채움·외곽선). 별도 이미지 에셋 없이
    /// 패널·버튼·칩의 모양을 통일하려는 것이다. 한 번 만들어 재사용한다.
    /// </summary>
    public static class UiSprites
    {
        private const int Size = 32;
        private static Sprite _rounded;
        private static Sprite _outline;
        private static Sprite _pill;

        /// <summary>반경 UiTheme.CornerRadius 의 채운 사각형.</summary>
        public static Sprite Rounded => _rounded != null ? _rounded : _rounded = Create(UiTheme.CornerRadius, 0f);

        /// <summary>같은 모양의 1px 외곽선.</summary>
        public static Sprite Outline => _outline != null ? _outline : _outline = Create(UiTheme.CornerRadius, 1f);

        /// <summary>양 끝이 완전히 둥근 막대(진행 막대·칩).</summary>
        public static Sprite Pill => _pill != null ? _pill : _pill = Create(Size / 2 - 1, 0f);

        private static Sprite Create(int radius, float stroke)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = stroke > 0f ? "UiOutline" : "UiRounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[Size * Size];
            float half = Size * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // 둥근 사각형까지의 부호 있는 거리(안쪽 음수)로 가장자리를 1px 안티앨리어싱한다.
                    float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude
                        + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - outside);
                    if (stroke > 0f)
                        alpha *= Mathf.Clamp01(outside + stroke + 0.5f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            int border = Mathf.Min(radius + 2, Size / 2 - 1);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, Size, Size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
