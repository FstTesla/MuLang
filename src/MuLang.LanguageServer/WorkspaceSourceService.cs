namespace MuLang.LanguageServer;

internal sealed class WorkspaceSourceService
{
    private static readonly StringComparer UriComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public IReadOnlyList<string> GetDocumentUris(
        IReadOnlyCollection<DocumentSnapshot> openDocuments,
        IReadOnlyCollection<string> workspaceFolders,
        CancellationToken cancellationToken
    )
    {
        ISet<string> uris = new HashSet<string>(UriComparer);

        foreach (DocumentSnapshot document in openDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uris.Add(document.Uri);
        }

        foreach (string folder in workspaceFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (
                !Uri.TryCreate(folder, UriKind.Absolute, out Uri? folderUri) ||
                !folderUri.IsFile ||
                !Directory.Exists(folderUri.LocalPath)
            )
            {
                continue;
            }

            EnumerationOptions options = new ()
            {
                AttributesToSkip =
                    FileAttributes.ReparsePoint,
                IgnoreInaccessible = false,
                RecurseSubdirectories = true,
                ReturnSpecialDirectories = false,
            };

            foreach (
                string file in Directory.EnumerateFiles(
                    folderUri.LocalPath,
                    "*",
                    options
                )
            )
            {
                cancellationToken.ThrowIfCancellationRequested();
                string extension = Path.GetExtension(file);

                if (
                    !string.Equals(
                        extension,
                        ".mu",
                        StringComparison.OrdinalIgnoreCase
                    ) &&
                    !string.Equals(
                        extension,
                        ".mulang",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                uris.Add(new Uri(Path.GetFullPath(file)).AbsoluteUri);
            }
        }

        return [ .. uris.Order(UriComparer) ];
    }
}
