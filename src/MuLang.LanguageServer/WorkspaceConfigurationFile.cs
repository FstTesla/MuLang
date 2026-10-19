namespace MuLang.LanguageServer;

internal sealed class WorkspaceConfigurationFile
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private readonly IDictionary<string, IReadOnlyList<Uri>> cache =
        new Dictionary<string, IReadOnlyList<Uri>>();

    public const string FileName = "mulang.config.json";

    public void ClearCache()
    {
        cache.Clear();
    }

    public static bool IsConfigurationFile(string file)
    {
        if (
            !Uri.TryCreate(file, UriKind.Absolute, out Uri? uri) ||
            !uri.IsFile
        )
        {
            return false;
        }

        return
            string.Equals(
                Path.GetFileName(uri.LocalPath),
                FileName,
                PathComparison
            );
    }

    public bool TryResolve(
        string document,
        string workspaceFolder,
        out IReadOnlyList<Uri> files
    )
    {
        Uri documentUri = new (document, UriKind.Absolute);
        Uri workspaceFolderUri = new (workspaceFolder, UriKind.Absolute);

        if (!documentUri.IsFile || !workspaceFolderUri.IsFile)
        {
            throw new FormatException(
                "The document and workspace folder must use file URIs."
            );
        }

        string root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(workspaceFolderUri.LocalPath)
        );
        string? directory = Path.GetDirectoryName(
            Path.GetFullPath(documentUri.LocalPath)
        );

        if (
            directory is null ||
            !IsWithinRoot(directory, root)
        )
        {
            files = [ ];
            return false;
        }

        string cacheKey = $"{root}\0{directory}";

        if (cache.TryGetValue(cacheKey, out files!))
        {
            return files.Count > 0;
        }

        string current = directory;
        IList<Uri> discoveredFiles = new List<Uri>();

        while (true)
        {
            string candidate = Path.Combine(current, FileName);

            if (File.Exists(candidate))
            {
                discoveredFiles.Add(new Uri(Path.GetFullPath(candidate)));
            }

            if (string.Equals(current, root, PathComparison))
            {
                break;
            }

            string? parent = Path.GetDirectoryName(current);

            if (parent is null || !IsWithinRoot(parent, root))
            {
                break;
            }

            current = parent;
        }

        files = [ .. discoveredFiles.Reverse() ];
        cache[cacheKey] = files;
        return files.Count > 0;
    }

    private static bool IsWithinRoot(string path, string root)
    {
        string rootPrefix = Path.EndsInDirectorySeparator(root)
            ? root
            : $"{root}{Path.DirectorySeparatorChar}";

        return
            string.Equals(path, root, PathComparison) ||
            path.StartsWith(rootPrefix, PathComparison);
    }
}
