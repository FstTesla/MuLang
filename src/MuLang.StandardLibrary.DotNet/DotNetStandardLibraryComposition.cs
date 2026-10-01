using MuLang.Exporters.DotNet;

namespace MuLang.StandardLibrary.DotNet;

/// <summary>Represents runtime values and functions produced by standard-library composition.</summary>
public sealed class DotNetStandardLibraryComposition
{
    internal DotNetStandardLibraryComposition(
        IReadOnlyDictionary<string, object?> globals,
        IReadOnlyDictionary<string, DotNetProviderFunction> functions
    )
    {
        Globals = globals;
        Functions = functions;
    }

    /// <summary>Gets runtime globals keyed by provider identifier.</summary>
    public IReadOnlyDictionary<string, object?> Globals { get; }

    /// <summary>Gets runtime functions keyed by provider identifier.</summary>
    public IReadOnlyDictionary<string, DotNetProviderFunction> Functions { get; }
}
