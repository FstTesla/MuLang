using MuLang.Core;
using System.Collections.ObjectModel;

namespace MuLang.StandardLibrary;

/// <summary>Represents a runtime-independent standard-library module.</summary>
public sealed class StandardLibraryModule
{
    /// <summary>Initializes a new instance of the <see cref="StandardLibraryModule" /> class.</summary>
    /// <param name="id">The stable module identifier.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="symbols">The symbols declared by the module.</param>
    /// <exception cref="ArgumentException">Thrown when metadata or symbols are invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="symbols" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when symbol dependencies contain conflicts or cycles.</exception>
    public StandardLibraryModule(
        string id,
        string displayName,
        IEnumerable<IStandardLibrarySymbol> symbols
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

        if (symbols is null)
        {
            throw new ArgumentNullException(nameof(symbols));
        }

        IStandardLibrarySymbol[] copy = [ .. symbols ];

        if (copy.Length == 0)
        {
            throw new ArgumentException("A standard-library module cannot be empty.", nameof(symbols));
        }

        if (copy.Any(static symbol => symbol is null))
        {
            throw new ArgumentException(
                "Module symbols cannot contain null values.",
                nameof(symbols)
            );
        }

        IGrouping<string, IStandardLibrarySymbol>? duplicate = copy
            .GroupBy(static symbol => symbol.Id, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Symbol identifier '{duplicate.Key}' is declared more than once.",
                nameof(symbols)
            );
        }

        string symbolIdPrefix = $"{id}.";
        IStandardLibrarySymbol? inconsistent = copy.FirstOrDefault(
            symbol => !symbol.Id.StartsWith(symbolIdPrefix, StringComparison.Ordinal)
        );

        if (inconsistent is not null)
        {
            throw new ArgumentException(
                $"Symbol identifier '{inconsistent.Id}' is inconsistent with module identifier '{id}'.",
                nameof(symbols)
            );
        }

        StandardLibrarySelection expanded = StandardLibrarySelection.Create(copy);

        Id = id;
        DisplayName = displayName;
        Symbols = new ReadOnlyCollection<IStandardLibrarySymbol>(copy);
        Types = new ReadOnlyCollection<StandardLibraryType>(
            [ .. copy.OfType<StandardLibraryType>() ]
        );
        Globals = new ReadOnlyCollection<StandardLibraryGlobal>(
            [ .. copy.OfType<StandardLibraryGlobal>() ]
        );
        Functions = new ReadOnlyCollection<StandardLibraryFunction>(
            [ .. copy.OfType<StandardLibraryFunction>() ]
        );
        MinimumLanguageVersion = expanded.MinimumLanguageVersion;
        Capability = expanded.Capability;
    }

    /// <summary>Gets the stable module identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the symbols in deterministic declaration order.</summary>
    public IReadOnlyList<IStandardLibrarySymbol> Symbols { get; }

    /// <summary>Gets the minimum supported language version derived from symbols and dependencies.</summary>
    public LanguageVersion MinimumLanguageVersion { get; }

    /// <summary>Gets the required runtime capabilities derived from symbols and dependencies.</summary>
    public StandardLibraryCapability Capability { get; }

    /// <summary>Gets the structured types in deterministic declaration order.</summary>
    public IReadOnlyList<StandardLibraryType> Types { get; }

    /// <summary>Gets the globals in deterministic declaration order.</summary>
    public IReadOnlyList<StandardLibraryGlobal> Globals { get; }

    /// <summary>Gets the functions in deterministic declaration order.</summary>
    public IReadOnlyList<StandardLibraryFunction> Functions { get; }
}
