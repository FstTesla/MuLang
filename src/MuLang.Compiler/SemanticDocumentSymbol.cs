using MuLang.Core.Text;

namespace MuLang.Compiler;

/// <summary>Represents a source declaration and its nested declarations in a document outline.</summary>
public sealed class SemanticDocumentSymbol
{
    internal SemanticDocumentSymbol(
        string name,
        SemanticDocumentSymbolKind kind,
        TextSpan span,
        TextSpan selectionSpan,
        IReadOnlyList<SemanticDocumentSymbol> children
    )
    {
        Name = name;
        Kind = kind;
        Span = span;
        SelectionSpan = selectionSpan;
        Children = children;
    }

    /// <summary>Gets the declaration name.</summary>
    public string Name { get; }

    /// <summary>Gets the declaration kind.</summary>
    public SemanticDocumentSymbolKind Kind { get; }

    /// <summary>Gets the complete declaration span in Unicode scalar offsets.</summary>
    public TextSpan Span { get; }

    /// <summary>Gets the declaration name span in Unicode scalar offsets.</summary>
    public TextSpan SelectionSpan { get; }

    /// <summary>Gets the nested declarations in source order.</summary>
    public IReadOnlyList<SemanticDocumentSymbol> Children { get; }
}
