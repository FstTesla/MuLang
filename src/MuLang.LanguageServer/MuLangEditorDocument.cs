using MuLang.Compiler;
using MuLang.Core.Diagnostics;
using MuLang.Core.Text;

namespace MuLang.LanguageServer;

internal sealed class MuLangEditorDocument : EditorDocument
{
    private readonly SemanticDocumentResult document;

    public MuLangEditorDocument(SemanticDocumentResult document)
    {
        this.document = document;
        Occurrences = [ .. document.Occurrences.Select(Adapt) ];
        Symbols = [ .. document.DocumentSymbols.Select(AdaptSymbol) ];
    }

    public override IReadOnlyList<EditorOccurrence> Occurrences { get; }
    public override IEnumerable<Diagnostic> Diagnostics => document.Diagnostics;
    public override IReadOnlyList<EditorDocumentSymbol> Symbols { get; }
    public override IReadOnlyList<TextSpan> FoldingRanges => document.FoldingSpans;

    public override EditorHover? GetHover(int offset) => document.GetHover(offset) is { } hover
        ? new EditorHover([ .. hover.Items.Select(AdaptHover) ], hover.Span)
        : null;

    public override IReadOnlyList<EditorOccurrence> GetHighlights(int offset) =>
        [ .. document.GetHighlights(offset).Select(Adapt) ];

    private static EditorOccurrence Adapt(SemanticOccurrence occurrence) => new (
        occurrence.Symbol,
        (int)occurrence.Kind,
        occurrence.Role,
        occurrence.Modifiers,
        occurrence.Span,
        [ .. occurrence.HoverItems.Select(AdaptHover) ],
        occurrence.DefinitionSpan,
        occurrence.IsClassification
    );

    private static EditorHoverItem AdaptHover(SemanticHoverItem item) => new (
        item.Category.ToString().ToLowerInvariant(),
        [ .. item.Parts.Select(static part => new EditorHoverPart(part.Kind, part.Text)) ]
    );

    private static EditorDocumentSymbol AdaptSymbol(SemanticDocumentSymbol symbol) => new (
        symbol.Name,
        symbol.Kind switch
        {
            SemanticDocumentSymbolKind.Type => 5,
            SemanticDocumentSymbolKind.Function => 12,
            SemanticDocumentSymbolKind.Property => 7,
            SemanticDocumentSymbolKind.Parameter or SemanticDocumentSymbolKind.Variable => 13,
            _ => throw new ArgumentOutOfRangeException(nameof(symbol)),
        },
        symbol.Span,
        symbol.SelectionSpan,
        [ .. symbol.Children.Select(AdaptSymbol) ]
    );
}
