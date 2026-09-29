using MuLang.Compiler;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using System.Text.Json;

namespace MuLang.LanguageServer;

internal sealed class LanguageServer
{
    private static readonly LanguageProfile Profile = LanguageProfiles.Latest;

    private static readonly EnvironmentSchema Environment =
        new EnvironmentBuilder().Build(Profile.LanguageVersion);

    private readonly IDictionary<string, DocumentSnapshot> documents =
        new Dictionary<string, DocumentSnapshot>(StringComparer.Ordinal);

    private readonly Stream input;
    private readonly LspMessageWriter writer;
    private bool exitRequested;
    private bool shutdownRequested;
    private bool useVisualStudioClassifications;

    public LanguageServer(
        Stream input,
        Stream output,
        bool useVisualStudioClassifications = false
    )
    {
        this.input = input ?? throw new ArgumentNullException(nameof(input));
        writer = new LspMessageWriter(
            output ?? throw new ArgumentNullException(nameof(output))
        );
        this.useVisualStudioClassifications = useVisualStudioClassifications;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        while (!exitRequested)
        {
            using JsonDocument? message = await LspMessageReader.ReadAsync(
                input,
                cancellationToken
            );

            if (message is null)
            {
                break;
            }

            await HandleMessageAsync(message.RootElement, cancellationToken);
        }

        return shutdownRequested ? 0 : 1;
    }

