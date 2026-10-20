namespace MuLang.Compiler;

/// <summary>Represents one classified part of a semantic hover signature.</summary>
public sealed class SemanticHoverPart
{
    internal SemanticHoverPart(SemanticHoverPartKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    /// <summary>Gets the classification of the signature part.</summary>
    public SemanticHoverPartKind Kind { get; }

    /// <summary>Gets the signature text, including any whitespace.</summary>
    public string Text { get; }
}
