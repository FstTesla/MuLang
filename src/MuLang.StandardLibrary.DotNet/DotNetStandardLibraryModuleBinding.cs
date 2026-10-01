using MuLang.Exporters.DotNet;
using MuLang.StandardLibrary;
using System.Collections.ObjectModel;

namespace MuLang.StandardLibrary.DotNet;

/// <summary>Represents an immutable .NET binding for a declarative standard-library module.</summary>
public sealed class DotNetStandardLibraryModuleBinding
{
    /// <summary>Initializes a new instance of the <see cref="DotNetStandardLibraryModuleBinding" /> class.</summary>
    /// <param name="moduleId">The implemented module identifier.</param>
    /// <param name="module">The matching declarative module.</param>
    /// <param name="globals">The runtime global implementations.</param>
    /// <param name="functions">The runtime function implementations.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="moduleId" /> is invalid or a collection contains an invalid entry.</exception>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    public DotNetStandardLibraryModuleBinding(
        string moduleId,
        StandardLibraryModule module,
        IEnumerable<KeyValuePair<string, object?>> globals,
        IEnumerable<DotNetStandardLibraryFunction> functions
    )
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            throw new ArgumentException("Module identifier cannot be null or whitespace.", nameof(moduleId));
        }

        ModuleId = moduleId;
        Module = module ?? throw new ArgumentNullException(nameof(module));
        Globals = CopyGlobals(globals);
        Functions = CopyFunctions(functions);
    }

    /// <summary>Gets the implemented module identifier.</summary>
    public string ModuleId { get; }

    /// <summary>Gets the matching declarative module.</summary>
    public StandardLibraryModule Module { get; }

    /// <summary>Gets the runtime global implementations.</summary>
    public IReadOnlyList<KeyValuePair<string, object?>> Globals { get; }

    /// <summary>Gets the runtime function implementations.</summary>
    public IReadOnlyList<DotNetStandardLibraryFunction> Functions { get; }

    private static IReadOnlyList<KeyValuePair<string, object?>> CopyGlobals(
        IEnumerable<KeyValuePair<string, object?>> globals
    )
    {
        if (globals is null)
        {
            throw new ArgumentNullException(nameof(globals));
        }

        KeyValuePair<string, object?>[] copy = [ .. globals ];

        if (copy.Any(static pair => string.IsNullOrWhiteSpace(pair.Key)))
        {
            throw new ArgumentException("Global identifiers cannot be null or whitespace.", nameof(globals));
        }

        return new ReadOnlyCollection<KeyValuePair<string, object?>>(copy);
    }

    private static IReadOnlyList<DotNetStandardLibraryFunction> CopyFunctions(
        IEnumerable<DotNetStandardLibraryFunction> functions
    )
    {
        if (functions is null)
        {
            throw new ArgumentNullException(nameof(functions));
        }

        DotNetStandardLibraryFunction[] copy = [ .. functions ];

        if (copy.Any(static function => function is null))
        {
            throw new ArgumentException("Function implementations cannot contain null values.", nameof(functions));
        }

        return new ReadOnlyCollection<DotNetStandardLibraryFunction>(copy);
    }
}
