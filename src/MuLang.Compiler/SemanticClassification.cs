using MuLang.Core.Text;

namespace MuLang.Compiler;

/// <summary>Represents a binding-based semantic classification over a source span.</summary>
public sealed class SemanticClassification
{
    internal SemanticClassification(
        SemanticClassificationKind kind,
        SemanticClassificationModifiers modifiers,
        TextSpan span
    )
    {
        Kind = kind;
        Modifiers = modifiers;
        Span = span;
    }

    /// <summary>Gets the semantic classification kind.</summary>
    public SemanticClassificationKind Kind { get; }

    /// <summary>Gets the semantic classification modifiers.</summary>
    public SemanticClassificationModifiers Modifiers { get; }

    /// <summary>Gets the classified source span.</summary>
    public TextSpan Span { get; }
}
