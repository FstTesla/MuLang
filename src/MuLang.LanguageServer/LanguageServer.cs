using MuLang.Compiler;
using MuLang.Core.Diagnostics;
using MuLang.Core.Text;
using System.Text.Json;

namespace MuLang.LanguageServer;

internal sealed class LanguageServer
{
    private static readonly StringComparer UriComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly IDictionary<string, DocumentSnapshot> documents =
        new Dictionary<string, DocumentSnapshot>(UriComparer);

    private readonly IDictionary<string, DocumentSnapshot> configurationDocuments =
        new Dictionary<string, DocumentSnapshot>(UriComparer);

    private readonly ISet<string> loadedConfigurationFiles =
        new HashSet<string>(UriComparer);

    private readonly ISet<string> configurationDiagnosticFiles =
        new HashSet<string>(UriComparer);

    private readonly IDictionary<int, IReadOnlyList<string?>> pendingConfigurationRequests =
        new Dictionary<int, IReadOnlyList<string?>>();

    private readonly IDictionary<string, int> latestConfigurationRequests =
        new Dictionary<string, int>(UriComparer);

    private readonly SemaphoreSlim stateGate = new (1, 1);
    private readonly SemanticDocumentCache semanticDocuments = new ();
    private readonly string? fallbackWorkspaceFolder;
    private readonly Stream input;
    private readonly WorkspaceConfigurationFile workspaceConfigurationFile = new ();
    private readonly WorkspaceConfigurationStore workspaceConfigurations = new ();
    private readonly WorkspaceSourceService workspaceSources = new ();
    private readonly LspMessageWriter writer;
    private bool exitRequested;
    private int nextRequestId = 1;
    private bool shutdownRequested;
    private SolutionConfigurationWatcher? solutionConfigurationWatcher;
    private bool supportsWorkspaceConfiguration;
    private bool supportsMarkdownHover;
    private bool supportsVisualStudioExtensions;
    private bool useVisualStudioClassifications;
    private bool supportsHierarchicalSymbols;
    private bool lineFoldingOnly;

