using System.Buffers;
using System.Text;

namespace MuLang.Core.Text;

public sealed class SourceText
{
    private readonly int[] scalarOffsets;
    private readonly int[] lineStarts;

    private SourceText(string text, int[] scalarOffsets, int[] lineStarts)
    {
        Text = text;
        this.scalarOffsets = scalarOffsets;
        this.lineStarts = lineStarts;
    }

    public string Text { get; }

    public int Length => scalarOffsets.Length - 1;

    public int LineCount => lineStarts.Length;

    public static SourceText From(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        ICollection<int> scalarOffsets = new List<int>(text.Length + 1);
        ICollection<int> lineStarts = [ 0 ];
        int utf16Offset = 0;
        int scalarOffset = 0;

        while (utf16Offset < text.Length)
        {
            scalarOffsets.Add(utf16Offset);

            OperationStatus status = Rune.DecodeFromUtf16(
                text.AsSpan(utf16Offset),
                out Rune rune,
                out int consumed
            );
            if (status != OperationStatus.Done)
            {
                throw new ArgumentException("Source text contains invalid UTF-16.", nameof(text));
            }

            utf16Offset += consumed;
            scalarOffset++;

            if (rune.Value == '\r')
            {
                if (utf16Offset < text.Length && text[utf16Offset] == '\n')
                {
                    scalarOffsets.Add(utf16Offset);
                    utf16Offset++;
                    scalarOffset++;
                }

                lineStarts.Add(scalarOffset);
            }
            else if (rune.Value == '\n')
            {
                lineStarts.Add(scalarOffset);
            }
        }

        scalarOffsets.Add(text.Length);

        return new SourceText(text, [ .. scalarOffsets ], [ .. lineStarts ]);
    }

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

    public string GetText(TextSpan span)
    {
        ValidateOffset(span.Start);
        ValidateOffset(span.End);

        int utf16Start = scalarOffsets[span.Start];
        int utf16End = scalarOffsets[span.End];

        return Text[utf16Start..utf16End];
    }

    public Rune GetRune(int offset)
    {
        if (offset < 0 || offset >= Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        OperationStatus status = Rune.DecodeFromUtf16(
            Text.AsSpan(scalarOffsets[offset]),
            out Rune rune,
            out _
        );
        if (status != OperationStatus.Done)
        {
            throw new InvalidOperationException("Source text contains invalid UTF-16.");
        }

        return rune;
    }

    private void ValidateOffset(int offset)
    {
        if (offset < 0 || offset > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
    }
}
