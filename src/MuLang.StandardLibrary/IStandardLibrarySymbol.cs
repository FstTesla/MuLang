using MuLang.Core;

namespace MuLang.StandardLibrary;

/// <summary>Represents a standard-library symbol with its declarative metadata.</summary>
public interface IStandardLibrarySymbol
{
    /// <summary>Gets the provider identifier.</summary>
    string Id { get; }

    /// <summary>Gets the language name.</summary>
    string Name { get; }

    /// <summary>Gets the minimum supported language version.</summary>
    LanguageVersion MinimumLanguageVersion { get; }

    /// <summary>Gets the required runtime capabilities.</summary>
    StandardLibraryCapability Capability { get; }

    /// <summary>Gets the direct symbol dependencies.</summary>
    IReadOnlyList<IStandardLibrarySymbol> Dependencies { get; }
}
