using System.Buffers;
using System.Globalization;
using System.Text;

namespace MuLang.Core;

internal static class LanguageNames
{
    private static readonly HashSet<string> reservedKeywords = new(
        [
            "as",
            "bool",
            "break",
            "continue",
            "else",
            "false",
            "for",
            "has",
            "if",
            "int",
            "is",
            "null",
            "number",
            "object",
            "return",
            "string",
            "true",
            "unknown",
            "var",
            "void",
            "while",
        ],
        StringComparer.Ordinal
    );

    public static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Language identifier cannot be null or whitespace.",
                parameterName
            );
        }

        if (reservedKeywords.Contains(value))
        {
            throw new ArgumentException(
                $"'{value}' is a reserved language keyword.",
                parameterName
            );
        }

        int offset = 0;
        OperationStatus status = Rune.DecodeFromUtf16(
            value.AsSpan(offset),
            out Rune firstRune,
            out int consumed
        );

        if (
            status != OperationStatus.Done ||
            firstRune.Value != '_' && !Rune.IsLetter(firstRune)
        )
        {
            throw new ArgumentException(
                $"'{value}' is not a valid language identifier.",
                parameterName
            );
        }

        offset += consumed;

        while (offset < value.Length)
        {
            status = Rune.DecodeFromUtf16(
                value.AsSpan(offset),
                out Rune rune,
                out consumed
            );

            if (
                status != OperationStatus.Done ||
                rune.Value != '_' &&
                !Rune.IsLetter(rune) &&
                Rune.GetUnicodeCategory(rune) != UnicodeCategory.DecimalDigitNumber
            )
            {
                throw new ArgumentException(
                    $"'{value}' is not a valid language identifier.",
                    parameterName
                );
            }

            offset += consumed;
        }
    }
}