    public LanguageServer(
        Stream input,
        Stream output,
        bool useVisualStudioClassifications = false,
        string? fallbackWorkspaceFolder = null
    )
    {
        this.input = input ?? throw new ArgumentNullException(nameof(input));
        writer = new LspMessageWriter(
            output ?? throw new ArgumentNullException(nameof(output))
        );
        this.useVisualStudioClassifications = useVisualStudioClassifications;
        this.fallbackWorkspaceFolder = fallbackWorkspaceFolder;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        try
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

                await stateGate.WaitAsync(cancellationToken);

                try
                {
                    await HandleMessageAsync(
                        message.RootElement,
                        cancellationToken
                    );
                }
                finally
                {
                    stateGate.Release();
                }
            }
        }
        finally
        {
            if (solutionConfigurationWatcher is not null)
            {
                await solutionConfigurationWatcher.DisposeAsync();
            }
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
            await HandleResponseAsync(message, cancellationToken);
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
                    supportsWorkspaceConfiguration =
                        SupportsWorkspaceConfiguration(parameters);
                    supportsMarkdownHover =
                        SupportsMarkdownHover(parameters);
                    supportsHierarchicalSymbols = SupportsTextDocumentCapability(
                        parameters, "documentSymbol", "hierarchicalDocumentSymbolSupport"
                    );
                    lineFoldingOnly = SupportsTextDocumentCapability(
                        parameters, "foldingRange", "lineFoldingOnly"
                    );
                    supportsVisualStudioExtensions =
                        parameters.ValueKind == JsonValueKind.Object &&
                        parameters.TryGetProperty(
                            "capabilities",
                            out JsonElement capabilities
                        ) &&
                        capabilities.ValueKind == JsonValueKind.Object &&
                        capabilities.TryGetProperty(
                            "_vs_supportsVisualStudioExtensions",
                            out JsonElement visualStudioExtensions
                        ) &&
                        visualStudioExtensions.ValueKind is
                            JsonValueKind.True or JsonValueKind.False &&
                        visualStudioExtensions.GetBoolean();
                    IReadOnlyCollection<string> initialWorkspaceFolders =
                        GetInitialWorkspaceFolders(parameters);

                    if (
                        initialWorkspaceFolders.Count == 0 &&
                        fallbackWorkspaceFolder is not null
                    )
                    {
                        initialWorkspaceFolders = [ fallbackWorkspaceFolder ];
                    }

                    workspaceConfigurations.Initialize(initialWorkspaceFolders);
                    semanticDocuments.InvalidateConfiguration();

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
                    StartSolutionConfigurationWatcher();

                    if (
                        !useVisualStudioClassifications &&
                        supportsWorkspaceConfiguration
                    )
                    {
                        await RequestWorkspaceConfigurationsAsync(
                            workspaceConfigurations.Folders.Count == 0
                                ? [ null ]
                                : [ .. workspaceConfigurations.Folders ],
                            cancellationToken
                        );
                    }

                    break;
                }

                case "workspace/didChangeConfiguration":
                {
                    if (supportsWorkspaceConfiguration)
                    {
                        await RequestWorkspaceConfigurationsAsync(
                            workspaceConfigurations.Folders.Count == 0
                                ? [ null ]
                                : [ .. workspaceConfigurations.Folders ],
                            cancellationToken
                        );
                    }
                    else
                    {
                        await ApplyPushedConfigurationAsync(
                            parameters,
                            cancellationToken
                        );
                    }

                    break;
                }

                case "workspace/didChangeWorkspaceFolders":
                {
                    await ChangeWorkspaceFoldersAsync(
                        parameters,
                        cancellationToken
                    );
                    break;
                }

                case "workspace/didChangeWatchedFiles":
                {
                    await ChangeWatchedFilesAsync(
                        parameters,
                        cancellationToken
                    );
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

                case "textDocument/didSave":
                {
                    string uri = parameters.GetProperty("textDocument")
                        .GetProperty("uri")
                        .GetString()!;

                    if (
                        WorkspaceConfigurationFile.IsConfigurationFile(uri) &&
                        await ApplyConfigurationFileAsync(
                            new Uri(uri),
                            true,
                            cancellationToken
                        )
                    )
                    {
                        await PublishAllDiagnosticsAsync(cancellationToken);
                    }

                    break;
                }

                case "textDocument/hover":
                {
                    if (hasId)
                    {
                        await WriteHoverAsync(id, parameters, cancellationToken);
                    }

                    break;
                }

                case "mulang/quickInfo":
                {
                    if (hasId)
                    {
                        await WriteQuickInfoAsync(
                            id,
                            parameters,
                            cancellationToken
                        );
                    }

                    break;
                }

                case "textDocument/documentHighlight":
                {
                    if (hasId)
                    {
                        await WriteDocumentHighlightsAsync(
                            id,
                            parameters,
                            cancellationToken
                        );
                    }

                    break;
                }

                case "textDocument/definition":
                {
                    if (hasId)
                    {
                        await WriteDefinitionAsync(
                            id,
                            parameters,
                            cancellationToken
                        );
                    }

                    break;
                }

                case "textDocument/references":
                {
                    if (hasId)
                    {
                        await WriteReferencesAsync(
                            id,
                            parameters,
                            cancellationToken
                        );
                    }

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

                case "textDocument/documentSymbol":
                {
                    if (hasId)
                    {
                        await WriteDocumentSymbolsAsync(id, parameters, cancellationToken);
                    }

                    break;
                }

                case "textDocument/foldingRange":
                {
                    if (hasId)
                    {
                        await WriteFoldingRangesAsync(id, parameters, cancellationToken);
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
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException
        )
        {
            if (hasId)
            {
                await writer.WriteErrorAsync(
                    id,
                    -32603,
                    exception.Message,
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
        DocumentSnapshot document = new (
            uri, version, text,
            documents.TryGetValue(uri, out DocumentSnapshot? previous) ? previous.LanguageId : null
        );

        if (WorkspaceConfigurationFile.IsConfigurationFile(uri))
        {
            configurationDocuments[uri] = document;
            await ValidateConfigurationDocumentAsync(document, cancellationToken);
            return;
        }

        documents[uri] = document;
        semanticDocuments.Invalidate(uri);
        await EnsureDocumentConfigurationFileAsync(
            document.Uri,
            cancellationToken
        );
        await PublishDiagnosticsAsync(document, cancellationToken);
    }

    private async Task CloseDocumentAsync(
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        string uri = parameters
            .GetProperty("textDocument")
            .GetProperty("uri")
            .GetString()!;

        if (configurationDocuments.Remove(uri))
        {
            await PublishConfigurationDiagnosticsAsync(
                uri,
                "",
                null,
                null,
                cancellationToken,
                true
            );

            if (
                await ApplyConfigurationFileAsync(
                    new Uri(uri),
                    true,
                    cancellationToken
                )
            )
            {
                await PublishAllDiagnosticsAsync(cancellationToken);
            }

            return;
        }

        bool isMuIr = IsMuIrDocument(uri);
        documents.Remove(uri);
        semanticDocuments.Invalidate(uri);

        if (isMuIr)
        {
            return;
        }

        await writer.WriteNotificationAsync(
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

    private async Task OpenDocumentAsync(
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        JsonElement textDocument = parameters.GetProperty("textDocument");
        DocumentSnapshot document = new (
            textDocument.GetProperty("uri").GetString()!,
            textDocument.GetProperty("version").GetInt32(),
            textDocument.GetProperty("text").GetString()!,
            textDocument.TryGetProperty("languageId", out JsonElement languageId) ? languageId.GetString() : null
        );

        if (WorkspaceConfigurationFile.IsConfigurationFile(document.Uri))
        {
            configurationDocuments[document.Uri] = document;
            await ValidateConfigurationDocumentAsync(document, cancellationToken);

            if (
                await EnsureDocumentConfigurationFileAsync(
                    document.Uri,
                    cancellationToken
                )
            )
            {
                await PublishAllDiagnosticsAsync(cancellationToken);
            }

            return;
        }

        documents[document.Uri] = document;
        semanticDocuments.Invalidate(document.Uri);
        await EnsureDocumentConfigurationFileAsync(
            document.Uri,
            cancellationToken
        );
        await PublishDiagnosticsAsync(document, cancellationToken);
    }

    private Task PublishDiagnosticsAsync(
        DocumentSnapshot document,
        CancellationToken cancellationToken
    )
    {
        if (IsMuIrDocument(document.Uri))
        {
            return Task.CompletedTask;
        }

        WorkspaceConfiguration configuration = workspaceConfigurations.Get(
            document.Uri
        );
        SemanticDocumentSnapshot snapshot = semanticDocuments.Get(
            document,
            configuration
        );

        return writer.WriteNotificationAsync(
            "textDocument/publishDiagnostics",
            result =>
            {
                result.WriteStartObject();
                result.WriteString("uri", document.Uri);
                result.WriteNumber("version", document.Version);
                result.WriteStartArray("diagnostics");

                foreach (
                    Diagnostic diagnostic
                    in snapshot.SemanticDocument.Diagnostics
                )
                {
                    WriteDiagnostic(
                        result,
                        snapshot.Source,
                        diagnostic
                    );
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
                result.WriteBoolean(
                    "hoverProvider",
                    !useVisualStudioClassifications
                );
                result.WriteBoolean("documentHighlightProvider", true);
                result.WriteBoolean("definitionProvider", true);
                result.WriteBoolean("referencesProvider", true);
                result.WriteBoolean("documentSymbolProvider", true);
                result.WriteBoolean("foldingRangeProvider", true);
                result.WriteStartObject("textDocumentSync");
                result.WriteBoolean("openClose", true);
                result.WriteNumber("change", 1);
                result.WriteBoolean("save", true);
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
                result.WriteStringValue(
                    useVisualStudioClassifications
                        ? "property name"
                        : "property"
                );
                result.WriteStringValue(useVisualStudioClassifications ? "label name" : "label");
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
                result.WriteStartObject("workspace");
                result.WriteStartObject("workspaceFolders");
                result.WriteBoolean("supported", true);
                result.WriteBoolean("changeNotifications", true);
                result.WriteEndObject();
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

    private async Task WriteHoverAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        (
            SemanticDocumentSnapshot? document,
            EditorHover? hover
        ) = await GetHoverAsync(parameters, cancellationToken);

        await writer.WriteResponseAsync(
            id,
            result =>
            {
                if (
                    document is null ||
                    hover is null
                )
                {
                    result.WriteNullValue();
                    return;
                }

                result.WriteStartObject();
                result.WriteStartObject("contents");
                result.WriteString("kind", supportsMarkdownHover ? "markdown" : "plaintext");
                result.WriteString(
                    "value",
                    supportsMarkdownHover
                        ? FormatMarkdownHover(hover.Items, document.SemanticDocument.LanguageId)
                        : hover.Content
                );
                result.WriteEndObject();
                WriteRange(
                    result,
                    "range",
                    document.Source,
                    hover.Span
                );
                result.WriteEndObject();
            },
            cancellationToken
        );
    }

    private async Task WriteQuickInfoAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        (
            SemanticDocumentSnapshot? document,
            EditorHover? hover
        ) = await GetHoverAsync(parameters, cancellationToken);

        await writer.WriteResponseAsync(
            id,
            result =>
            {
                if (
                    document is null ||
                    hover is null
                )
                {
                    result.WriteNullValue();
                    return;
                }

                result.WriteStartObject();
                result.WriteStartArray("items");

                foreach (EditorHoverItem item in hover.Items)
                {
                    result.WriteStartObject();
                    result.WriteString(
                        "category",
                        item.Category
                    );
                    result.WriteString("content", item.Content);
                    result.WriteStartArray("parts");

                    foreach (EditorHoverPart part in item.Parts)
                    {
                        result.WriteStartObject();
                        result.WriteString(
                            "kind",
                            part.Kind.ToString().ToLowerInvariant()
                        );
                        result.WriteString("text", part.Text);
                        result.WriteEndObject();
                    }

                    result.WriteEndArray();
                    result.WriteEndObject();
                }

                result.WriteEndArray();
                WriteRange(
                    result,
                    "range",
                    document.Source,
                    hover.Span
                );
                result.WriteEndObject();
            },
            cancellationToken
        );
    }

    private async Task<(
        SemanticDocumentSnapshot? Document,
        EditorHover? Hover
        )> GetHoverAsync(
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        (
            string uri,
            int line,
            int character
        ) = GetTextDocumentPosition(parameters);
        SemanticDocumentSnapshot? document =
            await GetOpenSemanticDocumentAsync(
                uri,
                cancellationToken
            );
        EditorHover? hover = document?.SemanticDocument.GetHover(
            EditorPositions.GetScalarOffset(document.Source, line, character)
        );

        return (document, hover);
    }

    private async Task WriteDocumentHighlightsAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        (
            string uri,
            int line,
            int character
        ) = GetTextDocumentPosition(parameters);
        SemanticDocumentSnapshot? document =
            await GetOpenSemanticDocumentAsync(
                uri,
                cancellationToken
            );
        IReadOnlyList<EditorOccurrence> highlights =
            document is null
                ? [ ]
                : document.SemanticDocument.GetHighlights(
                    EditorPositions.GetScalarOffset(document.Source, line, character)
                );

        await writer.WriteResponseAsync(
            id,
            result =>
            {
                if (
                    document is null ||
                    highlights.Count == 0
                )
                {
                    result.WriteNullValue();
                    return;
                }

                result.WriteStartArray();

                foreach (
                    EditorOccurrence occurrence in highlights
                )
                {
                    result.WriteStartObject();
                    WriteRange(
                        result,
                        "range",
                        document.Source,
                        occurrence.Span
                    );
                    result.WriteNumber(
                        "kind",
                        occurrence.Role switch
                        {
                            SemanticOccurrenceRole.Read => 2,
                            SemanticOccurrenceRole.Write => 3,
                            _ => 1,
                        }
                    );
                    result.WriteEndObject();
                }

                result.WriteEndArray();
            },
            cancellationToken
        );
    }

    private async Task WriteDefinitionAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        (
            string uri,
            int line,
            int character
        ) = GetTextDocumentPosition(parameters);
        SemanticDocumentSnapshot? document =
            await GetOpenSemanticDocumentAsync(
                uri,
                cancellationToken
            );
        EditorOccurrence? selected = document?.SemanticDocument.GetOccurrence(
            EditorPositions.GetScalarOffset(document.Source, line, character)
        );

        await writer.WriteResponseAsync(
            id,
            result =>
            {
                if (
                    document is null ||
                    selected?.DefinitionSpan is not { } definitionSpan
                )
                {
                    result.WriteNullValue();
                    return;
                }

                WriteLocation(
                    result,
                    document.Uri,
                    document.Source,
                    definitionSpan
                );
            },
            cancellationToken
        );
    }

    private async Task WriteReferencesAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        (
            string uri,
            int line,
            int character
        ) = GetTextDocumentPosition(parameters);
        bool includeDeclaration = parameters
            .GetProperty("context")
            .GetProperty("includeDeclaration")
            .GetBoolean();
        IReadOnlyCollection<string>? solutionDocumentUris = null;

        if (
            !IsMuIrDocument(uri) &&
            parameters.TryGetProperty(
                "_muLang_solutionDocumentUris",
                out JsonElement solutionDocuments
            )
        )
        {
            IList<string> uris = new List<string>();

            foreach (JsonElement document in solutionDocuments.EnumerateArray())
            {
                if (
                    !Uri.TryCreate(
                        document.GetString(),
                        UriKind.Absolute,
                        out Uri? documentUri
                    ) ||
                    !documentUri.IsFile
                )
                {
                    throw new InvalidOperationException(
                        "Solution documents must have absolute file URIs."
                    );
                }

                uris.Add(documentUri.AbsoluteUri);
            }

            solutionDocumentUris = [ .. uris ];
        }

        SemanticDocumentSnapshot? origin =
            await GetOpenSemanticDocumentAsync(
                uri,
                cancellationToken
            );
        EditorOccurrence? selected = origin?.SemanticDocument.GetOccurrence(
            EditorPositions.GetScalarOffset(origin.Source, line, character)
        );
        IList<(
            SemanticDocumentSnapshot Document,
            EditorOccurrence Occurrence
            )> references = [ ];

        if (
            origin is not null &&
            selected?.Symbol is { } symbol &&
            (
                origin.SemanticDocument.LanguageId == "muir" ||
                solutionDocumentUris is null ||
                solutionDocumentUris.Contains(origin.Uri, UriComparer)
            )
        )
        {
            if (symbol is SemanticSymbolKey { IsWorkspaceStable: true })
            {
                string? originFolder =
                    workspaceConfigurations.GetFolder(origin.Uri);
                IReadOnlyCollection<DocumentSnapshot> openDocuments =
                    originFolder is null
                        ? [ documents[origin.Uri] ]
                        :
                        [
                            .. documents.Values.Where(
                                document =>
                                    !IsMuIrDocument(document.Uri) &&
                                    workspaceConfigurations.GetFolder(
                                        document.Uri
                                    ) is not null
                            ),
                        ];
                IReadOnlyCollection<string> workspaceFolders =
                    originFolder is null
                        ? [ ]
                        : workspaceConfigurations.Folders;
                IReadOnlyList<string> documentUris =
                    WorkspaceSourceService.GetDocumentUris(
                        openDocuments,
                        workspaceFolders,
                        cancellationToken,
                        solutionDocumentUris
                    );

                foreach (string documentUri in documentUris)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await EnsureDocumentConfigurationFileAsync(
                        documentUri,
                        cancellationToken
                    );
                    SemanticDocumentSnapshot? document =
                        await GetSemanticDocumentAsync(
                            documentUri,
                            cancellationToken
                        );

                    if (document is null)
                    {
                        continue;
                    }

                    foreach (
                        EditorOccurrence occurrence
                        in document.SemanticDocument
                            .GetOccurrences(symbol)
                    )
                    {
                        if (
                            includeDeclaration ||
                            occurrence.Role !=
                            SemanticOccurrenceRole.Declaration
                        )
                        {
                            references.Add((document, occurrence));
                        }
                    }
                }
            }
            else
            {
                foreach (
                    EditorOccurrence occurrence
                    in origin.SemanticDocument.GetOccurrences(symbol)
                )
                {
                    if (
                        includeDeclaration ||
                        occurrence.Role !=
                        SemanticOccurrenceRole.Declaration
                    )
                    {
                        references.Add((origin, occurrence));
                    }
                }
            }
        }

        await writer.WriteResponseAsync(
            id,
            result =>
            {
                if (origin is null || selected?.Symbol is null)
                {
                    result.WriteNullValue();
                    return;
                }

                result.WriteStartArray();
                int referenceId = 0;

                foreach (
                    (
                        SemanticDocumentSnapshot document,
                        EditorOccurrence occurrence
                    ) in references
                        .OrderBy(static item => item.Document.Uri, UriComparer)
                        .ThenBy(
                            static item =>
                                item.Occurrence.Span.Start
                        )
                )
                {
                    WriteReference(
                        result,
                        document,
                        occurrence,
                        referenceId++
                    );
                }

                result.WriteEndArray();
            },
            cancellationToken
        );
    }

    private void WriteReference(
        Utf8JsonWriter writer,
        SemanticDocumentSnapshot document,
        EditorOccurrence occurrence,
        int id
    )
    {
        if (!supportsVisualStudioExtensions)
        {
            WriteLocation(
                writer,
                document.Uri,
                document.Source,
                occurrence.Span
            );
            return;
        }

        SourceText source = document.Source;
        int line = source.GetPosition(occurrence.Span.Start).Line - 1;
        int lineStart = source.LineMap.LineStarts[line];
        int lineEnd = line + 1 < source.LineCount
            ? source.LineMap.LineStarts[line + 1]
            : source.Length;
        writer.WriteStartObject();
        writer.WriteNumber("_vs_id", id);
        writer.WritePropertyName("_vs_location");
        WriteLocation(writer, document.Uri, source, occurrence.Span);
        writer.WriteNumber("_vs_resolutionStatus", 0);
        writer.WriteStartArray("_vs_kind");
        writer.WriteNumberValue(
            occurrence.Role switch
            {
                SemanticOccurrenceRole.Declaration => 14,
                SemanticOccurrenceRole.Read => 3,
                SemanticOccurrenceRole.Write => 4,
                SemanticOccurrenceRole.Reference => 5,
                _ => throw new ArgumentOutOfRangeException(nameof(occurrence)),
            }
        );
        writer.WriteEndArray();
        writer.WriteString(
            "_vs_text",
            source.GetText(TextSpan.FromBounds(lineStart, lineEnd))
                .TrimEnd('\r', '\n')
        );
        writer.WriteEndObject();
    }

    private static (
        string Uri,
        int Line,
        int Character
        ) GetTextDocumentPosition(JsonElement parameters)
    {
        string uri = parameters
                .GetProperty("textDocument")
                .GetProperty("uri")
                .GetString() ??
            throw new FormatException("Missing document URI.");
        JsonElement position = parameters.GetProperty("position");
        int line = position.GetProperty("line").GetInt32();
        int character = position.GetProperty("character").GetInt32();

        if (line < 0 || character < 0)
        {
            throw new FormatException("Invalid document position.");
        }

        return (uri, line, character);
    }

    private async Task<SemanticDocumentSnapshot?>
        GetOpenSemanticDocumentAsync(
            string uri,
            CancellationToken cancellationToken
        )
    {
        await EnsureDocumentConfigurationFileAsync(
            uri,
            cancellationToken
        );

        return documents.TryGetValue(
            uri,
            out DocumentSnapshot? document
        )
            ? semanticDocuments.Get(
                document,
                IsMuIrDocument(uri) ? null : workspaceConfigurations.Get(uri)
            )
            : null;
    }

    private async Task<SemanticDocumentSnapshot?> GetSemanticDocumentAsync(
        string uri,
        CancellationToken cancellationToken
    )
    {
        if (documents.TryGetValue(uri, out DocumentSnapshot? document))
        {
            return semanticDocuments.Get(
                document,
                IsMuIrDocument(uri) ? null : workspaceConfigurations.Get(uri)
            );
        }

        return await semanticDocuments.GetFileAsync(
            uri,
            IsMuIrDocument(uri) ? null : workspaceConfigurations.Get(uri),
            cancellationToken
        );
    }

    private async Task WriteSemanticTokensAsync(
        JsonElement id,
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        string uri = parameters
            .GetProperty("textDocument")
            .GetProperty("uri")
            .GetString()!;

        await EnsureDocumentConfigurationFileAsync(uri, cancellationToken);
        SemanticDocumentSnapshot? document =
            await GetOpenSemanticDocumentAsync(
                uri,
                cancellationToken
            );
        await writer.WriteResponseAsync(
            id,
            result =>
            {
                result.WriteStartObject();
                result.WriteStartArray("data");

                if (document is not null)
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

    private async Task HandleResponseAsync(
        JsonElement message,
        CancellationToken cancellationToken
    )
    {
        if (
            !message.TryGetProperty("id", out JsonElement idElement) ||
            idElement.ValueKind != JsonValueKind.Number ||
            !idElement.TryGetInt32(out int id) ||
            !pendingConfigurationRequests.Remove(
                id,
                out IReadOnlyList<string?>? folders
            )
        )
        {
            return;
        }

        if (message.TryGetProperty("error", out JsonElement error))
        {
            string errorMessage = error.TryGetProperty(
                "message",
                out JsonElement errorMessageElement
            )
                ? errorMessageElement.GetString() ?? "Unknown client error."
                : "Unknown client error.";
            await ShowConfigurationErrorAsync(errorMessage, cancellationToken);
            return;
        }

        if (
            !message.TryGetProperty("result", out JsonElement result) ||
            result.ValueKind != JsonValueKind.Array ||
            result.GetArrayLength() != folders.Count
        )
        {
            await ShowConfigurationErrorAsync(
                "workspace/configuration returned an invalid result.",
                cancellationToken
            );
            return;
        }

        bool changed = false;

        for (int index = 0; index < folders.Count; index++)
        {
            string key = GetConfigurationRequestKey(folders[index]);

            if (
                !latestConfigurationRequests.TryGetValue(key, out int latestId) ||
                latestId != id
            )
            {
                continue;
            }

            changed |= await ApplyConfigurationAsync(
                folders[index],
                result[index],
                cancellationToken
            );
        }

        if (changed)
        {
            await PublishAllDiagnosticsAsync(cancellationToken);
        }
    }

    private async Task ApplyPushedConfigurationAsync(
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        JsonElement configuration = parameters.TryGetProperty(
            "settings",
            out JsonElement settings
        )
            ? settings
            : parameters;

        if (
            configuration.ValueKind == JsonValueKind.Object &&
            configuration.TryGetProperty("mulang", out JsonElement mulang)
        )
        {
            configuration = mulang;
        }

        IReadOnlyList<string?> folders = workspaceConfigurations.Folders.Count == 0
            ? [ null ]
            : [ .. workspaceConfigurations.Folders ];
        bool changed = false;

        foreach (string? folder in folders)
        {
            changed |= await ApplyConfigurationAsync(
                folder,
                configuration,
                cancellationToken
            );
        }

        if (changed)
        {
            await PublishAllDiagnosticsAsync(cancellationToken);
        }
    }

    private async Task<bool> ApplyConfigurationAsync(
        string? folder,
        JsonElement element,
        CancellationToken cancellationToken
    )
    {
        try
        {
            WorkspaceConfigurationSet configuration =
                WorkspaceConfigurationSetParser.Parse(element);
            workspaceConfigurations.Set(folder, configuration);
            semanticDocuments.InvalidateConfiguration();
            return true;
        }
        catch (Exception exception) when (
            exception is FormatException or
                IOException or
                JsonException or
                UnauthorizedAccessException
        )
        {
            string scope = folder is null ? "the workspace" : $"'{folder}'";
            await ShowConfigurationErrorAsync(
                $"Invalid MuLang configuration for {scope}: {exception.Message}",
                cancellationToken
            );
            return false;
        }
    }

    private async Task<bool> ApplyConfigurationFileAsync(
        Uri file,
        bool force,
        CancellationToken cancellationToken
    )
    {
        if (
            !force &&
            loadedConfigurationFiles.Contains(file.AbsoluteUri)
        )
        {
            return false;
        }

        if (!File.Exists(file.LocalPath))
        {
            loadedConfigurationFiles.Remove(file.AbsoluteUri);

            if (!configurationDocuments.ContainsKey(file.AbsoluteUri))
            {
                await PublishConfigurationDiagnosticsAsync(
                    file.AbsoluteUri,
                    "",
                    null,
                    null,
                    cancellationToken
                );
            }

            bool removed = workspaceConfigurations.RemoveConfigurationFile(
                file.AbsoluteUri
            );

            if (removed)
            {
                semanticDocuments.InvalidateConfiguration();
            }

            return removed;
        }

        string json;

        try
        {
            json = await File.ReadAllTextAsync(
                file.LocalPath,
                cancellationToken
            );
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException
        )
        {
            await ShowConfigurationErrorAsync(
                $"Could not read MuLang configuration file '{file.LocalPath}': {exception.Message}",
                cancellationToken
            );
            return false;
        }
        (
            WorkspaceConfigurationSet? configuration,
            Diagnostic? diagnostic
        ) = WorkspaceConfigurationValidation.Validate(json);
        loadedConfigurationFiles.Add(file.AbsoluteUri);

        if (!configurationDocuments.ContainsKey(file.AbsoluteUri))
        {
            await PublishConfigurationDiagnosticsAsync(
                file.AbsoluteUri,
                json,
                null,
                diagnostic,
                cancellationToken
            );
        }

        if (
            configuration is null ||
            !useVisualStudioClassifications ||
            workspaceConfigurations.GetFolder(file.AbsoluteUri) is null
        )
        {
            return false;
        }

        workspaceConfigurations.SetConfigurationFile(
            file.AbsoluteUri,
            configuration
        );
        semanticDocuments.InvalidateConfiguration();
        return true;
    }

    private Task ValidateConfigurationDocumentAsync(
        DocumentSnapshot document,
        CancellationToken cancellationToken
    )
    {
        (_, Diagnostic? diagnostic) =
            WorkspaceConfigurationValidation.Validate(document.Text);
        return PublishConfigurationDiagnosticsAsync(
            document.Uri,
            document.Text,
            document.Version,
            diagnostic,
            cancellationToken
        );
    }

    private Task PublishConfigurationDiagnosticsAsync(
        string uri,
        string text,
        int? version,
        Diagnostic? diagnostic,
        CancellationToken cancellationToken,
        bool force = false
    )
    {
        bool hadDiagnostics = configurationDiagnosticFiles.Remove(uri);

        if (diagnostic is not null)
        {
            configurationDiagnosticFiles.Add(uri);
        }
        else if (!hadDiagnostics && version is null && !force)
        {
            return Task.CompletedTask;
        }

        return writer.WriteNotificationAsync(
            "textDocument/publishDiagnostics",
            result =>
            {
                result.WriteStartObject();
                result.WriteString("uri", uri);

                if (version is { } documentVersion)
                {
                    result.WriteNumber("version", documentVersion);
                }

                result.WriteStartArray("diagnostics");

                if (diagnostic is not null)
                {
                    WriteDiagnostic(result, SourceText.From(text), diagnostic);
                }

                result.WriteEndArray();
                result.WriteEndObject();
            },
            cancellationToken
        );
    }

    private async Task<bool> EnsureDocumentConfigurationFileAsync(
        string document,
        CancellationToken cancellationToken
    )
    {
        if (!useVisualStudioClassifications || IsMuIrDocument(document))
        {
            return false;
        }

        try
        {
            string? folder = workspaceConfigurations.GetFolder(document);

            if (
                folder is null ||
                !workspaceConfigurationFile.TryResolve(
                    document,
                    folder,
                    out IReadOnlyList<Uri> files
                )
            )
            {
                return false;
            }

            bool changed = false;

            foreach (Uri file in files)
            {
                changed |= await ApplyConfigurationFileAsync(
                    file,
                    false,
                    cancellationToken
                );
            }

            return changed;
        }
        catch (Exception exception) when (
            exception is FormatException or
                IOException or
                JsonException or
                UnauthorizedAccessException
        )
        {
            await ShowConfigurationErrorAsync(
                $"Invalid MuLang configuration for '{document}': {exception.Message}",
                cancellationToken
            );
            return false;
        }
    }

    private Task ChangeWatchedFilesAsync(
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> files =
        [
            .. parameters
                .GetProperty("changes")
                .EnumerateArray()
                .Select(
                    static change =>
                        change.GetProperty("uri").GetString()!
                ),
        ];

        foreach (string file in files)
        {
            if (!WorkspaceConfigurationFile.IsConfigurationFile(file))
            {
                semanticDocuments.Invalidate(file);
            }
        }

        return ChangeConfigurationFilesAsync(
            files,
            cancellationToken
        );
    }

    private async Task ChangeConfigurationFilesAsync(
        IReadOnlyCollection<string> files,
        CancellationToken cancellationToken
    )
    {
        bool changed = false;
        bool configurationFileChanged = false;

        foreach (string file in files)
        {
            if (
                !WorkspaceConfigurationFile.IsConfigurationFile(file) ||
                workspaceConfigurations.GetFolder(file) is null
            )
            {
                continue;
            }

            configurationFileChanged = true;
            Uri fileUri = new (file, UriKind.Absolute);

            try
            {
                changed |= await ApplyConfigurationFileAsync(
                    fileUri,
                    true,
                    cancellationToken
                );
            }
            catch (Exception exception) when (
                exception is FormatException or
                    IOException or
                    JsonException or
                    UnauthorizedAccessException
            )
            {
                await ShowConfigurationErrorAsync(
                    $"Invalid MuLang configuration file '{fileUri.LocalPath}': {exception.Message}",
                    cancellationToken
                );
            }
        }

        if (configurationFileChanged)
        {
            workspaceConfigurationFile.ClearCache();
            semanticDocuments.InvalidateConfiguration();

            foreach (DocumentSnapshot document in documents.Values)
            {
                changed |= await EnsureDocumentConfigurationFileAsync(
                    document.Uri,
                    cancellationToken
                );
            }
        }

        if (changed)
        {
            await PublishAllDiagnosticsAsync(cancellationToken);
        }
    }

    private void StartSolutionConfigurationWatcher()
    {
        if (
            fallbackWorkspaceFolder is null ||
            solutionConfigurationWatcher is not null
        )
        {
            return;
        }

        Uri root = new (fallbackWorkspaceFolder, UriKind.Absolute);
        solutionConfigurationWatcher = new SolutionConfigurationWatcher(
            root.LocalPath,
            ProcessSolutionConfigurationChangesAsync
        );
    }

    private async Task ProcessSolutionConfigurationChangesAsync(
        IReadOnlyCollection<string> files,
        CancellationToken cancellationToken
    )
    {
        await stateGate.WaitAsync(cancellationToken);

        try
        {
            await ChangeConfigurationFilesAsync(
                [
                    .. files.Select(
                        static file => new Uri(file).AbsoluteUri
                    ),
                ],
                cancellationToken
            );
        }
        finally
        {
            stateGate.Release();
        }
    }

    private async Task ChangeWorkspaceFoldersAsync(
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        JsonElement eventElement = parameters.GetProperty("event");

        if (
            eventElement.TryGetProperty("removed", out JsonElement removedFolders)
        )
        {
            foreach (JsonElement folder in removedFolders.EnumerateArray())
            {
                workspaceConfigurations.RemoveFolder(
                    folder.GetProperty("uri").GetString()!
                );
            }
        }

        foreach (
            string file in loadedConfigurationFiles
                .Where(
                    file => workspaceConfigurations.GetFolder(file) is null
                )
                .ToArray()
        )
        {
            loadedConfigurationFiles.Remove(file);

            if (!configurationDocuments.ContainsKey(file))
            {
                await PublishConfigurationDiagnosticsAsync(
                    file,
                    "",
                    null,
                    null,
                    cancellationToken
                );
            }
        }

        IList<string?> addedFolderUris = new List<string?>();

        if (eventElement.TryGetProperty("added", out JsonElement addedFolders))
        {
            foreach (JsonElement folder in addedFolders.EnumerateArray())
            {
                string uri = folder.GetProperty("uri").GetString()!;
                workspaceConfigurations.AddFolder(uri);
                addedFolderUris.Add(uri);
            }
        }

        workspaceConfigurationFile.ClearCache();
        loadedConfigurationFiles.Clear();
        semanticDocuments.InvalidateConfiguration();

        if (
            !useVisualStudioClassifications &&
            supportsWorkspaceConfiguration &&
            addedFolderUris.Count > 0
        )
        {
            await RequestWorkspaceConfigurationsAsync(
                [ .. addedFolderUris ],
                cancellationToken
            );
        }

        if (useVisualStudioClassifications)
        {
            foreach (DocumentSnapshot document in documents.Values)
            {
                await EnsureDocumentConfigurationFileAsync(
                    document.Uri,
                    cancellationToken
                );
            }
        }

        await PublishAllDiagnosticsAsync(cancellationToken);
    }

    private Task RequestWorkspaceConfigurationsAsync(
        IReadOnlyList<string?> folders,
        CancellationToken cancellationToken
    )
    {
        int id = nextRequestId++;
        pendingConfigurationRequests.Add(id, folders);

        foreach (string? folder in folders)
        {
            latestConfigurationRequests[GetConfigurationRequestKey(folder)] = id;
        }

        return writer.WriteRequestAsync(
            id,
            "workspace/configuration",
            parameters =>
            {
                parameters.WriteStartObject();
                parameters.WriteStartArray("items");

                foreach (string? folder in folders)
                {
                    parameters.WriteStartObject();

                    if (folder is null)
                    {
                        parameters.WriteNull("scopeUri");
                    }
                    else
                    {
                        parameters.WriteString("scopeUri", folder);
                    }

                    parameters.WriteString("section", "mulang");
                    parameters.WriteEndObject();
                }

                parameters.WriteEndArray();
                parameters.WriteEndObject();
            },
            cancellationToken
        );
    }

    private async Task PublishAllDiagnosticsAsync(
        CancellationToken cancellationToken
    )
    {
        foreach (DocumentSnapshot document in documents.Values)
        {
            await PublishDiagnosticsAsync(document, cancellationToken);
        }
    }

    private Task ShowConfigurationErrorAsync(
        string message,
        CancellationToken cancellationToken
    )
    {
        return writer.WriteNotificationAsync(
            "window/showMessage",
            parameters =>
            {
                parameters.WriteStartObject();
                parameters.WriteNumber("type", 1);
                parameters.WriteString("message", message);
                parameters.WriteEndObject();
            },
            cancellationToken
        );
    }

    private static void WriteSemanticTokenData(
        Utf8JsonWriter writer,
        SemanticDocumentSnapshot document,
        bool useVisualStudioClassifications
    )
    {
        int previousLine = 0;
        int previousCharacter = 0;

        foreach (
            EditorOccurrence occurrence
            in document.SemanticDocument.Occurrences
                .Where(
                    static occurrence =>
                        occurrence.IsClassification
                )
        )
        {
            TextPosition start = document.Source.GetPosition(
                occurrence.Span.Start
            );
            TextPosition end = document.Source.GetPosition(
                occurrence.Span.End
            );

            if (start.Line != end.Line)
            {
                continue;
            }

            int line = start.Line - 1;
            int startCharacter = GetUtf16Character(document.Source, start);
            int endCharacter = GetUtf16Character(document.Source, end);
            int deltaLine = line - previousLine;
            int deltaStart = deltaLine == 0
                ? startCharacter - previousCharacter
                : startCharacter;

            writer.WriteNumberValue(deltaLine);
            writer.WriteNumberValue(deltaStart);
            writer.WriteNumberValue(endCharacter - startCharacter);
            writer.WriteNumberValue((int)occurrence.Kind);
            writer.WriteNumberValue(
                useVisualStudioClassifications
                    ? 0
                    : (int)occurrence.Modifiers
            );
            previousLine = line;
            previousCharacter = startCharacter;
        }
    }

    private static IReadOnlyCollection<string> GetInitialWorkspaceFolders(
        JsonElement parameters
    )
    {
        IList<string> folders = new List<string>();

        if (
            parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty(
                "workspaceFolders",
                out JsonElement workspaceFolders
            ) &&
            workspaceFolders.ValueKind == JsonValueKind.Array
        )
        {
            foreach (JsonElement folder in workspaceFolders.EnumerateArray())
            {
                folders.Add(folder.GetProperty("uri").GetString()!);
            }
        }

        if (
            folders.Count == 0 &&
            parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty("rootUri", out JsonElement rootUri) &&
            rootUri.ValueKind == JsonValueKind.String
        )
        {
            folders.Add(rootUri.GetString()!);
        }

        return [ .. folders ];
    }

    private static string GetConfigurationRequestKey(string? folder)
    {
        if (folder is null)
        {
            return "";
        }

        return folder.EndsWith("/", StringComparison.Ordinal)
            ? folder
            : $"{folder}/";
    }

    private static bool SupportsMarkdownHover(JsonElement parameters)
    {
        return
            parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty("capabilities", out JsonElement capabilities) &&
            capabilities.ValueKind == JsonValueKind.Object &&
            capabilities.TryGetProperty("textDocument", out JsonElement textDocument) &&
            textDocument.ValueKind == JsonValueKind.Object &&
            textDocument.TryGetProperty("hover", out JsonElement hover) &&
            hover.ValueKind == JsonValueKind.Object &&
            hover.TryGetProperty("contentFormat", out JsonElement formats) &&
            formats.ValueKind == JsonValueKind.Array &&
            formats.EnumerateArray().Any(
                static format =>
                    format.ValueKind == JsonValueKind.String &&
                    format.GetString() == "markdown"
            );
    }

    private bool IsMuIrDocument(string uri) =>
        MuIrEditorDocument.IsMuIr(uri) ||
        documents.TryGetValue(uri, out DocumentSnapshot? document) && document.LanguageId == "muir";

    private static bool SupportsTextDocumentCapability(JsonElement parameters, string feature, string capability) =>
        parameters.ValueKind == JsonValueKind.Object &&
        parameters.TryGetProperty("capabilities", out JsonElement capabilities) &&
        capabilities.ValueKind == JsonValueKind.Object &&
        capabilities.TryGetProperty("textDocument", out JsonElement textDocument) &&
        textDocument.ValueKind == JsonValueKind.Object &&
        textDocument.TryGetProperty(feature, out JsonElement featureCapabilities) &&
        featureCapabilities.ValueKind == JsonValueKind.Object &&
        featureCapabilities.TryGetProperty(capability, out JsonElement value) &&
        value.ValueKind == JsonValueKind.True;

    private async Task WriteDocumentSymbolsAsync(JsonElement id, JsonElement parameters, CancellationToken cancellationToken)
    {
        string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString()!;
        SemanticDocumentSnapshot? document = await GetOpenSemanticDocumentAsync(uri, cancellationToken);

        await writer.WriteResponseAsync(
            id,
            result =>
            {
                result.WriteStartArray();

                if (document is not null)
                {
                    foreach (EditorDocumentSymbol symbol in document.SemanticDocument.Symbols)
                    {
                        WriteSymbol(result, document, symbol, null);
                    }
                }

                result.WriteEndArray();
            },
            cancellationToken
        );
    }

    private void WriteSymbol(
        Utf8JsonWriter result,
        SemanticDocumentSnapshot document,
        EditorDocumentSymbol symbol,
        string? containerName
    )
    {
        result.WriteStartObject();
        result.WriteString("name", symbol.Name);
        result.WriteNumber("kind", symbol.Kind);

        if (supportsHierarchicalSymbols)
        {
            WriteRange(result, "range", document.Source, symbol.Range);
            WriteRange(result, "selectionRange", document.Source, symbol.SelectionRange);
            result.WriteStartArray("children");

            foreach (EditorDocumentSymbol child in symbol.Children)
            {
                WriteSymbol(result, document, child, symbol.Name);
            }

            result.WriteEndArray();
        }
        else
        {
            result.WritePropertyName("location");
            WriteLocation(result, document.Uri, document.Source, symbol.Range);

            if (containerName is not null)
            {
                result.WriteString("containerName", containerName);
            }
        }

        result.WriteEndObject();

        if (!supportsHierarchicalSymbols)
        {
            foreach (EditorDocumentSymbol child in symbol.Children)
            {
                WriteSymbol(result, document, child, symbol.Name);
            }
        }
    }

    private async Task WriteFoldingRangesAsync(JsonElement id, JsonElement parameters, CancellationToken cancellationToken)
    {
        string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString()!;
        SemanticDocumentSnapshot? document = await GetOpenSemanticDocumentAsync(uri, cancellationToken);

        await writer.WriteResponseAsync(
            id,
            result =>
            {
                result.WriteStartArray();

                if (document is not null)
                {
                    foreach (TextSpan range in document.SemanticDocument.FoldingRanges)
                    {
                        TextPosition start = document.Source.GetPosition(range.Start);
                        TextPosition end = document.Source.GetPosition(range.End);
                        int endLine = end.Line - 1 - (lineFoldingOnly ? 1 : 0);

                        if (endLine <= start.Line - 1)
                        {
                            continue;
                        }

                        result.WriteStartObject();
                        result.WriteNumber("startLine", start.Line - 1);
                        result.WriteNumber("endLine", endLine);

                        if (!lineFoldingOnly)
                        {
                            result.WriteNumber("startCharacter", GetUtf16Character(document.Source, start));
                            result.WriteNumber("endCharacter", GetUtf16Character(document.Source, end) - 1);
                        }

                        result.WriteEndObject();
                    }
                }

                result.WriteEndArray();
            },
            cancellationToken
        );
    }

    private static bool SupportsWorkspaceConfiguration(JsonElement parameters)
    {
        return
            parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty(
                "capabilities",
                out JsonElement capabilities
            ) &&
            capabilities.ValueKind == JsonValueKind.Object &&
            capabilities.TryGetProperty(
                "workspace",
                out JsonElement workspace
            ) &&
            workspace.ValueKind == JsonValueKind.Object &&
            workspace.TryGetProperty(
                "configuration",
                out JsonElement configuration
            ) &&
            configuration.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            configuration.GetBoolean();
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

    private static void WriteLocation(
        Utf8JsonWriter writer,
        string uri,
        SourceText source,
        TextSpan span
    )
    {
        writer.WriteStartObject();
        writer.WriteString("uri", uri);
        WriteRange(writer, "range", source, span);
        writer.WriteEndObject();
    }

    private static void WriteRange(
        Utf8JsonWriter writer,
        string propertyName,
        SourceText source,
        TextSpan span
    )
    {
        writer.WriteStartObject(propertyName);
        WritePosition(
            writer,
            "start",
            source,
            source.GetPosition(span.Start)
        );
        WritePosition(
            writer,
            "end",
            source,
            source.GetPosition(span.End)
        );
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

    private static string FormatMarkdownHover(
        IReadOnlyList<EditorHoverItem> items,
        string languageId
    )
    {
        return string.Join(
            "\n\n",
            items.Select(
                item =>
                    item.Content.Contains('\n')
                        ? $"*({item.Category})*\n```{languageId}\n{item.Content}\n```"
                        : $"*({item.Category})* {EscapeMarkdown(item.Content)}"
            )
        );
    }

    private static string EscapeMarkdown(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
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
