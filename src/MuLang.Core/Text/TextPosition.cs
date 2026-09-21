namespace MuLang.Core.Text;

/// <summary>Represents a one-based line and column at a source-text offset.</summary>
public readonly record struct TextPosition
{
    /// <summary>Initializes a new instance of the <see cref="TextPosition" /> struct.</summary>
    /// <param name="offset">The Unicode scalar offset.</param>
    /// <param name="line">The one-based line number.</param>
    /// <param name="column">The one-based column number.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is outside its valid range.</exception>
    public TextPosition(int offset, int line, int column)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (line < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        if (column < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        Offset = offset;
        Line = line;
        Column = column;
    }

    /// <summary>Gets the zero-based Unicode scalar offset.</summary>
    public int Offset { get; }

    /// <summary>Gets the one-based line number.</summary>
    public int Line { get; }

    /// <summary>Gets the one-based column number.</summary>
    public int Column { get; }
}
