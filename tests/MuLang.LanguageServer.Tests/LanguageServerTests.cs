using System.Text;
using System.Text.Json;

namespace MuLang.LanguageServer.Tests;

public sealed class LanguageServerTests
{
    [Test]
    public async Task AdvertisesFullSynchronizationAndUtf16Positions()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":2}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement capabilities = output[0]
            .GetProperty("result")
            .GetProperty("capabilities");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                capabilities.GetProperty("positionEncoding").GetString(),
                Is.EqualTo("utf-16")
            );
            Assert.That(
                capabilities
                    .GetProperty("textDocumentSync")
                    .GetProperty("change")
                    .GetInt32(),
                Is.EqualTo(1)
            );
            Assert.That(
                capabilities
                    .GetProperty("semanticTokensProvider")
                    .GetProperty("legend")
                    .GetProperty("tokenTypes")
                    .EnumerateArray()
                    .Select(static token => token.GetString()),
                Is.EqualTo(
                    [ "type", "method", "parameter", "variable", "property" ]
                )
            );
        }
    }

    [Test]
    public async Task AdvertisesVisualStudioSemanticClassifications()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            true,
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":2}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement legend = output[0]
            .GetProperty("result")
            .GetProperty("capabilities")
            .GetProperty("semanticTokensProvider")
            .GetProperty("legend");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                legend
                    .GetProperty("tokenTypes")
                    .EnumerateArray()
                    .Select(static token => token.GetString()),
                Is.EqualTo(
                    [
                        "class name",
                        "method name",
                        "parameter",
                        "variable",
                        "property",
                    ]
                )
            );
            Assert.That(
                legend.GetProperty("tokenModifiers").GetArrayLength(),
                Is.Zero
            );
        }
    }

    [Test]
    public async Task PublishesVersionedDiagnosticsUsingUtf16Characters()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mu","languageId":"mulang","version":7,"text":"😀@"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":2}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement parameters = output[1].GetProperty("params");
        JsonElement diagnostics = parameters.GetProperty("diagnostics");
        JsonElement secondInvalidCharacter = diagnostics
            .EnumerateArray()
            .Single(
                static diagnostic =>
                    diagnostic.GetProperty("code").GetString() == "MUL1001" &&
                    diagnostic
                        .GetProperty("range")
                        .GetProperty("start")
                        .GetProperty("character")
                        .GetInt32() == 2
            );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parameters.GetProperty("version").GetInt32(), Is.EqualTo(7));
            Assert.That(
                secondInvalidCharacter
                    .GetProperty("range")
                    .GetProperty("start")
                    .GetProperty("character")
                    .GetInt32(),
                Is.EqualTo(2)
            );
        }
    }

    [Test]
    public async Task ReanalyzesChangesAndClearsClosedDocuments()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mulang","languageId":"mulang","version":1,"text":"@"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didChange","params":{"textDocument":{"uri":"file:///test.mulang","version":2},"contentChanges":[{"text":"var value: int = 1;"}]}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didClose","params":{"textDocument":{"uri":"file:///test.mulang"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":2}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                output[1]
                    .GetProperty("params")
                    .GetProperty("diagnostics")
                    .GetArrayLength(),
                Is.GreaterThan(0)
            );
            Assert.That(
                output[2]
                    .GetProperty("params")
                    .GetProperty("version")
                    .GetInt32(),
                Is.EqualTo(2)
            );
            Assert.That(
                output[2]
                    .GetProperty("params")
                    .GetProperty("diagnostics")
                    .GetArrayLength(),
                Is.EqualTo(0)
            );
            Assert.That(
                output[3]
                    .GetProperty("params")
                    .GetProperty("diagnostics")
                    .GetArrayLength(),
                Is.EqualTo(0)
            );
        }
    }

    [Test]
    public async Task ReturnsDeltaEncodedSemanticTokens()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mu","languageId":"mulang","version":1,"text":"\"😀\"; var value = 1; value = 2;"}}}
            """,
            """
            {"jsonrpc":"2.0","id":3,"method":"textDocument/semanticTokens/full","params":{"textDocument":{"uri":"file:///test.mu"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":2}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement response = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.ValueKind == JsonValueKind.Number &&
                id.GetInt32() == 3
        );

        Assert.That(
            response
                .GetProperty("result")
                .GetProperty("data")
                .EnumerateArray()
                .Select(static value => value.GetInt32()),
            Is.EqualTo(
                [
                    0, 10, 5, 3, 1,
                    0, 11, 5, 3, 0,
                ]
            )
        );
    }

    [Test]
    public async Task ReturnsMethodAndNamedTypeSemanticTokens()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mu","languageId":"mulang","version":1,"text":"func identity(value: Customer): Customer { return value; } identity(1);"}}}
            """,
            """
            {"jsonrpc":"2.0","id":3,"method":"textDocument/semanticTokens/full","params":{"textDocument":{"uri":"file:///test.mu"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":2}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement response = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.ValueKind == JsonValueKind.Number &&
                id.GetInt32() == 3
        );

        Assert.That(
            response
                .GetProperty("result")
                .GetProperty("data")
                .EnumerateArray()
                .Select(static value => value.GetInt32()),
            Is.EqualTo(
                [
                    0, 5, 8, 1, 1,
                    0, 9, 5, 2, 1,
                    0, 7, 8, 0, 0,
                    0, 11, 8, 0, 0,
                    0, 18, 5, 2, 0,
                    0, 9, 8, 1, 0,
                ]
            )
        );
    }

    [Test]
    public async Task ReturnsVisualStudioMethodAndTypeClassifications()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            true,
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mu","languageId":"mulang","version":1,"text":"func identity(value: Customer): Customer { return value; } identity(1);"}}}
            """,
            """
            {"jsonrpc":"2.0","id":3,"method":"textDocument/semanticTokens/full","params":{"textDocument":{"uri":"file:///test.mu"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":2}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement response = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.ValueKind == JsonValueKind.Number &&
                id.GetInt32() == 3
        );

        Assert.That(
            response
                .GetProperty("result")
                .GetProperty("data")
                .EnumerateArray()
                .Select(static value => value.GetInt32()),
            Is.EqualTo(
                [
                    0, 5, 8, 1, 0,
                    0, 9, 5, 2, 0,
                    0, 7, 8, 0, 0,
                    0, 11, 8, 0, 0,
                    0, 18, 5, 2, 0,
                    0, 9, 8, 1, 0,
                ]
            )
        );
    }

    [Test]
    public async Task ReturnsEmptySemanticTokensForUnknownDocument()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","id":3,"method":"textDocument/semanticTokens/full","params":{"textDocument":{"uri":"file:///unknown.mu"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":2}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement response = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.ValueKind == JsonValueKind.Number &&
                id.GetInt32() == 3
        );

        Assert.That(
            response
                .GetProperty("result")
                .GetProperty("data")
                .GetArrayLength(),
            Is.Zero
        );
    }

    [Test]
    public async Task HighlightsSampleWithoutDiagnostics()
    {
        string source = await File.ReadAllTextAsync(
            Path.Combine(
                AppContext.BaseDirectory,
                "Samples",
                "highlighting.mu"
            )
        );
        string openDocument = JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                method = "textDocument/didOpen",
                @params = new
                {
                    textDocument = new
                    {
                        uri = "file:///highlighting.mu",
                        languageId = "mulang",
                        version = 1,
                        text = source,
                    },
                },
            }
        );
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            openDocument,
            """
            {"jsonrpc":"2.0","id":3,"method":"textDocument/semanticTokens/full","params":{"textDocument":{"uri":"file:///highlighting.mu"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":2}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement diagnostics = output
            .Single(
                static message =>
                    message.TryGetProperty("method", out JsonElement method) &&
                    method.GetString() == "textDocument/publishDiagnostics"
            )
            .GetProperty("params")
            .GetProperty("diagnostics");
        JsonElement semanticTokens = output
            .Single(
                static message =>
                    message.TryGetProperty("id", out JsonElement id) &&
                    id.ValueKind == JsonValueKind.Number &&
                    id.GetInt32() == 3
            )
            .GetProperty("result")
            .GetProperty("data");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnostics.GetArrayLength(), Is.Zero);
            Assert.That(semanticTokens.GetArrayLength(), Is.GreaterThan(0));
            Assert.That(semanticTokens.GetArrayLength() % 5, Is.Zero);
        }
    }

    private static Task<IReadOnlyList<JsonElement>> RunServerAsync(
        params string[] messages
    )
    {
        return RunServerAsync(false, messages);
    }

    private static async Task<IReadOnlyList<JsonElement>> RunServerAsync(
        bool useVisualStudioClassifications,
        params string[] messages
    )
    {
        using MemoryStream input = new ();

        foreach (string message in messages)
        {
            await WriteMessageAsync(input, message);
        }

        input.Position = 0;
        using MemoryStream output = new ();
        LanguageServer server = new (
            input,
            output,
            useVisualStudioClassifications
        );
        int exitCode = await server.RunAsync(CancellationToken.None);
        Assert.That(exitCode, Is.Zero);
        output.Position = 0;
        IList<JsonElement> results = new List<JsonElement>();

        while (true)
        {
            using JsonDocument? message = await LspMessageReader.ReadAsync(
                output,
                CancellationToken.None
            );

            if (message is null)
            {
                break;
            }

            results.Add(message.RootElement.Clone());
        }

        return [ .. results ];
    }

    private static async Task WriteMessageAsync(Stream stream, string message)
    {
        byte[] payload = Encoding.UTF8.GetBytes(message);
        byte[] header = Encoding.ASCII.GetBytes(
            $"Content-Length: {payload.Length}\r\n\r\n"
        );
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
    }
}
