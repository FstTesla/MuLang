namespace MuLang.LanguageServer;

internal sealed record WorkspaceConfigurationSet(
    WorkspaceConfiguration? DefaultConfiguration,
    IReadOnlyList<WorkspaceConfigurationOverride> Overrides
)
{
    public static WorkspaceConfigurationSet Default { get; } = new (
        null,
        []
    );

    public WorkspaceConfiguration Get(string path)
    {
        WorkspaceConfiguration configuration =
            DefaultConfiguration ?? WorkspaceConfiguration.Default;

        return ApplyOverrides(path, configuration);
    }

    public WorkspaceConfiguration ApplyOverrides(
        string path,
        WorkspaceConfiguration configuration
    )
    {
        foreach (WorkspaceConfigurationOverride configurationOverride in Overrides)
        {
            if (configurationOverride.Matches(path))
            {
                configuration = configurationOverride.Configuration;
            }
        }

        return configuration;
    }
}
