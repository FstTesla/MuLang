using MuLang.Core.Text;

namespace MuLang.Compiler;

/// <summary>Represents semantic hover information over a source span.</summary>
public sealed class HoverInfo
{
    internal HoverInfo(
        IReadOnlyList<SemanticHoverItem> items,
        TextSpan span
    )
    {
        Items = items;
        Content = SemanticHoverFormatting.PlainText(items);
        Span = span;
    }

    /// <summary>Gets the categorized hover items.</summary>
    public IReadOnlyList<SemanticHoverItem> Items { get; }

    /// <summary>Gets the plain-text hover representation.</summary>
    public string Content { get; }

    /// <summary>Gets the source span described by the hover.</summary>
    public TextSpan Span { get; }
}
