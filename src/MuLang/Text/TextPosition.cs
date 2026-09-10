namespace MuLang.Text;

public readonly record struct TextPosition
{
    public TextPosition(int offset, int line, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);

        Offset = offset;
        Line = line;
        Column = column;
    }

    public int Offset { get; }

    public int Line { get; }

    public int Column { get; }
}
