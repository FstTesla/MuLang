using MuLang.Core.Text;

namespace MuLang.Compiler;

/// <summary>Represents an editor-oriented lexical classification over a source span.</summary>
public sealed class SourceClassification
{
    internal SourceClassification(SourceClassificationKind kind, TextSpan span)
    {
        Kind = kind;
        Span = span;
    }

    /// <summary>Gets the classification kind.</summary>
    public SourceClassificationKind Kind { get; }

    /// <summary>Gets the classified source span.</summary>
    public TextSpan Span { get; }
}
