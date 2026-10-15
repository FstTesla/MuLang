namespace MuLang.LanguageServer;

internal sealed record WorkspaceConfigurationSet(
    WorkspaceConfiguration DefaultConfiguration,
    IReadOnlyList<WorkspaceConfigurationOverride> Overrides
)
{
    public static WorkspaceConfigurationSet Default { get; } = new (
        WorkspaceConfiguration.Default,
        []
    );

    public WorkspaceConfiguration Get(string path)
    {
        WorkspaceConfiguration configuration = DefaultConfiguration;

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
