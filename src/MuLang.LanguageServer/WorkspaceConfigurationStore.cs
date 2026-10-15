namespace MuLang.LanguageServer;

internal sealed class WorkspaceConfigurationStore
{
    private static readonly StringComparer UriComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly StringComparison UriComparison =
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private readonly IDictionary<string, WorkspaceConfigurationSet> configurations =
        new Dictionary<string, WorkspaceConfigurationSet>(UriComparer);

    private readonly IDictionary<string, WorkspaceConfigurationSet> configurationFiles =
        new Dictionary<string, WorkspaceConfigurationSet>(UriComparer);

    private readonly IList<string> folders = new List<string>();

    public IReadOnlyList<string> Folders => [ .. folders ];

    public void Initialize(IEnumerable<string> workspaceFolders)
    {
        folders.Clear();
        configurations.Clear();
        configurationFiles.Clear();

        foreach (string folder in workspaceFolders)
        {
            AddFolder(folder);
        }
    }

    public void AddFolder(string folder)
    {
        string normalizedFolder = NormalizeFolder(folder);

        if (!folders.Contains(normalizedFolder, UriComparer))
        {
            folders.Add(normalizedFolder);
        }
    }

    public void RemoveFolder(string folder)
    {
        string normalizedFolder = NormalizeFolder(folder);
        _ = folders.Remove(normalizedFolder);
        _ = configurations.Remove(normalizedFolder);

        foreach (
            string directory in configurationFiles.Keys
                .Where(directory => directory.StartsWith(
                    normalizedFolder,
                    UriComparison
                ))
                .ToArray()
        )
        {
            _ = configurationFiles.Remove(directory);
        }
    }

    public WorkspaceConfiguration Get(string documentUri)
    {
        string? configurationDirectory = configurationFiles.Keys
            .Where(directory => documentUri.StartsWith(
                directory,
                UriComparison
            ))
            .OrderByDescending(static directory => directory.Length)
            .FirstOrDefault();

        if (configurationDirectory is not null)
        {
            return configurationFiles[configurationDirectory].Get(
                GetRelativePath(configurationDirectory, documentUri)
            );
        }

        string? folder = folders
            .Where(folder => documentUri.StartsWith(folder, UriComparison))
            .OrderByDescending(static folder => folder.Length)
            .FirstOrDefault();

        if (
            folder is not null &&
            configurations.TryGetValue(folder, out WorkspaceConfigurationSet? configuration)
        )
        {
            return configuration.Get(GetRelativePath(folder, documentUri));
        }

        return configurations.TryGetValue(string.Empty, out WorkspaceConfigurationSet? fallback)
            ? fallback.DefaultConfiguration
            : WorkspaceConfiguration.Default;
    }

    public void Set(string? folder, WorkspaceConfigurationSet configuration)
    {
        configurations[folder is null ? string.Empty : NormalizeFolder(folder)] = configuration;
    }

    public string? GetFolder(string uri)
    {
        return folders
            .Where(folder => uri.StartsWith(folder, UriComparison))
            .OrderByDescending(static folder => folder.Length)
            .FirstOrDefault();
    }

    public bool ContainsConfigurationFile(string file)
    {
        return configurationFiles.ContainsKey(GetConfigurationDirectory(file));
    }

    public void SetConfigurationFile(
        string file,
        WorkspaceConfigurationSet configuration
    )
    {
        configurationFiles[GetConfigurationDirectory(file)] = configuration;
    }

    public bool RemoveConfigurationFile(string file)
    {
        return configurationFiles.Remove(GetConfigurationDirectory(file));
    }

    private static string NormalizeFolder(string folder)
    {
        return folder.EndsWith("/", StringComparison.Ordinal)
            ? folder
            : $"{folder}/";
    }

    private static string GetRelativePath(string folder, string documentUri)
    {
        Uri folderUri = new (folder, UriKind.Absolute);
        Uri document = new (documentUri, UriKind.Absolute);
        return Uri.UnescapeDataString(
            folderUri.MakeRelativeUri(document).OriginalString
        ).Replace('\\', '/');
    }

    private static string GetConfigurationDirectory(string file)
    {
        Uri fileUri = new (file, UriKind.Absolute);
        return NormalizeFolder(new Uri(fileUri, ".").AbsoluteUri);
    }
}
