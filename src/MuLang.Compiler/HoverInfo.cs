using MuLang.Core.Text;

namespace MuLang.Compiler;

/// <summary>Represents semantic hover information over a source span.</summary>
public sealed class HoverInfo
{
    internal HoverInfo(string content, TextSpan span)
    {
        Content = content;
        Span = span;
    }

    /// <summary>Gets the symbol signature in MuLang source notation.</summary>
    public string Content { get; }

    /// <summary>Gets the source span described by the hover.</summary>
    public TextSpan Span { get; }
}
