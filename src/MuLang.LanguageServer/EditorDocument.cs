using MuLang.Core.Diagnostics;
using MuLang.Core.Text;

namespace MuLang.LanguageServer;

internal abstract class EditorDocument
{
    public abstract IReadOnlyList<EditorOccurrence> Occurrences { get; }
    public virtual IEnumerable<Diagnostic> Diagnostics => [ ];
    public virtual IReadOnlyList<EditorDocumentSymbol> Symbols => [ ];
    public virtual IReadOnlyList<TextSpan> FoldingRanges => [ ];
    public virtual string LanguageId => "mulang";

    public virtual EditorOccurrence? GetOccurrence(int offset) => Occurrences
        .Where(occurrence => occurrence.Span.Contains(offset))
        .OrderBy(static occurrence => occurrence.Span.Length)
        .FirstOrDefault();

    public virtual EditorHover? GetHover(int offset) => GetOccurrence(offset) is { HoverItems.Count: > 0 } occurrence
        ? new EditorHover(occurrence.HoverItems, occurrence.Span)
        : null;

    public IReadOnlyList<EditorOccurrence> GetOccurrences(object symbol) =>
        [ .. Occurrences.Where(occurrence => symbol.Equals(occurrence.Symbol)) ];

    public virtual IReadOnlyList<EditorOccurrence> GetHighlights(int offset) =>
        GetOccurrence(offset)?.Symbol is { } symbol ? GetOccurrences(symbol) : [ ];
}
