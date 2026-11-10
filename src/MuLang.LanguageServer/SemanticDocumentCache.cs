using MuLang.Compiler;
using MuLang.Core.Text;

namespace MuLang.LanguageServer;

internal sealed class SemanticDocumentCache
{
    private const int Capacity = 256;

    private static readonly StringComparer UriComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly IDictionary<string, CacheEntry> entries =
        new Dictionary<string, CacheEntry>(UriComparer);

    private readonly Queue<(string Uri, int Generation)> insertionOrder = new ();
    private int configurationGeneration;
    private int entryGeneration;

    public void Invalidate(string uri)
    {
        entries.Remove(uri);
    }

    public void InvalidateConfiguration()
    {
        configurationGeneration++;
        entries.Clear();
        insertionOrder.Clear();
    }

    public SemanticDocumentSnapshot Get(
        DocumentSnapshot document,
        WorkspaceConfiguration? configuration
    )
    {
        string stamp =
            $"open:{document.Version}:{configurationGeneration}";
        return GetOrCreate(
            document.Uri,
            document.Text,
            stamp,
            configuration,
            document.LanguageId == "muir"
        );
    }

    public async Task<SemanticDocumentSnapshot?> GetFileAsync(
        string uri,
        WorkspaceConfiguration? configuration,
        CancellationToken cancellationToken
    )
    {
        Uri fileUri = new (uri, UriKind.Absolute);

        if (!fileUri.IsFile)
        {
            return null;
        }

        FileInfo file = new (fileUri.LocalPath);

        try
        {
            file.Refresh();

            if (!file.Exists)
            {
                return null;
            }

            string stamp =
                $"disk:{file.LastWriteTimeUtc.Ticks}:{file.Length}:{configurationGeneration}";

            if (
                entries.TryGetValue(uri, out CacheEntry? entry) &&
                entry.Stamp == stamp
            )
            {
                return entry.Snapshot;
            }

            string text = await File.ReadAllTextAsync(
                file.FullName,
                cancellationToken
            );
            return GetOrCreate(uri, text, stamp, configuration);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private SemanticDocumentSnapshot GetOrCreate(
        string uri,
        string text,
        string stamp,
        WorkspaceConfiguration? configuration,
        bool isMuIr = false
    )
    {
        if (
            entries.TryGetValue(uri, out CacheEntry? entry) &&
            entry.Stamp == stamp
        )
        {
            return entry.Snapshot;
        }

        Uri? sourceDocument = Uri.TryCreate(
            uri,
            UriKind.Absolute,
            out Uri? parsedUri
        )
            ? parsedUri
            : null;
        SourceText source = SourceText.From(text);
        EditorDocument semanticDocument;

        if (isMuIr || MuIrEditorDocument.IsMuIr(uri))
        {
            semanticDocument = new MuIrEditorDocument(text, source);
        }
        else
        {
            if (configuration is null)
            {
                throw new InvalidOperationException("MuLang analysis requires a compilation configuration.");
            }

            semanticDocument = new MuLangEditorDocument(
                MuLangCompiler.GetSemanticDocument(
                    text,
                    configuration.Environment,
                    configuration.CompilationMode,
                    new MuLangCompilationOptions
                    {
                        ExpectedResultType =
                            configuration.ExpectedResultType,
                        Profile = configuration.Profile,
                        SourceDocument = sourceDocument,
                    }
                )
            );
        }
        SemanticDocumentSnapshot snapshot = new (
            uri,
            text,
            source,
            semanticDocument
        );
        Add(uri, stamp, snapshot);
        return snapshot;
    }

    private void Add(
        string uri,
        string stamp,
        SemanticDocumentSnapshot snapshot
    )
    {
        int generation = ++entryGeneration;
        entries[uri] = new CacheEntry(stamp, snapshot, generation);
        insertionOrder.Enqueue((uri, generation));

        while (insertionOrder.Count > Capacity)
        {
            (string oldestUri, int oldestGeneration) =
                insertionOrder.Dequeue();

            if (
                entries.TryGetValue(
                    oldestUri,
                    out CacheEntry? oldestEntry
                ) &&
                oldestEntry.Generation == oldestGeneration
            )
            {
                entries.Remove(oldestUri);
            }
        }
    }

    private sealed record CacheEntry(
        string Stamp,
        SemanticDocumentSnapshot Snapshot,
        int Generation
    );
}
