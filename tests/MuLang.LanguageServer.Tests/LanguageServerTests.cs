using MuLang.Core;
using MuLang.Core.Types;
using System.IO.Pipelines;
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
              "$schema": "docs/schemas/mulang.config.schema.json",
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
    public void MergesHierarchicalConfigurationFiles()
    {
        using JsonDocument rootDocument = JsonDocument.Parse(
            """
            {
              "configuration": {
                "compilationMode": "expression",
                "expectedResultType": "int"
              },
              "overrides": [
                {
                  "includes": [
                    "nested/programs/*.mu"
                  ],
                  "configuration": {
                    "compilationMode": "program"
                  }
                }
              ]
            }
            """
        );
        using JsonDocument nestedDocument = JsonDocument.Parse(
            """
            {
              "overrides": [
                {
                  "includes": [
                    "expressions/*.mu"
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
        using JsonDocument replacedDocument = JsonDocument.Parse(
            """
            {
              "configuration": {
                "compilationMode": "program"
              }
            }
            """
        );
        WorkspaceConfigurationStore store = new ();
        store.Initialize(["file:///workspace"]);
        store.SetConfigurationFile(
            "file:///workspace/mulang.config.json",
            WorkspaceConfigurationSetParser.Parse(rootDocument.RootElement)
        );
        store.SetConfigurationFile(
            "file:///workspace/nested/mulang.config.json",
            WorkspaceConfigurationSetParser.Parse(nestedDocument.RootElement)
        );
        store.SetConfigurationFile(
            "file:///workspace/nested/replaced/mulang.config.json",
            WorkspaceConfigurationSetParser.Parse(replacedDocument.RootElement)
        );

        WorkspaceConfiguration root = store.Get(
            "file:///workspace/main.mu"
        );
        WorkspaceConfiguration inherited = store.Get(
            "file:///workspace/nested/main.mu"
        );
        WorkspaceConfiguration rootOverride = store.Get(
            "file:///workspace/nested/programs/main.mu"
        );
        WorkspaceConfiguration nestedOverride = store.Get(
            "file:///workspace/nested/expressions/condition.mu"
        );
        WorkspaceConfiguration replaced = store.Get(
            "file:///workspace/nested/replaced/main.mu"
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.CompilationMode, Is.EqualTo(CompilationMode.Expression));
            Assert.That(root.ExpectedResultType, Is.SameAs(TypeSymbols.Int));
            Assert.That(
                inherited.CompilationMode,
                Is.EqualTo(CompilationMode.Expression)
            );
            Assert.That(
                inherited.ExpectedResultType,
                Is.SameAs(TypeSymbols.Int)
            );
            Assert.That(
                rootOverride.CompilationMode,
                Is.EqualTo(CompilationMode.Program)
            );
            Assert.That(rootOverride.ExpectedResultType, Is.Null);
            Assert.That(
                nestedOverride.CompilationMode,
                Is.EqualTo(CompilationMode.Expression)
            );
            Assert.That(
                nestedOverride.ExpectedResultType,
                Is.SameAs(TypeSymbols.Bool)
            );
            Assert.That(
                replaced.CompilationMode,
                Is.EqualTo(CompilationMode.Program)
            );
            Assert.That(replaced.ExpectedResultType, Is.Null);
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
            Assert.That(capabilities.GetProperty("hoverProvider").GetBoolean(), Is.True);
            Assert.That(
                capabilities
                    .GetProperty("documentHighlightProvider")
                    .GetBoolean(),
                Is.True
            );
            Assert.That(
                capabilities.GetProperty("definitionProvider").GetBoolean(),
                Is.True
            );
            Assert.That(
                capabilities.GetProperty("referencesProvider").GetBoolean(),
                Is.True
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
                        "property name",
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReturnsHoverWithUtf16Ranges(bool useVisualStudioClassifications)
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            useVisualStudioClassifications,
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"textDocument":{"hover":{"contentFormat":["markdown","plaintext"]}}}}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mu","version":1,"text":"\"😀\";\r\n\"😀\"; var value$ = 1; value;"}}}
            """,
            """
            {"jsonrpc":"2.0","id":3,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///test.mu"},"position":{"line":1,"character":24}}}
            """,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement hover = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.GetInt32() == 3
        ).GetProperty("result");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                hover.GetProperty("contents").GetProperty("kind").GetString(),
                Is.EqualTo("markdown")
            );
            Assert.That(
                hover.GetProperty("contents").GetProperty("value").GetString(),
                Is.EqualTo("*(variable)* value$: int")
            );
            Assert.That(
                hover.GetProperty("range").GetProperty("start").GetProperty("line").GetInt32(),
                Is.EqualTo(1)
            );
            Assert.That(
                hover.GetProperty("range").GetProperty("start").GetProperty("character").GetInt32(),
                Is.EqualTo(22)
            );
            Assert.That(
                hover.GetProperty("range").GetProperty("end").GetProperty("character").GetInt32(),
                Is.EqualTo(27)
            );
        }
    }

    [Test]
    public async Task FormatsCategorizedMarkdownHover()
    {
        const string source =
            "var obj: object = { }; var key = \"name\"; " +
            "var missingValue = missing; var access = obj[key];";
        int missing = source.IndexOf(
            "missing;",
            StringComparison.Ordinal
        );
        int key = source.IndexOf(
            "obj[key]",
            StringComparison.Ordinal
        ) + "obj[".Length;
        string HoverRequest(int id, int character) =>
            JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id,
                    method = "textDocument/hover",
                    @params = new
                    {
                        textDocument = new { uri = "file:///test.mu" },
                        position = new { line = 0, character },
                    },
                }
            );
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{"textDocument":{"hover":{"contentFormat":["markdown"]}}}}}
            """,
            CreateOpenDocumentMessage("file:///test.mu", source),
            HoverRequest(3, missing),
            HoverRequest(4, key),
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        IReadOnlyList<string?> values =
        [
            .. output
                .Where(
                    static message =>
                        message.TryGetProperty(
                            "id",
                            out JsonElement id
                        ) &&
                        id.GetInt32() is 3 or 4
                )
                .OrderBy(
                    static message =>
                        message.GetProperty("id").GetInt32()
                )
                .Select(
                    static message =>
                        message
                            .GetProperty("result")
                            .GetProperty("contents")
                            .GetProperty("value")
                            .GetString()
                ),
        ];

        Assert.That(
            values,
            Is.EqualTo(
                [
                    "*(variable)* missing: &lt;unresolved&gt;",
                    "*(variable)* key: string\n\n*(expression)* obj\\[key\\]: unknown?",
                ]
            )
        );
    }

    [Test]
    public async Task RefreshesHoverAfterDocumentAndConfigurationChanges()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mu","version":1,"text":"var value = 1; value;"}}}
            """,
            """
            {"jsonrpc":"2.0","id":3,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///test.mu"},"position":{"line":0,"character":15}}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didChange","params":{"textDocument":{"uri":"file:///test.mu","version":2},"contentChanges":[{"text":"var value = \"text\"; value;"}]}}
            """,
            """
            {"jsonrpc":"2.0","id":4,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///test.mu"},"position":{"line":0,"character":20}}}
            """,
            """
            {"jsonrpc":"2.0","method":"workspace/didChangeConfiguration","params":{"settings":{"mulang":{"configuration":{"compilationMode":"expression","expectedResultType":"string","environment":{"globals":[{"id":"value","name":"value","type":"string"}]}}}}}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didChange","params":{"textDocument":{"uri":"file:///test.mu","version":3},"contentChanges":[{"text":"value"}]}}
            """,
            """
            {"jsonrpc":"2.0","id":5,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///test.mu"},"position":{"line":0,"character":1}}}
            """,
            """
            {"jsonrpc":"2.0","method":"workspace/didChangeConfiguration","params":{"settings":{"mulang":{"configuration":{"compilationMode":"expression","environment":{"globals":[{"id":"value","name":"value","type":"int"}]}}}}}}
            """,
            """
            {"jsonrpc":"2.0","id":6,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///test.mu"},"position":{"line":0,"character":1}}}
            """,
            """
            {"jsonrpc":"2.0","method":"workspace/didChangeConfiguration","params":{"settings":{"mulang":{"configuration":{"compilationMode":"invalid"}}}}}
            """,
            """
            {"jsonrpc":"2.0","id":7,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///test.mu"},"position":{"line":0,"character":1}}}
            """,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        IReadOnlyList<JsonElement> hovers =
        [
            .. output.Where(
                static message =>
                    message.TryGetProperty("id", out JsonElement id) &&
                    id.GetInt32() >= 3
            ).Select(static message => message.GetProperty("result").GetProperty("contents")),
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                hovers.Select(static hover => hover.GetProperty("kind").GetString()),
                Is.All.EqualTo("plaintext")
            );
            Assert.That(
                hovers.Select(static hover => hover.GetProperty("value").GetString()),
                Is.EqualTo(
                    [
                        "[variable] value: int",
                        "[variable] value: string",
                        "[variable] value$: string",
                        "[variable] value$: int",
                        "[variable] value$: int",
                    ]
                )
            );
        }
    }

    [Test]
    public async Task ReturnsRecoveryHoverForDeclaredAndUnresolvedNames()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mu","version":1,"text":"var value = missing;"}}}
            """,
            """
            {"jsonrpc":"2.0","id":3,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///test.mu"},"position":{"line":0,"character":5}}}
            """,
            """
            {"jsonrpc":"2.0","id":4,"method":"textDocument/hover","params":{"textDocument":{"uri":"file:///test.mu"},"position":{"line":0,"character":13}}}
            """,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement localHover = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.GetInt32() == 3
        ).GetProperty("result");
        JsonElement missingHover = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.GetInt32() == 4
        ).GetProperty("result");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                localHover
                    .GetProperty("contents")
                    .GetProperty("value")
                    .GetString(),
                Is.EqualTo("[variable] value: <unresolved>")
            );
            Assert.That(
                missingHover
                    .GetProperty("contents")
                    .GetProperty("value")
                    .GetString(),
                Is.EqualTo("[variable] missing: <unresolved>")
            );
        }
    }

    [Test]
    public async Task ReturnsLiteralFunctionPropertyAndDynamicKeyHovers()
    {
        const string source =
            "var obj: object = { }; var key = \"name\"; " +
            "var literal = 1; var call = missing(2); " +
            "var access = obj[key]; obj.dynamic;";
        string HoverRequest(int id, int character) =>
            JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id,
                    method = "textDocument/hover",
                    @params = new
                    {
                        textDocument = new { uri = "file:///test.mu" },
                        position = new { line = 0, character },
                    },
                }
            );
        int literal = source.IndexOf(
            "= 1",
            StringComparison.Ordinal
        ) + 2;
        int function = source.IndexOf(
            "missing",
            StringComparison.Ordinal
        );
        int argument = source.IndexOf(
            "(2)",
            StringComparison.Ordinal
        ) + 1;
        int key = source.IndexOf(
            "obj[key]",
            StringComparison.Ordinal
        ) + "obj[".Length;
        int property = source.LastIndexOf(
            "dynamic",
            StringComparison.Ordinal
        );
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            CreateOpenDocumentMessage("file:///test.mu", source),
            HoverRequest(3, literal),
            HoverRequest(4, function),
            HoverRequest(5, argument),
            HoverRequest(6, key),
            HoverRequest(7, property),
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        IReadOnlyList<string?> hovers =
        [
            .. output
                .Where(
                    static message =>
                        message.TryGetProperty(
                            "id",
                            out JsonElement id
                        ) &&
                        id.GetInt32() is >= 3 and <= 7
                )
                .OrderBy(
                    static message =>
                        message.GetProperty("id").GetInt32()
                )
                .Select(
                    static message =>
                        message
                            .GetProperty("result")
                            .GetProperty("contents")
                            .GetProperty("value")
                            .GetString()
                ),
        ];

        Assert.That(
            hovers,
            Is.EqualTo(
                [
                    "[literal] 1: int",
                    "[function] missing(...): <unresolved>",
                    "[literal] 2: int",
                    "[variable] key: string\n[expression] obj[key]: unknown?",
                    "[property] dynamic: unknown?",
                ]
            )
        );
    }

    [TestCase(0, 3)]
    [TestCase(0, 0)]
    [TestCase(0, 14)]
    [TestCase(0, 100)]
    [TestCase(0, int.MaxValue)]
    [TestCase(100, 0)]
    public async Task ReturnsNullHoverForNonSymbolsAndClosedDocuments(int line, int character)
    {
        string request = JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "textDocument/hover",
                @params = new
                {
                    textDocument = new { uri = "file:///test.mu" },
                    position = new { line, character },
                },
            }
        );
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            request,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mu","version":1,"text":"var value = 1; missing;"}}}
            """,
            request.Replace("\"id\":3", "\"id\":4", StringComparison.Ordinal),
            """
            {"jsonrpc":"2.0","method":"textDocument/didClose","params":{"textDocument":{"uri":"file:///test.mu"}}}
            """,
            request.Replace("\"id\":3", "\"id\":5", StringComparison.Ordinal),
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );

        Assert.That(
            output.Where(
                static message =>
                    message.TryGetProperty("id", out JsonElement id) &&
                    id.GetInt32() >= 3
            ).Select(static message => message.GetProperty("result").ValueKind),
            Is.All.EqualTo(JsonValueKind.Null)
        );
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    public async Task RejectsNegativeHoverPositions(int line, int character)
    {
        string request = JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "textDocument/hover",
                @params = new
                {
                    textDocument = new { uri = "file:///test.mu" },
                    position = new { line, character },
                },
            }
        );
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            request,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );

        Assert.That(
            output[0].GetProperty("error").GetProperty("code").GetInt32(),
            Is.EqualTo(-32602)
        );
    }

    [TestCase("""{}""")]
    [TestCase("""{"textDocument":{"uri":null},"position":{"line":0,"character":0}}""")]
    [TestCase("""{"textDocument":{"uri":"file:///test.mu"},"position":{"line":"0","character":0}}""")]
    [TestCase("""{"textDocument":{"uri":"file:///test.mu"},"position":{"line":0}}""")]
    public async Task RejectsMalformedHoverParameters(string parameters)
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            $$"""
            {"jsonrpc":"2.0","id":3,"method":"textDocument/hover","params":{{parameters}}}
            """,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );

        Assert.That(
            output[0].GetProperty("error").GetProperty("code").GetInt32(),
            Is.EqualTo(-32602)
        );
    }

    [Test]
    public async Task ReturnsSemanticDocumentHighlights()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///test.mu","version":1,"text":"var value = 1; value = 2; var copy = value;"}}}
            """,
            """
            {"jsonrpc":"2.0","id":3,"method":"textDocument/documentHighlight","params":{"textDocument":{"uri":"file:///test.mu"},"position":{"line":0,"character":5}}}
            """,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement highlights = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.GetInt32() == 3
        ).GetProperty("result");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                highlights.EnumerateArray().Select(
                    static highlight =>
                        highlight.GetProperty("kind").GetInt32()
                ),
                Is.EqualTo([1, 3, 2])
            );
            Assert.That(
                highlights.EnumerateArray().Select(
                    static highlight =>
                        highlight
                            .GetProperty("range")
                            .GetProperty("start")
                            .GetProperty("character")
                            .GetInt32()
                ),
                Is.EqualTo([4, 15, 37])
            );
        }
    }

    [Test]
    public async Task HighlightsOnlyTheResolvedProperty()
    {
        const string source =
            "type First { value: int }; type Second { value: int }; " +
            "var first: First = { value = 1 }; " +
            "var second: Second = { value = 2 }; " +
            "var a = first.value; var b = second.value;";
        string openDocument = CreateOpenDocumentMessage(
            "file:///test.mu",
            source
        );
        int firstAccess = source.IndexOf(
            "first.value",
            StringComparison.Ordinal
        ) + "first.".Length;
        string request = JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "textDocument/documentHighlight",
                @params = new
                {
                    textDocument = new { uri = "file:///test.mu" },
                    position = new { line = 0, character = firstAccess },
                },
            }
        );
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            openDocument,
            request,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement highlights = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.GetInt32() == 3
        ).GetProperty("result");

        Assert.That(highlights.GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task ReturnsSourceDefinitionLocations()
    {
        const string source =
            "type Node { name: string }; " +
            "var node: Node = { name = \"root\" }; " +
            "var selected = node.name;";
        int propertyAccess = source.LastIndexOf(
            "name",
            StringComparison.Ordinal
        );
        string request = JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "textDocument/definition",
                @params = new
                {
                    textDocument = new { uri = "file:///test.mu" },
                    position = new { line = 0, character = propertyAccess },
                },
            }
        );
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            CreateOpenDocumentMessage("file:///test.mu", source),
            request,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement definition = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.GetInt32() == 3
        ).GetProperty("result");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                definition.GetProperty("uri").GetString(),
                Is.EqualTo("file:///test.mu")
            );
            Assert.That(
                definition
                    .GetProperty("range")
                    .GetProperty("start")
                    .GetProperty("character")
                    .GetInt32(),
                Is.EqualTo(source.IndexOf("name", StringComparison.Ordinal))
            );
        }
    }

    [TestCase(false, 2)]
    [TestCase(true, 3)]
    public async Task ReturnsDocumentLocalReferences(
        bool includeDeclaration,
        int expectedCount
    )
    {
        const string source =
            "var value = 1; value = 2; var copy = value;";
        string request = JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "textDocument/references",
                @params = new
                {
                    textDocument = new { uri = "file:///test.mu" },
                    position = new { line = 0, character = 5 },
                    context = new { includeDeclaration },
                },
            }
        );
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            CreateOpenDocumentMessage("file:///test.mu", source),
            request,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );
        JsonElement references = output.Single(
            static message =>
                message.TryGetProperty("id", out JsonElement id) &&
                id.GetInt32() == 3
        ).GetProperty("result");

        Assert.That(references.GetArrayLength(), Is.EqualTo(expectedCount));
    }

    [TestCase("textDocument/documentHighlight")]
    [TestCase("textDocument/definition")]
    [TestCase("textDocument/references")]
    public async Task RejectsMalformedNavigationParameters(string method)
    {
        string request = JsonSerializer.Serialize(
            new
            {
                jsonrpc = "2.0",
                id = 3,
                method,
                @params = new { },
            }
        );
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            request,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );

        Assert.That(
            output[0].GetProperty("error").GetProperty("code").GetInt32(),
            Is.EqualTo(-32602)
        );
    }

    [Test]
    public async Task ReturnsNullNavigationForUnknownDocuments()
    {
        IReadOnlyList<JsonElement> output = await RunServerAsync(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
            """,
            """
            {"jsonrpc":"2.0","id":3,"method":"textDocument/documentHighlight","params":{"textDocument":{"uri":"file:///unknown.mu"},"position":{"line":0,"character":0}}}
            """,
            """
            {"jsonrpc":"2.0","id":4,"method":"textDocument/definition","params":{"textDocument":{"uri":"file:///unknown.mu"},"position":{"line":0,"character":0}}}
            """,
            """
            {"jsonrpc":"2.0","id":5,"method":"textDocument/references","params":{"textDocument":{"uri":"file:///unknown.mu"},"position":{"line":0,"character":0},"context":{"includeDeclaration":true}}}
            """,
            """
            {"jsonrpc":"2.0","id":2,"method":"shutdown"}
            """,
            """
            {"jsonrpc":"2.0","method":"exit"}
            """
        );

        Assert.That(
            output.Where(
                static message =>
                    message.TryGetProperty("id", out JsonElement id) &&
                    id.GetInt32() is 3 or 4 or 5
            ).Select(
                static message =>
                    message.GetProperty("result").ValueKind
            ),
            Is.All.EqualTo(JsonValueKind.Null)
        );
    }

    [Test]
    public async Task FindsConfiguredReferencesInUnopenedWorkspaceFiles()
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"MuLang.LanguageServer.References-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(workspace);

        try
        {
            string openPath = Path.Combine(workspace, "open.mu");
            string unopenedPath = Path.Combine(workspace, "unopened.mulang");
            await File.WriteAllTextAsync(openPath, "0");
            await File.WriteAllTextAsync(unopenedPath, "current");
            string workspaceUri = new Uri(workspace).AbsoluteUri;
            string openUri = new Uri(openPath).AbsoluteUri;
            string initialize = JsonSerializer.Serialize(
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
            );
            string request = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = 3,
                    method = "textDocument/references",
                    @params = new
                    {
                        textDocument = new { uri = openUri },
                        position = new { line = 0, character = 1 },
                        context = new { includeDeclaration = false },
                    },
                }
            );
            IReadOnlyList<JsonElement> output = await RunServerAsync(
                initialize,
                """
                {"jsonrpc":"2.0","method":"workspace/didChangeConfiguration","params":{"settings":{"mulang":{"configuration":{"compilationMode":"expression","environment":{"globals":[{"id":"current","name":"current","type":"int"}]}}}}}}
                """,
                CreateOpenDocumentMessage(openUri, "current + current"),
                request,
                """
                {"jsonrpc":"2.0","id":2,"method":"shutdown"}
                """,
                """
                {"jsonrpc":"2.0","method":"exit"}
                """
            );
            JsonElement references = output.Single(
                static message =>
                    message.TryGetProperty("id", out JsonElement id) &&
                    id.GetInt32() == 3
            ).GetProperty("result");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(references.GetArrayLength(), Is.EqualTo(3));
                Assert.That(
                    references.EnumerateArray().Count(
                        reference =>
                            reference
                                .GetProperty("uri")
                                .GetString() ==
                            new Uri(unopenedPath).AbsoluteUri
                    ),
                    Is.EqualTo(1)
                );
            }
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Test]
    public async Task KeepsConfiguredReferenceIdentitiesSeparate()
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"MuLang.LanguageServer.ReferenceIdentity-{Guid.NewGuid():N}"
        );
        string nested = Path.Combine(workspace, "nested");
        Directory.CreateDirectory(nested);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(workspace, WorkspaceConfigurationFile.FileName),
                """
                {
                  "configuration": {
                    "compilationMode": "expression",
                    "environment": {
                      "globals": [
                        { "id": "root-current", "name": "current", "type": "int" }
                      ]
                    }
                  }
                }
                """
            );
            await File.WriteAllTextAsync(
                Path.Combine(nested, WorkspaceConfigurationFile.FileName),
                """
                {
                  "configuration": {
                    "compilationMode": "expression",
                    "environment": {
                      "globals": [
                        { "id": "nested-current", "name": "current", "type": "int" }
                      ]
                    }
                  }
                }
                """
            );
            string rootPath = Path.Combine(workspace, "root.mu");
            string nestedPath = Path.Combine(nested, "nested.mu");
            await File.WriteAllTextAsync(rootPath, "current");
            await File.WriteAllTextAsync(nestedPath, "current");
            string rootUri = new Uri(rootPath).AbsoluteUri;
            string request = JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id = 3,
                    method = "textDocument/references",
                    @params = new
                    {
                        textDocument = new { uri = rootUri },
                        position = new { line = 0, character = 1 },
                        context = new { includeDeclaration = false },
                    },
                }
            );
            IReadOnlyList<JsonElement> output =
                await RunServerWithWorkspaceRootAsync(
                    new Uri(workspace).AbsoluteUri,
                    """
                    {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
                    """,
                    CreateOpenDocumentMessage(rootUri, "current"),
                    request,
                    """
                    {"jsonrpc":"2.0","id":2,"method":"shutdown"}
                    """,
                    """
                    {"jsonrpc":"2.0","method":"exit"}
                    """
                );
            JsonElement references = output.Single(
                static message =>
                    message.TryGetProperty("id", out JsonElement id) &&
                    id.GetInt32() == 3
            ).GetProperty("result");

            Assert.That(references.GetArrayLength(), Is.EqualTo(1));
            Assert.That(
                references[0].GetProperty("uri").GetString(),
                Is.EqualTo(rootUri)
            );
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Test]
    public async Task RefreshesUnopenedReferencesAfterFileChanges()
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            $"MuLang.LanguageServer.ReferenceRefresh-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(workspace);

        try
        {
            string openPath = Path.Combine(workspace, "open.mu");
            string unopenedPath = Path.Combine(workspace, "unopened.mu");
            await File.WriteAllTextAsync(openPath, "current");
            await File.WriteAllTextAsync(unopenedPath, "current");
            string openUri = new Uri(openPath).AbsoluteUri;
            string unopenedUri = new Uri(unopenedPath).AbsoluteUri;
            string workspaceUri = new Uri(workspace).AbsoluteUri;
            string initialize = JsonSerializer.Serialize(
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
            );
            string ReferencesRequest(int id) => JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id,
                    method = "textDocument/references",
                    @params = new
                    {
                        textDocument = new { uri = openUri },
                        position = new { line = 0, character = 1 },
                        context = new { includeDeclaration = false },
                    },
                }
            );
            string watchedChange = JsonSerializer.Serialize(
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
                                uri = unopenedUri,
                                type = 2,
                            },
                        },
                    },
                }
            );
            Pipe inputPipe = new ();
            await using Stream input = inputPipe.Reader.AsStream();
            await using Stream inputWriter = inputPipe.Writer.AsStream();
            using MemoryStream outputStream = new ();
            LanguageServer server = new (
                input,
                outputStream
            );
            Task<int> serverTask = server.RunAsync(CancellationToken.None);
            await WriteMessageAsync(inputWriter, initialize);
            await WriteMessageAsync(
                inputWriter,
                """
                {"jsonrpc":"2.0","method":"workspace/didChangeConfiguration","params":{"settings":{"mulang":{"configuration":{"compilationMode":"expression","environment":{"globals":[{"id":"current","name":"current","type":"int"}]}}}}}}
                """
            );
            await WriteMessageAsync(
                inputWriter,
                CreateOpenDocumentMessage(openUri, "current")
            );
            await WriteMessageAsync(inputWriter, ReferencesRequest(3));
            await Task.Delay(250);
            await File.WriteAllTextAsync(
                unopenedPath,
                "current + current"
            );
            await WriteMessageAsync(inputWriter, watchedChange);
            await WriteMessageAsync(inputWriter, ReferencesRequest(4));
            await WriteMessageAsync(
                inputWriter,
                """
                {"jsonrpc":"2.0","id":2,"method":"shutdown"}
                """
            );
            await WriteMessageAsync(
                inputWriter,
                """
                {"jsonrpc":"2.0","method":"exit"}
                """
            );
            Assert.That(
                await serverTask.WaitAsync(TimeSpan.FromSeconds(5)),
                Is.Zero
            );
            outputStream.Position = 0;
            IReadOnlyList<JsonElement> output =
                await ReadMessagesAsync(outputStream);
            IReadOnlyList<JsonElement> responses =
            [
                .. output.Where(
                    static message =>
                        message.TryGetProperty(
                            "id",
                            out JsonElement id
                        ) &&
                        id.GetInt32() is 3 or 4
                ),
            ];

            Assert.That(
                responses.Select(
                    static response =>
                        response
                            .GetProperty("result")
                            .GetArrayLength()
                ),
                Is.EqualTo([2, 3])
            );
        }
        finally
        {
            Directory.Delete(workspace, true);
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
    public async Task LoadsCommentedConfigurationFromWorkspaceFile()
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
                  // Documents are compiled as integer expressions.
                  "configuration": {
                    "compilationMode": "expression",
                    /* The expected type is validated by the compiler. */
                    "expectedResultType": "int",
                  },
                  "overrides": [],
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
    public async Task MergesConfigurationFilesWithLocalGlobs()
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
        string programDirectory = Path.Combine(
            nestedDirectory,
            "programs"
        );
        Directory.CreateDirectory(expressionDirectory);
        Directory.CreateDirectory(programDirectory);

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
                  },
                  "overrides": [
                    {
                      "includes": [
                        "nested/programs/*.mu"
                      ],
                      "configuration": {
                        "compilationMode": "program"
                      }
                    }
                  ]
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
            string programDocumentUri = new Uri(
                Path.Combine(programDirectory, "program.mu")
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
                CreateOpenDocumentMessage(programDocumentUri, "1"),
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
                    Is.Zero
                );
                Assert.That(
                    diagnostics[expressionDocumentUri].GetArrayLength(),
                    Is.Zero
                );
                Assert.That(
                    diagnostics[programDocumentUri].GetArrayLength(),
                    Is.GreaterThan(0)
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
                    out IReadOnlyList<Uri> initialFiles
                ),
                Is.True
            );
            Assert.That(initialFiles, Has.Count.EqualTo(1));
            Assert.That(
                initialFiles[0].LocalPath,
                Is.EqualTo(rootConfiguration)
            );

            File.WriteAllText(nestedConfiguration, "{}");
            resolver.ClearCache();

            Assert.That(
                resolver.TryResolve(
                    documentUri,
                    workspaceUri,
                    out IReadOnlyList<Uri> nestedFiles
                ),
                Is.True
            );
            Assert.That(nestedFiles, Has.Count.EqualTo(2));
            Assert.That(
                nestedFiles.Select(static file => file.LocalPath),
                Is.EqualTo(new[] { rootConfiguration, nestedConfiguration })
            );

            File.Delete(nestedConfiguration);
            resolver.ClearCache();

            Assert.That(
                resolver.TryResolve(
                    documentUri,
                    workspaceUri,
                    out IReadOnlyList<Uri> restoredFiles
                ),
                Is.True
            );
            Assert.That(restoredFiles, Has.Count.EqualTo(1));
            Assert.That(
                restoredFiles[0].LocalPath,
                Is.EqualTo(rootConfiguration)
            );
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ReloadsConfigurationChangesFromFallbackWorkspaceRoot(
        bool providesRootUri
    )
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
            Pipe inputPipe = new ();
            await using Stream input = inputPipe.Reader.AsStream();
            await using Stream inputWriter = inputPipe.Writer.AsStream();
            using MemoryStream output = new ();
            LanguageServer server = new (
                input,
                output,
                true,
                workspaceUri
            );
            Task<int> serverTask = server.RunAsync(CancellationToken.None);
            string initializeMessage = providesRootUri
                ? JsonSerializer.Serialize(
                    new
                    {
                        jsonrpc = "2.0",
                        id = 1,
                        method = "initialize",
                        @params = new { rootUri = workspaceUri },
                    }
                )
                : """
                  {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
                  """;

            await WriteMessageAsync(
                inputWriter,
                initializeMessage
            );
            await WriteMessageAsync(
                inputWriter,
                """
                {"jsonrpc":"2.0","method":"initialized","params":{}}
                """
            );
            await WriteMessageAsync(
                inputWriter,
                CreateOpenDocumentMessage(documentUri, "1")
            );
            await Task.Delay(500);
            await File.WriteAllTextAsync(
                configurationFile,
                """
                {
                  "configuration": {
                    "compilationMode": "program"
                  }
                }
                """
            );
            await Task.Delay(750);
            File.Delete(configurationFile);
            await Task.Delay(750);
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
            await Task.Delay(750);
            File.Delete(configurationFile);
            await Task.Delay(750);
            await WriteMessageAsync(
                inputWriter,
                """
                {"jsonrpc":"2.0","method":"shutdown","id":2}
                """
            );
            await WriteMessageAsync(
                inputWriter,
                """
                {"jsonrpc":"2.0","method":"exit"}
                """
            );
            Assert.That(
                await serverTask.WaitAsync(TimeSpan.FromSeconds(5)),
                Is.Zero
            );
            output.Position = 0;
            IReadOnlyList<JsonElement> messages = await ReadMessagesAsync(output);
            IReadOnlyList<JsonElement> diagnostics = messages
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

            using (Assert.EnterMultipleScope())
            {
                Assert.That(diagnostics, Has.Count.GreaterThanOrEqualTo(5));
                Assert.That(diagnostics[0].GetArrayLength(), Is.Zero);
                Assert.That(diagnostics[^1].GetArrayLength(), Is.GreaterThan(0));
                Assert.That(
                    diagnostics.Any(
                        static value => value.GetArrayLength() == 0
                    ),
                    Is.True
                );
                Assert.That(
                    diagnostics.Any(
                        static value => value.GetArrayLength() > 0
                    ),
                    Is.True
                );
            }
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
        return await ReadMessagesAsync(output);
    }

    private static async Task<IReadOnlyList<JsonElement>> ReadMessagesAsync(
        Stream output
    )
    {
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
