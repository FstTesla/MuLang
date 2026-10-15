namespace MuLang.LanguageServer;

internal sealed record WorkspaceConfigurationOverride(
    IReadOnlyList<WorkspacePathGlob> Includes,
    IReadOnlyList<WorkspacePathGlob> Excludes,
    WorkspaceConfiguration Configuration
)
{
    public bool Matches(string path)
    {
        return
            Includes.Any(include => include.IsMatch(path)) &&
            !Excludes.Any(exclude => exclude.IsMatch(path));
    }
}
