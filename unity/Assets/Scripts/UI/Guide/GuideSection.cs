using System.Collections.Generic;

namespace Echelon.Unity.UI.Guide
{
    public enum GuideBlockKind
    {
        RichText,
        Table
    }

    public readonly struct GuideRow
    {
        public GuideRow(string key, string description)
        {
            Key = key;
            Description = description;
        }

        public string Key { get; }
        public string Description { get; }
    }

    public sealed class GuideBlock
    {
        public GuideBlock(string text)
        {
            Kind = GuideBlockKind.RichText;
            Text = text;
            Rows = new GuideRow[0];
        }

        public GuideBlock(IReadOnlyList<GuideRow> rows)
        {
            Kind = GuideBlockKind.Table;
            Text = string.Empty;
            Rows = rows;
        }

        public GuideBlockKind Kind { get; }
        public string Text { get; }
        public IReadOnlyList<GuideRow> Rows { get; }
    }

    public sealed class GuideSection
    {
        public GuideSection(string title, params GuideBlock[] blocks)
        {
            Title = title;
            Blocks = blocks;
        }

        public string Title { get; }
        public IReadOnlyList<GuideBlock> Blocks { get; }
    }
}
