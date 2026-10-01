namespace MuLang.StandardLibrary.DotNet;

/// <summary>Represents configuration for the Random standard-library module.</summary>
public sealed class RandomStandardLibraryOptions
{
    /// <summary>Gets or sets the random source, or <c>null</c> to use the thread-safe default.</summary>
    public IStandardLibraryRandomSource? RandomSource { get; set; }
}
