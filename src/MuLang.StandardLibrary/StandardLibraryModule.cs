using MuLang.Core;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.StandardLibrary;

/// <summary>Represents a runtime-independent standard-library module.</summary>
public sealed class StandardLibraryModule
{
    /// <summary>Initializes a new instance of the <see cref="StandardLibraryModule" /> class.</summary>
    /// <param name="id">The stable module identifier.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="minimumLanguageVersion">The minimum supported language version.</param>
    /// <param name="capability">The required runtime capabilities.</param>
    /// <param name="types">The structured types.</param>
    /// <param name="globals">The global declarations.</param>
    /// <param name="functions">The function declarations.</param>
    /// <exception cref="ArgumentException">Thrown when a metadata value is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when a declaration collection is <c>null</c>.</exception>
    public StandardLibraryModule(
        string id,
        string displayName,
        LanguageVersion minimumLanguageVersion,
        StandardLibraryCapability capability,
        IEnumerable<ObjectTypeSymbol> types,
        IEnumerable<GlobalSymbol> globals,
        IEnumerable<FunctionSymbol> functions
    )
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Module identifier cannot be null or whitespace.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be null or whitespace.", nameof(displayName));
        }

        if (!Enum.IsDefined(minimumLanguageVersion))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLanguageVersion));
        }

        if ((capability & ~(StandardLibraryCapability.Randomness | StandardLibraryCapability.Clock)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capability));
        }

        Id = id;
        DisplayName = displayName;
        MinimumLanguageVersion = minimumLanguageVersion;
        Capability = capability;
        Types = Copy(types, nameof(types));
        Globals = Copy(globals, nameof(globals));
        Functions = Copy(functions, nameof(functions));
    }

    /// <summary>Gets the stable module identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the minimum supported language version.</summary>
    public LanguageVersion MinimumLanguageVersion { get; }

    /// <summary>Gets the required runtime capabilities.</summary>
    public StandardLibraryCapability Capability { get; }

    /// <summary>Gets the structured types in deterministic declaration order.</summary>
    public IReadOnlyList<ObjectTypeSymbol> Types { get; }

    /// <summary>Gets the global declarations in deterministic declaration order.</summary>
    public IReadOnlyList<GlobalSymbol> Globals { get; }

    /// <summary>Gets the function declarations in deterministic declaration order.</summary>
    public IReadOnlyList<FunctionSymbol> Functions { get; }

    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> items, string parameterName)
    {
        if (items is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        IReadOnlyList<T> copy = [ .. items ];

        if (copy.Any(static item => item is null))
        {
            throw new ArgumentException("Declaration collections cannot contain null values.", parameterName);
        }

        return copy;
    }
}
