using System.Buffers;
using System.Text;

namespace MuLang.Core.Text;

/// <summary>Represents MuLang source text indexed by Unicode scalar values.</summary>
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

    /// <summary>Gets the underlying MuLang source string.</summary>
    public string Text { get; }

    /// <summary>Gets the length in Unicode scalar values.</summary>
    public int Length => scalarOffsets.Length - 1;

    /// <summary>Gets the number of source lines.</summary>
    public int LineCount => lineStarts.Length;

    /// <summary>Creates source text from a MuLang source string.</summary>
    /// <param name="text">The source text.</param>
    /// <returns>The source-text representation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text" /> contains invalid UTF-16.</exception>
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

    /// <summary>Gets the line and column for a Unicode scalar offset.</summary>
    /// <param name="offset">The Unicode scalar offset.</param>
    /// <returns>The corresponding source position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset" /> is outside the source-text bounds.</exception>
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

    /// <summary>Gets the text within a source span.</summary>
    /// <param name="span">The source span.</param>
    /// <returns>The text contained in the span.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the span is outside the source-text bounds.</exception>
    /// <exception cref="OverflowException">Thrown when the span end exceeds <see cref="int.MaxValue" />.</exception>
    public string GetText(TextSpan span)
    {
        ValidateOffset(span.Start);
        ValidateOffset(span.End);

        int utf16Start = scalarOffsets[span.Start];
        int utf16End = scalarOffsets[span.End];

        return Text[utf16Start..utf16End];
    }

    /// <summary>Gets the Unicode scalar value at an offset.</summary>
    /// <param name="offset">The Unicode scalar offset.</param>
    /// <returns>The Unicode scalar value at the specified offset.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset" /> does not identify a scalar value.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the stored source text contains invalid UTF-16.</exception>
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
        return status != OperationStatus.Done
            ? throw new InvalidOperationException("Source text contains invalid UTF-16.")
            : rune;
    }

    private void ValidateOffset(int offset)
    {
        if (offset < 0 || offset > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
    }
}
