namespace MuLang.StandardLibrary.DotNet;

internal sealed class DotNetStandardLibraryServices
{
    private readonly TimeProvider? clockTimeProvider;
    private readonly TimeProvider? guidTimeProvider;

    public DotNetStandardLibraryServices(
        IStandardLibraryRandomSource randomSource,
        TimeProvider? clockTimeProvider,
        TimeProvider? guidTimeProvider
    )
    {
        RandomSource = randomSource;
        this.clockTimeProvider = clockTimeProvider;
        this.guidTimeProvider = guidTimeProvider;
    }

    public IStandardLibraryRandomSource RandomSource { get; }

    public TimeProvider GetClockTimeProvider()
    {
        return clockTimeProvider ??
            throw new InvalidOperationException(
                "The configured Clock time provider cannot be null for clock functions."
            );
    }

    public TimeProvider GetGuidTimeProvider()
    {
        return guidTimeProvider ??
            throw new InvalidOperationException(
                "The configured Guid time provider cannot be null for newGuidV7."
            );
    }
}
