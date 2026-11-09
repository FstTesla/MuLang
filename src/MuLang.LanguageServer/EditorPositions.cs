using MuLang.Core.Text;

namespace MuLang.LanguageServer;

internal static class EditorPositions
{
    public static int GetScalarOffset(SourceText source, int line, int character)
    {
        if (line >= source.LineCount)
        {
            return source.Length;
        }

        int start = source.LineMap.LineStarts[line];
        int end = line + 1 < source.LineCount ? source.LineMap.LineStarts[line + 1] : source.Length;

        while (end > start && source.GetRune(end - 1).Value is '\r' or '\n')
        {
            end--;
        }

        int utf16Offset = (int)Math.Min(
            (long)source.GetUtf16Offset(start) + character,
            source.GetUtf16Offset(end)
        );
        return GetScalarOffset(source, utf16Offset);
    }

    public static int GetScalarOffset(SourceText source, int utf16Offset)
    {
        int start = 0;
        int end = source.Length;

        while (start < end)
        {
            int middle = start + (end - start) / 2;

            if (source.GetUtf16Offset(middle) < utf16Offset)
            {
                start = middle + 1;
            }
            else
            {
                end = middle;
            }
        }

        return start;
    }
}
