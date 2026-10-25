using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Echelon.Unity.Presentation.Symbols
{
    internal readonly struct SymbolLabelSpec
    {
        internal readonly Vector2 Position;
        internal readonly string Text;
        internal readonly int FontSize;
        internal readonly Color Color;
        internal readonly float Width;
        internal readonly TextAnchor Alignment;

        internal SymbolLabelSpec(
            Vector2 position,
            string text,
            int fontSize,
            Color color,
            float width,
            TextAnchor alignment)
        {
            Position = position;
            Text = text;
            FontSize = fontSize;
            Color = color;
            Width = width;
            Alignment = alignment;
        }
    }

    internal sealed class SymbolLabelPool
    {
        private readonly List<Text> _labels = new List<Text>();
        private readonly List<Outline> _outlines = new List<Outline>();

        internal int VisibleCount { get; private set; }

        internal void Sync(
            Transform parent,
            Rect parentRect,
            IReadOnlyList<SymbolLabelSpec> specs,
            Font font)
        {
            int count = specs == null ? 0 : specs.Count;
            for (int i = 0; i < count; i++)
            {
                if (i == _labels.Count)
                    Create(parent, i, font);

                SymbolLabelSpec spec = specs[i];
                Text label = _labels[i];
                Outline outline = _outlines[i];
                if (!label.gameObject.activeSelf)
                    label.gameObject.SetActive(true);
                label.font = font;
                label.text = spec.Text ?? string.Empty;
                label.fontSize = spec.FontSize;
                label.color = spec.Color;
                label.alignment = spec.Alignment;
                label.supportRichText = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.resizeTextForBestFit = false;
                label.raycastTarget = false;

                RectTransform rect = label.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(spec.Width > 0f ? spec.Width : 300f, spec.FontSize + 8f);
                rect.anchoredPosition = spec.Position - parentRect.min;
                rect.localScale = Vector3.one;

                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1.25f, 1.25f);
                outline.useGraphicAlpha = true;
            }

            for (int i = count; i < _labels.Count; i++)
            {
                if (_labels[i].gameObject.activeSelf)
                    _labels[i].gameObject.SetActive(false);
            }
            VisibleCount = count;
        }

        private void Create(Transform parent, int index, Font font)
        {
            var labelObject = new GameObject(
                "SymbolLabel_" + index,
                typeof(RectTransform),
                typeof(Text),
                typeof(Outline));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.GetComponent<Text>();
            label.font = font;
            label.raycastTarget = false;
            _labels.Add(label);
            _outlines.Add(labelObject.GetComponent<Outline>());
        }
    }
}
