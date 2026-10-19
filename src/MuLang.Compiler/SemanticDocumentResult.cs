using MuLang.Core.Diagnostics;
using MuLang.Core.Text;

namespace MuLang.Compiler;

/// <summary>Represents resolved semantic occurrences and diagnostics for a MuLang document.</summary>
public sealed class SemanticDocumentResult
{
    internal SemanticDocumentResult(
        IReadOnlyList<SemanticOccurrence> occurrences,
        DiagnosticCollection diagnostics,
        Uri? sourceDocument,
        TextLineMap lineMap
    )
    {
        Occurrences = occurrences;
        Diagnostics = diagnostics;
        SourceDocument = sourceDocument;
        LineMap = lineMap;
    }

    /// <summary>Gets the resolved semantic occurrences.</summary>
    public IReadOnlyList<SemanticOccurrence> Occurrences { get; }

    /// <summary>Gets the diagnostics produced while analyzing the document.</summary>
    public DiagnosticCollection Diagnostics { get; }

    /// <summary>Gets the source document URI, when available.</summary>
    public Uri? SourceDocument { get; }

    /// <summary>Gets the source line map.</summary>
    public TextLineMap LineMap { get; }

    /// <summary>Gets the smallest semantic occurrence containing an offset.</summary>
    /// <param name="offset">The zero-based Unicode scalar offset.</param>
    /// <returns>The containing occurrence, or <c>null</c> when no occurrence contains the offset.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset" /> is outside the source bounds.</exception>
    public SemanticOccurrence? GetOccurrence(int offset)
    {
        if (offset < 0 || offset > LineMap.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        return Occurrences
            .Where(
                occurrence =>
                    occurrence.Span.Start <= offset &&
                    offset < occurrence.Span.End
            )
            .OrderBy(static occurrence => occurrence.Span.Length)
            .FirstOrDefault();
    }

    /// <summary>Gets all occurrences of a resolved symbol.</summary>
    /// <param name="symbol">The semantic symbol identity.</param>
    /// <returns>The matching occurrences in source order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="symbol" /> is <c>null</c>.</exception>
    public IReadOnlyList<SemanticOccurrence> GetOccurrences(
        SemanticSymbolKey symbol
    )
    {
        if (symbol is null)
        {
            throw new ArgumentNullException(nameof(symbol));
        }

        return
        [
            .. Occurrences.Where(occurrence => symbol.Equals(occurrence.Symbol)),
        ];
    }

    /// <summary>Gets the semantic or control-flow highlights containing an offset.</summary>
    /// <param name="offset">The zero-based Unicode scalar offset.</param>
    /// <returns>The related occurrences in source order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset" /> is outside the source bounds.</exception>
    public IReadOnlyList<SemanticOccurrence> GetHighlights(int offset)
    {
        SemanticOccurrence? selected = GetOccurrence(offset);

        if (selected?.Symbol is { } symbol)
        {
            return GetOccurrences(symbol);
        }

        if (selected?.HighlightGroup is not { } group)
        {
            return [];
        }

        return
        [
            .. Occurrences.Where(
                occurrence =>
                    ReferenceEquals(occurrence.HighlightGroup, group)
            ),
        ];
    }
}
