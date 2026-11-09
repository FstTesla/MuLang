using MuLang.Core.Diagnostics;
using MuLang.Core.Text;
using System.Text;
using System.Text.Json;

namespace MuLang.LanguageServer;

internal static class WorkspaceConfigurationValidation
{
    public static (
        WorkspaceConfigurationSet? Configuration,
        Diagnostic? Diagnostic
        ) Validate(string text)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(text);

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                }
            );

            try
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new WorkspaceConfigurationException(
                        "The configuration must be an object.",
                        document.RootElement
                    );
                }

                return (
                    WorkspaceConfigurationSetParser.Parse(document.RootElement),
                    null
                );
            }
            catch (WorkspaceConfigurationException exception)
            {
                return (
                    null,
                    new Diagnostic(
                        "MULCFG002",
                        DiagnosticSeverity.Warning,
                        DiagnosticCategory.Binding,
                        GetElementSpan(
                            utf8,
                            document.RootElement,
                            exception.Element
                        ),
                        exception.Message
                    )
                );
            }
        }
        catch (JsonException exception)
        {
            int offset = 0;
            long line = 0;

            while (offset < utf8.Length && line < exception.LineNumber)
            {
                if (utf8[offset++] == '\n')
                {
                    line++;
                }
            }

            offset = (int)Math.Min(
                utf8.Length,
                offset + (exception.BytePositionInLine ?? 0)
            );
            int start = Encoding.UTF8.GetString(utf8, 0, offset)
                .EnumerateRunes()
                .Count();
            int length = start < text.EnumerateRunes().Count() ? 1 : 0;

            return (
                null,
                new Diagnostic(
                    "MULCFG001",
                    DiagnosticSeverity.Error,
                    DiagnosticCategory.Syntax,
                    new TextSpan(start, length),
                    exception.Message
                )
            );
        }
    }

    private static TextSpan GetElementSpan(
        byte[] utf8,
        JsonElement root,
        JsonElement target
    )
    {
        int index = EnumerateElements(root)
            .TakeWhile(element => !element.Equals(target))
            .Count();
        Utf8JsonReader reader = new (
            utf8,
            new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            }
        );

        while (reader.Read())
        {
            if (
                reader.TokenType is JsonTokenType.PropertyName or
                JsonTokenType.EndObject or
                JsonTokenType.EndArray
            )
            {
                continue;
            }

            if (index-- != 0)
            {
                continue;
            }

            int start = (int)reader.TokenStartIndex;
            reader.Skip();
            int end = (int)reader.BytesConsumed;
            return new TextSpan(
                Encoding.UTF8.GetString(utf8, 0, start).EnumerateRunes().Count(),
                Encoding.UTF8.GetString(utf8, start, end - start)
                    .EnumerateRunes()
                    .Count()
            );
        }

        throw new InvalidOperationException(
            "The invalid configuration element was not found."
        );
    }

    private static IEnumerable<JsonElement> EnumerateElements(JsonElement element)
    {
        yield return element;

        IEnumerable<JsonElement> children = element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .Select(static property => property.Value),
            JsonValueKind.Array => element.EnumerateArray(),
            _ => [ ],
        };

        foreach (JsonElement child in children)
        {
            foreach (JsonElement descendant in EnumerateElements(child))
            {
                yield return descendant;
            }
        }
    }
}
