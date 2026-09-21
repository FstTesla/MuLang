namespace MuLang.Core.Text;

/// <summary>Represents a contiguous range of source text.</summary>
public readonly record struct TextSpan
{
    /// <summary>Initializes a new instance of the <see cref="TextSpan" /> struct.</summary>
    /// <param name="start">The start offset.</param>
    /// <param name="length">The span length.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start" /> or <paramref name="length" /> is negative.</exception>
    public TextSpan(int start, int length)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Start = start;
        Length = length;
    }

    /// <summary>Gets the inclusive start offset.</summary>
    public int Start { get; }

    /// <summary>Gets the span length.</summary>
    public int Length { get; }

    /// <summary>Gets the exclusive end offset.</summary>
    /// <exception cref="OverflowException">Thrown when the end offset exceeds <see cref="int.MaxValue" />.</exception>
    public int End => checked(Start + Length);

    /// <summary>Creates a span from inclusive start and exclusive end offsets.</summary>
    /// <param name="start">The start offset.</param>
    /// <param name="end">The exclusive end offset.</param>
    /// <returns>The span between the specified bounds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start" /> is negative or <paramref name="end" /> precedes it.</exception>
    public static TextSpan FromBounds(int start, int end)
    {
        return end < start
            ? throw new ArgumentOutOfRangeException(nameof(end))
            : new TextSpan(start, end - start);
    }

    /// <summary>Determines whether an offset is within the span.</summary>
    /// <param name="offset">The Unicode scalar offset.</param>
    /// <returns><c>true</c> if the offset is within the span; otherwise, <c>false</c>.</returns>
    /// <exception cref="OverflowException">Thrown when the span end exceeds <see cref="int.MaxValue" />.</exception>
    public bool Contains(int offset)
    {
        return offset >= Start && offset < End;
    }
}
