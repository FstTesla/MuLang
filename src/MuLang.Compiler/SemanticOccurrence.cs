using MuLang.Core.Text;

namespace MuLang.Compiler;

/// <summary>Represents a semantic occurrence over a source span.</summary>
public sealed class SemanticOccurrence
{
    internal SemanticOccurrence(
        SemanticSymbolKey? symbol,
        SemanticClassificationKind kind,
        SemanticOccurrenceRole role,
        SemanticClassificationModifiers modifiers,
        TextSpan span,
        IReadOnlyList<SemanticHoverItem> hoverItems,
        TextSpan? definitionSpan,
        bool isClassification,
        object? highlightGroup,
        IReadOnlyList<TextSpan>? additionalHoverSpans = null
    )
    {
        Symbol = symbol;
        Kind = kind;
        Role = role;
        Modifiers = modifiers;
        Span = span;
        HoverItems = hoverItems;
        DefinitionSpan = definitionSpan;
        IsClassification = isClassification;
        HighlightGroup = highlightGroup;
        AdditionalHoverSpans = additionalHoverSpans ?? [ ];
    }

    /// <summary>Gets the resolved symbol identity, or <c>null</c> for a non-navigable occurrence.</summary>
    public SemanticSymbolKey? Symbol { get; }

    /// <summary>Gets the semantic classification kind.</summary>
    public SemanticClassificationKind Kind { get; }

    /// <summary>Gets the occurrence role.</summary>
    public SemanticOccurrenceRole Role { get; }

    /// <summary>Gets the semantic classification modifiers.</summary>
    public SemanticClassificationModifiers Modifiers { get; }

    /// <summary>Gets the occurrence source span.</summary>
    public TextSpan Span { get; }

    /// <summary>Gets the categorized hover items.</summary>
    public IReadOnlyList<SemanticHoverItem> HoverItems { get; }

    /// <summary>Gets the plain-text hover representation, or <c>null</c> when unavailable.</summary>
    public string? HoverContent => HoverItems.Count == 0
        ? null
        : SemanticHoverFormatting.PlainText(HoverItems);

    /// <summary>Gets the source definition span, or <c>null</c> when the symbol has no source definition.</summary>
    public TextSpan? DefinitionSpan { get; }

    /// <summary>Gets a value indicating whether the occurrence contributes a semantic token.</summary>
    public bool IsClassification { get; }

    internal object? HighlightGroup { get; }

    internal IReadOnlyList<TextSpan> AdditionalHoverSpans { get; }
}
