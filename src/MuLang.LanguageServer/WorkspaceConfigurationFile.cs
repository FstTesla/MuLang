namespace MuLang.LanguageServer;

internal sealed class WorkspaceConfigurationFile
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private readonly IDictionary<string, Uri?> cache =
        new Dictionary<string, Uri?>();

    public const string FileName = "mulang.config.json";

    public void ClearCache()
    {
        cache.Clear();
    }

    public bool IsConfigurationFile(string file)
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
        out Uri? file
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
            file = null;
            return false;
        }

        string cacheKey = $"{root}\0{directory}";

        if (cache.TryGetValue(cacheKey, out file))
        {
            return file is not null;
        }

        string current = directory;

        while (true)
        {
            string candidate = Path.Combine(current, FileName);

            if (File.Exists(candidate))
            {
                file = new Uri(Path.GetFullPath(candidate));
                cache[cacheKey] = file;
                return true;
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

        file = null;
        cache[cacheKey] = null;
        return false;
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