    private async Task HandleMessageAsync(
        JsonElement message,
        CancellationToken cancellationToken
    )
    {
        if (
            !message.TryGetProperty("method", out JsonElement methodElement) ||
            methodElement.ValueKind != JsonValueKind.String
        )
        {
            return;
        }

        string method = methodElement.GetString()!;
        bool hasId = message.TryGetProperty("id", out JsonElement id);
        JsonElement parameters = message.TryGetProperty(
            "params",
            out JsonElement parameterValue
        )
            ? parameterValue
            : default;

        try
        {
            switch (method)
            {
                case "initialize":
                {
                    useVisualStudioClassifications =
                        useVisualStudioClassifications ||
                        UsesVisualStudioClassifications(parameters);

                    if (hasId)
                    {
                        await WriteInitializeResponseAsync(
                            id,
                            cancellationToken
                        );
                    }

                    break;
                }

                case "initialized":
                {
                    break;
                }

                case "textDocument/didOpen":
                {
                    await OpenDocumentAsync(parameters, cancellationToken);
                    break;
                }

                case "textDocument/didChange":
                {
                    await ChangeDocumentAsync(parameters, cancellationToken);
                    break;
                }

                case "textDocument/didClose":
                {
                    await CloseDocumentAsync(parameters, cancellationToken);
                    break;
                }

                case "textDocument/semanticTokens/full":
                {
                    if (hasId)
                    {
                        await WriteSemanticTokensAsync(
                            id,
                            parameters,
                            cancellationToken
                        );
                    }

                    break;
                }

                case "shutdown":
                {
                    shutdownRequested = true;

                    if (hasId)
                    {
                        await writer.WriteResponseAsync(
                            id,
                            static result => result.WriteNullValue(),
                            cancellationToken
                        );
                    }

                    break;
                }

                case "exit":
                {
                    exitRequested = true;
                    break;
                }

                default:
                {
                    if (hasId)
                    {
                        await writer.WriteErrorAsync(
                            id,
                            -32601,
                            $"Method not found: {method}",
                            cancellationToken
                        );
                    }

                    break;
                }
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                KeyNotFoundException or
                FormatException
        )
        {
            if (hasId)
            {
                await writer.WriteErrorAsync(
                    id,
                    -32602,
                    "Invalid method parameters.",
                    cancellationToken
                );
            }
        }
    }

    private async Task ChangeDocumentAsync(
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        JsonElement textDocument = parameters.GetProperty("textDocument");
        string uri = textDocument.GetProperty("uri").GetString()!;
        int version = textDocument.GetProperty("version").GetInt32();
        JsonElement changes = parameters.GetProperty("contentChanges");

        if (changes.GetArrayLength() == 0)
        {
            return;
        }

        string text = changes[changes.GetArrayLength() - 1]
            .GetProperty("text")
            .GetString()!;
        DocumentSnapshot document = new (uri, version, text);
        documents[uri] = document;
        await PublishDiagnosticsAsync(document, cancellationToken);
    }

    private Task CloseDocumentAsync(
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        string uri = parameters
            .GetProperty("textDocument")
            .GetProperty("uri")
            .GetString()!;
        documents.Remove(uri);
        return writer.WriteNotificationAsync(
            "textDocument/publishDiagnostics",
            result =>
            {
                result.WriteStartObject();
                result.WriteString("uri", uri);
                result.WriteStartArray("diagnostics");
                result.WriteEndArray();
                result.WriteEndObject();
            },
            cancellationToken
        );
    }

    private Task OpenDocumentAsync(
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        JsonElement textDocument = parameters.GetProperty("textDocument");
        DocumentSnapshot document = new (
            textDocument.GetProperty("uri").GetString()!,
            textDocument.GetProperty("version").GetInt32(),
            textDocument.GetProperty("text").GetString()!
        );
        documents[document.Uri] = document;
        return PublishDiagnosticsAsync(document, cancellationToken);
    }

    private Task PublishDiagnosticsAsync(
        DocumentSnapshot document,
        CancellationToken cancellationToken
    )
    {
        SourceText source = SourceText.From(document.Text);
        AnalysisResult analysis = MuLangCompiler.Analyze(
            document.Text,
            Environment,
            CompilationMode.Program,
            profile: Profile
        );

        return writer.WriteNotificationAsync(
            "textDocument/publishDiagnostics",
            result =>
            {
                result.WriteStartObject();
                result.WriteString("uri", document.Uri);
                result.WriteNumber("version", document.Version);
                result.WriteStartArray("diagnostics");

                foreach (Diagnostic diagnostic in analysis.Diagnostics)
                {
                    WriteDiagnostic(result, source, diagnostic);
                }

                result.WriteEndArray();
                result.WriteEndObject();
            },
            cancellationToken
        );
    }

    private Task WriteInitializeResponseAsync(
        JsonElement id,
        CancellationToken cancellationToken
    )
    {
        return writer.WriteResponseAsync(
            id,
            result =>
            {
                result.WriteStartObject();
                result.WriteStartObject("capabilities");
                result.WriteString("positionEncoding", "utf-16");
                result.WriteStartObject("textDocumentSync");
                result.WriteBoolean("openClose", true);
                result.WriteNumber("change", 1);
                result.WriteBoolean("save", false);
                result.WriteEndObject();
                result.WriteStartObject("semanticTokensProvider");
                result.WriteStartObject("legend");
                result.WriteStartArray("tokenTypes");
                result.WriteStringValue(
                    useVisualStudioClassifications
                        ? "class name"
                        : "type"
                );
                result.WriteStringValue(
                    useVisualStudioClassifications
                        ? "method name"
                        : "method"
                );
                result.WriteStringValue("parameter");
                result.WriteStringValue("variable");
                result.WriteStringValue("property");
                result.WriteEndArray();
                result.WriteStartArray("tokenModifiers");

                if (!useVisualStudioClassifications)
                {
                    result.WriteStringValue("declaration");
                    result.WriteStringValue("readonly");
                    result.WriteStringValue("defaultLibrary");
                }

                result.WriteEndArray();
                result.WriteEndObject();
                result.WriteBoolean("full", true);
                result.WriteBoolean("range", false);
                result.WriteEndObject();
                result.WriteEndObject();
                result.WriteStartObject("serverInfo");
                result.WriteString("name", "MuLang Language Server");
                result.WriteString("version", "1.0");
                result.WriteEndObject();
                result.WriteEndObject();
            },
            cancellationToken
        );
    }

    private Task WriteSemanticTokensAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        string uri = parameters
            .GetProperty("textDocument")
            .GetProperty("uri")
            .GetString()!;

        return writer.WriteResponseAsync(
            id,
            result =>
            {
                result.WriteStartObject();
                result.WriteStartArray("data");

                if (documents.TryGetValue(uri, out DocumentSnapshot? document))
                {
                    WriteSemanticTokenData(
                        result,
                        document,
                        useVisualStudioClassifications
                    );
                }

                result.WriteEndArray();
                result.WriteEndObject();
            },
            cancellationToken
        );
    }

