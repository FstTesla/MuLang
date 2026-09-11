namespace MuLang.Core.Text;

public readonly record struct TextPosition
{
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

    public int Offset { get; }

    public int Line { get; }

    public int Column { get; }
}
