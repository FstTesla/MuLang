namespace MuLang;

internal sealed record LanguageProfile
{
    public LanguageProfile(LanguageVersion version, CompilationMode compilationMode)
    {
        if (!Enum.IsDefined(version))
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (!Enum.IsDefined(compilationMode))
        {
            throw new ArgumentOutOfRangeException(nameof(compilationMode));
        }

        Version = version;
        CompilationMode = compilationMode;
    }

    public LanguageVersion Version { get; }

    public CompilationMode CompilationMode { get; }
}
