using UnityEngine;

namespace Echelon.Unity.Presentation.Symbols
{
    public enum SymbolGlyphKind
    {
        Unknown,
        Tank,
        Mech,
        Infantry,
        Recon,
        Mortar,
        Supply,
        ForwardObserver,
        AntiAir,
        Wheeled,
        AntiTank,
        Sniper,
        Engineer,
        Aviation,
        Headquarters,
        CommandPost,
        ElectronicWarfare,
        Artillery,
        FixedWing
    }

    public static class SymbolGlyphRenderer
    {
        public static SymbolGlyphKind Classify(string branch)
        {
            switch ((branch ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "tank": return SymbolGlyphKind.Tank;
                case "mech": return SymbolGlyphKind.Mech;
                case "inf": return SymbolGlyphKind.Infantry;
                case "recon": return SymbolGlyphKind.Recon;
                case "mortar": return SymbolGlyphKind.Mortar;
                case "supply": return SymbolGlyphKind.Supply;
                case "fo": return SymbolGlyphKind.ForwardObserver;
                case "aa": return SymbolGlyphKind.AntiAir;
                case "wheeled": return SymbolGlyphKind.Wheeled;
                case "at": return SymbolGlyphKind.AntiTank;
                case "sniper": return SymbolGlyphKind.Sniper;
                case "engineer": return SymbolGlyphKind.Engineer;
                case "aviation": return SymbolGlyphKind.Aviation;
                case "hq": return SymbolGlyphKind.Headquarters;
                case "cp": return SymbolGlyphKind.CommandPost;
                case "ew": return SymbolGlyphKind.ElectronicWarfare;
                case "artillery": return SymbolGlyphKind.Artillery;
                case "fixed_wing": return SymbolGlyphKind.FixedWing;
                default: return SymbolGlyphKind.Unknown;
            }
        }

        public static bool IsVectorGlyph(SymbolGlyphKind kind)
        {
            return kind != SymbolGlyphKind.Unknown && kind != SymbolGlyphKind.Headquarters
                && kind != SymbolGlyphKind.CommandPost && kind != SymbolGlyphKind.ElectronicWarfare;
        }

        public static string GetFallbackText(string branch)
        {
            switch (Classify(branch))
            {
                case SymbolGlyphKind.Headquarters: return "HQ";
                case SymbolGlyphKind.CommandPost: return "CP";
                case SymbolGlyphKind.ElectronicWarfare: return "EW";
                default:
                    string value = (branch ?? string.Empty).Trim();
                    return value.Substring(0, Mathf.Min(2, value.Length)).ToUpperInvariant();
            }
        }

        public static bool Draw(SymbolMeshBatch batch, Rect rect, string branch, Color color)
        {
            SymbolGlyphKind kind = Classify(branch);
            if (!IsVectorGlyph(kind))
                return false;

            Vector2 center = rect.center;
            float cx = center.x;
            float cy = center.y;
            float left = rect.xMin;
            float right = rect.xMax;
            float top = rect.yMax;
            float bottom = rect.yMin;
            Vector2 topLeft = new Vector2(left, top);
            Vector2 topRight = new Vector2(right, top);
            Vector2 bottomLeft = new Vector2(left, bottom);
            Vector2 bottomRight = new Vector2(right, bottom);

            switch (kind)
            {
                case SymbolGlyphKind.Tank:
                    Oval(batch, rect, color);
                    break;
                case SymbolGlyphKind.Mech:
                    Oval(batch, rect, color);
                    CrossedDiagonals(batch, topLeft, topRight, bottomLeft, bottomRight, color);
                    break;
                case SymbolGlyphKind.Infantry:
                    CrossedDiagonals(batch, topLeft, topRight, bottomLeft, bottomRight, color);
                    break;
                case SymbolGlyphKind.Recon:
                    batch.AddLine(bottomLeft, topRight, color, 1.4f);
                    break;
                case SymbolGlyphKind.Mortar:
                    batch.AddCircle(new Vector2(cx, cy - 3f), 2.5f, color, 12);
                    batch.AddLine(new Vector2(cx, cy - 1f), new Vector2(cx, top - 2f), color, 1.4f);
                    break;
                case SymbolGlyphKind.Supply:
                    batch.AddLine(new Vector2(left, bottom + 5f), new Vector2(right, bottom + 5f), color, 1.4f);
                    break;
                case SymbolGlyphKind.ForwardObserver:
                    Vector2 apex = new Vector2(cx, top - 3f);
                    Vector2 baseRight = new Vector2(cx + 6f, bottom + 3f);
                    Vector2 baseLeft = new Vector2(cx - 6f, bottom + 3f);
                    batch.AddLine(apex, baseRight, color, 1.4f);
                    batch.AddLine(baseRight, baseLeft, color, 1.4f);
                    batch.AddLine(baseLeft, apex, color, 1.4f);
                    break;
                case SymbolGlyphKind.AntiAir:
                    batch.AddArc(new Vector2(cx, bottom + 2f), 7f, 0f, Mathf.PI, color, 1.4f, 10);
                    break;
                case SymbolGlyphKind.Wheeled:
                    Oval(batch, rect, color);
                    CrossedDiagonals(batch, topLeft, topRight, bottomLeft, bottomRight, color);
                    batch.AddCircle(new Vector2(cx - 5f, bottom - 3f), 1.8f, color, 10);
                    batch.AddCircle(new Vector2(cx + 5f, bottom - 3f), 1.8f, color, 10);
                    break;
                case SymbolGlyphKind.AntiTank:
                    Vector2 point = new Vector2(cx, top - 2f);
                    batch.AddLine(new Vector2(left + 3f, bottom + 2f), point, color, 1.5f);
                    batch.AddLine(point, new Vector2(right - 3f, bottom + 2f), color, 1.5f);
                    break;
                case SymbolGlyphKind.Sniper:
                    batch.AddCircleOutline(center, 5.5f, color, 1.3f, 14);
                    batch.AddLine(new Vector2(cx - 8f, cy), new Vector2(cx + 8f, cy), color, 1f);
                    batch.AddLine(new Vector2(cx, cy - 8f), new Vector2(cx, cy + 8f), color, 1f);
                    break;
                case SymbolGlyphKind.Engineer:
                    batch.AddLine(new Vector2(cx - 8f, cy + 3f), new Vector2(cx + 8f, cy + 3f), color, 1.5f);
                    for (int i = -1; i <= 1; i++)
                    {
                        float x = cx + i * 8f;
                        batch.AddLine(new Vector2(x, cy + 3f), new Vector2(x, cy - 4f), color, 1.5f);
                    }
                    break;
                case SymbolGlyphKind.Aviation:
                    batch.AddTriangle(center, new Vector2(cx - 9f, cy + 5f), new Vector2(cx - 9f, cy - 5f), color);
                    batch.AddTriangle(center, new Vector2(cx + 9f, cy + 5f), new Vector2(cx + 9f, cy - 5f), color);
                    break;
                case SymbolGlyphKind.Artillery:
                    // 야전 포병: 가운데 찬 원.
                    batch.AddCircle(center, 3.2f, color, 14);
                    break;
                case SymbolGlyphKind.FixedWing:
                    // 고정익: 가로로 누운 8 자(∞).
                    batch.AddEllipseOutline(new Vector2(cx - 4.5f, cy), 4.5f, 3.2f, color, 1.4f);
                    batch.AddEllipseOutline(new Vector2(cx + 4.5f, cy), 4.5f, 3.2f, color, 1.4f);
                    break;
            }
            return true;
        }

        private static void Oval(SymbolMeshBatch batch, Rect rect, Color color)
        {
            batch.AddEllipseOutline(rect.center, rect.width * 0.32f, rect.height * 0.26f, color, 1.4f);
        }

        private static void CrossedDiagonals(
            SymbolMeshBatch batch,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomLeft,
            Vector2 bottomRight,
            Color color)
        {
            batch.AddLine(topLeft, bottomRight, color, 1.2f);
            batch.AddLine(bottomLeft, topRight, color, 1.2f);
        }
    }
}