    private static void WriteSemanticTokenData(
        Utf8JsonWriter writer,
        DocumentSnapshot document,
        bool useVisualStudioClassifications
    )
    {
        SourceText source = SourceText.From(document.Text);
        SemanticClassificationResult result =
            MuLangCompiler.ClassifySemantically(
                document.Text,
                Environment,
                CompilationMode.Program,
                profile: Profile
            );
        int previousLine = 0;
        int previousCharacter = 0;

        foreach (SemanticClassification classification in result.Classifications)
        {
            TextPosition start = source.GetPosition(classification.Span.Start);
            TextPosition end = source.GetPosition(classification.Span.End);

            if (start.Line != end.Line)
            {
                continue;
            }

            int line = start.Line - 1;
            int startCharacter = GetUtf16Character(source, start);
            int endCharacter = GetUtf16Character(source, end);
            int deltaLine = line - previousLine;
            int deltaStart = deltaLine == 0
                ? startCharacter - previousCharacter
                : startCharacter;

            writer.WriteNumberValue(deltaLine);
            writer.WriteNumberValue(deltaStart);
            writer.WriteNumberValue(endCharacter - startCharacter);
            writer.WriteNumberValue((int)classification.Kind);
            writer.WriteNumberValue(
                useVisualStudioClassifications
                    ? 0
                    : (int)classification.Modifiers
            );
            previousLine = line;
            previousCharacter = startCharacter;
        }
    }

    private static bool UsesVisualStudioClassifications(JsonElement parameters)
    {
        return
            parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty(
                "initializationOptions",
                out JsonElement initializationOptions
            ) &&
            initializationOptions.ValueKind == JsonValueKind.Object &&
            initializationOptions.TryGetProperty(
                "useVisualStudioClassifications",
                out JsonElement value
            ) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            value.GetBoolean();
    }

    private static void WriteDiagnostic(
        Utf8JsonWriter writer,
        SourceText source,
        Diagnostic diagnostic
    )
    {
        TextPosition start = source.GetPosition(diagnostic.Span.Start);
        TextPosition end = source.GetPosition(diagnostic.Span.End);

        writer.WriteStartObject();
        writer.WriteStartObject("range");
        WritePosition(writer, "start", source, start);
        WritePosition(writer, "end", source, end);
        writer.WriteEndObject();
        writer.WriteNumber("severity", MapSeverity(diagnostic.Severity));
        writer.WriteString("code", diagnostic.Code);
        writer.WriteString("source", "MuLang");
        writer.WriteString("message", diagnostic.Message);
        writer.WriteEndObject();
    }

    private static void WritePosition(
        Utf8JsonWriter writer,
        string propertyName,
        SourceText source,
        TextPosition position
    )
    {
        writer.WriteStartObject(propertyName);
        writer.WriteNumber("line", position.Line - 1);
        writer.WriteNumber("character", GetUtf16Character(source, position));
        writer.WriteEndObject();
    }

    private static int GetUtf16Character(
        SourceText source,
        TextPosition position
    )
    {
        int lineStart = position.Offset - position.Column + 1;
        return
            source.GetUtf16Offset(position.Offset) -
            source.GetUtf16Offset(lineStart);
    }

    private static int MapSeverity(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Error => 1,
            DiagnosticSeverity.Warning => 2,
            DiagnosticSeverity.Information => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(severity)),
        };
    }
}
