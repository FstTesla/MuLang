using MuLang.Core;
using MuLang.Core.Types;
using System.Text;
using System.Text.Json;

namespace MuLang.LanguageServer.Tests;

public sealed class LanguageServerTests
{
    [Test]
    public void ParsesCompleteWorkspaceConfiguration()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "compilationMode": "expression",
              "expectedResultType": "int",
              "profile": {
                "base": "1.2",
                "userDefinedFunctions": "disabled",
                "loops": ["while"],
                "mutations": [],
                "shadowing": ["nestedScopes"]
              },
              "environment": {
                "types": [
                  {
                    "id": "node",
                    "name": "Node",
                    "properties": [
                      {
                        "name": "next",
                        "type": {
                          "kind": "nullable",
                          "underlyingType": "Node"
                        }
                      }
                    ]
                  }
                ],
                "globals": [
                  {
                    "id": "root",
                    "name": "root",
                    "type": "Node"
                  }
                ],
                "functions": [
                  {
                    "id": "getRoot",
                    "name": "getRoot",
                    "parameters": [],
                    "returnType": "Node"
                  }
                ]
              }
            }
            """
        );

        WorkspaceConfiguration configuration =
            WorkspaceConfigurationParser.Parse(document.RootElement);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                configuration.CompilationMode,
                Is.EqualTo(CompilationMode.Expression)
            );
            Assert.That(configuration.ExpectedResultType, Is.SameAs(TypeSymbols.Int));
            Assert.That(
                configuration.Profile.UserDefinedFunctions,
                Is.EqualTo(UserDefinedFunctionsFeature.Disabled)
            );
            Assert.That(configuration.Profile.Loops, Is.EqualTo(LoopFeatures.While));
            Assert.That(configuration.Profile.Mutations, Is.EqualTo(MutationFeatures.None));
            Assert.That(
                configuration.Profile.Shadowing,
                Is.EqualTo(ShadowingPolicy.NestedScopes)
            );
            Assert.That(configuration.Environment.Types, Has.Count.EqualTo(1));
            Assert.That(configuration.Environment.Globals, Has.Count.EqualTo(1));
            Assert.That(configuration.Environment.Functions, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void ResolvesOrderedGlobConfigurationOverrides()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "configuration": {
                "compilationMode": "program"
              },
              "overrides": [
                {
                  "includes": [
                    "**/*.mu"
                  ],
                  "excludes": [
                    "generated/**"
                  ],
                  "configuration": {
                    "compilationMode": "expression",
                    "expectedResultType": "int"
                  }
                },
                {
                  "includes": [
                    "special/*.mu",
                    "tests/?ingle.mu"
                  ],
                  "configuration": {
                    "compilationMode": "program"
                  }
                }
              ]
            }
            """
        );

        WorkspaceConfigurationSet configuration =
            WorkspaceConfigurationSetParser.Parse(document.RootElement);
        WorkspaceConfigurationStore store = new ();
        store.Initialize(["file:///workspace"]);
        store.Set("file:///workspace", configuration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                store
                    .Get("file:///workspace/source/main.mu")
                    .CompilationMode,
                Is.EqualTo(CompilationMode.Expression)
            );
            Assert.That(
                store
                    .Get("file:///workspace/generated/main.mu")
                    .CompilationMode,
                Is.EqualTo(CompilationMode.Program)
            );
            Assert.That(
                store
                    .Get("file:///workspace/special/main.mu")
                    .CompilationMode,
                Is.EqualTo(CompilationMode.Program)
            );
            Assert.That(
                store
                    .Get("file:///workspace/tests/single.mu")
                    .CompilationMode,
                Is.EqualTo(CompilationMode.Program)
            );
            Assert.That(
                store
                    .Get("file:///workspace/source/main.mu")
                    .ExpectedResultType,
                Is.SameAs(TypeSymbols.Int)
            );
        }
    }

    [Test]
    public void RejectsLegacyWorkspaceConfigurationFormat()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "compilationMode": "expression"
            }
            """
        );

        Assert.That(
            () => WorkspaceConfigurationSetParser.Parse(document.RootElement),
            Throws.TypeOf<FormatException>()
        );
    }

    [TestCase(
        """
        {
          "overrides": [
            {
              "include": "**/*.mu",
              "configuration": {}
            }
          ]
        }
        """
    )]
    [TestCase(
        """
        {
          "overrides": [
            {
              "includes": "**/*.mu",
              "configuration": {}
            }
          ]
        }
        """
    )]
    public void RejectsSingularOrScalarGlobProperties(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.That(
            () => WorkspaceConfigurationSetParser.Parse(document.RootElement),
            Throws.TypeOf<FormatException>()
        );
    }

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
                    ["type", "method", "parameter", "variable", "property"]
                )
            );
            Assert.That(
                capabilities
                    .GetProperty("workspace")
                    .GetProperty("workspaceFolders")
                    .GetProperty("supported")
                    .GetBoolean(),
                Is.True
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

    [TestCase(false, 1)]
    [TestCase(true, 0)]
    public async Task ReturnsSourceAndInlineTypeSemanticTokens(
        bool visualStudio,
        int declarationModifier
    )
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            visualStudio,
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mu","languageId":"mulang","version":1,"text":"type Node { next?: Node, payload: { value: int } }; var node: Node;"}}}
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
                    0, 5, 4, 0, declarationModifier,
                    0, 7, 4, 4, declarationModifier,
                    0, 7, 4, 0, 0,
                    0, 6, 7, 4, declarationModifier,
                    0, 11, 5, 4, declarationModifier,
                    0, 20, 4, 3, declarationModifier,
                    0, 6, 4, 0, 0,
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
    public async Task HighlightsWarningSampleWithDiagnostics()
    {
        string source = await File.ReadAllTextAsync(
            Path.Combine(
                AppContext.BaseDirectory,
                "Samples",
                "warnings",
                "semantic-warnings.mu"
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
                        uri = "file:///semantic-warnings.mu",
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
            {"jsonrpc":"2.0","id":3,"method":"textDocument/semanticTokens/full","params":{"textDocument":{"uri":"file:///semantic-warnings.mu"}}}
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
            Assert.That(
                diagnostics
                    .EnumerateArray()
                    .Select(
                        static diagnostic =>
                            diagnostic.GetProperty("code").GetString()
                    )
                    .Order(),
                Is.EqualTo(
                    [
                        "MUL3030",
                        "MUL3032",
                        "MUL3033",
                        "MUL3033",
                        "MUL3034",
                        "MUL3035",
                        "MUL3036",
                        "MUL3037",
                        "MUL3038",
                        "MUL3039",
                        "MUL3040",
                        "MUL3048",
                    ]
                )
            );
            Assert.That(
                diagnostics
                    .EnumerateArray()
                    .Select(
                        static diagnostic =>
                            diagnostic.GetProperty("severity").GetInt32()
                    ),
                Has.All.EqualTo(2)
            );
            Assert.That(semanticTokens.GetArrayLength(), Is.GreaterThan(0));
            Assert.That(semanticTokens.GetArrayLength() % 5, Is.Zero);
        }
    }

    [Test]
    public async Task RequestsAndAppliesConfigurationPerWorkspaceFolder()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"workspace":{"configuration":true}},"workspaceFolders":[{"uri":"file:///expression","name":"expression"},{"uri":"file:///program","name":"program"}]}}
            """,
            """
            {"jsonrpc":"2.0","method":"initialized","params":{}}
            """,
            """
            {"jsonrpc":"2.0","id":1,"result":[{"configuration":{"compilationMode":"expression"}},{}]}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///expression/test.mu","languageId":"mulang","version":1,"text":"1"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///program/test.mu","languageId":"mulang","version":1,"text":"1"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":2}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement configurationRequest = output.Single(
            static message =>
                message.TryGetProperty("method", out JsonElement method) &&
                method.GetString() == "workspace/configuration"
        );
        IReadOnlyDictionary<string, JsonElement> diagnosticsByUri = output
            .Where(
                static message =>
                    message.TryGetProperty("method", out JsonElement method) &&
                    method.GetString() == "textDocument/publishDiagnostics"
            )
            .ToDictionary(
                static message => message
                    .GetProperty("params")
                    .GetProperty("uri")
                    .GetString()!,
                static message => message
                    .GetProperty("params")
                    .GetProperty("diagnostics")
            );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                configurationRequest
                    .GetProperty("params")
                    .GetProperty("items")
                    .EnumerateArray()
                    .Select(
                        static item =>
                            item.GetProperty("scopeUri").GetString()
                    ),
                Is.EqualTo(["file:///expression/", "file:///program/"])
            );
            Assert.That(
                diagnosticsByUri["file:///expression/test.mu"].GetArrayLength(),
                Is.Zero
            );
            Assert.That(
                diagnosticsByUri["file:///program/test.mu"].GetArrayLength(),
                Is.GreaterThan(0)
            );
        }
    }

    [Test]
    public async Task LoadsConfigurationFromWorkspaceFile()
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"MuLang.LanguageServer.Tests-{Guid.NewGuid():N}"
        );
        string configurationFile = Path.Combine(
            workspace,
            WorkspaceConfigurationFile.FileName
        );
        Directory.CreateDirectory(workspace);

        try
        {
            await File.WriteAllTextAsync(
                configurationFile,
                """
                {
                  "configuration": {
                    "compilationMode": "expression",
                    "expectedResultType": "int"
                  },
                  "overrides": []
                }
                """
            );
            string workspaceUri = new Uri(
                $"{workspace}{Path.DirectorySeparatorChar}"
            ).AbsoluteUri;
            string documentUri = new Uri(
                Path.Combine(workspace, "test.mu")
            ).AbsoluteUri;
            IReadOnlyList<JsonElement> output = await RunServerAsync(
                true,
                JsonSerializer.Serialize(
                    new
                    {
                        jsonrpc = "2.0",
                        id = 1,
                        method = "initialize",
                        @params = new
                        {
                            workspaceFolders = new[]
                            {
                                new
                                {
                                    uri = workspaceUri,
                                    name = "workspace",
                                },
                            },
                        },
                    }
                ),
                """
                {"jsonrpc":"2.0","method":"initialized","params":{}}
                """,
                JsonSerializer.Serialize(
                    new
                    {
                        jsonrpc = "2.0",
                        method = "textDocument/didOpen",
                        @params = new
                        {
                            textDocument = new
                            {
                                uri = documentUri,
                                languageId = "mulang",
                                version = 1,
                                text = "1",
                            },
                        },
                    }
                ),
                JsonSerializer.Serialize(
                    new
                    {
                        jsonrpc = "2.0",
                        method = "workspace/didChangeWatchedFiles",
                        @params = new
                        {
                            changes = new[]
                            {
                                new
                                {
                                    uri = new Uri(configurationFile).AbsoluteUri,
                                    type = 2,
                                },
                            },
                        },
                    }
                ),
                """
                {"jsonrpc":"2.0","method":"shutdown","id":2}
                """,
                """
                {"jsonrpc":"2.0","method":"exit"}
                """
            );
            IReadOnlyList<JsonElement> diagnostics = output
                .Where(
                    static message =>
                        message.TryGetProperty(
                            "method",
                            out JsonElement method
                        ) &&
                        method.GetString() ==
                            "textDocument/publishDiagnostics"
                )
                .Select(
                    static message => message
                        .GetProperty("params")
                        .GetProperty("diagnostics")
                )
                .ToArray();

            Assert.That(
                diagnostics,
                Has.Count.EqualTo(2).And.All.Matches<JsonElement>(
                    static value => value.GetArrayLength() == 0
                )
            );
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Test]
    public async Task LoadsConfigurationFromFallbackWorkspaceRoot()
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"MuLang.LanguageServer.Tests-{Guid.NewGuid():N}"
        );
        string configurationFile = Path.Combine(
            workspace,
            WorkspaceConfigurationFile.FileName
        );
        Directory.CreateDirectory(workspace);

        try
        {
            await File.WriteAllTextAsync(
                configurationFile,
                """
                {
                  "configuration": {
                    "compilationMode": "expression",
                    "expectedResultType": "int"
                  }
                }
                """
            );
            string workspaceUri = new Uri(
                $"{workspace}{Path.DirectorySeparatorChar}"
            ).AbsoluteUri;
            string documentUri = new Uri(
                Path.Combine(workspace, "test.mu")
            ).AbsoluteUri;
            IReadOnlyList<JsonElement> output =
                await RunServerWithWorkspaceRootAsync(
                    workspaceUri,
                    """
                    {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
                    """,
                    """
                    {"jsonrpc":"2.0","method":"initialized","params":{}}
                    """,
                    CreateOpenDocumentMessage(documentUri, "1"),
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
                        message.TryGetProperty(
                            "method",
                            out JsonElement method
                        ) &&
                        method.GetString() ==
                            "textDocument/publishDiagnostics"
                )
                .GetProperty("params")
                .GetProperty("diagnostics");

            Assert.That(diagnostics.GetArrayLength(), Is.Zero);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Test]
    public async Task UsesNearestConfigurationFileWithLocalGlobs()
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"MuLang.LanguageServer.Tests-{Guid.NewGuid():N}"
        );
        string nestedDirectory = Path.Combine(workspace, "nested");
        string expressionDirectory = Path.Combine(
            nestedDirectory,
            "expressions"
        );
        Directory.CreateDirectory(expressionDirectory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(
                    workspace,
                    WorkspaceConfigurationFile.FileName
                ),
                """
                {
                  "configuration": {
                    "compilationMode": "expression",
                    "expectedResultType": "int"
                  }
                }
                """
            );
            await File.WriteAllTextAsync(
                Path.Combine(
                    nestedDirectory,
                    WorkspaceConfigurationFile.FileName
                ),
                """
                {
                  "configuration": {
                    "compilationMode": "program"
                  },
                  "overrides": [
                    {
                      "includes": [
                        "expressions/**/*.mu"
                      ],
                      "configuration": {
                        "compilationMode": "expression",
                        "expectedResultType": "bool"
                      }
                    }
                  ]
                }
                """
            );
            string workspaceUri = new Uri(
                $"{workspace}{Path.DirectorySeparatorChar}"
            ).AbsoluteUri;
            string rootDocumentUri = new Uri(
                Path.Combine(workspace, "root.mu")
            ).AbsoluteUri;
            string nestedDocumentUri = new Uri(
                Path.Combine(nestedDirectory, "nested.mu")
            ).AbsoluteUri;
            string expressionDocumentUri = new Uri(
                Path.Combine(expressionDirectory, "condition.mu")
            ).AbsoluteUri;
            IReadOnlyList<JsonElement> output = await RunServerAsync(
                true,
                JsonSerializer.Serialize(
                    new
                    {
                        jsonrpc = "2.0",
                        id = 1,
                        method = "initialize",
                        @params = new
                        {
                            workspaceFolders = new[]
                            {
                                new
                                {
                                    uri = workspaceUri,
                                    name = "workspace",
                                },
                            },
                        },
                    }
                ),
                """
                {"jsonrpc":"2.0","method":"initialized","params":{}}
                """,
                CreateOpenDocumentMessage(rootDocumentUri, "1"),
                CreateOpenDocumentMessage(nestedDocumentUri, "1"),
                CreateOpenDocumentMessage(expressionDocumentUri, "true"),
                """
                {"jsonrpc":"2.0","method":"shutdown","id":2}
                """,
                """
                {"jsonrpc":"2.0","method":"exit"}
                """
            );
            IReadOnlyDictionary<string, JsonElement> diagnostics = output
                .Where(
                    static message =>
                        message.TryGetProperty(
                            "method",
                            out JsonElement method
                        ) &&
                        method.GetString() ==
                            "textDocument/publishDiagnostics"
                )
                .ToDictionary(
                    static message => message
                        .GetProperty("params")
                        .GetProperty("uri")
                        .GetString()!,
                    static message => message
                        .GetProperty("params")
                        .GetProperty("diagnostics")
                );

            using (Assert.EnterMultipleScope())
            {
                Assert.That(
                    diagnostics[rootDocumentUri].GetArrayLength(),
                    Is.Zero
                );
                Assert.That(
                    diagnostics[nestedDocumentUri].GetArrayLength(),
                    Is.GreaterThan(0)
                );
                Assert.That(
                    diagnostics[expressionDocumentUri].GetArrayLength(),
                    Is.Zero
                );
            }
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Test]
    public void InvalidatesCachedConfigurationFileDiscovery()
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"MuLang.LanguageServer.Tests-{Guid.NewGuid():N}"
        );
        string nestedDirectory = Path.Combine(workspace, "nested");
        string rootConfiguration = Path.Combine(
            workspace,
            WorkspaceConfigurationFile.FileName
        );
        string nestedConfiguration = Path.Combine(
            nestedDirectory,
            WorkspaceConfigurationFile.FileName
        );
        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllText(rootConfiguration, "{}");
        WorkspaceConfigurationFile resolver = new ();
        string workspaceUri = new Uri(
            $"{workspace}{Path.DirectorySeparatorChar}"
        ).AbsoluteUri;
        string documentUri = new Uri(
            Path.Combine(nestedDirectory, "test.mu")
        ).AbsoluteUri;

        try
        {
            Assert.That(
                resolver.TryResolve(
                    documentUri,
                    workspaceUri,
                    out Uri? initialFile
                ),
                Is.True
            );
            Assert.That(initialFile!.LocalPath, Is.EqualTo(rootConfiguration));

            File.WriteAllText(nestedConfiguration, "{}");
            resolver.ClearCache();

            Assert.That(
                resolver.TryResolve(
                    documentUri,
                    workspaceUri,
                    out Uri? nestedFile
                ),
                Is.True
            );
            Assert.That(nestedFile!.LocalPath, Is.EqualTo(nestedConfiguration));

            File.Delete(nestedConfiguration);
            resolver.ClearCache();

            Assert.That(
                resolver.TryResolve(
                    documentUri,
                    workspaceUri,
                    out Uri? restoredFile
                ),
                Is.True
            );
            Assert.That(restoredFile!.LocalPath, Is.EqualTo(rootConfiguration));
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Test]
    public async Task AppliesDeclarativeRecursiveEnvironment()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"workspace":{"configuration":true}},"workspaceFolders":[{"uri":"file:///workspace","name":"workspace"}]}}
            """,
            """
            {"jsonrpc":"2.0","method":"initialized","params":{}}
            """,
            """
            {"jsonrpc":"2.0","id":1,"result":[{"configuration":{"environment":{"types":[{"id":"node","name":"Node","properties":[{"name":"next","type":{"kind":"nullable","underlyingType":"Node"},"isOptional":true}]}],"globals":[{"id":"root","name":"root","type":"Node"}]}}}]}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///workspace/test.mu","languageId":"mulang","version":1,"text":"var node: Node = root; var next = node.next;"}}}
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

        Assert.That(diagnostics.GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task RetainsLastValidConfiguration()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"workspace":{"configuration":true}},"workspaceFolders":[{"uri":"file:///workspace","name":"workspace"}]}}
            """,
            """
            {"jsonrpc":"2.0","method":"initialized","params":{}}
            """,
            """
            {"jsonrpc":"2.0","id":1,"result":[{"configuration":{"compilationMode":"expression"}}]}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///workspace/test.mu","languageId":"mulang","version":1,"text":"1"}}}
            """,
            """
            {"jsonrpc":"2.0","method":"workspace/didChangeConfiguration","params":{"settings":{}}}
            """,
            """
            {"jsonrpc":"2.0","id":2,"result":[{"configuration":{"compilationMode":"invalid"}}]}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didChange","params":{"textDocument":{"uri":"file:///workspace/test.mu","version":2},"contentChanges":[{"text":"2"}]}}
            """,
            """
            {"jsonrpc":"2.0","method":"shutdown","id":99}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        IReadOnlyList<JsonElement> diagnostics = output
            .Where(
                static message =>
                    message.TryGetProperty("method", out JsonElement method) &&
                    method.GetString() == "textDocument/publishDiagnostics"
            )
            .Select(
                static message => message
                    .GetProperty("params")
                    .GetProperty("diagnostics")
            )
            .ToArray();
        JsonElement error = output.Single(
            static message =>
                message.TryGetProperty("method", out JsonElement method) &&
                method.GetString() == "window/showMessage"
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnostics, Has.All.Matches<JsonElement>(
                static value => value.GetArrayLength() == 0
            ));
            Assert.That(
                error
                    .GetProperty("params")
                    .GetProperty("message")
                    .GetString(),
                Does.Contain("Invalid MuLang configuration")
            );
        }
    }

    private static Task<IReadOnlyList<JsonElement>> RunServerAsync(
        params string[] messages
    )
    {
        return RunServerAsync(false, messages);
    }

    private static Task<IReadOnlyList<JsonElement>> RunServerAsync(
        bool useVisualStudioClassifications,
        params string[] messages
    )
    {
        return RunServerCoreAsync(
            useVisualStudioClassifications,
            null,
            messages
        );
    }

    private static Task<IReadOnlyList<JsonElement>>
        RunServerWithWorkspaceRootAsync(
            string workspaceRoot,
            params string[] messages
        )
    {
            return RunServerCoreAsync(true, workspaceRoot, messages);
    }

        private static async Task<IReadOnlyList<JsonElement>> RunServerCoreAsync(
        bool useVisualStudioClassifications,
        string? fallbackWorkspaceFolder,
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
            useVisualStudioClassifications,
            fallbackWorkspaceFolder
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

    private static string CreateOpenDocumentMessage(string uri, string text)
    {
        return JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                method = "textDocument/didOpen",
                @params = new
                {
                    textDocument = new
                    {
                        uri,
                        languageId = "mulang",
                        version = 1,
                        text,
                    },
                },
            }
        );
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
