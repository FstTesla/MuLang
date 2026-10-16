using MuLang.Compiler;
using MuLang.Core;
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
        new Dictionary<string, DocumentSnapshot>(StringComparer.Ordinal);

    private readonly IDictionary<int, IReadOnlyList<string?>> pendingConfigurationRequests =
        new Dictionary<int, IReadOnlyList<string?>>();

    private readonly IDictionary<string, int> latestConfigurationRequests =
        new Dictionary<string, int>(UriComparer);

    private readonly SemaphoreSlim stateGate = new (1, 1);
    private readonly string? fallbackWorkspaceFolder;
    private readonly Stream input;
    private readonly WorkspaceConfigurationFile workspaceConfigurationFile = new ();
    private readonly WorkspaceConfigurationStore workspaceConfigurations = new ();
    private readonly LspMessageWriter writer;
    private bool exitRequested;
    private int nextRequestId = 1;
    private bool shutdownRequested;
    private SolutionConfigurationWatcher? solutionConfigurationWatcher;
    private bool supportsWorkspaceConfiguration;
    private bool useVisualStudioClassifications;

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
        await EnsureDocumentConfigurationFileAsync(
            document.Uri,
            cancellationToken
        );
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

    private async Task OpenDocumentAsync(
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
        WorkspaceConfiguration configuration = workspaceConfigurations.Get(
            document.Uri
        );
        SourceText source = SourceText.From(document.Text);
        AnalysisResult analysis = MuLangCompiler.Analyze(
            document.Text,
            configuration.Environment,
            configuration.CompilationMode,
            new MuLangCompilationOptions
            {
                ExpectedResultType = configuration.ExpectedResultType,
                Profile = configuration.Profile,
                SourceDocument = Uri.TryCreate(
                    document.Uri,
                    UriKind.Absolute,
                    out Uri? sourceDocument
                )
                    ? sourceDocument
                    : null,
            }
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
        await writer.WriteResponseAsync(
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
                        workspaceConfigurations.Get(document.Uri),
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
            workspaceConfigurations.ContainsConfigurationFile(file.AbsoluteUri)
        )
        {
            return false;
        }

        if (!File.Exists(file.LocalPath))
        {
            return workspaceConfigurations.RemoveConfigurationFile(
                file.AbsoluteUri
            );
        }

        string json = await File.ReadAllTextAsync(
            file.LocalPath,
            cancellationToken
        );
        using JsonDocument document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            }
        );
        WorkspaceConfigurationSet configuration =
            WorkspaceConfigurationSetParser.Parse(document.RootElement);
        workspaceConfigurations.SetConfigurationFile(
            file.AbsoluteUri,
            configuration
        );
        return true;
    }

    private async Task<bool> EnsureDocumentConfigurationFileAsync(
        string document,
        CancellationToken cancellationToken
    )
    {
        if (!useVisualStudioClassifications)
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

    private async Task ChangeWatchedFilesAsync(
        JsonElement parameters,
        CancellationToken cancellationToken
    )
    {
        await ChangeConfigurationFilesAsync(
            [
                .. parameters
                    .GetProperty("changes")
                    .EnumerateArray()
                    .Select(
                        static change =>
                            change.GetProperty("uri").GetString()!
                    ),
            ],
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
                !workspaceConfigurationFile.IsConfigurationFile(file) ||
                workspaceConfigurations.GetFolder(file) is null
            )
            {
                continue;
            }

            configurationFileChanged = true;
            Uri fileUri = new (file, UriKind.Absolute);

            if (!File.Exists(fileUri.LocalPath))
            {
                changed |= workspaceConfigurations.RemoveConfigurationFile(file);
                continue;
            }

            if (!workspaceConfigurations.ContainsConfigurationFile(file))
            {
                continue;
            }

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
        DocumentSnapshot document,
        WorkspaceConfiguration configuration,
        bool useVisualStudioClassifications
    )
    {
        SourceText source = SourceText.From(document.Text);
        SemanticClassificationResult result =
            MuLangCompiler.ClassifySemantically(
                document.Text,
                configuration.Environment,
                configuration.CompilationMode,
                configuration.ExpectedResultType,
                configuration.Profile
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
            return string.Empty;
        }

        return folder.EndsWith("/", StringComparison.Ordinal)
            ? folder
            : $"{folder}/";
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
