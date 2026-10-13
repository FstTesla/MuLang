using System.Collections.ObjectModel;

namespace MuLang.Core.Text;

/// <summary>Represents source line boundaries indexed by Unicode scalar offsets.</summary>
public sealed class TextLineMap
{
    private readonly int[] lineStarts;

    /// <summary>Initializes a new instance of the <see cref="TextLineMap" /> class.</summary>
    /// <param name="length">The source length in Unicode scalar values.</param>
    /// <param name="lineStarts">The zero-based Unicode scalar offsets at which source lines begin.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lineStarts" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length" /> is negative.</exception>
    /// <exception cref="ArgumentException">Thrown when the line starts do not describe a valid source.</exception>
    public TextLineMap(int length, IEnumerable<int> lineStarts)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (lineStarts is null)
        {
            throw new ArgumentNullException(nameof(lineStarts));
        }

        int[] values = [ .. lineStarts ];

        if (values.Length == 0 || values[0] != 0)
        {
            throw new ArgumentException(
                "The first source line must begin at offset zero.",
                nameof(lineStarts)
            );
        }

        for (int index = 0; index < values.Length; index++)
        {
            if (values[index] > length)
            {
                throw new ArgumentException(
                    "A source line begins beyond the source length.",
                    nameof(lineStarts)
                );
            }

            if (index > 0 && values[index] <= values[index - 1])
            {
                throw new ArgumentException(
                    "Source line starts must be strictly increasing.",
                    nameof(lineStarts)
                );
            }
        }

        Length = length;
        this.lineStarts = values;
        LineStarts = new ReadOnlyCollection<int>(values);
    }

    /// <summary>Gets the source length in Unicode scalar values.</summary>
    public int Length { get; }

    /// <summary>Gets the number of source lines.</summary>
    public int LineCount => lineStarts.Length;

    /// <summary>Gets the zero-based Unicode scalar offsets at which source lines begin.</summary>
    public IReadOnlyList<int> LineStarts { get; }

    /// <summary>Gets the line and column for a Unicode scalar offset.</summary>
    /// <param name="offset">The Unicode scalar offset.</param>
    /// <returns>The corresponding source position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset" /> is outside the source bounds.</exception>
    public TextPosition GetPosition(int offset)
    {
        ValidateOffset(offset);

        int lineIndex = Array.BinarySearch(lineStarts, offset);
        if (lineIndex < 0)
        {
            lineIndex = ~lineIndex - 1;
        }

        return new TextPosition(
            offset,
            lineIndex + 1,
            offset - lineStarts[lineIndex] + 1
        );
    }

    /// <summary>Gets the Unicode scalar offset for a one-based line and column.</summary>
    /// <param name="line">The one-based line number.</param>
    /// <param name="column">The one-based column number.</param>
    /// <returns>The corresponding Unicode scalar offset.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="line" /> or <paramref name="column" /> is outside the source bounds.</exception>
    public int GetOffset(int line, int column)
    {
        if (line < 1 || line > LineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        int lineIndex = line - 1;
        int maximumColumn = lineIndex + 1 < lineStarts.Length
            ? lineStarts[lineIndex + 1] - lineStarts[lineIndex]
            : Length - lineStarts[lineIndex] + 1;

        if (column < 1 || column > maximumColumn)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        return lineStarts[lineIndex] + column - 1;
    }

    /// <summary>Creates a materialized source location for a span.</summary>
    /// <param name="span">The source span.</param>
    /// <param name="sourceDocument">The source document URI, when available.</param>
    /// <returns>The corresponding source location.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sourceDocument" /> is not absolute.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="span" /> is outside the source bounds.</exception>
    /// <exception cref="OverflowException">Thrown when the span end exceeds <see cref="int.MaxValue" />.</exception>
    public TextLocation GetLocation(
        TextSpan span,
        Uri? sourceDocument = null
    )
    {
        ValidateOffset(span.Start);
        ValidateOffset(span.End);

        return new TextLocation(
            span,
            GetPosition(span.Start),
            GetPosition(span.End),
            sourceDocument
        );
    }

    private void ValidateOffset(int offset)
    {
        if (offset < 0 || offset > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
    }
}
