using MuLang.Core;
using MuLang.Core.Symbols;

namespace MuLang.StandardLibrary;

/// <summary>Represents a standard-library function.</summary>
public sealed class StandardLibraryFunction : IStandardLibrarySymbol
{
    /// <summary>Initializes a new instance of the <see cref="StandardLibraryFunction" /> class.</summary>
    /// <param name="declaration">The function declaration.</param>
    /// <param name="minimumLanguageVersion">The minimum supported language version.</param>
    /// <param name="capability">The required runtime capabilities.</param>
    /// <param name="dependencies">The direct symbol dependencies.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="declaration" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when declaration metadata or dependencies are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when metadata values are not defined.</exception>
    public StandardLibraryFunction(
        FunctionSymbol declaration,
        LanguageVersion minimumLanguageVersion = LanguageVersion.Version1,
        StandardLibraryCapability capability = StandardLibraryCapability.Deterministic,
        IEnumerable<IStandardLibrarySymbol>? dependencies = null
    )
    {
        if (declaration is null)
        {
            throw new ArgumentNullException(nameof(declaration));
        }

        StandardLibrarySymbolValidation.ValidateMetadata(
            declaration.Id,
            declaration.Name,
            minimumLanguageVersion,
            capability
        );

        Declaration = declaration;
        MinimumLanguageVersion = minimumLanguageVersion;
        Capability = capability;
        Dependencies = StandardLibrarySymbolValidation.CopyDependencies(dependencies, Id);
    }

    /// <summary>Gets the function declaration.</summary>
    public FunctionSymbol Declaration { get; }

    /// <inheritdoc />
    public string Id => Declaration.Id;

    /// <inheritdoc />
    public string Name => Declaration.Name;

    /// <inheritdoc />
    public LanguageVersion MinimumLanguageVersion { get; }

    /// <inheritdoc />
    public StandardLibraryCapability Capability { get; }

    /// <inheritdoc />
    public IReadOnlyList<IStandardLibrarySymbol> Dependencies { get; }
}
