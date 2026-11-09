using MuLang.Compiler;
using MuLang.Core.Diagnostics;

namespace MuLang.LanguageServer;

internal sealed class MuLangEditorDocument : EditorDocument
{
    private readonly SemanticDocumentResult document;

    public MuLangEditorDocument(SemanticDocumentResult document)
    {
        this.document = document;
        Occurrences = [ .. document.Occurrences.Select(Adapt) ];
    }

    public override IReadOnlyList<EditorOccurrence> Occurrences { get; }
    public override IEnumerable<Diagnostic> Diagnostics => document.Diagnostics;

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
}
