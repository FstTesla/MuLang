namespace MuLang.Core.Text;

/// <summary>Represents a materialized source span with line and column information.</summary>
public readonly record struct TextLocation
{
    /// <summary>Initializes a new instance of the <see cref="TextLocation" /> struct.</summary>
    /// <param name="span">The source span.</param>
    /// <param name="start">The inclusive start position.</param>
    /// <param name="end">The exclusive end position.</param>
    /// <param name="sourceDocument">The source document name or URI, when available.</param>
    /// <exception cref="ArgumentException">Thrown when a position does not match the span or <paramref name="sourceDocument" /> is empty or whitespace.</exception>
    public TextLocation(
        TextSpan span,
        TextPosition start,
        TextPosition end,
        string? sourceDocument = null
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

        if (sourceDocument is not null && string.IsNullOrWhiteSpace(sourceDocument))
        {
            throw new ArgumentException(
                "The source document cannot be empty or whitespace.",
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

    /// <summary>Gets the source document name or URI, when available.</summary>
    public string? SourceDocument { get; }
}
