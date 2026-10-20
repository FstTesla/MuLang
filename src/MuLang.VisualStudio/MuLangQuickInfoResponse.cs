namespace MuLang.VisualStudio;

internal sealed class MuLangQuickInfoResponse
{
    public Item[] Items { get; set; } = [ ];

    public SourceRange? Range { get; set; }

    internal sealed class Item
    {
        public string Category { get; set; } = "";

        public string Content { get; set; } = "";

        public Part[] Parts { get; set; } = [ ];
    }

    internal sealed class Part
    {
        public string Kind { get; set; } = "";

        public string Text { get; set; } = "";
    }

    internal sealed class SourceRange
    {
        public Position? End { get; set; }

        public Position? Start { get; set; }
    }

    internal sealed class Position
    {
        public int Character { get; set; }

        public int Line { get; set; }
    }
}
