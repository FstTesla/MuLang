using MuLang.Exporters.DotNet;

namespace MuLang.StandardLibrary.DotNet;

/// <summary>Represents immutable .NET bindings for selected standard-library symbols.</summary>
public sealed class DotNetStandardLibraryBindings
{
    internal DotNetStandardLibraryBindings(
        IReadOnlyDictionary<string, object?> globals,
        IReadOnlyDictionary<string, DotNetProviderFunction> functions
    )
    {
        Globals = globals;
        Functions = functions;
    }

    /// <summary>Gets the selected runtime globals keyed by provider identifier.</summary>
    public IReadOnlyDictionary<string, object?> Globals { get; }

    /// <summary>Gets the selected runtime functions keyed by provider identifier.</summary>
    public IReadOnlyDictionary<string, DotNetProviderFunction> Functions { get; }
}
