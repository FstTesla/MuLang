namespace MuLang.Core.Text;

/// <summary>Represents a materialized source span with line and column information.</summary>
public readonly record struct TextLocation
{
    /// <summary>Initializes a new instance of the <see cref="TextLocation" /> struct.</summary>
    /// <param name="span">The source span.</param>
    /// <param name="start">The inclusive start position.</param>
    /// <param name="end">The exclusive end position.</param>
    /// <param name="sourceDocument">The source document URI, when available.</param>
    /// <exception cref="ArgumentException">Thrown when a position does not match the span or <paramref name="sourceDocument" /> is not absolute.</exception>
    public TextLocation(
        TextSpan span,
        TextPosition start,
        TextPosition end,
        Uri? sourceDocument = null
    )
    {
        if (start.Offset != span.Start)
        {
            throw new ArgumentException(
                "The start position does not match the span.",
                nameof(start)
            );
        }

        if (end.Offset != span.End)
        {
            throw new ArgumentException(
                "The end position does not match the span.",
                nameof(end)
            );
        }

        if (
            sourceDocument is not null &&
            (
                !sourceDocument.IsAbsoluteUri ||
                string.IsNullOrWhiteSpace(sourceDocument.OriginalString)
            )
        )
        {
            throw new ArgumentException(
                "The source document URI must be absolute.",
                nameof(sourceDocument)
            );
        }

        Span = span;
        Start = start;
        End = end;
        SourceDocument = sourceDocument;
    }

    /// <summary>Gets the source span.</summary>
    public TextSpan Span { get; }

    /// <summary>Gets the inclusive start position.</summary>
    public TextPosition Start { get; }

    /// <summary>Gets the exclusive end position.</summary>
    public TextPosition End { get; }

    /// <summary>Gets the source document URI, when available.</summary>
    public Uri? SourceDocument { get; }
}
